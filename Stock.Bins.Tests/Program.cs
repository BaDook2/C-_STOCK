using System.Text.Json;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private static readonly Instrument Us = new("TEST", Exchange.NASDAQ);
    private static readonly Instrument Kr = new("005930", Exchange.KRX);
    private static readonly DateTimeOffset Epoch = DateTimeOffset.Parse("2026-10-05T13:30:00Z");
    private static readonly List<object> results = [];
    private static int passed;
    private static string output = "artifacts/bins-tests";
    private static void Check(string id, bool condition, string expectation)
    { results.Add(new { id, expectation, actual = condition ? "PASS" : "FAIL" }); if (!condition) throw new InvalidOperationException(id + ": " + expectation); passed++; Console.WriteLine("PASS " + id + ": " + expectation); }
    private static void Throws(string id, Action action, string expectation)
    { try { action(); } catch (Exception e) when (e is InvalidOperationException or JsonException) { Check(id, true, expectation); return; } Check(id, false, expectation); }
    private static BinsStrategyConfig Config()
    {
        var profile = new CountryProfile { MaxOrderCash = 2000, PlannedInvestment = 1000, MaxConcurrentExposure = 5000, RiskBudgetPerPosition = 100, MaxDailyLoss = 1000, MinEntryNetRewardPct = 0,
            Fees = [new(new(2020, 1, 1), new(2030, 1, 1), Epoch.AddDays(-100), "EXPLICIT_SYNTHETIC_FEES_NOT_BROKER", .01m, .01m, 0, 0)] };
        return new() { Market = new() { CountryProfiles = new() { US = profile, KRX = profile with { MaxOrderCash = 2000000, PlannedInvestment = 1000000, MaxConcurrentExposure = 5000000, RiskBudgetPerPosition = 100000, MaxDailyLoss = 1000000 } } },
            Regime = new() { Timeframe = new(1, BarUnit.Minute), Lookback = DurationSpec.Minutes(10), MinimumBars = 8, AtrPeriod = 2, MinTrendMovePct = .1m, AtrTrendMultiplier = 0, StructureAtrTolerance = 0 },
            Entries = new() { UptrendBreakout = new() { Lookback = DurationSpec.Minutes(10) }, UptrendPullback = new() { ImpulseLookback = DurationSpec.Minutes(10) } },
            SharedExit = new() { StopNewEntriesBeforeClose = DurationSpec.Minutes(1), StartLiquidationBeforeClose = DurationSpec.Minutes(.5m) },
            Execution = new() { SimulationParticipationPct = 100 }, Quality = new() { MaximumTradeGap = DurationSpec.Seconds(2) } };
    }
    private static MarketSession Session(Instrument? i = null, DateTimeOffset? start = null)
    {
        var instrument = i ?? Us; var open = start ?? (instrument.Exchange == Exchange.KRX ? DateTimeOffset.Parse("2026-10-05T00:00:00Z") : Epoch);
        return new("fixture:" + instrument.Exchange + ":" + open.ToString("O"), instrument.Exchange, MarketCalendars.TradingDate(instrument.Exchange, open), SessionPhase.Regular, open, open.AddHours(6), open.AddDays(-1), true, true, [OrderKind.Limit]);
    }
    private static PriceRule Rule(decimal tick = .01m) => new("EXPLICIT_FIXTURE_RULE", Epoch.AddDays(-100), Epoch.AddDays(-100), Epoch.AddDays(100), [new(0, null, tick)]);
    private static BinsMarketInput Input(decimal seconds, decimal price = 100, bool quote = false, decimal? bid = null, decimal? ask = null, decimal quantity = 10000, Instrument? instrument = null, MarketSession? session = null)
    {
        var i = instrument ?? Us; var s = session ?? Session(i); var at = s.Open.AddTicks((long)(seconds * TimeSpan.TicksPerSecond));
        var spreadTick = i.Exchange == Exchange.KRX ? 1m : .01m;
        var e = new MarketEvent(i, quote ? EventKind.Quote : EventKind.Trade, at, at, "SYNTHETIC", (long)(seconds * 1000) + (quote ? 1 : 2)) { TradePrice = quote ? null : price, TradeSize = quote ? null : quantity, Bid = quote ? bid ?? price - spreadTick : null, Ask = quote ? ask ?? price + spreadTick : null, BidSize = quote ? quantity : null, AskSize = quote ? quantity : null };
        return new(e, at, s, new(i, s.KnownAt, s.Close.AddDays(1), true, false, true, Rule(i.Exchange == Exchange.KRX ? 1 : .01m)), true);
    }
    private static CompletedBar Bar(int n, decimal close, decimal? high = null, decimal? low = null, BarTimeframeSpec? frame = null, MarketSession? session = null, Instrument? instrument = null)
    {
        var i = instrument ?? Us; var s = session ?? Session(i); var f = frame ?? new BarTimeframeSpec(1, BarUnit.Minute); var end = s.Open.AddMinutes(n + 1);
        return new(i, f, s.Id, s.TradingDate, end.AddMinutes(-1), end, end, close, high ?? close + .05m, low ?? close - .05m, close, 1000, "SYNTHETIC");
    }
    private static CompletedBar[] TrendBars(bool up = true, int count = 14) => Enumerable.Range(0, count).Select(n => Bar(n, up ? 100 + n * .2m : 104 - n * .2m)).ToArray();
    private static RegimeSnapshot Regime(MarketRegime regime, DateTimeOffset? at = null, BoxSnapshot? box = null)
    {
        var time = at ?? Epoch; var c = Config();
        return new(regime, regime, 2, time, time, true, "FIXTURE_CONFIRMED", new(1, .8m, .1m, .1m, .1m, new(regime == MarketRegime.Uptrend, regime == MarketRegime.Downtrend, 100, 99, 101, 100), 2, 1, 2, 2, 10), box, c.ConfigurationHash);
    }
    private static EntrySignal Signal(EntryPattern pattern = EntryPattern.UptrendBreakout, decimal reference = 100, decimal anchor = 100, BinsStrategyConfig? config = null)
    {
        var c = config ?? Config(); return new("fixture-signal:" + pattern, Us.Key, Us.Exchange, pattern is EntryPattern.RangeLowerBounce or EntryPattern.RangeUpperBreakout ? StrategyFamily.Range : pattern == EntryPattern.DowntrendRebound ? StrategyFamily.DowntrendRebound : StrategyFamily.Uptrend,
            pattern, "fixture-episode:" + pattern, Epoch, 1, reference, BinsJson.Export(c), Regime(MarketRegime.Uptrend), new(StageOneState.Disabled, null, null, null, null, Session().Id, Epoch, "bypassed"), anchor, anchor, reference, null, null, .02m, "FIXTURE", c.ConfigurationHash, "FIXTURE");
    }
    private static void Push(BinsStrategyEngine engine, BinsMarketInput row) => engine.OnMarket(row, row.KnownAt);
    private static (BinsStrategyEngine Engine, BinsPaperExecution Account, BinsStrategyConfig Config) EngineFixture(BinsStrategyConfig? config = null)
    {
        var c = config ?? Config(); var a = new BinsPaperExecution("USD", 10000); var engine = new BinsStrategyEngine(c, [Us], [a]);
        foreach (var bar in TrendBars()) engine.LoadCompletedBar(bar, bar.KnownAt);
        return (engine, a, c);
    }
    private static void StartBreakout(BinsStrategyEngine engine)
    {
        Push(engine, Input(840, 102.6m, true)); Push(engine, Input(840, 102.6m));
        Push(engine, Input(841, 102.78m, true)); Push(engine, Input(841, 102.78m));
        Push(engine, Input(842, 102.79m, true)); Push(engine, Input(842, 102.79m));
        Push(engine, Input(843, 102.8m, true)); Push(engine, Input(843, 102.8m));
    }
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--export-default") { File.WriteAllText(args[1], BinsJson.Export(new())); return 0; }
        if (args.Length == 2 && args[0] == "--metadata") { File.WriteAllText(args[1], JsonSerializer.Serialize(BinsValidation.Metadata(), BinsJson.Options)); return 0; }
        if (args.Length == 2 && args[0] == "--validate-guide")
        {
            var blocks = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(args[1]), "```json\\s*([\\s\\S]*?)```");
            if (blocks.Count != 1 || BinsJson.Export(BinsJson.Import(blocks[0].Groups[1].Value)) != BinsJson.Export(new())) throw new InvalidOperationException("Guide must contain the complete actual Bin default import");
            Console.WriteLine("PASS: Bin guide contains one complete strict import matching every actual default field"); return 0;
        }
        try
        {
            output = args.FirstOrDefault() ?? output; Directory.CreateDirectory(output);
            ConfigurationChecks(); DataAndRegimeChecks(); StageOneChecks(); EntryChecks(); PositionAndSelectionChecks(); ExecutionChecks(); EngineAndReplayChecks(); BoundaryChecks(); ExtendedChecks(); BridgeChecks();
            Console.WriteLine($"PASS: {passed} Bin checks; synthetic fixtures, isolated paper ledgers, no network/broker orders"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true })); }
    }
}
