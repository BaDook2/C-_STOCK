using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public sealed record EntryCandidateSnapshot(EntryPattern Pattern, EntryCandidateState State, string EpisodeId, DateTimeOffset CreatedAt,
    DateTimeOffset LastLowAt, decimal Low, decimal High, decimal Reference, decimal? MicroHigh, DateTimeOffset? MicroHighKnownAt, int ValidTicks, string Reason);
public sealed record EntrySignal(string Id, string Symbol, Exchange Market, StrategyFamily Family, EntryPattern EntryPattern,
    string EpisodeId, DateTimeOffset Time, long RawTickSequence, decimal ReferencePrice, string ThresholdsSnapshot,
    RegimeSnapshot RegimeSnapshot, StageOneSnapshot Stage1Snapshot, decimal TechnicalStopAnchor,
    decimal CandidateLow, decimal CandidateHigh, decimal? MicroHigh, DateTimeOffset? MicroHighKnownAt,
    decimal SpreadPct, string QuoteQuality, string ConfigurationHash, string Reason);
public sealed record PatternIntent(EntryPattern Pattern, string EpisodeId, decimal TechnicalAnchor, decimal Low, decimal High,
    decimal? MicroHigh, DateTimeOffset? MicroHighKnownAt, string Reason);
public sealed record PatternEvaluation(PatternIntent[] Ready, EntryCandidateSnapshot[] Candidates, bool BoxBrokenUp, bool BoxBrokenDown)
{ public string[] InsufficientHistory { get; init; } = []; }
public sealed record EntryContext(BinsMarketInput Input, RegimeSnapshot Regime, BinsHistory History, decimal? PreviousPrice);

/// <summary>Five independent patterns. Micro/resistance/box anchors never contain the signal tick or an unfinished bar.</summary>
public sealed class EntryPatternBook
{
    private sealed class Candidate(EntryPattern pattern, string episode, DateTimeOffset at, decimal price, decimal reference, decimal? micro, DateTimeOffset? microAt)
    {
        public EntryPattern Pattern = pattern; public string Episode = episode; public DateTimeOffset Created = at, LowAt = at;
        public decimal Low = price, High = price, Reference = reference, PreviousRebound; public decimal? Micro = micro; public DateTimeOffset? MicroAt = microAt;
        public int Ticks = 1; public bool ReboundConfirmed; public EntryCandidateState State = EntryCandidateState.Watching; public string Reason = "Watching";
        public EntryCandidateSnapshot Snapshot() => new(Pattern, State, Episode, Created, LowAt, Low, High, Reference, Micro, MicroAt, Ticks, Reason);
    }
    private readonly BinsStrategyConfig config;
    private readonly Dictionary<EntryPattern, Candidate> candidates = [];
    private readonly Dictionary<EntryPattern, decimal> consumedLow = [];
    private readonly Dictionary<EntryPattern, decimal> consumedResistance = [];
    private readonly HashSet<EntryPattern> rearmed = [];
    private long episodeSequence;
    private readonly List<string> missingHistory = [];
    public EntryPatternBook(BinsStrategyConfig config) { this.config = config; }
    public EntryCandidateSnapshot[] Candidates => candidates.Values.Select(c => c.Snapshot()).ToArray();
    public void ResetCandidates(string reason)
    { foreach (var c in candidates.Values) if (c.State is EntryCandidateState.Watching or EntryCandidateState.Confirming) { c.State = EntryCandidateState.Invalidated; c.Reason = reason; } }
    public void ResetSession() { candidates.Clear(); consumedLow.Clear(); consumedResistance.Clear(); rearmed.Clear(); }
    public void ObserveWhileBlocked(decimal price)
    {
        // Genuine movement during holdings may re-arm a later episode, but never creates an entry by itself.
        foreach (var p in consumedResistance) if (price < p.Value) rearmed.Add(p.Key);
        foreach (var p in consumedLow) if (price < p.Value) rearmed.Add(p.Key);
        ResetCandidates("Holding/order/risk/permission blocks new candidates");
    }
    public PatternEvaluation Evaluate(EntryContext x, StrategyFamily family)
    {
        missingHistory.Clear();
        var ready = new List<PatternIntent>(); var brokenUp = false; var brokenDown = false;
        var price = x.Input.Event.TradePrice!.Value; var at = x.Input.KnownAt; var box = x.Regime.Box;
        if (box is { State: BoxState.Valid } && price < box.Lower * (1 - config.Entries.RangeLowerBounce.BreakTolerancePct / 100)) { ResetCandidates("Frozen box broken down"); return new([], Candidates, false, true); }
        if (family == StrategyFamily.DowntrendRebound && config.Entries.DowntrendRebound.Enabled)
            Rebound(x, EntryPattern.DowntrendRebound, ready);
        if (family == StrategyFamily.Uptrend)
        {
            if (config.Entries.UptrendBreakout.Enabled) Breakout(x, EntryPattern.UptrendBreakout, config.Entries.UptrendBreakout, ready);
            if (config.Entries.UptrendPullback.Enabled) Rebound(x, EntryPattern.UptrendPullback, ready);
        }
        if (family == StrategyFamily.Range && box is { State: BoxState.Valid } && at < box.ExpiresAt)
        {
            if (config.Entries.RangeUpperBreakout.Enabled) { Breakout(x, EntryPattern.RangeUpperBreakout, config.Entries.RangeUpperBreakout, ready); brokenUp = ready.Any(r => r.Pattern == EntryPattern.RangeUpperBreakout); }
            if (!brokenUp && x.Regime.EntryAllowed && config.Entries.RangeLowerBounce.Enabled) Rebound(x, EntryPattern.RangeLowerBounce, ready);
        }
        return new(ready.OrderBy(i => Array.IndexOf(config.Entries.PatternPriority, i.Pattern)).ToArray(), Candidates, brokenUp, brokenDown) { InsufficientHistory = missingHistory.Distinct().ToArray() };
    }
    private (decimal? Price, DateTimeOffset? KnownAt) Micro(EntryContext x, DurationSpec duration)
    { if (!x.History.CoversTrades(x.Input.KnownAt, duration)) return (null, null); var past = x.History.Trades(x.Input.KnownAt, duration).Where(t => t.Time < x.Input.Event.ExchangeTime || t.Time == x.Input.Event.ExchangeTime && t.Sequence < x.Input.Event.Sequence).ToArray(); return past.Length == 0 ? (null, null) : (past.Max(t => t.Price), past.Max(t => t.KnownAt)); }
    private Candidate New(EntryContext x, EntryPattern pattern, decimal reference, DurationSpec? micro)
    {
        var m = micro == null ? (Price: (decimal?)null, KnownAt: (DateTimeOffset?)null) : Micro(x, micro);
        var c = new Candidate(pattern, $"{x.Input.Session!.Id}:{pattern}:{++episodeSequence}:{x.Input.Event.Sequence}", x.Input.KnownAt, x.Input.Event.TradePrice!.Value, reference, m.Price, m.KnownAt);
        candidates[pattern] = c; return c;
    }
    private static bool Active(Candidate? c) => c?.State is EntryCandidateState.Watching or EntryCandidateState.Confirming;
    private void Rebound(EntryContext x, EntryPattern pattern, List<PatternIntent> ready)
    {
        var e = x.Input.Event; var price = e.TradePrice!.Value; var at = x.Input.KnownAt; var down = config.Entries.DowntrendRebound; var pull = config.Entries.UptrendPullback; var range = config.Entries.RangeLowerBounce;
        if ((pattern == EntryPattern.UptrendPullback || pattern == EntryPattern.DowntrendRebound && down.EntryMode == DowntrendEntryMode.Confirmed) && !x.History.CoversTrades(at, pattern == EntryPattern.UptrendPullback ? pull.MicroHighLookback : down.MicroHighLookback)) { missingHistory.Add(pattern + ": complete prior micro-high window"); return; }
        DurationSpec lifetime, stability, micro; decimal threshold, chase, reference; bool eligible;
        if (pattern == EntryPattern.DowntrendRebound)
        {
            var trades = x.History.Trades(at, down.DropLookback); reference = trades.Length == 0 ? price : Math.Max(price, trades.Max(t => t.Price));
            eligible = x.Regime.Evidence is {} ev && 100 * (1 - price / reference) >= Math.Max(down.MinDropPct, down.DropAtrMultiplier * ev.ATRpct);
            lifetime = down.CandidateLifetime; stability = down.LowStabilization; threshold = down.MinReboundPct; chase = down.MaxChasePct; micro = down.MicroHighLookback;
        }
        else if (pattern == EntryPattern.UptrendPullback)
        {
            var bars = x.History.Window(config.Regime.Timeframe, at, pull.ImpulseLookback, x.Input.Session!.Id); reference = bars.Length == 0 ? 0 : bars.Max(b => b.High);
            var ema = x.History.Ema(at); var correction = reference > 0 ? 100 * (1 - price / reference) : -1;
            if (ema == null || !x.History.Covers(config.Regime.Timeframe, at, pull.ImpulseLookback, x.Input.Session!.Id)) missingHistory.Add(pattern + ": confirmed EMA/impulse bars");
            eligible = x.History.Covers(config.Regime.Timeframe, at, pull.ImpulseLookback, x.Input.Session!.Id) && ema > 0 && correction >= pull.MinPullbackPct && correction <= pull.MaxPullbackPct && 100 * Math.Abs(price - ema.Value) / ema <= pull.MaxEMADistancePct;
            lifetime = pull.CandidateLifetime; stability = pull.LowStabilization; threshold = pull.MinReboundPct; chase = pull.MaxChasePct; micro = pull.MicroHighLookback;
        }
        else
        {
            reference = x.Regime.Box!.Lower; eligible = price <= reference * (1 + range.ApproachPct / 100);
            lifetime = range.CandidateLifetime; stability = range.LowStabilization; threshold = range.MinReboundPct; chase = range.MaxChasePct; micro = down.MicroHighLookback;
        }
        candidates.TryGetValue(pattern, out var c);
        if (!Active(c))
        {
            if (!eligible) { rearmed.Add(pattern); return; }
            if (consumedLow.TryGetValue(pattern, out var used) && !rearmed.Contains(pattern) && price >= used) return;
            c = New(x, pattern, reference, pattern == EntryPattern.RangeLowerBounce ? null : micro); rearmed.Remove(pattern); return;
        }
        if (at - c!.Created >= lifetime.Elapsed) { c.State = EntryCandidateState.Expired; c.Reason = "Candidate lifetime"; consumedLow[pattern] = c.Low; return; }
        // Recheck changing structures, including excessive pullback; frozen anchors still remain frozen.
        if (pattern == EntryPattern.UptrendPullback && (!eligible && (price > c.Low * (1 + chase / 100) || 100 * (1 - price / c.Reference) > pull.MaxPullbackPct))) { Invalidate(c, "Pullback/chase invalid"); return; }
        c.Ticks++; c.High = Math.Max(c.High, price);
        if (price < c.Low)
        { c.Low = price; c.LowAt = at; c.PreviousRebound = 0; c.ReboundConfirmed = false; var m = Micro(x, micro); c.Micro = m.Price; c.MicroAt = m.KnownAt; c.Reason = "New low resets stabilization and pre-rebound micro anchor"; return; }
        var rebound = 100 * (price / c.Low - 1); var cross = c.PreviousRebound < threshold && rebound >= threshold; c.PreviousRebound = rebound;
        var stable = at - c.LowAt >= stability.Elapsed && c.Ticks >= 2;
        // The first crossing must occur AFTER stability; holding a rebound true is not a new event.
        if (cross && stable) { c.ReboundConfirmed = true; c.State = EntryCandidateState.Confirming; }
        if (price > (pattern == EntryPattern.RangeLowerBounce ? c.Reference : c.Low) * (1 + chase / 100)) { Invalidate(c, "Maximum chase exceeded"); return; }
        var microBuffer = pattern == EntryPattern.DowntrendRebound ? down.BreakBufferPct : pull.MicroBreakBufferPct;
        var needsMicro = pattern == EntryPattern.UptrendPullback || pattern == EntryPattern.DowntrendRebound && down.EntryMode == DowntrendEntryMode.Confirmed;
        var microCross = c.Micro > 0 && c.MicroAt <= at && price >= c.Micro * (1 + microBuffer / 100) && x.PreviousPrice < c.Micro * (1 + microBuffer / 100);
        var qualifies = needsMicro ? c.ReboundConfirmed && rebound >= threshold && stable && microCross : cross && stable;
        if (qualifies)
        {
            if (pattern == EntryPattern.UptrendPullback && !eligible) { Invalidate(c, "EMA/correction no longer valid at entry"); return; }
            var anchor = pattern == EntryPattern.RangeLowerBounce ? c.Reference : c.Low;
            Signal(c, ready, anchor, needsMicro ? "Stable low, fresh rebound and frozen prior micro high break" : "Stable low and fresh rebound crossing");
        }
    }
    private void Breakout(EntryContext x, EntryPattern pattern, BreakoutConfig p, List<PatternIntent> ready)
    {
        var price = x.Input.Event.TradePrice!.Value; var at = x.Input.KnownAt;
        candidates.TryGetValue(pattern, out var c);
        var past = x.History.Window(config.Regime.Timeframe, at, p.Lookback, x.Input.Session!.Id);
        if (pattern == EntryPattern.UptrendBreakout && !Active(c) && !x.History.Covers(config.Regime.Timeframe, at, p.Lookback, x.Input.Session!.Id)) { missingHistory.Add(pattern + ": full confirmed resistance lookback"); return; }
        var reference = pattern == EntryPattern.RangeUpperBreakout ? x.Regime.Box!.Upper : past.Length == 0 ? 0 : past.Max(b => b.High);
        if (reference <= 0) return;
        var floor = reference * (1 + p.BreakoutBufferPct / 100);
        if (!Active(c))
        {
            if (price < floor) { rearmed.Add(pattern); return; }
            // Require a real upward crossing. Late startup already above resistance is not a breakout.
            if (x.PreviousPrice == null || x.PreviousPrice >= floor) return;
            if (consumedResistance.TryGetValue(pattern, out var used) && reference == used && !rearmed.Contains(pattern)) return;
            c = New(x, pattern, reference, null); c.State = EntryCandidateState.Confirming; rearmed.Remove(pattern);
        }
        else { reference = c!.Reference; floor = reference * (1 + p.BreakoutBufferPct / 100); c.Ticks++; c.High = Math.Max(c.High, price); }
        if (at - c!.Created >= p.CandidateLifetime.Elapsed || price < floor || price > reference * (1 + p.MaxChasePct / 100)) { Invalidate(c, "Breakout failed/expired/chased"); return; }
        if (at - c.Created >= p.HoldDuration.Elapsed && c.Ticks >= p.MinValidTicks) Signal(c, ready, c.Reference, "Frozen resistance held with continuous valid trades");
    }
    private void Signal(Candidate c, List<PatternIntent> ready, decimal anchor, string reason)
    { c.State = EntryCandidateState.Signaled; c.Reason = reason; consumedLow[c.Pattern] = c.Low; if (c.Pattern is EntryPattern.UptrendBreakout or EntryPattern.RangeUpperBreakout) consumedResistance[c.Pattern] = c.Reference; ready.Add(new(c.Pattern, c.Episode, anchor, c.Low, c.High, c.Micro, c.MicroAt, reason)); }
    private void Invalidate(Candidate c, string reason) { c.State = EntryCandidateState.Invalidated; c.Reason = reason; consumedLow[c.Pattern] = c.Low; }
}
/// <summary>Separate daily family lock. Holdings and ANY unresolved order prevent a family/config change.</summary>
public sealed class StrategyFamilySelector
{
    private DateOnly date; public StrategyFamily? Selected { get; private set; }
    public StrategyFamily? Select(SelectionConfig config, RegimeSnapshot regime, DateOnly tradingDate, bool unresolved)
    {
        if (unresolved) return Selected;
        if (date != tradingDate) { date = tradingDate; Selected = null; }
        // The sole transition exception: a still-committed Range can finish its locked upper breakout
        // while the first completed Up candidate awaits confirmation. Never authorizes the old lower bounce.
        var valid = regime.EntryAllowed || regime.Regime == MarketRegime.Range && regime.CandidateRegime == MarketRegime.Uptrend && regime.Box is { State: BoxState.Valid } && regime.Evidence?.Structure.Up == true;
        var family = regime.Regime switch { MarketRegime.Downtrend => StrategyFamily.DowntrendRebound, MarketRegime.Uptrend => StrategyFamily.Uptrend, MarketRegime.Range => StrategyFamily.Range, _ => (StrategyFamily?)null };
        if (!valid || family == null) return null;
        if (config.Mode == StrategySelectionMode.Manual) { if (family != config.ManualFamily) return null; Selected = config.ManualFamily; return Selected; }
        if (Selected == null || config.AutoSwitchAfterExit) Selected = family;
        return Selected == family ? Selected : null;
    }
}
