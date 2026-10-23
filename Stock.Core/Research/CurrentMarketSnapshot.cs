using Stock.Core.Research.Oversold;
namespace Stock.Core.Research;

/// <summary>Current observations only. None of these fields are backfilled into historical facts.</summary>
public sealed record CurrentMarketSnapshot(StockItem Stock,MarketQuote Quote,DateTimeOffset ReceivedAt,
    decimal? Open,decimal? High,decimal? Low,decimal? Volume,decimal? TradingValue,
    string Source,TimeSpan PollInterval,DailySecurityStatus? Security=null,bool ExchangeTimestamp=true);
public interface ICurrentMarketData
{
    Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token);
    Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token);
}

/// <summary>One in-flight read, provider-aware throttling, injectable clock and latest-generation delivery.</summary>
public sealed class SelectedQuotePump(ICurrentMarketData source,TimeProvider? clock=null):IDisposable
{
    private readonly TimeProvider time=clock??TimeProvider.System;
    private readonly SemaphoreSlim gate=new(1,1);
    private CancellationTokenSource? cancellation;
    private long generation;
    private string? key;
    public Task Completion {get;private set;}=Task.CompletedTask;
    public void Select(StockItem stock,Action<CurrentMarketSnapshot> update,Action<string> error)
    {
        if(key==stock.Key&&cancellation!=null)return;
        Stop();key=stock.Key;var version=generation;var stop=new CancellationTokenSource();cancellation=stop;
        Completion=Run(stock,version,stop,update,error);
    }
    private async Task Run(StockItem stock,long version,CancellationTokenSource stop,Action<CurrentMarketSnapshot> update,Action<string> error)
    {
        int failures=0;var interval=TimeSpan.FromSeconds(7);
        try
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    await gate.WaitAsync(stop.Token);
                    CurrentMarketSnapshot snapshot;
                    try{snapshot=await source.Current(stock,stop.Token);}finally{gate.Release();}
                    if(version!=generation||stop.IsCancellationRequested)return;
                    if(snapshot.Stock.Key!=stock.Key)throw new InvalidOperationException("종목 응답 불일치");
                    update(snapshot);failures=0;
                    interval=snapshot.PollInterval<TimeSpan.FromSeconds(1)?TimeSpan.FromSeconds(1):snapshot.PollInterval;
                    if(snapshot.Quote.State!="OPEN")interval=TimeSpan.FromSeconds(Math.Max(30,interval.TotalSeconds));
                }
                catch(OperationCanceledException) when(stop.IsCancellationRequested){return;}
                catch{if(version!=generation)return;failures++;interval=TimeSpan.FromSeconds(Math.Min(120,Math.Max(interval.TotalSeconds,Math.Pow(2,Math.Min(failures,6))*7)));error($"갱신 실패 · {interval.TotalSeconds:N0}초 후 재시도 · 이전 관측값 유지");}
                await Task.Delay(interval,time,stop.Token);
            }
        }
        catch(OperationCanceledException){}
        finally{stop.Dispose();}
    }
    public void Stop(){generation++;key=null;var old=cancellation;cancellation=null;try{old?.Cancel();}catch(ObjectDisposedException){}}
    public void Dispose()=>Stop();
}

public static class DailyQuoteMerger
{
    public static DateOnly Date(DateTimeOffset time,Stock.Core.Trading.Exchange exchange)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time,TimeZoneInfo.FindSystemTimeZoneById(exchange==Stock.Core.Trading.Exchange.KRX?"Korea Standard Time":"Eastern Standard Time")).DateTime);
    /// <summary>Quotes update a display-only, unfinished bar. Research completion comes exclusively from History.</summary>
    public static PriceBar[] Merge(IReadOnlyList<PriceBar> history,CurrentMarketSnapshot snapshot,DateTimeOffset now)
    {
        var q=snapshot.Quote;var day=Date(q.TradeTime,snapshot.Stock.Exchange);
        if(q.Price<=0||q.TradeTime>now.AddMinutes(1)||day!=Date(now,snapshot.Stock.Exchange))return history.ToArray();
        var old=history.LastOrDefault(b=>Date(b.Time,snapshot.Stock.Exchange)==day);
        if(snapshot.Open is not >0||snapshot.High is not >0||snapshot.Low is not >0||snapshot.Volume is not >=0)return history.ToArray();
        // A later valid official completed candle is never downgraded by a cached poll.
        if(old is {IsComplete:true}&&PublicMarketData.IsDailyComplete(old,snapshot.Stock.Exchange,now))return history.ToArray();
        var axis=old?.Time??(snapshot.Stock.Exchange==Stock.Core.Trading.Exchange.KRX?new DateTimeOffset(day.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)):q.TradeTime);
        var bar=new PriceBar(axis,snapshot.Open.Value,Math.Max(snapshot.High.Value,q.Price),Math.Min(snapshot.Low.Value,q.Price),q.Price,snapshot.Volume.Value,false);
        bar.Validate();return history.Where(b=>Date(b.Time,snapshot.Stock.Exchange)!=day).Append(bar).OrderBy(b=>b.Time).ToArray();
    }
}
