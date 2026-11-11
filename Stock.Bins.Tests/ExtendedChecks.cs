using System.Text.Json;
using System.Text.Json.Nodes;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void ExtendedChecks()
    {
        var c = Config();
        // Set every elapsed field to an equivalent SECOND representation: serialization must preserve the user's units/value exactly.
        var node = JsonNode.Parse(BinsJson.Export(c))!; var durationCount = 0;
        void ConvertDurations(JsonNode? current)
        {
            if (current is JsonObject obj)
            {
                if (obj.Count == 2 && obj["value"] != null && obj["unit"] != null && Enum.TryParse<DurationUnit>(obj["unit"]!.GetValue<string>(), out var unit))
                { var original = new DurationSpec(obj["value"]!.GetValue<decimal>(), unit); obj["value"] = (decimal)original.Elapsed.Ticks / TimeSpan.TicksPerSecond; obj["unit"] = "Second"; durationCount++; return; }
                foreach (var pair in obj.ToArray()) ConvertDurations(pair.Value);
            }
            else if (current is JsonArray array) foreach (var child in array) ConvertDurations(child);
        }
        ConvertDurations(node); var json = node.ToJsonString(); var imported = BinsJson.Import(json);
        Check("CONFIG", durationCount > 20 && JsonNode.DeepEquals(node, JsonNode.Parse(BinsJson.Export(imported))), "All elapsed fields and bar timeframes preserve explicit edited values and units across full import");
        Check("WARMUP", BinsWarmupPlan.For(new()).Bars.Any(b => b.RequiredCompletedBars == 15) && BinsWarmupPlan.For(new()).Bars.All(b => b.RequiredCompletedBars != 240), "Default ATR15 seed is separate from60min observation; Stage1 OFF does not require MA240");
        var dailyConfig = c with { Stage1 = c.Stage1 with { Enabled = true, MovingAverageBar = new(3, BarUnit.TradingDay) } }; dailyConfig.Validate();
        Check("TIME", BinsWarmupPlan.For(dailyConfig).Bars.Any(b => b.Timeframe == new BarTimeframeSpec(3, BarUnit.TradingDay) && b.RequiredCompletedBars == 240), "N-trading-day bar count is supported as provider completed input, distinct from elapsed Day");
        var smallHistory = c with { Quality = c.Quality with { MaximumHistoryBars = 100 } }; smallHistory.Validate();
        Check("B01", BinsWarmupPlan.For(smallHistory).Bars.All(b => b.RequiredCompletedBars <= 100), "Stage1 OFF never imposes a hidden MA240 history capacity requirement");
        var tradeHistory = new BinsHistory(Us, c);
        for (int n = 0; n < 240; n++)
        {
            var end = Epoch.AddDays(n - 240); var bar = new CompletedBar(Us, new(1, BarUnit.TradingDay), "past:" + n, MarketCalendars.TradingDate(Us.Exchange, end), end.AddHours(-6), end, end, 100, 100, 100, 100, 1000, "SYNTHETIC");
            tradeHistory.AddBar(bar, Epoch); tradeHistory.AddProfileTrade(new(Us, end.AddMinutes(-1), end, 100, 1000, "ORIGINAL_FIXTURE", n), Epoch);
        }
        tradeHistory.ResetTrades();
        var tradeSupport = SupportCalculator.Calculate(Us, c.Stage1 with { ProfileSource = ProfileSource.TradeBased }, tradeHistory, Rule(), Epoch);
        Check("B10", tradeSupport.Support is { InputVolume: 240000 } && tradeSupport.Support.Quality.StartsWith("TradeBased"), "Historical genuine profile input survives live observation-window reset and reconciles every bar");
        var lowFee = c.Profile(Us).Fees[0] with { KnownAt = Epoch.AddMinutes(1) };
        var unknownFees = c with { Market = c.Market with { CountryProfiles = c.Market.CountryProfiles with { US = c.Profile(Us) with { Fees = [lowFee] } } } };
        Check("A01", !BinsRisk.Approve(unknownFees, Signal(config: unknownFees), Input(1), Input(1, quote: true).Event, new BinsPaperExecution("USD", 10000), 0, 0, 0, false).Allowed, "Future-known fee/tax schedule cannot authorize an earlier order");
        Check("E07", !BinsRisk.Approve(c, Signal(), Input(1), Input(1, quote: true, ask: 100.015m).Event, new BinsPaperExecution("USD", 10000), 0, 0, 0, false).Allowed, "Off-tick quote prices are rejected using provided rules, not rounded into fake liquidity");
        var krAccount = new BinsPaperExecution("KRW", 10000000); var krEngine = new BinsStrategyEngine(c, [Kr], [krAccount]); var ks = Session(Kr);
        for (int n = 0; n < 14; n++) { var close = 50000 + n * 100; var bar = Bar(n, close, close + 10, close - 10, session: ks, instrument: Kr); krEngine.LoadCompletedBar(bar, bar.KnownAt); }
        foreach (var (second, price) in new[] { (840, 51300m), (841, 51370m), (842, 51375m), (843, 51380m), (844, 51385m) }) { Push(krEngine, Input(second, price, true, instrument: Kr, session: ks)); Push(krEngine, Input(second, price, instrument: Kr, session: ks)); }
        Check("E07", krAccount.Orders is [{ Buy: true, Currency: "KRW", Kind: OrderKind.Limit }] && krAccount.Positions[Kr.Key].Quantity > 0 && krAccount.Orders[0].Price % 1 == 0, "KRX completed-bar classification, tick entry, KRW risk and provided ticks integrate without inherited US/KRX restriction");
        var request = new BinsReplayRequest(c, [Us], BreakoutEvents().ToArray(), TrendBars(), [], Epoch.AddSeconds(840), Epoch.AddSeconds(850), new Dictionary<string, decimal> { ["USD"] = 10000 }, "SYNTHETIC_FIXTURE", ReplayPrecision.OriginalTicks, true, true, DurationSpec.Seconds(.25m), true);
        var replay = new BinsReplayEngine(); var expected = replay.Run(request); var stream = EngineFixture(); var now = request.From;
        foreach (var input in request.Events)
        {
            while (now < input.KnownAt) { if (!stream.Engine.RequiresClock(now)) { now = input.KnownAt; break; } var next = now + request.ClockInterval.Elapsed; if (next > input.KnownAt) next = input.KnownAt; stream.Engine.OnClock(next); now = next; }
            Push(stream.Engine, input);
        }
        while (now < request.To && stream.Engine.RequiresClock(now)) { now += request.ClockInterval.Elapsed; stream.Engine.OnClock(now); }
        stream.Engine.OnClock(request.To);
        Check("E09", BinsJson.Hash(JsonSerializer.Serialize(stream.Engine.Decisions, BinsJson.Options)) == expected.AuditHash, "Direct paper feed with same clock policy matches ENTIRE replay state/decision audit hash");
        var duplicated = replay.Run(request with { Events = request.Events.Concat([request.Events[2]]).ToArray() });
        Check("E09", duplicated.AuditHash == expected.AuditHash && duplicated.Fills.Length == expected.Fills.Length && duplicated.Orders.Length == expected.Orders.Length, "Duplicated original tick/quote callbacks cannot alter audit decisions or create extra fills/orders");
        var noSignal = EngineFixture(); StartBreakout(noSignal.Engine);
        Check("D10", noSignal.Engine.RiskSnapshot("USD", Session().TradingDate) is { ConfirmedEntries: 0, ReservedEntries: 1 }, "Issued but unfilled order reserves a daily entry slot without counting a real position");
        noSignal.Engine.OnClock(Epoch.AddSeconds(846)); noSignal.Engine.OnClock(Epoch.AddSeconds(846.2));
        Check("D10", noSignal.Engine.RiskSnapshot("USD", Session().TradingDate) is { ConfirmedEntries: 0, ReservedEntries: 0 }, "Cancelled zero-fill entry releases reservation and never increments entry counters");
        var lossConfig = c with { Market = c.Market with { CountryProfiles = c.Market.CountryProfiles with { US = c.Profile(Us) with { MaxDailyLoss = .01m } } } };
        var loss = EngineFixture(lossConfig); StartBreakout(loss.Engine); Push(loss.Engine, Input(844, 102.82m, true)); Push(loss.Engine, Input(845, 102.82m));
        Check("D10", loss.Engine.RiskSnapshot("USD", Session().TradingDate) is { ConfirmedEntries: 1, DailyLossLatched: true }, "Confirmed fee/spread loss latches per-currency daily limit without waiting for another new entry");
        Push(loss.Engine, Input(846, 105, true)); Push(loss.Engine, Input(846, 105));
        Check("D10", loss.Engine.RiskSnapshot("USD", Session().TradingDate).DailyLossLatched, "Later unrealized recovery cannot automatically unlock latched daily loss");
        var cancelled = new BinsPaperExecution("USD", 10000); var cancelledIntent = BuyIntent(c); cancelled.SubmitIntent(cancelledIntent); cancelled.Cancel(cancelledIntent.Order, Epoch.AddSeconds(.3)); cancelled.OnTime(Epoch.AddSeconds(.5));
        cancelled.Report(new("late-after-cancel", "buy", "paper:buy", Epoch.AddSeconds(2), OrderState.PartiallyFilled, "known-late-fill", Epoch.AddSeconds(.4), 3, 100, .03m));
        Check("E03", cancelled.Positions[Us.Key].Quantity == 3 && cancelled.Orders[0].State == OrderState.Unknown, "Actual pre-cancel fill arriving AFTER acknowledgement updates holdings and requires remainder reconciliation");
        var callback = EngineFixture(); StartBreakout(callback.Engine); var pending = callback.Account.Orders[0];
        callback.Engine.OnExecutionReport("USD", new("external-timeout", pending.Id, "paper:" + pending.Id, Epoch.AddSeconds(844), OrderState.Unknown), Epoch.AddSeconds(844));
        Check("E04", callback.Engine.Snapshot(Us.Key).AwaitReconciliation && !callback.Account.Synchronized, "Execution callback uses account lock and latches Unknown before any duplicate retry");
        callback.Engine.OnExecutionReport("USD", new("external-query", pending.Id, "paper:" + pending.Id, Epoch.AddSeconds(845), OrderState.Cancelled, ReconciliationConfirmed: true), Epoch.AddSeconds(845)); callback.Engine.ConfirmReconciled(Us.Key, Epoch.AddSeconds(845));
        Check("E04", !callback.Engine.Snapshot(Us.Key).AwaitReconciliation && callback.Account.Orders.Count == 1, "Explicit callback inquiry and reconciliation unlock state without resending old signal");
        var rangeConfig = c with { Regime = c.Regime with { RangeTouchSeparation = DurationSpec.Minutes(1) }, SharedExit = c.SharedExit with { TechnicalStopEnabled = false } };
        var rangeAccount = new BinsPaperExecution("USD", 10000); var rangeEngine = new BinsStrategyEngine(rangeConfig, [Us], [rangeAccount]); foreach (var b in RangeBars()) rangeEngine.LoadCompletedBar(b, b.KnownAt);
        foreach (var (second, price) in new[] { (1080, 99.1m), (1081, 99.1m), (1082, 99.15m), (1083, 99.2m), (1084, 99.4m), (1085, 99.4m) }) { Push(rangeEngine, Input(second, price, true)); Push(rangeEngine, Input(second, price)); }
        Push(rangeEngine, Input(1086, 98.6m, true)); Push(rangeEngine, Input(1086, 98.6m));
        Check("C08", rangeAccount.Positions[Us.Key].Quantity > 0 && rangeEngine.Snapshot(Us.Key).Regime.Box?.State == BoxState.BrokenDown && rangeAccount.Orders.All(o => o.Buy), "Box break is remembered EVEN WHILE HOLDING; it blocks future box reuse without forcing an exit above the separately configured fixed stop");
        var future = EngineFixture(); var futureBar = Bar(40, 999); Check("A01", !future.Engine.LoadCompletedBar(futureBar, Epoch.AddMinutes(15)), "Engine rejects a future bar even when earlier indicators are already warmed");
        future.Engine.LoadCompletedBar(futureBar, futureBar.KnownAt); StartBreakout(future.Engine);
        Check("A01", future.Account.Orders.Count == 0 && future.Engine.Decisions.All(d => d.Kind != "EntrySignal"), "Loading a legitimately later bar advances availability time; rewinding market ticks cannot generate a future-informed signal");
        var transitionBox = new BoxSnapshot("transition", 100, 102, Epoch, Epoch.AddHours(1), Session().Id, 2, 2);
        var transition = Regime(MarketRegime.Range, box: transitionBox) with { CandidateRegime = MarketRegime.Uptrend, CandidateConsecutiveBars = 1, EntryAllowed = false, Evidence = Regime(MarketRegime.Uptrend).Evidence };
        var transitionSelector = new StrategyFamilySelector();
        Check("C10", transitionSelector.Select(c.Selection, transition, Session().TradingDate, false) == StrategyFamily.Range, "First Up candidate cannot suppress the still-committed Range's sole locked-upper-breakout exception");
        var transitionPattern = new PatternFixture(MarketRegime.Range, box: transitionBox) { Regime = transition }; transitionPattern.Seed(0, 101.9m); transitionPattern.Tick(1, 102.11m, StrategyFamily.Range); transitionPattern.Tick(2, 102.12m, StrategyFamily.Range);
        Check("C10", transitionPattern.Tick(3, 102.13m, StrategyFamily.Range).Ready is [{ Pattern: EntryPattern.RangeUpperBreakout }], "Transition exception emits only locked RangeUpperBreakout, never an additional Uptrend entry");
        Check("D09", BinsRisk.IsStopExit(ExitReason.FixedStop, 10) && BinsRisk.IsStopExit(ExitReason.TechnicalStop, 0) && !BinsRisk.IsStopExit(ExitReason.TrailingStop, 10), "Fixed/technical stop retains longer stop cooldown even after profitable partial TPs; profitable trailing exit is a profit exit");
        var series = Enumerable.Range(0, 100).Select(n => Bar(n, 100 + n * .1m + n % 7, 108 + n * .1m + n % 7, 99 + n * .1m)).ToArray();
        var atrState = new WilderAtrAccumulator(14); var emaState = new EmaAccumulator(20); foreach (var b in series) { atrState.Add(b); emaState.Add(b); }
        Check("A10", atrState.Value == BinsMath.WilderAtr(series, 14) && emaState.Value == BinsMath.Ema(series.Select(b => b.Close).ToArray(), 20), "Incremental ATR/EMA exactly equal independently computed full-history formulas after100 bars");
        var small = c with { Quality = c.Quality with { MaximumHistoryBars = 32 } }; var retained = new BinsHistory(Us, small); foreach (var b in series) retained.AddBar(b, b.KnownAt);
        Check("A10", retained.Bars(c.Entries.UptrendPullback.EmaBar, series[^1].KnownAt).Length == 32 && retained.Ema(series[^1].KnownAt) == emaState.Value, "Evicting history buffers never silently reseeds EMA or changes exact smoothing state");
        var reboundConfig = c with { Entries = c.Entries with { DowntrendRebound = c.Entries.DowntrendRebound with { DropAtrMultiplier = 0, BreakBufferPct = 0, MicroHighLookback = DurationSpec.Seconds(2), LowStabilization = DurationSpec.Seconds(1) } } };
        var recross = new PatternFixture(MarketRegime.Downtrend, reboundConfig); recross.Seed(0, 102); recross.Seed(10, 100.1m); recross.Tick(11, 100, StrategyFamily.DowntrendRebound); recross.Tick(12, 100.3m, StrategyFamily.DowntrendRebound); recross.Tick(13, 100.5m, StrategyFamily.DowntrendRebound); recross.Tick(14, 100.05m, StrategyFamily.DowntrendRebound);
        Check("C01", recross.Tick(15, 100.2m, StrategyFamily.DowntrendRebound).Ready.Length == 0, "After a latched rebound fades, a micro-high recross below minimum rebound cannot enter Confirmed mode");
        var secondFrame = new BarTimeframeSpec(1, BarUnit.Second);
        var switchConfig = c with { Regime = c.Regime with { Timeframe = secondFrame, Lookback = DurationSpec.Seconds(4), MinimumBars = 4, ConfirmBars = 1 }, Entries = c.Entries with { UptrendBreakout = c.Entries.UptrendBreakout with { Lookback = DurationSpec.Seconds(4) } } };
        var switchAccount = new BinsPaperExecution("USD", 10000); var switchEngine = new BinsStrategyEngine(switchConfig, [Us], [switchAccount]);
        CompletedBar SecondBar(int n, decimal price) => new(Us, secondFrame, Session().Id, Session().TradingDate, Epoch.AddSeconds(n), Epoch.AddSeconds(n + 1), Epoch.AddSeconds(n + 1), price, price + .05m, price - .05m, price, 100, "CONFIRMED_FIXTURE");
        for (int n = 0; n < 10; n++) { var bar = SecondBar(n, 100 + n * .2m); switchEngine.LoadCompletedBar(bar, bar.KnownAt); }
        Push(switchEngine, Input(10, 101.8m, true)); Push(switchEngine, Input(10, 101.8m)); Push(switchEngine, Input(10.1m, 102.1m, true)); Push(switchEngine, Input(10.1m, 102.1m));
        var firstDown = SecondBar(10, 95); var secondDown = SecondBar(11, 94); switchEngine.LoadCompletedBar(firstDown, firstDown.KnownAt); switchEngine.LoadCompletedBar(secondDown, secondDown.KnownAt);
        Push(switchEngine, Input(12, 94, true)); Push(switchEngine, Input(12, 94));
        Check("D01", switchEngine.Snapshot(Us.Key).SelectedFamily == StrategyFamily.DowntrendRebound && switchEngine.Snapshot(Us.Key).Candidates.Any(p => p.Pattern == EntryPattern.UptrendBreakout && p.State == EntryCandidateState.Invalidated), "Even ConfirmBars=1 and classification updates between ticks invalidate old-family unfinished candidates before a flat switch");
    }
}
