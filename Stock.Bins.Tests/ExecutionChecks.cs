using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void PositionAndSelectionChecks()
    {
        var c = Config(); var selector = new StrategyFamilySelector(); var date = Session().TradingDate;
        Check("D01", selector.Select(c.Selection, Regime(MarketRegime.Uptrend), date, false) == StrategyFamily.Uptrend && selector.Select(c.Selection, Regime(MarketRegime.Downtrend), date, true) == StrategyFamily.Uptrend, "Family freezes while holding or any order is unresolved");
        Check("D01", selector.Select(c.Selection, Regime(MarketRegime.Downtrend), date, false) == StrategyFamily.DowntrendRebound, "Switch ON reselects latest confirmed family only once flat/resolved");
        var fixedSelector = new StrategyFamilySelector(); var off = c.Selection with { AutoSwitchAfterExit = false };
        fixedSelector.Select(off, Regime(MarketRegime.Uptrend), date, false);
        Check("D02", fixedSelector.Select(off, Regime(MarketRegime.Downtrend), date, false) == null && fixedSelector.Selected == StrategyFamily.Uptrend, "Switch OFF keeps first valid daily family; invalid old structure blocks entries");
        Check("D02", fixedSelector.Select(off, Regime(MarketRegime.Downtrend), date.AddDays(1), false) == StrategyFamily.DowntrendRebound, "New trading day can select a new family while flat");
        var manual = new StrategyFamilySelector(); var mc = c.Selection with { Mode = StrategySelectionMode.Manual, ManualFamily = StrategyFamily.Range };
        Check("D03", manual.Select(mc, Regime(MarketRegime.Uptrend), date, false) == null && manual.Select(mc, Regime(MarketRegime.Range), date, false) == StrategyFamily.Range, "Manual family does not bypass valid corresponding market structure");
        var entry = Signal(anchor: 99); var first = new Fill("buy", Epoch.AddSeconds(1), Us, true, 10, 100, .1m, 0, "bins-strategy-v1");
        var state = new BinsPositionState(entry, c, first); var p = new Position(Us) { Quantity = 10, AveragePrice = 100, CostFees = .1m, Last = 100 };
        var tp1 = state.Evaluate(p, 101.1m, Epoch.AddSeconds(2), Session(), c.Profile(Us).Fees[0]);
        Check("D04", tp1 is { Kind: ExitReason.TakeProfit1, Quantity: 3 }, "TP1 floor(10*30%) requests3 of actual confirmed entry shares");
        Check("D05", !state.TrailingActive && state.TP1Filled == 0, "TP1 signal/order submission does not activate trailing");
        state.OnFill(new("tp1", Epoch.AddSeconds(3), Us, false, 3, 101.1m, .03m, 3.27m, "bins", "TP1"), ExitReason.TakeProfit1); p.Quantity = 7; p.CostFees = .07m;
        var tp2 = state.Evaluate(p, 102.1m, Epoch.AddSeconds(4), Session(), c.Profile(Us).Fees[0]);
        Check("D04", state.TrailingActive && tp2 is { Kind: ExitReason.TakeProfit2, Quantity: 3 }, "Confirmed TP1 fill activates trailing; TP2 remains30% of initial10");
        state.OnFill(new("tp2", Epoch.AddSeconds(5), Us, false, 3, 102.1m, .03m, 6.27m, "bins", "TP2"), ExitReason.TakeProfit2); p.Quantity = 4;
        Check("D04", p.Quantity == 4 && state.TP1Filled == 3 && state.TP2Filled == 3, "30/30/40 share split retains4 for common protection/trailing");
        var peakStop = state.TrailingStop; state.Evaluate(p, 101.9m, Epoch.AddSeconds(6), Session(), null);
        Check("D07", state.TrailingStop >= peakStop && state.TechnicalStop == 99 * .997m, "Trailing never lowers and technical stop remains entry snapshot");
        var risk = state.Evaluate(p, 98, Epoch.AddSeconds(7), Session(), null);
        Check("D06", risk != null && risk.Protective && risk.Quantity == 4, "Simultaneously hit common stops produce one remaining-quantity risk intent");
        var sessionExit = state.Evaluate(p, 105, Session().Close.AddSeconds(-20), Session(), null);
        Check("D11", sessionExit?.Kind == ExitReason.SessionClose, "Verified exchange close drives liquidation ATTEMPT priority over TP");
        Check("B07", new BinsPositionState(entry, c, first).Evaluate(new(Us) { Quantity = 10, AveragePrice = 100 }, 100, Epoch.AddSeconds(2), Session(), null) == null, "Stage1 break alone does not invoke a holding exit; common stop decides independently");
        Check("D09", c.SharedReentry.AfterStop.Elapsed > c.SharedReentry.AfterProfit.Elapsed, "Common stop cooldown cannot be bypassed by family-specific values");
    }
    private static BinsOrderIntent BuyIntent(BinsStrategyConfig c, string id = "buy", int quantity = 10, decimal price = 100)
    {
        var o = new ManagedOrder { Id = id, Instrument = Us, Buy = true, Quantity = quantity, Price = price, Kind = OrderKind.Limit, Created = Epoch, Strategy = "bins-strategy-v1", Reason = "fixture-signal" };
        return new("intent:" + id, "fixture-signal", o, c.Profile(Us).Fees[0], Rule(), c.Execution, c.ConfigurationHash);
    }
    private static void ExecutionChecks()
    {
        var c = Config(); var account = new BinsPaperExecution("USD", 10000); var intent = BuyIntent(c); account.SubmitIntent(intent);
        account.OnMarket(Input(0, 99, true).Event);
        Check("E03", account.Fills.Count == 0 && account.ReservedCash > 1000, "Signal event never fills and buy cash/fees are reserved");
        account.OnMarket(Input(.2m, 100, quantity: 100).Event);
        Check("E03", account.Fills.Count == 0, "Equal-price trade cannot assert a passive queue fill");
        account.OnMarket(Input(.3m, 99.9m, true, quantity: 3).Event);
        Check("E03", account.Orders[0].State == OrderState.PartiallyFilled && account.Positions[Us.Key].Quantity == 3 && account.Fills.Single().Quantity == 3, "Later actual quote supports only3 partial shares");
        Throws("C12", () => account.SubmitIntent(BuyIntent(c, "second")), "Partial holding or pending buy prevents pyramiding/new pattern order");
        account.Cancel(intent.Order, Epoch.AddSeconds(.4));
        Check("E03", intent.Order.State == OrderState.CancelRequested && intent.Order.Active, "Cancel request is not cancel acknowledgement");
        account.OnTime(Epoch.AddSeconds(.45)); Check("E03", intent.Order.State == OrderState.CancelRequested, "Cancellation latency preserves pending reservation");
        account.OnTime(Epoch.AddSeconds(.6)); Check("E03", intent.Order.State == OrderState.Cancelled && account.ReservedCash == 0 && account.Positions[Us.Key].Quantity == 3, "Cancel acknowledgement releases remainder only, never removes confirmed holdings");
        var unknown = new BinsPaperExecution("USD", 10000); var u = BuyIntent(c); unknown.SubmitIntent(u); unknown.MarkNetworkTimeout(u.Order.Id, Epoch.AddSeconds(1));
        unknown.OnMarket(Input(2, 99, true).Event);
        Check("E04", !unknown.Synchronized && u.Order.State == OrderState.Unknown && unknown.Fills.Count == 0, "Network timeout remains Unknown and cannot simulate a rejection or fill");
        Throws("E04", () => unknown.Report(new("unverified-reply", "buy", "paper:buy", Epoch.AddSeconds(2), OrderState.Cancelled)), "Unknown cannot be cleared without explicit reconciliation");
        unknown.Report(new("verified-reply", "buy", "paper:buy", Epoch.AddSeconds(2), OrderState.Cancelled, ReconciliationConfirmed: true));
        Check("E04", unknown.Synchronized && u.Order.State == OrderState.Cancelled, "Confirmed broker state inquiry resolves Unknown without re-submission");
        var idempotent = new BinsPaperExecution("USD", 10000); var same = BuyIntent(c); idempotent.SubmitIntent(same);
        var report = new BinsExecutionReport("r1", "buy", "broker:123", Epoch.AddSeconds(1), OrderState.Filled, "trade:1", Epoch.AddSeconds(1), 10, 100, .1m);
        idempotent.Report(report); idempotent.Report(report); idempotent.Report(report with { ReportId = "r2" });
        Check("E03", idempotent.Fills.Count == 1 && idempotent.Positions[Us.Key].Quantity == 10, "Broker order + execution ID deduplicates reports even when callback ID changes");
        Throws("E03", () => idempotent.Report(report with { ReportId = "r3", FillQuantity = 9 }), "Conflicting duplicate execution cannot mutate ledger");
        var invalid = new BinsPaperExecution("USD", 10000); invalid.SubmitIntent(BuyIntent(c));
        Throws("E03", () => invalid.Report(report with { FillQuantity = 3 }), "Inconsistent Filled status/partial quantity is rejected before ledger mutation");
        Check("E03", invalid.Fills.Count == 0 && invalid.Cash == 10000, "Failed report validation leaves cash and holdings intact");
        var baseSignal = Signal(); var validQuote = Input(1, 100, true).Event; var validInput = Input(1, 100);
        EntryApproval Approve(BinsMarketInput input, MarketEvent? quote, BinsStrategyConfig? settings = null) => BinsRisk.Approve(settings ?? c, baseSignal, input, quote, new BinsPaperExecution("USD", 10000), 0, 0, 0, false);
        Check("E01", Approve(validInput, validQuote).Allowed, "Fully specified synthetic rule/fees/session/budget/quote can pass risk gate");
        Check("E01", !Approve(validInput, null).Allowed && !Approve(validInput, validQuote with { BidSize = null }).Allowed, "Missing quotes/quantities are not synthesized from last price");
        Check("E01", !Approve(validInput, validQuote with { Bid = 98, Ask = 100 }).Allowed, "Excessive spread blocks entries");
        Check("E01", !Approve(validInput, validQuote with { AskSize = 1 }).Allowed, "Insufficient ask capacity defaults to BLOCK, not automatic budget shrink");
        Check("E01", !Approve(validInput, validQuote with { ExchangeTime = Epoch.AddSeconds(-10) }).Allowed, "Stale quote blocks entry");
        Check("E02", !Approve(validInput, validQuote with { Ask = 100.21m }).Allowed, "Ask above100*(1+0.2%) blocks capped entry");
        Check("E07", !Approve(validInput with { Metadata = validInput.Metadata! with { PriceRule = null } }, validQuote).Allowed, "Missing provider tick rule is never inferred from Instrument.Tick");
        Check("E07", !Approve(validInput with { Session = validInput.Session! with { Phase = SessionPhase.PreMarket } }, validQuote).Allowed, "Extended session blocked by regular-only policy");
        Check("E07", !Approve(validInput with { Session = validInput.Session! with { AllowedOrderKinds = [] } }, validQuote).Allowed, "Unconfirmed broker order kind permission blocks entry");
        var noBudget = c with { Market = c.Market with { CountryProfiles = c.Market.CountryProfiles with { US = c.Profile(Us) with { PlannedInvestment = null } } } };
        Check("E06", !Approve(validInput, validQuote, noBudget).Allowed, "Absent USD budget cannot fall back to KRW or old D defaults");
        var noFees = c with { Market = c.Market with { CountryProfiles = c.Market.CountryProfiles with { US = c.Profile(Us) with { Fees = [] } } } };
        Check("E01", !Approve(validInput, validQuote, noFees).Allowed, "Missing dated fees/tax do not become unexplained zero costs");
        Check("D10", !BinsRisk.Approve(c, baseSignal, validInput, validQuote, new BinsPaperExecution("USD", 10000), 0, 0, 5, false).Allowed && !BinsRisk.Approve(c, baseSignal, validInput, validQuote, new BinsPaperExecution("USD", 10000), 0, 0, 0, true).Allowed, "Concurrent positions and latched daily risk independently block new buys");
        var krw = new BinsPaperExecution("KRW", 1000000);
        Throws("E06", () => krw.SubmitIntent(BuyIntent(c)), "KRW ledger cannot reserve or fill a USD instrument");
        var exitOrder = new ManagedOrder { Id = "exit", Instrument = Us, Buy = false, Quantity = 10, Price = 99.7m, Kind = OrderKind.Limit, Created = Epoch.AddSeconds(2), Strategy = "bins-strategy-v1" };
        idempotent.SubmitIntent(new("intent-exit", "fixture-signal", exitOrder, c.Profile(Us).Fees[0], Rule(), c.Execution, c.ConfigurationHash));
        idempotent.OnMarket(Input(3, 90, true).Event);
        Check("E05", exitOrder.Filled == 0 && exitOrder.Kind == OrderKind.Limit && idempotent.Positions[Us.Key].Quantity == 10, "Gapped stop limit remains unfilled; no loss guarantee or implicit market fallback");
        idempotent.Cancel(exitOrder, Epoch.AddSeconds(4)); idempotent.OnTime(Epoch.AddSeconds(4.2));
        Check("E05", idempotent.Positions[Us.Key].Quantity == 10 && exitOrder.State == OrderState.Cancelled, "Cancel of failed stop leaves real remaining position visible");
    }
}
