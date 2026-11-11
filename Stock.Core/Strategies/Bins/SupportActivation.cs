using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public sealed record ProfileBin(decimal Low, decimal High, decimal Volume);
public sealed record SupportSnapshot(string Id, decimal MovingAverage, decimal ZoneLow, decimal ZoneHigh, decimal Lower, decimal Upper,
    DateTimeOffset KnownAt, string Quality, ProfileBin[] Bins, decimal InputVolume, string PriceRuleSource);
public sealed record SupportCalculation(SupportSnapshot? Support, string Validity);
public static class SupportCalculator
{
    public static SupportCalculation Calculate(Instrument instrument, StageOneConfig c, BinsHistory history, PriceRule? rule, DateTimeOffset now)
    {
        if (rule == null || !rule.ValidAt(now)) return new(null, "MissingData: verified price rule");
        var date = MarketCalendars.TradingDate(instrument.Exchange, now);
        var maBars = history.Bars(c.MovingAverageBar, now).Where(b => c.MovingAverageBar.Unit != BarUnit.TradingDay || b.TradingDate < date).TakeLast(c.MovingAveragePeriod).ToArray();
        var profileBars = history.Bars(c.ProfileBar, now).Where(b => c.ProfileBar.Unit != BarUnit.TradingDay || b.TradingDate < date).TakeLast(c.ProfileLookbackBars).ToArray();
        if (maBars.Length != c.MovingAveragePeriod || profileBars.Length != c.ProfileLookbackBars) return new(null, "InsufficientHistory: configured MA/profile bars (no automatic shortening)");
        var ma = maBars.Average(b => b.Close); var low = profileBars.Min(b => b.Low); var high = profileBars.Max(b => b.High);
        var edges = new List<(decimal Low, decimal High)>(); var cursor = rule.Floor(low);
        while (cursor <= high)
        {
            var next = cursor;
            for (int n = 0; n < c.ProfileBinSizeTicks; n++) next = rule.Ceiling(next + rule.TickAt(next));
            if (next <= cursor || edges.Count >= c.ProfileMaxBins) return new(null, "Profile capacity exceeded: adjust explicit bin size/capacity; no hidden tuning");
            edges.Add((cursor, next)); cursor = next;
        }
        var volumes = new decimal[edges.Count]; decimal inputVolume = 0;
        if (c.ProfileSource == ProfileSource.ApproximateOHLCV)
        {
            foreach (var bar in profileBars)
            {
                var indices = Enumerable.Range(0, edges.Count).Where(n => bar.High == bar.Low ? bar.Low >= edges[n].Low && bar.Low < edges[n].High : edges[n].Low < bar.High && edges[n].High > bar.Low).ToArray();
                if (indices.Length == 0) return new(null, "Invalid profile coverage");
                var allocated = 0m;
                for (int n = 0; n < indices.Length; n++) { var amount = n == indices.Length - 1 ? bar.Volume - allocated : bar.Volume / indices.Length; volumes[indices[n]] += amount; allocated += amount; }
                inputVolume += bar.Volume;
            }
        }
        else
        {
            var actual = history.ProfileTrades(now, profileBars[0].OpenedAt, profileBars[^1].ClosedAt);
            if (actual.Length == 0 || profileBars.Any(bar => actual.Where(t => t.Time >= bar.OpenedAt && t.Time < bar.ClosedAt).Sum(t => t.Size) != bar.Volume)) return new(null, "MissingData: complete original profile trades (each bar volume must reconcile)");
            foreach (var trade in actual) { var n = edges.FindIndex(b => trade.Price >= b.Low && trade.Price < b.High); if (n < 0) return new(null, "Trade outside profile"); volumes[n] += trade.Size; inputVolume += trade.Size; }
        }
        if (inputVolume <= 0) return new(null, "MissingData: positive profile volume");
        var bins = edges.Select((b, n) => new ProfileBin(b.Low, b.High, volumes[n])).ToArray(); var threshold = volumes.Max() * c.VolumeZoneThresholdPct / 100;
        var zones = new List<(decimal Low, decimal High)>();
        foreach (var bin in bins.Where(b => b.Volume >= threshold && b.Volume > 0))
        {
            if (zones.Count > 0 && 100 * (bin.Low / zones[^1].High - 1) <= c.ProfileMergeRangePct && 100 * (bin.High - zones[^1].Low) / ((bin.High + zones[^1].Low) / 2) <= c.MaxZoneWidthPct) zones[^1] = (zones[^1].Low, bin.High);
            else zones.Add((bin.Low, bin.High));
        }
        var valid = zones.Where(z => 100 * (z.High - z.Low) / ((z.High + z.Low) / 2) <= c.MaxZoneWidthPct)
            .Select(z => (z.Low, z.High, Gap: ma >= z.Low && ma <= z.High ? 0 : 100 * Math.Min(Math.Abs(ma - z.Low), Math.Abs(ma - z.High)) / ma))
            .Where(z => z.Gap <= c.MaxOverlapPct).OrderBy(z => z.Gap).ThenBy(z => Math.Abs((z.Low + z.High) / 2 - ma)).ToArray();
        if (valid.Length == 0) return new(null, "No overlapping MA/volume zone");
        var zone = valid[0]; var known = maBars.Concat(profileBars).Max(b => b.KnownAt); if (rule.KnownAt > known) known = rule.KnownAt;
        var id = FormattableString.Invariant($"support:{instrument.Key}:{zone.Low}:{zone.High}:{ma}");
        return new(new(id, ma, zone.Low, zone.High, Math.Min(ma, zone.Low), Math.Max(ma, zone.High), known,
            c.ProfileSource == ProfileSource.ApproximateOHLCV ? "ApproximateOHLCV (uniform bin allocation, not actual executions)" : "TradeBased (original volume reconciled)", bins, inputVolume, rule.Source), "ValidSupport");
    }
}
public sealed record StageOneSnapshot(StageOneState State, DateTimeOffset? ActiveUntil, string? ActivationEpisodeId,
    SupportSnapshot? LockedSupport, decimal? CandidateLow, string SessionId, DateTimeOffset KnownAt, string Reason)
{
    public bool Allows(DateTimeOffset at, string session) => State == StageOneState.Disabled || State == StageOneState.Active && ActiveUntil > at && SessionId == session;
}
/// <summary>Independent permission window. A TP, family switch or reentry does not consume or reset it.</summary>
public sealed class SupportActivationGate(StageOneConfig config)
{
    private bool armed = true; private decimal? low, priorRebound; private DateTimeOffset created, lastActivation; private string episode = ""; private long sequence;
    private SupportSnapshot? watched;
    public StageOneSnapshot Snapshot { get; private set; } = new(config.Enabled ? StageOneState.Watching : StageOneState.Disabled, null, null, null, null, "", default, config.Enabled ? "Watching support" : "Stage1 bypassed");
    public void ResetSession(string id, DateTimeOffset now)
    { armed = true; ClearCandidate(); lastActivation = default; Snapshot = new(config.Enabled ? StageOneState.Watching : StageOneState.Disabled, null, null, null, null, id, now, "New session; no permission carry"); }
    public void InvalidateData(DateTimeOffset at)
    { if (!config.Enabled) return; ClearCandidate(); armed = false; Snapshot = Snapshot with { State = StageOneState.Expired, ActiveUntil = null, KnownAt = at, Reason = "Data quality unverified; fresh support episode required" }; }
    public void OnClock(DateTimeOffset now, MarketSession? session)
    { if (!config.Enabled) return; if (Snapshot.State == StageOneState.Active && (Snapshot.ActiveUntil <= now || session == null || session.Id != Snapshot.SessionId || now >= session.Close)) { ClearCandidate(); armed = false; Snapshot = Snapshot with { State = StageOneState.Expired, ActiveUntil = null, KnownAt = now, Reason = "Permission expired/session ended" }; } }
    public void OnTrade(BinsMarketInput input, SupportSnapshot? support, DateTimeOffset? sessionEntryEnd = null)
    {
        if (!config.Enabled) return;
        var e = input.Event; var at = input.KnownAt; var s = input.Session!; var price = e.TradePrice!.Value;
        if (Snapshot.SessionId != s.Id) ResetSession(s.Id, at);
        OnClock(at, s);
        if (Snapshot.LockedSupport is {} active && Snapshot.State == StageOneState.Active && price < active.Lower * (1 - config.BreakBufferPct / 100))
        { ClearCandidate(); armed = false; Snapshot = Snapshot with { State = StageOneState.Invalidated, ActiveUntil = null, KnownAt = at, Reason = "Locked support broken; entries blocked, holdings use common protection" }; return; }
        if (support == null || support.KnownAt > at) { if (Snapshot.State != StageOneState.Active) Snapshot = Snapshot with { Reason = "InsufficientHistory/support missing", KnownAt = at }; return; }
        // Keep the original physical support frozen while active; recalculation cannot lower its invalidation anchor.
        var basis = Snapshot.State == StageOneState.Active ? Snapshot.LockedSupport! : support;
        var near = price >= basis.Lower * (1 - config.ApproachPct / 100) && price <= basis.Upper * (1 + config.ApproachPct / 100);
        if (!near) { if (low != null) ClearCandidate(); armed = true; return; }
        if (price < basis.Lower * (1 - config.BreakBufferPct / 100)) { ClearCandidate(); Snapshot = Snapshot with { State = StageOneState.Invalidated, ActiveUntil = null, KnownAt = at, Reason = "Support below invalidation anchor" }; return; }
        if (low == null)
        {
            if (!armed || lastActivation != default && at - lastActivation < config.MinDistinctEpisodeGap.Elapsed) return;
            watched = basis; low = price; priorRebound = 0; created = at; episode = $"support:{s.Id}:{++sequence}:{e.Sequence}"; armed = false;
            Snapshot = Snapshot with { CandidateLow = low, KnownAt = at, Reason = "New independent approach" }; return;
        }
        if (at - created >= config.CandidateLifetime.Elapsed || watched == null || !SameZone(watched, basis)) { ClearCandidate(); return; }
        if (price < low) { low = price; priorRebound = 0; }
        var rebound = 100 * (price / low!.Value - 1);
        if (priorRebound < config.MinReboundPct && rebound >= config.MinReboundPct)
        {
            var until = at + config.ActivationDuration.Elapsed; var end = sessionEntryEnd ?? s.Close; if (until > end) until = end;
            Snapshot = new(StageOneState.Active, until, episode, watched, low, s.Id, at, "Fresh independent support rebound crossing"); lastActivation = at; ClearCandidate();
        }
        else { priorRebound = rebound; Snapshot = Snapshot with { CandidateLow = low, KnownAt = at }; }
    }
    private bool SameZone(SupportSnapshot a, SupportSnapshot b) => Math.Abs(a.Lower / b.Lower - 1) * 100 <= config.ZoneIdentityTolerancePct && Math.Abs(a.Upper / b.Upper - 1) * 100 <= config.ZoneIdentityTolerancePct;
    private void ClearCandidate() { low = priorRebound = null; watched = null; episode = ""; }
}
