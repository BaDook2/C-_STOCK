namespace Stock.Core.Trading;

public interface IStrategyExecutionAdapter
{
    bool IsLive {get;}
    bool Synchronized {get;}
    string Currency {get;}
    decimal Cash {get;}
    decimal Equity {get;}
    decimal ReservedCash {get;}
    IReadOnlyDictionary<string,Position> Positions {get;}
    IReadOnlyList<ManagedOrder> Orders {get;}
    IReadOnlyList<Fill> Fills {get;}
    void OnMarket(MarketEvent market);
    void Submit(ManagedOrder order);
    void Cancel(ManagedOrder order,DateTimeOffset now);
}
