using System.Windows.Threading;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private SelectedQuotePump? dailyPump;
    private DispatcherTimer? dailyTimer;
    private CurrentMarketSnapshot? pendingDaily,displayedDaily;
    private long dailyGeneration;
    private string dailyFailure="",historyDescription="",dailyRefreshKey="";
    private bool dailyHistoryRefreshing;
    private DateTimeOffset historyRetry;
    private DateTimeOffset lastStreamReceived,lastStreamTime;
    private bool LiveHeader=>displayedDaily?.Stock.Key==dataset?.Stock.Key&&displayedDaily!=null&&!Replaying&&paper==null&&!IsTick;
    private void StopDailyQuotes()
    {
        dailyGeneration++;dailyPump?.Dispose();dailyPump=null;dailyTimer?.Stop();dailyTimer=null;
        pendingDaily=displayedDaily=null;dailyFailure="";dailyRefreshKey="";historyRetry=lastStreamReceived=lastStreamTime=default;
    }
    private void StartDailyQuotes(StockItem item)
    {
        StopDailyQuotes();historyDescription=DataInfo;
        if(disposed||apiPaused||provider is not ICurrentMarketData current)return;
        var generation=dailyGeneration;
        dailyPump=new(current);
        dailyPump.Select(item,s=>dispatcher.BeginInvoke(()=>{if(!disposed&&generation==dailyGeneration&&dataset?.Stock.Key==s.Stock.Key&&s.ReceivedAt-lastStreamReceived>TimeSpan.FromSeconds(10)){if(pendingDaily==null||s.Quote.TradeTime>=pendingDaily.Quote.TradeTime){pendingDaily=s;dailyFailure="";}}}),
            message=>dispatcher.BeginInvoke(()=>{if(!disposed&&generation==dailyGeneration)dailyFailure=message;}));
        dailyTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};
        dailyTimer.Tick+=(_,_)=>RefreshDailyDisplay();dailyTimer.Start();
        if(IsOnline&&credentials!=null&&!IsTick)StartTicks();
    }
    /// <summary>Dispatcher-only coalescing. Chart zoom/range is not reset; unfinished quotes never enter dataset research bars.</summary>
    private void RefreshDailyDisplay()
    {
        if(disposed||dataset==null||pendingDaily is not {} current||current.Stock.Key!=dataset.Stock.Key)return;
        var now=DateTimeOffset.UtcNow;
        if(displayedDaily!=null&&current.Source==displayedDaily.Source&&current.Quote.TradeTime<displayedDaily.Quote.TradeTime)return;
        if(!Replaying&&paper==null&&!Busy)
        {
            var from=visibleDaily.FirstOrDefault()?.Time;
            var updated=DailyQuoteMerger.Merge(dataset.Bars,current,now);
            if(from.HasValue)updated=updated.Where(b=>b.Time>=from.Value).ToArray();
            // A completed oversold backtest deliberately focuses the chart on its tested
            // interval. The one-second quote pump must update the header without appending
            // every later history bar and compressing the BUY/SELL annotations off-screen.
            if(ShowBacktestTradeMarkers&&oversoldResult?.Stock.Key==dataset.Stock.Key)
                updated=updated.Where(b=>DailyDates.Of(b)>=oversoldResult.From&&DailyDates.Of(b)<=oversoldResult.To).ToArray();
            if(!visibleDaily.SequenceEqual(updated))ChartBars=updated;
            displayedDaily=current;StockQuote=current.Quote;Changed(nameof(StockQuote));
            foreach(var property in new[]{nameof(PriceText),nameof(ChangeText),nameof(PriceColor),nameof(PriceDate)})Changed(property);
        }
        var age=now-current.Quote.TradeTime;
        var state=current.Quote.State=="OPEN"?"진행 중 · 미완성 일봉":current.Quote.State is "HALT" or "SUSPENDED"?"거래정지":"장 종료/휴장 · 마지막 관측값";
        DataInfo=$"{current.Source} · {state} · {(current.ExchangeTimestamp?"시세":"서버 관측")} {current.Quote.TradeTime.ToOffset(TimeSpan.FromHours(9)):MM.dd HH:mm:ss} · 수신 {current.ReceivedAt.ToOffset(TimeSpan.FromHours(9)):HH:mm:ss} · 화면 1초 / 조회 {current.PollInterval.TotalSeconds:N0}초"+(age>TimeSpan.FromSeconds(20)?" · 지연/새 체결 없음":"")+(dailyFailure!=""?" · "+dailyFailure:"");
        _=RefreshClosedHistory(current,now,dailyGeneration);
    }
    private async Task RefreshClosedHistory(CurrentMarketSnapshot current,DateTimeOffset now,long generation)
    {
        if(dailyHistoryRefreshing||dataset==null||now<historyRetry)return;
        var today=DailyQuoteMerger.Date(now,current.Stock.Exchange);
        bool newDay=dataset.Bars.Length>0&&DailyQuoteMerger.Date(dataset.Bars[^1].Time,current.Stock.Exchange)<today&&DailyQuoteMerger.Date(current.Quote.TradeTime,current.Stock.Exchange)==today;
        var incomplete=dataset.Bars.LastOrDefault(b=>!b.IsComplete);
        bool closed=current.Quote.State!="OPEN"&&incomplete!=null&&PublicMarketData.IsDailyComplete(incomplete,current.Stock.Exchange,now);
        string key=today+":"+(closed?"close":"open");
        if((!newDay&&!closed)||dailyRefreshKey==key)return;
        dailyHistoryRefreshing=true;
        try
        {
            var fresh=await provider.History(current.Stock,ApiToken);
            if(disposed||generation!=dailyGeneration||dataset?.Stock.Key!=fresh.Stock.Key)return;
            dataset=fresh;historyDescription=fresh.Description;store.State("bars:"+fresh.Stock.Key,fresh);
            if(closed&&fresh.Bars.Any(b=>!b.IsComplete)||newDay&&!fresh.Bars.Any(b=>DailyQuoteMerger.Date(b.Time,current.Stock.Exchange)==today))historyRetry=now.AddSeconds(30);
            else dailyRefreshKey=key;
        }
        catch(OperationCanceledException){}
        catch{if(generation==dailyGeneration){dailyFailure="확정 일봉 조회 실패 · 진행봉은 백테스트 제외";historyRetry=now.AddSeconds(30);}}
        finally{dailyHistoryRefreshing=false;}
    }
    private void ObserveDailyTrade(StockItem item,MarketEvent e)
    {
        if(e.Kind!=EventKind.Trade||e.TradePrice is not {} price||dataset?.Stock.Key!=item.Key)return;
        if(e.ExchangeTime<lastStreamTime)return;
        var today=DailyQuoteMerger.Date(e.ExchangeTime,item.Exchange);
        var old=dataset.Bars.LastOrDefault(b=>DailyQuoteMerger.Date(b.Time,item.Exchange)==today);
        var previous=e.PreviousClose??dataset.CompletedBars.LastOrDefault(b=>DailyQuoteMerger.Date(b.Time,item.Exchange)<today)?.Close;
        if(previous is not >0)return;
        var prior=pendingDaily!=null&&DailyQuoteMerger.Date(pendingDaily.Quote.TradeTime,item.Exchange)==today?pendingDaily:null;
        var high=e.High??prior?.High??old?.High;var low=e.Low??prior?.Low??old?.Low;
        lastStreamReceived=e.ReceivedTime;lastStreamTime=e.ExchangeTime;
        pendingDaily=new(item,new(item.Name,price,price-previous.Value,(price/previous.Value-1)*100,e.ExchangeTime,"OPEN"),e.ReceivedTime,
            e.Open??prior?.Open??old?.Open,high.HasValue?Math.Max(high.Value,price):null,low.HasValue?Math.Min(low.Value,price):null,
            e.CumulativeVolume,null,"키움 실제 체결 · 화면 1초 반영",TimeSpan.FromSeconds(1));dailyFailure="";
    }
}
