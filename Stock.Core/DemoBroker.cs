namespace Stock.Core;

// Illustrative data only; demo orders never fill or affect these static holdings.
public sealed class DemoBroker : IBroker
{
    private readonly List<BrokerOrder> orders = [];
    public Task ConnectAsync() => Task.CompletedTask;
    public Task<Balance> GetBalanceAsync() => Task.FromResult(new Balance(
        [new("005930", "삼성전자 · 예시", 10, 70000, 72000, 20000), new("000660", "SK하이닉스 · 예시", 5, 180000, 185000, 25000)], 10_000_000, 11_645_000));
    public async Task<OrderReceipt> SubmitAsync(OrderRequest order)
    {
        order.Validate();
        await Task.Delay(300);
        var number = "D-" + Guid.NewGuid().ToString("N")[..8];
        orders.Insert(0, new BrokerOrder(KoreaClock.Today.ToString("yyyyMMdd"), number, order.Symbol, "데모 주문", order.Buy ? "매수" : "매도", order.Quantity, order.Price, 0, order.Quantity, false, false));
        return new OrderReceipt(number);
    }
    public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date) => Task.FromResult<IReadOnlyList<BrokerOrder>>(orders.Where(o => o.Date == date.ToString("yyyyMMdd")).ToList());
}
