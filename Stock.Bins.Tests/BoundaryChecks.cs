using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class DirectionOnlyStructure : IPriceStructureAnalyzer
    { public StructureEvidence Analyze(IReadOnlyList<CompletedBar> bars, decimal epsilon) => new(true, true, 0, 0, 0, 0); }
    private static void BoundaryChecks()
    {
        var c = Config();
        var confirmation = c.Regime with { MinimumBars = 4, Lookback = DurationSpec.Minutes(4), ERTrend = .2m, ERRange = .05m };
        var classifier = new MarketRegimeClassifier(confirmation, c.Quality, c.ConfigurationHash, new DirectionOnlyStructure());
        var prices = new[] { 100m, 102m, 104m, 106m, 108m, 90m, 120m };
        for (int n = 0; n < 5; n++) classifier.OnBar(Bar(n, prices[n]), Epoch.AddMinutes(n + 1));
        classifier.OnBar(Bar(5, prices[5]), Epoch.AddMinutes(6));
        Check("A08", classifier.Snapshot is { Regime: MarketRegime.Uptrend, CandidateRegime: MarketRegime.Downtrend, CandidateConsecutiveBars: 1, EntryAllowed: false }, "Single new down classification does not switch committed Up family and blocks unsafe old entry");
        classifier.OnBar(Bar(6, prices[6]), Epoch.AddMinutes(7));
        Check("A08", classifier.Snapshot.Regime == MarketRegime.Uptrend && classifier.Snapshot.CandidateRegime == MarketRegime.Uptrend && classifier.Snapshot.CandidateConsecutiveBars == 1, "Returning up candidate resets the one-bar down sequence");
        var rangeConfig = c.Regime with { RangeTouchSeparation = DurationSpec.Minutes(1) };
        var boxClassifier = new MarketRegimeClassifier(rangeConfig, c.Quality, c.ConfigurationHash);
        foreach (var b in RangeBars()) boxClassifier.OnBar(b, b.KnownAt);
        var old = boxClassifier.Snapshot.Box!; boxClassifier.UpdateBox(BoxState.BrokenUp, Epoch.AddMinutes(18));
        boxClassifier.OnBar(Bar(18, 100.6m, 101, 100), Epoch.AddMinutes(19));
        Check("C08", !boxClassifier.Snapshot.EntryAllowed && boxClassifier.Snapshot.Box!.Id == old.Id && boxClassifier.Snapshot.Box.State == BoxState.BrokenUp, "Consumed box cannot resurrect from old reaction history after one more bar");
        foreach (int n in Enumerable.Range(19, 13)) boxClassifier.OnBar(Bar(n, n % 2 == 0 ? 100.6m : 99.4m, n % 2 == 0 ? 101 : 100, n % 2 == 0 ? 100 : 99), Epoch.AddMinutes(n + 1));
        Check("C08", boxClassifier.Snapshot.Box is { State: BoxState.Valid } fresh && fresh.Id != old.Id, "Independent new reactions and confirmation can create a NEW box with the same physical prices");
        var asyncBuilder = new BinsBarBuilder(Us, new(1, BarUnit.Minute)); asyncBuilder.Add(Input(.1m, 100), true); asyncBuilder.Add(Input(59.9m, 100), true); asyncBuilder.Add(Input(60.1m, 101), true); asyncBuilder.Add(Input(119.9m, 101), true); var asynchronousBar = asyncBuilder.Add(Input(120.1m, 102), true);
        Check("A02", asynchronousBar is { Open: 101, Close: 101, IsComplete: true }, "Continuous observation across minute boundary does not require an impossible exactly-on-boundary trade");
        var unknown = new BinsPaperExecution("USD", 10000); unknown.SubmitIntent(BuyIntent(c)); unknown.MarkNetworkTimeout("buy", Epoch.AddSeconds(1));
        unknown.Report(new("late-partial", "buy", "paper:buy", Epoch.AddSeconds(2), OrderState.PartiallyFilled, "late-execution", Epoch.AddSeconds(1.5), 3, 100, .03m));
        Check("E04", unknown.Positions[Us.Key].Quantity == 3 && !unknown.Synchronized && unknown.Orders[0].State == OrderState.Unknown, "Late partial execution updates known holdings but does not clear unresolved remainder status");
        unknown.Report(new("reconciled-partial", "buy", "paper:buy", Epoch.AddSeconds(3), OrderState.PartiallyFilled, ReconciliationConfirmed: true));
        Check("E04", unknown.Synchronized && unknown.Orders[0].Remaining == 7, "Explicit inquiry reconciles remaining quantity after a late partial fill");
        var live = new PretendLive(); Throws("E10", () => new BinsStrategyEngine(c, [Us], [live]), "Even a live-labelled mock adapter is rejected before any Submit/network callback");
        Check("E10", live.Submissions == 0, "Actual/live sender invocation count remains exactly zero");
        var sellPartial = EngineFixture(); StartBreakout(sellPartial.Engine); Push(sellPartial.Engine, Input(844, 102.82m, true));
        Push(sellPartial.Engine, Input(845, 104, true)); Push(sellPartial.Engine, Input(845, 104));
        Check("D05", sellPartial.Account.Orders.Last().Reason == ExitReason.TakeProfit1.ToString() && sellPartial.Account.Orders.Last().Filled == 0, "TP1 engine intent remains pending before confirmed sell execution");
        Push(sellPartial.Engine, Input(846, 104.1m, true, quantity: 1));
        Check("D04", sellPartial.Account.Orders.Last().State == OrderState.PartiallyFilled && sellPartial.Account.Positions[Us.Key].Quantity == 8, "Actual partial TP1 fill reduces quantity by exactly one");
        sellPartial.Engine.OnClock(Epoch.AddSeconds(847)); sellPartial.Engine.OnClock(Epoch.AddSeconds(847.2));
        Push(sellPartial.Engine, Input(847.3m, 104.1m, true)); Push(sellPartial.Engine, Input(847.3m, 104.1m));
        Check("E05", sellPartial.Account.Orders.Last() is { Buy: false, Quantity: 1, Kind: OrderKind.Limit } && sellPartial.Account.Orders.Count(o => o.State == OrderState.CancelRequested) == 0, "After partial TP cancel acknowledgement, requote only the one unfilled TP share");
        var riskProfile = c.Profile(Us) with { RiskBudgetPerPosition = .1m };
        var lowRisk = c with { Market = c.Market with { CountryProfiles = c.Market.CountryProfiles with { US = riskProfile } } };
        Check("D10", !BinsRisk.Approve(lowRisk, Signal(config: lowRisk), Input(1), Input(1, quote: true).Event, new BinsPaperExecution("USD", 10000), 0, 0, 0, false).Allowed, "Position risk budget includes stop distance and roundtrip costs, yielding zero shares when insufficient");
        var limited = c with { SharedRisk = c.SharedRisk with { MaxEntriesPerSymbolPerDay = 1, MaxEntriesPerAccountPerDay = 1, MaxEntriesPerFamilyPerDay = 1 } };
        var daily = EngineFixture(limited); StartBreakout(daily.Engine); Push(daily.Engine, Input(844, 102.82m, true)); Push(daily.Engine, Input(845, 101.7m, true)); Push(daily.Engine, Input(845, 101.7m)); Push(daily.Engine, Input(846, 101.7m, true)); daily.Engine.OnClock(Epoch.AddSeconds(846));
        Push(daily.Engine, Input(1200, 102.6m, true)); Push(daily.Engine, Input(1200, 102.6m));
        Check("D10", daily.Account.Orders.Count(o => o.Buy) == 1 && daily.Engine.ClosedTrades.Length == 1, "Confirmed first-position daily count persists after full close and cannot be reused by another family");
        var a = new BinsPaperExecution("USD", 1000); var i1 = BuyIntent(c, "reserve1", 9, 100); a.SubmitIntent(i1);
        Throws("D10", () => a.SubmitIntent(BuyIntent(c, "reserve2", 9, 100) with { Order = new ManagedOrder { Id = "reserve2", Instrument = new("OTHER", Exchange.NASDAQ), Buy = true, Quantity = 9, Price = 100, Kind = OrderKind.Limit, Created = Epoch, Strategy = "bins-strategy-v1" } }), "Shared currency account cannot over-reserve cash across independent symbols");
    }
    private sealed class PretendLive : IBinsExecutionAdapter
    {
        private readonly BinsPaperExecution paper = new("USD", 10000); public int Submissions;
        public bool IsLive => true; public bool Synchronized => true; public string Currency => "USD"; public decimal Cash => 10000; public decimal Equity => 10000; public decimal ReservedCash => 0;
        public IReadOnlyDictionary<string, Position> Positions => paper.Positions; public IReadOnlyList<ManagedOrder> Orders => paper.Orders; public IReadOnlyList<Fill> Fills => paper.Fills;
        public void Submit(ManagedOrder order) => Submissions++; public void SubmitIntent(BinsOrderIntent intent) => Submissions++; public void Cancel(ManagedOrder o, DateTimeOffset now) { } public void OnMarket(MarketEvent e) { } public void OnTime(DateTimeOffset at) { } public void Report(BinsExecutionReport report) { }
    }
}
