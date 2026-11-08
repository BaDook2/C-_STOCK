namespace Stock.Core.Trading;
public sealed record PaperBrokerCheckpoint(string Currency,decimal InitialCash,decimal Cash,ManagedOrder[] Orders,Position[] Positions,Fill[] Fills);
public sealed partial class PaperBroker
{
    public PaperBrokerCheckpoint Capture()=>Serialization.Unpack<PaperBrokerCheckpoint>(Serialization.Pack(new PaperBrokerCheckpoint(Currency,InitialCash,Cash,Orders.ToArray(),Positions.Values.ToArray(),Fills.ToArray())));
    /// <summary>Restore a private simulation ledger only. Does not submit, match, or publish fills.</summary>
    public void Restore(PaperBrokerCheckpoint value)
    {
        if(Orders.Count!=0||Positions.Count!=0||Fills.Count!=0)throw new InvalidOperationException("빈 가상 계좌에만 복원할 수 있습니다.");
        if(value.Currency!=Currency||value.InitialCash!=InitialCash||value.Cash<0||value.Orders.Select(o=>o.Id).Distinct().Count()!=value.Orders.Length||value.Orders.Any(o=>o.Quantity<1||o.Filled<0||o.Filled>o.Quantity||o.Price<=0||o.Currency!=Currency)||value.Fills.Any(f=>f.Quantity<=0||f.Price<=0||f.Fee<0||f.Instrument.Currency!=Currency))throw new InvalidOperationException("가상 계좌 원장 형식이 올바르지 않습니다.");
        var expected=InitialCash+value.Fills.Sum(f=>f.Buy?-f.Price*f.Quantity-f.Fee:f.Price*f.Quantity-f.Fee);
        if(Math.Abs(expected-value.Cash)>.00000001m)throw new InvalidOperationException("가상 계좌 현금과 체결 원장이 일치하지 않습니다.");
        foreach(var position in value.Positions)
        {
            var quantity=value.Fills.Where(f=>f.Instrument==position.Instrument).Sum(f=>f.Buy?f.Quantity:-f.Quantity);
            if(position.Quantity!=quantity||quantity<0||position.Currency!=Currency||quantity>0&&position.AveragePrice<=0||position.CostFees<0)throw new InvalidOperationException("가상 보유수량과 체결 원장이 일치하지 않습니다.");
        }
        if(value.Fills.Any(f=>!value.Positions.Any(p=>p.Instrument==f.Instrument)||!value.Orders.Any(o=>o.Id==f.OrderId)))throw new InvalidOperationException("가상 체결의 포지션/원주문이 없습니다.");
        if(value.Orders.Any(o=>o.Filled!=value.Fills.Where(f=>f.OrderId==o.Id).Sum(f=>f.Quantity)||!Enum.IsDefined(o.Kind)||!Enum.IsDefined(o.State))||value.Fills.Any(f=>!value.Orders.Any(o=>o.Id==f.OrderId&&o.Instrument==f.Instrument&&o.Buy==f.Buy&&f.Time>o.Created)))throw new InvalidOperationException("가상 주문 체결수량·방향·시간의 대사가 일치하지 않습니다.");
        Cash=value.Cash;Orders.AddRange(value.Orders);Fills.AddRange(value.Fills);foreach(var p in value.Positions)Positions.Add(p.Symbol,p);
    }
}
