using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private static SupportSnapshot Support(decimal lower = 100, decimal upper = 100.1m) => new("support-fixture", 100, lower, upper, lower, upper, Epoch, "ApproximateOHLCV", [new(lower, upper, 1000)], 1000, "FIXTURE");
    private static void StageOneChecks()
    {
        var disabled = new SupportActivationGate(new()); disabled.OnTrade(Input(1), null);
        Check("B01", disabled.Snapshot.Allows(Epoch.AddSeconds(1), Session().Id), "Stage1 OFF completely bypasses missing MA/profile");
        var c = new StageOneConfig { Enabled = true }; var gate = new SupportActivationGate(c);
        gate.OnTrade(Input(1), null); Check("B02", !gate.Snapshot.Allows(Epoch.AddSeconds(1), Session().Id), "Stage1 ON cannot bypass missing support");
        gate.OnTrade(Input(2, 100), Support()); gate.OnTrade(Input(3, 100.6m), Support());
        var until = gate.Snapshot.ActiveUntil;
        Check("B03", gate.Snapshot.State == StageOneState.Active && until == Epoch.AddSeconds(3).AddMinutes(30), "Support approach then fresh .5% crossing grants30min");
        gate.OnTrade(Input(4, 100.7m), Support()); gate.OnTrade(Input(5, 100.5m), Support());
        Check("B05", gate.Snapshot.ActiveUntil == until, "Same support rebound episode cannot extend its own timer");
        Check("B04", gate.Snapshot.Allows(Epoch.AddMinutes(5), Session().Id) && gate.Snapshot.Allows(Epoch.AddMinutes(10), Session().Id), "Permission remains valid across independent Stage2 trades; not consumed by entry/exit");
        gate.OnTrade(Input(1200, 103), Support()); gate.OnTrade(Input(1201, 100), Support()); gate.OnTrade(Input(1202, 100.6m), Support());
        Check("B06", gate.Snapshot.ActiveUntil == Epoch.AddSeconds(1202).AddMinutes(30) && gate.Snapshot.ActivationEpisodeId != "", "Distinct departure and new approach20min later refreshes from NEW rebound time");
        var immutableLower = gate.Snapshot.LockedSupport!.Lower;
        gate.OnTrade(Input(1203, 99.5m), Support(95, 95.1m));
        Check("B07", gate.Snapshot.State == StageOneState.Invalidated && gate.Snapshot.ActiveUntil == null && immutableLower == 100, "New lower recalculated support cannot erase break of frozen original lower");
        gate.OnTrade(Input(1204, 100.6m), Support());
        Check("B08", gate.Snapshot.State == StageOneState.Invalidated, "Old rebound condition cannot reactivate broken permission");
        gate.OnTrade(Input(1220, 103), Support()); gate.OnTrade(Input(1221, 100), Support()); gate.OnTrade(Input(1222, 100.6m), Support());
        Check("B08", gate.Snapshot.State == StageOneState.Active, "New independent post-break approach and crossing can reactivate");
        gate.OnClock(Session().Close, Session()); Check("B09", !gate.Snapshot.Allows(Session().Close, Session().Id) && gate.Snapshot.State == StageOneState.Expired, "Session close expires permission and never carries it automatically");
        var h = new BinsHistory(Us, Config()); var insufficient = SupportCalculator.Calculate(Us, c, h, Rule(), Epoch);
        Check("B10", insufficient.Support == null && insufficient.Validity.Contains("InsufficientHistory"), "240 required daily bars are not automatically reduced");
        for (int n = 0; n < 240; n++) { var end = Epoch.AddDays(n - 240); h.AddBar(new(Us, new(1, BarUnit.TradingDay), "past:" + n, MarketCalendars.TradingDate(Us.Exchange, end), end.AddHours(-6), end, end, 100, 100, 100, 100, 1000, "SYNTHETIC"), Epoch); }
        var calculated = SupportCalculator.Calculate(Us, c, h, Rule(), Epoch);
        Check("B03", calculated.Support is {} support && support.MovingAverage == 100 && support.Bins.Sum(b => b.Volume) == 240000 && support.InputVolume == 240000 && support.Quality.StartsWith("Approximate"), "MA240 and uniformly allocated approximate profile conserve every volume unit and identify quality");
        var tradeBased = SupportCalculator.Calculate(Us, c with { ProfileSource = ProfileSource.TradeBased }, h, Rule(), Epoch);
        Check("B10", tradeBased.Support == null, "TradeBased cannot relabel OHLCV volume without original reconciled trades");
    }
    private sealed class PatternFixture
    {
        public readonly BinsStrategyConfig Config;
        public readonly BinsHistory History;
        public readonly EntryPatternBook Book;
        public RegimeSnapshot Regime;
        public decimal? Prior;
        public PatternFixture(MarketRegime regime, BinsStrategyConfig? config = null, BoxSnapshot? box = null)
        { Config = config ?? Program.Config(); History = new(Us, Config); Book = new(Config); Regime = Program.Regime(regime, box: box); }
        public PatternEvaluation Tick(decimal seconds, decimal price, StrategyFamily family)
        {
            var row = Input(seconds, price); var result = Book.Evaluate(new(row, Regime, History, Prior), family);
            History.AddTrade(new(Us, row.Event.ExchangeTime, row.KnownAt, price, 100, "SYNTHETIC", row.Event.Sequence)); Prior = price; return result;
        }
        public void Seed(decimal seconds, decimal price) { var row = Input(seconds, price); History.AddTrade(new(Us, row.Event.ExchangeTime, row.KnownAt, price, 100, "SYNTHETIC", row.Event.Sequence)); Prior = price; }
    }
    private static void EntryChecks()
    {
        var c = Config();
        PatternFixture Down(DowntrendEntryMode mode) => new(MarketRegime.Downtrend, c with { Entries = c.Entries with { DowntrendRebound = c.Entries.DowntrendRebound with { EntryMode = mode, DropAtrMultiplier = 0 } } });
        var fast = Down(DowntrendEntryMode.Fast); fast.Seed(0, 100); fast.Tick(11, 98.8m, StrategyFamily.DowntrendRebound);
        var fastEntry = fast.Tick(17, 99.3m, StrategyFamily.DowntrendRebound);
        var confirmed = Down(DowntrendEntryMode.Confirmed); confirmed.Seed(0, 100); confirmed.Seed(10, 99.4m); confirmed.Tick(11, 98.8m, StrategyFamily.DowntrendRebound);
        var denied = confirmed.Tick(17, 99.3m, StrategyFamily.DowntrendRebound); var confirmEntry = confirmed.Tick(18, 99.55m, StrategyFamily.DowntrendRebound);
        Check("C01", fastEntry.Ready.Length == 1 && denied.Ready.Length == 0 && confirmEntry.Ready is [{ Pattern: EntryPattern.DowntrendRebound }], "Fast uses fresh rebound; Confirmed additionally breaks pre-rebound micro high");
        Check("C01", confirmEntry.Ready[0].MicroHigh == 99.4m && confirmEntry.Ready[0].MicroHighKnownAt < Epoch.AddSeconds(18), "Micro high and its availability are frozen BEFORE signal tick");
        Check("C02", confirmed.Tick(19, 99.6m, StrategyFamily.DowntrendRebound).Ready.Length == 0, "Sustained rebound emits no repeated downtrend order");
        var lows = Down(DowntrendEntryMode.Fast); lows.Seed(0, 100); lows.Tick(1, 98.8m, StrategyFamily.DowntrendRebound); lows.Tick(5, 98.7m, StrategyFamily.DowntrendRebound);
        Check("C02", lows.Tick(7, 99.1m, StrategyFamily.DowntrendRebound).Ready.Length == 0 && lows.Book.Candidates.Single().LastLowAt == Epoch.AddSeconds(5), "New low restarts stabilization; early rebound cannot exploit old timer");
        var breakout = new PatternFixture(MarketRegime.Uptrend);
        foreach (var b in TrendBars()) breakout.History.AddBar(b, b.KnownAt);
        breakout.Seed(840, 102.6m); var zero = breakout.Tick(841, 102.78m, StrategyFamily.Uptrend); var one = breakout.Tick(842, 102.79m, StrategyFamily.Uptrend); var two = breakout.Tick(843, 102.8m, StrategyFamily.Uptrend);
        Check("C03", zero.Ready.Length == 0 && one.Ready.Length == 0, "Breakout cannot buy before 2 second hold");
        Check("C04", two.Ready is [{ Pattern: EntryPattern.UptrendBreakout }], "Frozen prior high+2 seconds+minimum distinct valid trades yield one breakout");
        Check("C04", breakout.Tick(844, 102.81m, StrategyFamily.Uptrend).Ready.Length == 0, "Same breakout held true cannot buy twice");
        var broken = new PatternFixture(MarketRegime.Uptrend); foreach (var b in TrendBars()) broken.History.AddBar(b, b.KnownAt); broken.Seed(840, 102.6m); broken.Tick(841, 102.78m, StrategyFamily.Uptrend); broken.Book.ResetCandidates("No valid ticks for hold");
        Check("C03", broken.Tick(850, 102.8m, StrategyFamily.Uptrend).Ready.Length == 0, "Data gap invalidates elapsed hold; late tick cannot backfill evidence");
        var pullConfig = c with { Entries = c.Entries with { UptrendBreakout = c.Entries.UptrendBreakout with { Enabled = false }, UptrendPullback = c.Entries.UptrendPullback with { EmaPeriod = 2, MaxEMADistancePct = 2 } } };
        var pull = new PatternFixture(MarketRegime.Uptrend, pullConfig);
        for (int n = 0; n < 14; n++) pull.History.AddBar(Bar(n, 100, 101, 99.9m), Epoch.AddMinutes(n + 1));
        pull.Seed(830, 100.2m); pull.Seed(840, 100.2m); var approach = pull.Tick(841, 100, StrategyFamily.Uptrend); pull.Tick(843, 100.1m, StrategyFamily.Uptrend); var pullEntry = pull.Tick(845, 100.35m, StrategyFamily.Uptrend);
        Check("C05", approach.Ready.Length == 0 && pullEntry.Ready is [{ Pattern: EntryPattern.UptrendPullback }], "Pullback requires correction, confirmed EMA proximity, stable low, fresh rebound AND prior micro break");
        var deep = new PatternFixture(MarketRegime.Uptrend, pullConfig); foreach (var b in TrendBars()) deep.History.AddBar(b, b.KnownAt); deep.Seed(830, 102); deep.Seed(840, 102); deep.Tick(841, 102, StrategyFamily.Uptrend);
        Check("C06", deep.Book.Candidates.Any(p => p.State == EntryCandidateState.Watching), "Overdeep-pullback fixture first establishes a valid warmed candidate");
        Check("C06", deep.Tick(842, 97, StrategyFamily.Uptrend).Ready.Length == 0 && deep.Book.Candidates.All(p => p.State == EntryCandidateState.Invalidated), "Overdeep pullback invalidates candidate");
        var box = new BoxSnapshot("fixed-box", 100, 102, Epoch, Epoch.AddHours(1), Session().Id, 2, 2);
        var lower = new PatternFixture(MarketRegime.Range, box: box); var near = lower.Tick(1, 100.1m, StrategyFamily.Range); lower.Tick(3, 100.15m, StrategyFamily.Range); var lowerEntry = lower.Tick(5, 100.4m, StrategyFamily.Range);
        Check("C07", near.Ready.Length == 0 && lowerEntry.Ready is [{ Pattern: EntryPattern.RangeLowerBounce }], "Box approach alone is not a buy; stable fresh low-relative crossing required");
        var collapse = new PatternFixture(MarketRegime.Range, box: box); collapse.Tick(1, 100.1m, StrategyFamily.Range); var collapseEvent = collapse.Tick(2, 99.6m, StrategyFamily.Range);
        Check("C08", collapseEvent.BoxBrokenDown && collapseEvent.Ready.Length == 0, "Frozen lower break explicitly consumes invalid box candidates");
        var upper = new PatternFixture(MarketRegime.Range, box: box); upper.Seed(0, 101.9m); upper.Tick(1, 102.11m, StrategyFamily.Range); upper.Tick(2, 102.12m, StrategyFamily.Range); var upperEntry = upper.Tick(3, 102.13m, StrategyFamily.Range);
        Check("C09", upperEntry.Ready is [{ Pattern: EntryPattern.RangeUpperBreakout }] && upperEntry.BoxBrokenUp, "Valid held upper break is one range breakout and consumes box");
        Check("C10", upperEntry.Ready.All(p => p.Pattern != EntryPattern.UptrendBreakout), "Range breakout never concurrently dispatches an Uptrend family order");
        foreach (var pattern in Enum.GetValues<EntryPattern>())
        {
            var approval = BinsRisk.Approve(c, Signal(pattern, 100, 99), Input(1, 100), Input(1, 100, true, ask: 101).Event, new BinsPaperExecution("USD", 10000), 0, 0, 0, false);
            Check("C11", !approval.Allowed, pattern + " cannot execute through excessive slippage/chase");
        }
    }
}
