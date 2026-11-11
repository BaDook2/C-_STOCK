namespace Stock.Core.Trading;

public sealed partial class PaperBroker
{
    private readonly Dictionary<string, Fill> confirmedSimulationFills = [];
    /// <summary>Shared SIMULATION ledger mutation after an execution model confirms a fill. Does not send orders or infer liquidity.</summary>
    internal bool ApplySimulationFill(string executionId, Fill fill)
    {
        if (string.IsNullOrWhiteSpace(executionId)) throw new InvalidOperationException("Execution ID required");
        if (confirmedSimulationFills.TryGetValue(executionId, out var prior)) { if (prior != fill) throw new InvalidOperationException("Conflicting duplicate execution ID"); return false; }
        var order = Orders.SingleOrDefault(o => o.Id == fill.OrderId) ?? throw new InvalidOperationException("Unknown order for fill");
        if (fill.Instrument.Currency != Currency || fill.Instrument != order.Instrument || fill.Buy != order.Buy || fill.Quantity <= 0 || fill.Quantity > order.Remaining || fill.Price <= 0 || fill.Fee < 0 || fill.Time <= order.Created || order.Kind == OrderKind.Limit && (fill.Buy ? fill.Price > order.Price : fill.Price < order.Price)) throw new InvalidOperationException("Invalid simulation fill quantity/limit/time");
        Positions.TryGetValue(fill.Instrument.Key, out var position);
        if (fill.Buy ? fill.Price * fill.Quantity + fill.Fee > Cash : position == null || position.Quantity < fill.Quantity) throw new InvalidOperationException("Fill exceeds verified cash/holdings");
        position ??= new(fill.Instrument); Positions.TryAdd(position.Symbol, position);
        decimal realized = 0;
        if (fill.Buy)
        {
            if (position.Quantity == 0) { position.OpenedAt = fill.Time; position.Owner = fill.Strategy; }
            position.AveragePrice = (position.AveragePrice * position.Quantity + fill.Price * fill.Quantity) / (position.Quantity + fill.Quantity);
            position.Quantity += fill.Quantity; position.CostFees += fill.Fee; Cash -= fill.Price * fill.Quantity + fill.Fee;
        }
        else
        {
            var entryFee = position.CostFees * fill.Quantity / position.Quantity;
            realized = (fill.Price - position.AveragePrice) * fill.Quantity - fill.Fee - entryFee;
            position.CostFees -= entryFee; position.Quantity -= fill.Quantity; Cash += fill.Price * fill.Quantity - fill.Fee;
        }
        position.Last = fill.Price; order.Filled += fill.Quantity;
        var actual = fill with { RealizedPnl = realized }; Fills.Add(actual); confirmedSimulationFills.Add(executionId, fill);
        order.State = order.Remaining == 0 ? OrderState.Filled : OrderState.PartiallyFilled;
        Record?.Invoke("Confirmed simulation execution", actual, fill.Time); return true;
    }
}
