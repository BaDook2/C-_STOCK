using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public sealed record BinsOrderIntent(string IntentId, string SignalId, ManagedOrder Order, FeeRate Fees, PriceRule PriceRule,
    ExecutionPolicy Policy, string ConfigurationHash);
public sealed record BinsExecutionReport(string ReportId, string OrderId, string BrokerOrderId, DateTimeOffset ReceivedAt,
    OrderState State, string? ExecutionId = null, DateTimeOffset? FillTime = null, int FillQuantity = 0, decimal FillPrice = 0,
    decimal Fee = 0, bool ReconciliationConfirmed = false);
public interface IBinsExecutionAdapter : IStrategyExecutionAdapter
{
    void SubmitIntent(BinsOrderIntent intent);
    void OnTime(DateTimeOffset now);
    void Report(BinsExecutionReport report);
}
/// <summary>Bin execution model over the shared paper ledger. Real adapters are deliberately not connected.</summary>
public sealed class BinsPaperExecution : IBinsExecutionAdapter
{
    private readonly PaperBroker ledger;
    private readonly Dictionary<string, BinsOrderIntent> intents = [];
    private readonly Dictionary<string, BinsExecutionReport> reports = [];
    private readonly Dictionary<string, string> brokerIds = [];
    private readonly Dictionary<string, BinsExecutionReport> executions = [];
    private readonly Dictionary<string, DateTimeOffset> cancellationConfirmedAt = [];
    private readonly Dictionary<(string Symbol, string Source, EventKind Kind), (long Sequence, DateTimeOffset Time)> lastMarket = [];
    private int executionSequence;
    public bool IsLive => false;
    public bool Synchronized => Orders.All(o => o.State != OrderState.Unknown);
    public string Currency => ledger.Currency;
    public decimal Cash => ledger.Cash;
    public decimal Equity => ledger.Equity;
    public decimal ReservedCash => Orders.Where(o => o.Active && o.Buy).Sum(o => o.Remaining * o.Price + intents[o.Id].Fees.Fee(true, o.Remaining, o.Price));
    public IReadOnlyDictionary<string, Position> Positions => ledger.Positions;
    public IReadOnlyList<ManagedOrder> Orders => ledger.Orders;
    public IReadOnlyList<Fill> Fills => ledger.Fills;
    public BinsPaperExecution(string currency, decimal cash)
    { if (currency is not ("KRW" or "USD") || cash <= 0) throw new InvalidOperationException("Explicit positive paper balance in KRW or USD required"); ledger = new(currency, cash, new()); }
    public void Submit(ManagedOrder order) => throw new InvalidOperationException("Bin requires a validated intent with dated fees and price rules");
    public void SubmitIntent(BinsOrderIntent intent)
    {
        var o = intent.Order;
        if (o.Currency != Currency || o.Quantity <= 0 || o.Price <= 0 || o.Kind != OrderKind.Limit || intent.PriceRule.Floor(o.Price) != o.Price || !intent.PriceRule.ValidAt(o.Created) || intents.ContainsKey(o.Id) || intents.Values.Any(i => i.IntentId == intent.IntentId)) throw new InvalidOperationException("Invalid/duplicate Bin limit intent");
        if (o.Buy && (Positions.GetValueOrDefault(o.Symbol)?.Quantity > 0 || Orders.Any(p => p.Symbol == o.Symbol && p.Active))) throw new InvalidOperationException("Single position/no pyramiding/unresolved order invariant");
        if (!o.Buy && (Positions.GetValueOrDefault(o.Symbol)?.Quantity ?? 0) - Orders.Where(p => p.Symbol == o.Symbol && p.Active && !p.Buy).Sum(p => p.Remaining) < o.Quantity) throw new InvalidOperationException("Sell reservation exceeds confirmed holdings");
        if (o.Buy && o.Price * o.Quantity + intent.Fees.Fee(true, o.Quantity, o.Price) > Cash - ReservedCash) throw new InvalidOperationException("Atomic cash reservation exceeded");
        intent.Fees.Validate(); intents.Add(o.Id, intent with { PriceRule = intent.PriceRule with { Bands = intent.PriceRule.Bands.ToArray() } }); ledger.Submit(o); o.State = OrderState.Accepted;
    }
    public void Cancel(ManagedOrder order, DateTimeOffset now) => ledger.RequestCancel(order, now);
    public void OnTime(DateTimeOffset now)
    {
        foreach (var order in Orders.Where(o => o.State == OrderState.CancelRequested).ToArray())
            if (now - order.CancellationRequestedAt >= intents[order.Id].Policy.CancellationLatency.Elapsed) { order.State = OrderState.Cancelled; cancellationConfirmedAt[order.Id] = now; }
    }
    public void OnMarket(MarketEvent e)
    {
        if (e.Instrument.Currency != Currency) return;
        OnTime(e.ReceivedTime);
        var key = (e.Key, e.Source, e.Kind);
        if (lastMarket.TryGetValue(key, out var last) && (e.ExchangeTime < last.Time || e.ExchangeTime == last.Time && e.Sequence <= last.Sequence)) return;
        lastMarket[key] = (e.Sequence, e.ExchangeTime);
        if (Positions.TryGetValue(e.Key, out var position)) position.Last = e.TradePrice ?? e.Bid ?? position.Last;
        decimal buyUsed = 0, sellUsed = 0;
        foreach (var o in Orders.Where(o => o.Active && o.Symbol == e.Key).ToArray())
        {
            if (o.State == OrderState.Unknown || e.ExchangeTime <= o.Created) continue;
            var terms = intents[o.Id];
            if (e.ExchangeTime - o.Created < terms.Policy.SubmissionLatency.Elapsed) continue;
            // Pending cancellation MAY still fill before the simulated acknowledgement.
            decimal? price; decimal liquidity;
            if (e.Kind == EventKind.Quote)
            { price = o.Buy ? e.Ask : e.Bid; liquidity = (o.Buy ? e.AskSize : e.BidSize) ?? 0; }
            else
            {
                // Queue unknown: equal-price prints do not prove a passive limit execution.
                if (o.Buy ? e.TradePrice >= o.Price : e.TradePrice <= o.Price) continue;
                price = o.Price; liquidity = e.TradeSize ?? 0;
            }
            if (price is not > 0 || (o.Buy ? price > o.Price : price < o.Price) || !terms.PriceRule.ValidAt(e.ExchangeTime) || terms.PriceRule.Floor(price.Value) != price) continue;
            var capacity = decimal.Floor(liquidity * terms.Policy.SimulationParticipationPct / 100);
            var used = o.Buy ? buyUsed : sellUsed;
            var quantity = (int)Math.Min(o.Remaining, Math.Max(0, Math.Min(int.MaxValue, capacity - used)));
            if (quantity == 0) continue;
            var fee = terms.Fees.Fee(o.Buy, quantity, price.Value);
            Report(new($"sim-report:{++executionSequence}", o.Id, "paper:" + o.Id, e.ReceivedTime, quantity == o.Remaining ? OrderState.Filled : OrderState.PartiallyFilled,
                "sim-fill:" + executionSequence, e.ExchangeTime, quantity, price.Value, fee));
            if (o.Buy) buyUsed += quantity; else sellUsed += quantity;
        }
    }
    /// <summary>Timeout is Unknown, never rejected. Duplicate report/execution IDs are idempotent or explicitly conflicting.</summary>
    public void Report(BinsExecutionReport report)
    {
        if (reports.TryGetValue(report.ReportId, out var old)) { if (old != report) throw new InvalidOperationException("Conflicting report ID"); return; }
        var o = Orders.SingleOrDefault(x => x.Id == report.OrderId) ?? throw new InvalidOperationException("Unknown reported order");
        var wasUnknown = o.State == OrderState.Unknown;
        var latePreCancelFill = o.State == OrderState.Cancelled && report.FillQuantity > 0 && report.FillTime != null && cancellationConfirmedAt.TryGetValue(o.Id, out var cancelledAt) && report.FillTime <= cancelledAt;
        if (!Enum.IsDefined(report.State) || string.IsNullOrWhiteSpace(report.ReportId) || string.IsNullOrWhiteSpace(report.BrokerOrderId) || report.ReceivedAt < o.Created || report.FillQuantity < 0) throw new InvalidOperationException("Invalid execution report");
        if (brokerIds.TryGetValue(o.Id, out var broker) && broker != report.BrokerOrderId || brokerIds.Any(pair => pair.Key != o.Id && pair.Value == report.BrokerOrderId)) throw new InvalidOperationException("Broker order identity mismatch");
        if (o.State == OrderState.Unknown && report.State != OrderState.Unknown && !report.ReconciliationConfirmed && report.FillQuantity == 0) throw new InvalidOperationException("Unknown order requires explicit reconciliation");
        if (report.ExecutionId != null && executions.TryGetValue(report.BrokerOrderId + ":" + report.ExecutionId, out var previousExecution))
        {
            if (previousExecution.OrderId != report.OrderId || previousExecution.FillTime != report.FillTime || previousExecution.FillQuantity != report.FillQuantity || previousExecution.FillPrice != report.FillPrice || previousExecution.Fee != report.Fee) throw new InvalidOperationException("Conflicting duplicate execution");
            reports.Add(report.ReportId, report); return;
        }
        var remainingAfter = o.Remaining - report.FillQuantity;
        if (report.State == OrderState.Filled && remainingAfter != 0 || report.State == OrderState.PartiallyFilled && (o.Filled + report.FillQuantity <= 0 || remainingAfter <= 0) || remainingAfter < 0) throw new InvalidOperationException("Reported fill state conflicts with confirmed ledger");
        var terminal = o.State is OrderState.Filled or OrderState.Cancelled or OrderState.Rejected or OrderState.Expired;
        if (remainingAfter == 0 && report.State != OrderState.Filled || !latePreCancelFill && terminal && o.State != report.State) throw new InvalidOperationException("Terminal order state reversal");
        if (report.FillQuantity > 0)
        {
            if (string.IsNullOrWhiteSpace(report.ExecutionId) || report.FillTime == null || report.FillTime > report.ReceivedAt) throw new InvalidOperationException("Confirmed execution ID/time required");
            ledger.ApplySimulationFill(report.BrokerOrderId + ":" + report.ExecutionId, new(o.Id, report.FillTime.Value, o.Instrument, o.Buy, report.FillQuantity, report.FillPrice, report.Fee, 0, o.Strategy, o.Reason));
            executions.Add(report.BrokerOrderId + ":" + report.ExecutionId, report);
        }
        if (report.State == OrderState.Filled && o.Remaining != 0 || report.State == OrderState.PartiallyFilled && (o.Filled <= 0 || o.Remaining <= 0)) throw new InvalidOperationException("Reported fill state conflicts with confirmed ledger");
        if (o.Remaining == 0 && report.State != OrderState.Filled) throw new InvalidOperationException("A full fill cannot transition back to pending/cancelled");
        if (o.State is OrderState.Filled or OrderState.Cancelled or OrderState.Rejected or OrderState.Expired && report.FillQuantity == 0 && o.State != report.State) throw new InvalidOperationException("Terminal order state reversal");
        // Unknown remains locked until a state inquiry is confirmed, even if a partial fill arrives.
        o.State = (wasUnknown || latePreCancelFill) && !report.ReconciliationConfirmed && o.Remaining > 0 ? OrderState.Unknown : report.State;
        if (o.State == OrderState.Cancelled) cancellationConfirmedAt[o.Id] = report.ReceivedAt;
        brokerIds[o.Id] = report.BrokerOrderId; reports.Add(report.ReportId, report);
    }
    public void MarkNetworkTimeout(string orderId, DateTimeOffset at) => Report(new("timeout:" + orderId + ":" + at.ToString("O"), orderId, brokerIds.GetValueOrDefault(orderId) ?? "paper:" + orderId, at, OrderState.Unknown));
}
