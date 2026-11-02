using System.Globalization;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private bool showTradeMarkers,showLiveTradeMarkers,showBacktestTradeMarkers;
    private bool refreshingObservedTrades;
    private string paperRun="",paperSource="모의매매";
    public bool ShowTradeMarkers {get=>showTradeMarkers;set{showTradeMarkers=value;store.State("show-trade-markers",value);Changed();ReloadTradeMarkers();if(value)_=RefreshObservedTrades();}}
    public bool ShowLiveTradeMarkers {get=>showLiveTradeMarkers;set{showLiveTradeMarkers=value;store.State("show-live-trade-markers",value);Changed();ReloadTradeMarkers();if(value)_=RefreshObservedTrades();}}
    // A backtest selection is a transient chart context. Persisting it made an old run
    // reappear after a fresh search or application restart, unlike confirmed paper/live fills.
    public bool ShowBacktestTradeMarkers {get=>showBacktestTradeMarkers;set{if(showBacktestTradeMarkers==value)return;showBacktestTradeMarkers=value;if(value)RestoreSelectedBacktestChartRange();Changed();Changed(nameof(DisplayTradeMarkers));Changed(nameof(TradeMarkerInfo));}}
    public IReadOnlyList<TradeMarker> TradeMarkers {get;private set;}=[];
    public string TradeMarkerInfo=>"실제·모의·선택 백테스트 체결을 각각 켜고 끌 수 있으며 모든 봉에서 같은 체결시각·가격을 표시합니다.";
    private string AccountScope=>broker==null?"":broker.Id+":"+TradingStore.Hash(broker.Account);
    private async Task RefreshObservedTrades()
    {
        if(MultiSessionExists||refreshingObservedTrades||Busy||!IsOnline||session==null)return;
        var current=session;refreshingObservedTrades=true;
        try{await current.Refresh(KoreaClock.Today);if(!disposed&&!Busy&&ReferenceEquals(current,session)){Replace(Orders,current.Orders);PersistLiveFills();}}
        catch{ /* Keep the last confirmed fills when the background read fails. */ }
        finally{refreshingObservedTrades=false;}
    }
    private void PersistPaperFills()
    {
        if(paper==null||dataset==null||paperRun==""||paperSource!="모의매매")return;
        for(int i=0;i<paper.Fills.Count;i++){var f=paper.Fills[i];var id="trade-marker:"+paperRun+":"+i;store.State(id,new TradeMarker(id,dataset.Stock.Key,f.Time,f.Side=="매수",f.Quantity,f.Price,paperSource));}
        ReloadTradeMarkers();
    }
    private void PersistLiveFills()
    {
        if(broker==null)return;
        foreach(var order in Orders.Where(o=>o.Filled>0&&!IsMultiOrder(o.Number)))
        {
            if(!DateTime.TryParseExact(order.Date.Replace("-",""),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))continue;
            var id="trade-marker:"+TradingStore.Hash(AccountScope+":"+order.Date+":"+order.Number);
            var key=order.Market+":"+order.Symbol;
            store.State(id,new TradeMarker(id,key,order.LastFillTime??new DateTimeOffset(date,TimeSpan.FromHours(9)),order.Side.Contains("매수"),order.Filled,order.AverageFillPrice,"실제매매",AccountScope,order.LastFillTime==null||order.AverageFillPrice==null));
        }
        ReloadTradeMarkers();
    }
    private void ReloadTradeMarkers()
    {
        var key=(selected??dataset?.Stock)?.Key;
        var rows=new List<TradeMarker>();
        if(key!=null&&(ShowTradeMarkers||ShowLiveTradeMarkers))foreach(var row in store.States("trade-marker:"))try
        {
            var m=Serialization.Unpack<TradeMarker>(row.Json);var paper=m.Source is "모의매매"||m.Source==RealtimePaperStrategyBatch.MarkerSource;var live=m.Source=="실제매매"&&IsOnline&&m.AccountScope==AccountScope;
            if(m.StockKey==key&&(ShowTradeMarkers&&paper||ShowLiveTradeMarkers&&live))rows.Add(m);
        }catch{ /* Preserve other records if one legacy row is damaged. */ }
        TradeMarkers=rows.OrderBy(m=>m.Time).ToArray();Changed(nameof(TradeMarkers));Changed(nameof(DisplayTradeMarkers));Changed(nameof(TradeMarkerInfo));
    }
    /// <summary>Clears only the visible/session backtest context. Saved test history remains available in the test-history tab.</summary>
    private void ResetVisibleBacktest()
    {
        showBacktestTradeMarkers=false;SelectedRun=null;LatestResult=null;oversoldResult=null;recurringResult=null;
        OversoldCards.Clear();RecurringCards.Clear();
        foreach(var name in new[]{nameof(ShowBacktestTradeMarkers),nameof(LatestResult),nameof(OversoldResultSummary),nameof(RecurringResultPeriod),nameof(RecurringDetails),nameof(ChartEquity),nameof(DisplayTradeMarkers),nameof(TradeMarkerInfo)})Changed(name);
    }
}
