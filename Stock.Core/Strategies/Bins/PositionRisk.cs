using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public enum ExitReason { SessionClose, FixedStop, TechnicalStop, TrailingStop, BreakEvenStop, TakeProfit1, TakeProfit2 }
public sealed record ExitIntent(ExitReason Kind, int Quantity, decimal ReferencePrice, decimal MinimumPrice, string Reason)
{
    public bool Protective => Kind is not (ExitReason.TakeProfit1 or ExitReason.TakeProfit2);
}
public sealed class BinsPositionState
{
    public string PositionId { get; }
    public EntrySignal Entry { get; }
    public BinsStrategyConfig Configuration { get; }
    public DateTimeOffset FirstFillAt { get; }
    public int InitialConfirmedQuantity { get; private set; }
    public int TP1Filled { get; private set; }
    public int TP2Filled { get; private set; }
    public decimal Peak { get; private set; }
    public decimal TrailingStop { get; private set; }
    public bool TrailingActive => TP1Filled > 0;
    public decimal TechnicalStop { get; }
    public decimal RealizedPnl { get; private set; }
    public ExitReason? LastExitReason { get; private set; }
    public BinsPositionState(EntrySignal entry, BinsStrategyConfig configuration, Fill first)
    {
        Entry = entry; Configuration = configuration.Snapshot(); FirstFillAt = first.Time; PositionId = entry.Id + ":position";
        TechnicalStop = entry.TechnicalStopAnchor * (1 - Configuration.SharedExit.TechnicalBreakBufferPct / 100); Peak = first.Price;
        OnFill(first, null);
    }
    public void OnFill(Fill fill, ExitReason? reason)
    {
        if (fill.Buy) InitialConfirmedQuantity += fill.Quantity;
        else
        {
            if (reason == ExitReason.TakeProfit1) { if (TP1Filled == 0 && Configuration.SharedExit.TrailingPeakBasis == TrailingPeakBasis.FromTP1Fill) Peak = fill.Price; TP1Filled += fill.Quantity; }
            if (reason == ExitReason.TakeProfit2) TP2Filled += fill.Quantity;
            LastExitReason = reason; RealizedPnl += fill.RealizedPnl;
        }
    }
    public ExitIntent? Evaluate(Position p, decimal price, DateTimeOffset at, MarketSession session, FeeRate? fees, bool sessionDue = false)
    {
        var c = Configuration.SharedExit;
        Peak = Math.Max(Peak, price);
        if (TrailingActive) TrailingStop = Math.Max(TrailingStop, Peak * (1 - c.TrailingStopPct / 100));
        ExitIntent Make(ExitReason kind, int qty, decimal basis, string why) => new(kind, Math.Min(p.Quantity, qty), basis, basis * (1 - Configuration.Execution.ExitLimitSlippagePct / 100), why);
        if (p.Quantity <= 0) return null;
        if (c.ClosePositionsAtSessionEnd && (sessionDue || at >= session.Close - c.StartLiquidationBeforeClose.Elapsed)) return Make(ExitReason.SessionClose, p.Quantity, price, "Session close liquidation ATTEMPT (not a guaranteed fill)");
        var fixedStop = c.FixedStopEnabled ? p.AveragePrice * (1 - c.FixedStopLossPct / 100) : 0;
        var technical = c.TechnicalStopEnabled ? TechnicalStop : 0;
        decimal breakEven = 0;
        if (c.BreakEvenAfterTP1 && TrailingActive && fees != null)
        { var exitRate = (fees.SellPercent + fees.SellTaxPercent) / 100; breakEven = (p.AveragePrice + p.CostFees / p.Quantity + fees.PerShare) / (1 - exitRate); }
        var stops = new[] { (fixedStop, ExitReason.FixedStop), (technical, ExitReason.TechnicalStop), (TrailingActive ? TrailingStop : 0, ExitReason.TrailingStop), (breakEven, ExitReason.BreakEvenStop) };
        var hit = stops.Where(s => s.Item1 > 0 && price <= s.Item1).OrderByDescending(s => s.Item1).FirstOrDefault();
        if (hit.Item1 > 0) return Make(hit.Item2, p.Quantity, price, "Common protective stop; frozen technical anchor and actual average fill");
        var tp1 = (int)decimal.Floor(InitialConfirmedQuantity * c.TakeProfit1RatioPct / 100);
        var tp2 = (int)decimal.Floor(InitialConfirmedQuantity * c.TakeProfit2RatioPct / 100);
        if (TP1Filled < tp1 && price >= p.AveragePrice * (1 + c.TakeProfit1Pct / 100)) return Make(ExitReason.TakeProfit1, tp1 - TP1Filled, p.AveragePrice * (1 + c.TakeProfit1Pct / 100), "TP1 confirmed initial quantity, integer floor");
        if (TP1Filled >= tp1 && TP2Filled < tp2 && price >= p.AveragePrice * (1 + c.TakeProfit2Pct / 100)) return Make(ExitReason.TakeProfit2, tp2 - TP2Filled, p.AveragePrice * (1 + c.TakeProfit2Pct / 100), "TP2 confirmed initial quantity, integer floor");
        return null;
    }
}
public sealed record EntryApproval(bool Allowed, string Reason, int Quantity = 0, decimal LimitPrice = 0, decimal Stop = 0,
    decimal RiskPerShare = 0, decimal NetRewardPct = 0, decimal SpreadPct = 0, FeeRate? Fees = null);
public static class BinsRisk
{
    public static bool IsStopExit(ExitReason? reason, decimal totalProfit) => (reason is ExitReason.FixedStop or ExitReason.TechnicalStop) || totalProfit < 0 || totalProfit == 0 && (reason is ExitReason.TrailingStop or ExitReason.BreakEvenStop);
    public static EntryApproval Approve(BinsStrategyConfig config, EntrySignal signal, BinsMarketInput input, MarketEvent? quote,
        IBinsExecutionAdapter account, decimal ownedValue, decimal reservedBuyValue, int concurrentPositions, bool dailyRiskBlocked)
    {
        EntryApproval No(string reason) => new(false, reason);
        var e = input.Event; var at = input.KnownAt; var p = config.Profile(e.Instrument); var x = config.Execution;
        if (e.Kind != EventKind.Trade || e.TradePrice is not > 0 || at < e.ExchangeTime || at < e.ReceivedTime || at - e.ExchangeTime > config.Quality.MaxTradeAge.Elapsed || at - e.ReceivedTime > config.Quality.MaximumProcessingLag.Elapsed || signal.Time > at || signal.RegimeSnapshot.KnownAt > at || signal.Stage1Snapshot.KnownAt > at || signal.MicroHighKnownAt > at) return No("Future/stale trade or signal evidence");
        if (!config.Enabled || !account.Synchronized || account.Orders.Any(o => o.Active && o.Symbol == e.Key) || account.Positions.GetValueOrDefault(e.Key)?.Quantity > 0) return No("Holding/pending/Unknown/disabled");
        if (dailyRiskBlocked || concurrentPositions >= config.SharedRisk.MaxConcurrentPositionsPerAccount) return No("Daily risk or concurrent position cap");
        if (input.Session is not {} s || !s.ValidAt(e.Instrument, at) || config.Market.RegularSessionOnly && s.Phase != SessionPhase.Regular || at >= s.Close - config.SharedExit.StopNewEntriesBeforeClose.Elapsed) return No("Session/entry cutoff/permissions");
        if (input.Metadata is not {} meta || meta.Instrument != e.Instrument || meta.KnownAt > at || at >= meta.ValidUntil || meta.Halted != false || config.Quality.RequireTradableInstrument && !meta.VerifiedAt(at) || meta.PriceRule is not {} rule || !rule.ValidAt(at)) return No("MissingData: instrument/halt/risk/verified price rule");
        if (quote == null || quote.Source != e.Source || quote.ExchangeTime > at || quote.ReceivedTime > at || at - quote.ExchangeTime > config.Quality.MaxQuoteAge.Elapsed || quote.Bid is not > 0 || quote.Ask < quote.Bid || quote.Ask == null || quote.BidSize is not > 0 || quote.AskSize is not > 0) return No("MissingData: fresh two-sided quote/depth");
        var bid = quote.Bid.Value; var ask = quote.Ask.Value; var spread = 100 * (ask - bid) / ((ask + bid) / 2);
        if (rule.Floor(bid) != bid || rule.Floor(ask) != ask) return No("Quote prices violate the supplied market tick rule");
        if (spread > p.MaxSpreadPct || quote.BidSize < p.MinBestBidQuantity || quote.AskSize < p.MinBestAskQuantity || bid * quote.BidSize < p.MinBidNotional || ask * quote.AskSize < p.MinAskNotional) return No("LiquidityRejected: spread/best quantities/notional");
        if (p.PlannedInvestment == null || p.MaxOrderCash == null || p.MaxConcurrentExposure == null || p.RiskBudgetPerPosition == null || p.MaxDailyLoss == null) return No("Missing explicit currency budget");
        var fee = FeeSchedule.At(p, e.Instrument.Exchange, at); if (fee == null) return No("MissingData: dated fee/tax schedule");
        var cap = signal.ReferencePrice * (1 + x.MaxEntrySlippagePct / 100);
        if (ask > cap) return No("Ask exceeds signal slippage cap");
        var limit = rule.Floor(cap);
        if (limit < ask || limit < meta.LowerPriceLimit || limit > meta.UpperPriceLimit) return No("No legal capped buy tick/price limit");
        var chase = signal.EntryPattern switch { EntryPattern.DowntrendRebound => config.Entries.DowntrendRebound.MaxChasePct, EntryPattern.UptrendPullback => config.Entries.UptrendPullback.MaxChasePct, EntryPattern.UptrendBreakout => config.Entries.UptrendBreakout.MaxChasePct, EntryPattern.RangeLowerBounce => config.Entries.RangeLowerBounce.MaxChasePct, _ => config.Entries.RangeUpperBreakout.MaxChasePct };
        var chaseBasis = signal.EntryPattern is EntryPattern.DowntrendRebound or EntryPattern.UptrendPullback ? signal.CandidateLow : signal.TechnicalStopAnchor;
        if (limit > chaseBasis * (1 + chase / 100)) return No("Buy limit exceeds pattern maximum chase anchor");
        var technical = signal.TechnicalStopAnchor * (1 - config.SharedExit.TechnicalBreakBufferPct / 100);
        var fixedStop = signal.ReferencePrice * (1 - config.SharedExit.FixedStopLossPct / 100);
        if (config.SharedExit.TechnicalStopEnabled && (technical <= 0 || technical >= signal.ReferencePrice) || config.SharedExit.FixedStopEnabled && (fixedStop <= 0 || fixedStop >= signal.ReferencePrice)) return No("Protective anchors must both be below entry reference");
        var stop = Math.Max(config.SharedExit.TechnicalStopEnabled ? technical : 0, config.SharedExit.FixedStopEnabled ? fixedStop : 0);
        var buyCost = fee.Fee(true, 1, limit); var sellCost = fee.Fee(false, 1, stop);
        var safetySlippage = limit * (x.MaxEntrySlippagePct + x.ExitLimitSlippagePct) / 100;
        var riskPerShare = limit - stop + buyCost + sellCost + safetySlippage;
        var target = signal.ReferencePrice * (1 + config.SharedExit.TakeProfit1Pct / 100);
        // Conservative reward reserves spread plus full permitted entry/exit slippage and dated costs.
        var reward = target - signal.ReferencePrice - fee.Fee(true, 1, limit) - fee.Fee(false, 1, target) - (ask - bid) - safetySlippage;
        var netReward = reward / signal.ReferencePrice * 100;
        if (netReward < p.MinEntryNetRewardPct) return No("Insufficient minimum net reward after round-trip costs");
        var cash = Math.Min(p.PlannedInvestment.Value, Math.Min(p.MaxOrderCash.Value, Math.Min(account.Cash - account.ReservedCash, p.MaxConcurrentExposure.Value - ownedValue - reservedBuyValue)));
        var cashQty = decimal.Floor(Math.Max(0, cash) / (limit + buyCost));
        var riskQty = decimal.Floor(p.RiskBudgetPerPosition.Value / riskPerShare);
        var quantity = Math.Min(cashQty, riskQty);
        var capacity = decimal.Floor(quote.AskSize.Value * x.MaxEntryQuoteParticipationPct / 100);
        if (quantity > capacity && !x.ReduceQuantityToQuoteCapacity) return No("Insufficient quote capacity; default policy blocks instead of shrinking");
        quantity = Math.Min(quantity, capacity);
        if (quantity <= 0 || quantity > int.MaxValue) return No("No valid sized quantity");
        return new(true, "All currency/session/liquidity/risk gates passed", (int)quantity, limit, stop, riskPerShare, netReward, spread, fee);
    }
}
