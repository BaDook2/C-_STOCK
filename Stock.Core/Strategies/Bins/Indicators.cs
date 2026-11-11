using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public static class BinsMath
{
    public static decimal EfficiencyRatio(IReadOnlyList<decimal> closes)
    { if (closes.Count < 2) return 0; decimal path = 0; for (int n = 1; n < closes.Count; n++) path += Math.Abs(closes[n] - closes[n - 1]); return path == 0 ? 0 : Math.Abs(closes[^1] - closes[0]) / path; }
    public static decimal? Ema(IReadOnlyList<decimal> closes, int period)
    { if (period < 2 || closes.Count < period) return null; var value = closes.Take(period).Average(); var alpha = 2m / (period + 1); for (int n = period; n < closes.Count; n++) value += alpha * (closes[n] - value); return value; }
    public static decimal? WilderAtr(IReadOnlyList<CompletedBar> bars, int period)
    {
        if (period < 2 || bars.Count < period + 1) return null;
        decimal Tr(int n) => Math.Max(bars[n].High - bars[n].Low, Math.Max(Math.Abs(bars[n].High - bars[n - 1].Close), Math.Abs(bars[n].Low - bars[n - 1].Close)));
        decimal atr = 0; for (int n = 1; n <= period; n++) atr += Tr(n); atr /= period;
        for (int n = period + 1; n < bars.Count; n++) atr = (atr * (period - 1) + Tr(n)) / period;
        return atr;
    }
    /// <summary>Linear interpolation on sorted observations at (n-1)*q (R7 quantile).</summary>
    public static decimal Quantile(IEnumerable<decimal> values, decimal q)
    { var v = values.Order().ToArray(); if (v.Length == 0 || q < 0 || q > 1) throw new InvalidOperationException("Invalid quantile"); var index = (v.Length - 1) * q; var lower = (int)decimal.Floor(index); return v[lower] + (v[Math.Min(lower + 1, v.Length - 1)] - v[lower]) * (index - lower); }
}
/// <summary>Exact Wilder state survives history-buffer eviction; it never silently reseeds a rolling suffix.</summary>
public sealed class WilderAtrAccumulator(int period)
{
    private decimal? previousClose; private decimal seed; private int count;
    public decimal? Value { get; private set; }
    public void Add(CompletedBar bar)
    {
        if (previousClose != null)
        {
            var tr = Math.Max(bar.High - bar.Low, Math.Max(Math.Abs(bar.High - previousClose.Value), Math.Abs(bar.Low - previousClose.Value)));
            if (Value != null) Value = (Value.Value * (period - 1) + tr) / period;
            else { seed += tr; if (++count == period) Value = seed / period; }
        }
        previousClose = bar.Close;
    }
}
public sealed class EmaAccumulator(int period)
{
    private decimal seed; private int count;
    public decimal? Value { get; private set; }
    public DateTimeOffset KnownAt { get; private set; }
    public void Add(CompletedBar bar)
    { if (Value != null) Value += 2m / (period + 1) * (bar.Close - Value.Value); else { seed += bar.Close; if (++count == period) Value = seed / period; } KnownAt = bar.KnownAt; }
}
public sealed record StructureEvidence(bool Up, bool Down, decimal EarlierHigh, decimal EarlierLow, decimal RecentHigh, decimal RecentLow);
public interface IPriceStructureAnalyzer { StructureEvidence Analyze(IReadOnlyList<CompletedBar> bars, decimal epsilon); }
public sealed class HalfWindowStructure : IPriceStructureAnalyzer
{
    public StructureEvidence Analyze(IReadOnlyList<CompletedBar> bars, decimal epsilon)
    {
        var half = bars.Count / 2; if (half == 0) return new(false, false, 0, 0, 0, 0);
        var a = bars.Take(half).ToArray(); var b = bars.Skip(half).ToArray();
        var ha = a.Max(x => x.High); var la = a.Min(x => x.Low); var hb = b.Max(x => x.High); var lb = b.Min(x => x.Low);
        return new(hb > ha + epsilon && lb > la + epsilon, hb < ha - epsilon && lb < la - epsilon, ha, la, hb, lb);
    }
}
public sealed record BoxSnapshot(string Id, decimal Lower, decimal Upper, DateTimeOffset ConfirmedAt, DateTimeOffset ExpiresAt,
    string SessionId, int UpperReactions, int LowerReactions, BoxState State = BoxState.Valid);
public sealed record RegimeEvidence(decimal DirectionPct, decimal ER, decimal ATR, decimal ATRpct, decimal TrendMinMove,
    StructureEvidence Structure, decimal? RangeWidthPct, decimal? InsideRatio, int UpperReactions, int LowerReactions, int ObservationBars);
public sealed record RegimeSnapshot(MarketRegime Regime, MarketRegime CandidateRegime, int CandidateConsecutiveBars,
    DateTimeOffset LastConfirmedAt, DateTimeOffset KnownAt, bool EntryAllowed, string Validity, RegimeEvidence? Evidence,
    BoxSnapshot? Box, string ConfigVersion)
{
    public static RegimeSnapshot Empty(string hash) => new(MarketRegime.Unknown, MarketRegime.Unknown, 0, default, default, false, "InsufficientHistory", null, null, hash);
}
/// <summary>Only NEW usable completed bars advance confirmation. A carried regime does not carry entry permission through Unknown.</summary>
public sealed class MarketRegimeClassifier
{
    private readonly RegimeConfig config; private readonly QualityConfig quality; private readonly IPriceStructureAnalyzer structure;
    private readonly WilderAtrAccumulator atr;
    private readonly List<CompletedBar> history = []; private string session = ""; private DateTimeOffset? consumedBoxAt;
    public RegimeSnapshot Snapshot { get; private set; }
    public string SessionId => session;
    public MarketRegimeClassifier(RegimeConfig config, QualityConfig quality, string hash, IPriceStructureAnalyzer? structure = null)
    { this.config = config; this.quality = quality; this.structure = structure ?? new HalfWindowStructure(); atr = new(config.AtrPeriod); Snapshot = RegimeSnapshot.Empty(hash); }
    public void Invalidate(string reason, DateTimeOffset at) => Snapshot = Snapshot with { EntryAllowed = false, CandidateRegime = MarketRegime.Unknown, CandidateConsecutiveBars = 0, KnownAt = at, Validity = reason };
    public void ResetSession(string id, DateTimeOffset at)
    { session = id; consumedBoxAt = null; Snapshot = RegimeSnapshot.Empty(Snapshot.ConfigVersion) with { KnownAt = at }; }
    public bool OnBar(CompletedBar bar, DateTimeOffset now)
    {
        if (bar.Timeframe != config.Timeframe || !bar.Usable(now) || history.Count > 0 && (bar.ClosedAt <= history[^1].ClosedAt || bar.KnownAt < history[^1].KnownAt)) return false;
        if (session != bar.SessionId) ResetSession(bar.SessionId, now);
        OnClock(now);
        history.Add(bar); if (history.Count > quality.MaximumHistoryBars) history.RemoveAt(0);
        atr.Add(bar);
        var raw = Analyze(history, config, structure, now, bar.SessionId, atrOverride: atr.Value);
        if (raw.Regime == MarketRegime.Range && consumedBoxAt != null)
        {
            // A new box may have the same physical prices, but all of its reaction evidence must be NEW.
            var fresh = Analyze(history, config, structure, now, bar.SessionId, consumedBoxAt, atr.Value);
            raw = fresh.Regime == MarketRegime.Range ? fresh : (MarketRegime.Unknown, fresh.Evidence, 0m, 0m, "Consumed box: awaiting new independent reactions");
        }
        var candidate = raw.Regime; var count = candidate == MarketRegime.Unknown ? 0 : candidate == Snapshot.CandidateRegime ? Math.Min(config.ConfirmBars, Snapshot.CandidateConsecutiveBars + 1) : 1;
        var committed = Snapshot.Regime; var confirmedAt = Snapshot.LastConfirmedAt; var box = Snapshot.Box;
        if (box != null && now >= box.ExpiresAt) box = box with { State = BoxState.Expired };
        if (candidate != MarketRegime.Unknown && count >= config.ConfirmBars)
        {
            if (committed != candidate) { committed = candidate; confirmedAt = now; box = null; }
            if (candidate == MarketRegime.Range && (box == null || box.State != BoxState.Valid))
            {
                // A consumed/broken box cannot be resurrected on the very next bar. Reconfirm a NEW box episode.
                if (raw.Lower > 0 && raw.Upper > raw.Lower)
                { box = new($"box:{bar.SessionId}:{bar.ClosedAt:O}", raw.Lower, raw.Upper, now, now + config.RangeSnapshotLifetime.Elapsed, bar.SessionId, raw.Evidence!.UpperReactions, raw.Evidence.LowerReactions); consumedBoxAt = null; }
            }
        }
        Snapshot = new(committed, candidate, count, confirmedAt, now, candidate != MarketRegime.Unknown && candidate == committed && (candidate != MarketRegime.Range || box is { State: BoxState.Valid }),
            raw.Reason, raw.Evidence, box, Snapshot.ConfigVersion);
        return true;
    }
    public void UpdateBox(BoxState state, DateTimeOffset? at = null)
    { if (Snapshot.Box != null) { if (state != BoxState.Valid) consumedBoxAt = at ?? Snapshot.KnownAt; Snapshot = Snapshot with { Box = Snapshot.Box with { State = state }, EntryAllowed = state == BoxState.Valid && Snapshot.EntryAllowed, CandidateRegime = MarketRegime.Unknown, CandidateConsecutiveBars = 0 }; } }
    public void OnClock(DateTimeOffset at)
    { if (Snapshot.Box is { State: BoxState.Valid } box && at >= box.ExpiresAt) UpdateBox(BoxState.Expired, box.ExpiresAt); }
    public static (MarketRegime Regime, RegimeEvidence? Evidence, decimal Lower, decimal Upper, string Reason) Analyze(
        IReadOnlyList<CompletedBar> all, RegimeConfig c, IPriceStructureAnalyzer structure, DateTimeOffset at, string session, DateTimeOffset? observationStart = null, decimal? atrOverride = null)
    {
        var usable = all.Where(b => b.Timeframe == c.Timeframe && b.Usable(at)).OrderBy(b => b.ClosedAt).ToArray();
        if (usable.Length == 0) return (MarketRegime.Unknown, null, 0, 0, "InsufficientHistory");
        var end = usable[^1].ClosedAt;
        var window = usable.Where(b => b.ClosedAt > end - c.Lookback.Elapsed && (observationStart == null || b.ClosedAt > observationStart) && (c.IncludePreviousSessionForLookback || b.SessionId == session)).ToArray();
        var atr = atrOverride ?? BinsMath.WilderAtr(usable, c.AtrPeriod);
        if (window.Length < c.MinimumBars || atr == null) return (MarketRegime.Unknown, null, 0, 0, "InsufficientHistory: observation and ATR warmup are distinct");
        var closes = window.Select(b => b.Close).ToArray(); var direction = 100 * (closes[^1] / closes[0] - 1); var er = BinsMath.EfficiencyRatio(closes);
        var atrPct = 100 * atr.Value / closes[^1]; var minimum = Math.Max(c.MinTrendMovePct, c.AtrTrendMultiplier * atrPct); var s = structure.Analyze(window, c.StructureAtrTolerance * atr.Value);
        var up = direction >= minimum && er >= c.ERTrend && s.Up; var down = direction <= -minimum && er >= c.ERTrend && s.Down;
        var upper = BinsMath.Quantile(window.Select(b => b.High), c.RangeUpperQuantile); var lower = BinsMath.Quantile(window.Select(b => b.Low), c.RangeLowerQuantile);
        var width = 100 * (upper - lower) / lower; var inside = (decimal)window.Count(b => b.Close >= lower && b.Close <= upper) / window.Length;
        var upperCount = Reactions(window, upper, true, c, atr.Value); var lowerCount = Reactions(window, lower, false, c, atr.Value);
        var range = er <= c.ERRange && upperCount >= c.RangeMinTouchesPerSide && lowerCount >= c.RangeMinTouchesPerSide && width >= c.RangeMinWidthPct && width <= c.RangeMaxWidthPct && inside >= c.RangeMinInsideRatio && !up && !down;
        var evidence = new RegimeEvidence(direction, er, atr.Value, atrPct, minimum, s, width, inside, upperCount, lowerCount, window.Length);
        var n = (up ? 1 : 0) + (down ? 1 : 0) + (range ? 1 : 0);
        return (n != 1 ? MarketRegime.Unknown : up ? MarketRegime.Uptrend : down ? MarketRegime.Downtrend : MarketRegime.Range, evidence, lower, upper, n != 1 ? "Unknown: ambiguous/missing required structure" : "Valid completed-bar structure");
    }
    private static int Reactions(CompletedBar[] bars, decimal boundary, bool upper, RegimeConfig c, decimal atr)
    {
        DateTimeOffset? approach = null, lastEpisode = null; decimal extreme = 0; var count = 0; var armed = true;
        foreach (var b in bars)
        {
            // Prior-bar approach only: OHLC does not tell whether H preceded L within the same bar.
            if (approach != null && b.ClosedAt > approach && (upper ? b.Close <= extreme - c.RangeReversalMinAtr * atr : b.Close >= extreme + c.RangeReversalMinAtr * atr))
            { count++; lastEpisode = approach; approach = null; armed = false; }
            var p = upper ? b.High : b.Low; var near = Math.Abs(p / boundary - 1) * 100 <= c.RangeTouchTolerancePct;
            if (!near) armed = true;
            if (approach == null && armed && near && (lastEpisode == null || b.ClosedAt - lastEpisode >= c.RangeTouchSeparation.Elapsed))
            { approach = b.ClosedAt; extreme = p; armed = false; }
            else if (approach != null) extreme = upper ? Math.Max(extreme, p) : Math.Min(extreme, p);
        }
        return count;
    }
}
/// <summary>History ownership and known-at filtering shared by every indicator/pattern. No future preload can leak into a snapshot.</summary>
public sealed class BinsHistory(Instrument instrument, BinsStrategyConfig config)
{
    private readonly Dictionary<BarTimeframeSpec, List<CompletedBar>> bars = [];
    private readonly List<HistoricalTrade> trades = [];
    private readonly EmaAccumulator ema = new(config.Entries.UptrendPullback.EmaPeriod);
    private readonly Dictionary<(string Source, long Sequence, DateTimeOffset Time), HistoricalTrade> profileTrades = [];
    public bool AddBar(CompletedBar b, DateTimeOffset at)
    {
        if (b.Instrument != instrument || !b.Usable(at)) return false;
        if (!bars.TryGetValue(b.Timeframe, out var list)) bars[b.Timeframe] = list = [];
        if (list.Count > 0 && (list[^1].ClosedAt >= b.ClosedAt || list[^1].KnownAt > b.KnownAt)) return false;
        list.Add(b); if (b.Timeframe == config.Entries.UptrendPullback.EmaBar) ema.Add(b); if (list.Count > config.Quality.MaximumHistoryBars) list.RemoveAt(0); return true;
    }
    public CompletedBar[] Bars(BarTimeframeSpec frame, DateTimeOffset at) => bars.GetValueOrDefault(frame)?.Where(b => b.Usable(at)).ToArray() ?? [];
    public CompletedBar[] Window(BarTimeframeSpec frame, DateTimeOffset at, DurationSpec duration, string session) => Bars(frame, at).Where(b => b.ClosedAt > at - duration.Elapsed && (config.Regime.IncludePreviousSessionForLookback || b.SessionId == session)).ToArray();
    public bool Covers(BarTimeframeSpec frame, DateTimeOffset at, DurationSpec duration, string session)
    { var window = Window(frame, at, duration, session); return window.Length > 0 && window[0].OpenedAt <= at - duration.Elapsed && (frame.Elapsed == null || at - window[^1].ClosedAt <= frame.Elapsed.Value); }
    public decimal? Ema(DateTimeOffset at) => ema.KnownAt <= at ? ema.Value : BinsMath.Ema(Bars(config.Entries.UptrendPullback.EmaBar, at).Select(b => b.Close).ToArray(), config.Entries.UptrendPullback.EmaPeriod);
    public bool AddTrade(HistoricalTrade trade)
    {
        if (trade.Instrument != instrument || trade.Time > trade.KnownAt || trade.Price <= 0 || trade.Size < 0 || string.IsNullOrWhiteSpace(trade.Source) || trades.Count > 0 && (trade.Time < trades[^1].Time || trade.KnownAt < trades[^1].KnownAt)) return false;
        trades.Add(trade); if (trades.Count > config.Quality.MaximumHistoryTicks) trades.RemoveAt(0); return true;
    }
    public HistoricalTrade[] Trades(DateTimeOffset at, DurationSpec duration) => trades.Where(t => t.Time >= at - duration.Elapsed && t.Time <= at && t.KnownAt <= at).ToArray();
    public bool CoversTrades(DateTimeOffset at, DurationSpec duration) => trades.Count > 0 && trades[0].Time <= at - duration.Elapsed && trades[0].KnownAt <= at;
    public bool AddProfileTrade(HistoricalTrade trade, DateTimeOffset at)
    {
        if (trade.Instrument != instrument || trade.Time > trade.KnownAt || trade.KnownAt > at || trade.Price <= 0 || trade.Size < 0 || string.IsNullOrWhiteSpace(trade.Source)) return false;
        var key = (trade.Source, trade.Sequence, trade.Time);
        if (profileTrades.TryGetValue(key, out var previous)) return previous == trade;
        if (profileTrades.Count >= config.Quality.MaximumHistoryTicks) return false;
        profileTrades.Add(key, trade); return true;
    }
    public HistoricalTrade[] ProfileTrades(DateTimeOffset at, DateTimeOffset from, DateTimeOffset to) => profileTrades.Values.Concat(trades)
        .Where(t => t.Time >= from && t.Time < to && t.KnownAt <= at).DistinctBy(t => (t.Source, t.Sequence, t.Time)).OrderBy(t => t.Time).ToArray();
    public void ResetTrades() => trades.Clear();
}

public sealed record BarWarmup(BarTimeframeSpec Timeframe, int RequiredCompletedBars, string Purpose);
public sealed record BinsWarmupPlan(BarWarmup[] Bars, DurationSpec RegimeObservation, DurationSpec TradeObservation)
{
    public static BinsWarmupPlan For(BinsStrategyConfig config)
    {
        config.Validate();
        var plan = new List<BarWarmup> { new(config.Regime.Timeframe, Math.Max(config.Regime.MinimumBars, config.Regime.AtrPeriod + 1), "Classification observation + separate Wilder ATR seed") };
        if (config.Entries.UptrendPullback.Enabled) plan.Add(new(config.Entries.UptrendPullback.EmaBar, config.Entries.UptrendPullback.EmaPeriod, "Confirmed EMA seed"));
        if (config.Regime.Timeframe.Elapsed is {} interval)
        { if (config.Entries.UptrendBreakout.Enabled) plan.Add(new(config.Regime.Timeframe, checked((int)decimal.Ceiling((decimal)config.Entries.UptrendBreakout.Lookback.Elapsed.Ticks / interval.Ticks)), "Prior breakout resistance lookback")); if (config.Entries.UptrendPullback.Enabled) plan.Add(new(config.Regime.Timeframe, checked((int)decimal.Ceiling((decimal)config.Entries.UptrendPullback.ImpulseLookback.Elapsed.Ticks / interval.Ticks)), "Confirmed impulse lookback")); }
        if (config.Stage1.Enabled) { plan.Add(new(config.Stage1.MovingAverageBar, config.Stage1.MovingAveragePeriod, "Stage1 MA")); plan.Add(new(config.Stage1.ProfileBar, config.Stage1.ProfileLookbackBars, "Stage1 volume profile")); }
        var trade = new[] { config.Entries.DowntrendRebound.DropLookback, config.Entries.DowntrendRebound.MicroHighLookback, config.Entries.UptrendPullback.MicroHighLookback }.MaxBy(d => d.Elapsed)!;
        return new(plan.ToArray(), config.Regime.Lookback, trade);
    }
}
