using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Core.Research;

/// <summary>Connected-account quote route. Failure never falls back to public price.</summary>
public sealed class BrokerCurrentMarketData(IAccountBroker broker,Func<StockItem,decimal?> previousClose):ICurrentMarketData
{
    public async Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(broker.Exchange!=stock.Exchange)throw new InvalidOperationException("연결 계좌의 시세 지원 시장과 다릅니다.");
        if(broker is ICurrentMarketData full)return await full.Current(stock,token);
        var price=await broker.CurrentPrice(stock.Symbol).WaitAsync(token);var now=DateTimeOffset.UtcNow;var previous=previousClose(stock);
        var change=previous>0?price-previous.Value:0;var percent=previous>0?(price/previous.Value-1)*100:0;
        return new(stock,new(stock.Name,price,change,percent,now,LiveBatchSupport.RegularSession(now)?"OPEN":"CLOSE"),now,null,null,null,null,null,broker.Name+" 현재가 · OHLCV 미제공",TimeSpan.FromSeconds(1),ExchangeTimestamp:false);
    }
    public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
}
