namespace Stock.Desktop;
public sealed partial class MainViewModel
{
    public RealtimeScreeningWorkspace Realtime {get;private set;}=null!;
    public event Action<LiveChartViewModel>? LiveChartPopupRequested;
    private void InitializeRealtime()
    {
        Realtime=new(services,Strategies,Screening,HistoryService,()=>Broker,WatchScreenTargets,()=>Research.FactsJson);Realtime.Notice+=Notify;Realtime.PopupRequested+=view=>LiveChartPopupRequested?.Invoke(view);Realtime.FillRecorded+=_=>RefreshPaperMarkers();
    }
    private bool showPaper=true;
    private bool showActual;
    public bool ShowActual {get=>showActual;set{Set(ref showActual,value);RefreshPaperMarkers();}}
    public bool ShowPaper {get=>showPaper;set{Set(ref showPaper,value);RefreshPaperMarkers();}}
    private void RefreshPaperMarkers()
    {
        var historical=ShowBacktest&&visibleBacktest!=null?new ResearchResultViewModel(visibleBacktest.Result,visibleBacktest.Bars).Markers:[];
        var paper=ShowPaper&&SelectedStock!=null?services.Store.States("trade-marker:").Select(s=>Stock.Core.Trading.Serialization.Unpack<Stock.Core.Research.TradeMarker>(s.Json)).Where(m=>m.StockKey==SelectedStock.Key&&m.Source!="실제매매").ToArray():[];
        var actual=ShowActual&&Online&&SelectedStock!=null?Accounts.ConfirmedMarkers().Where(m=>m.StockKey==SelectedStock.Key).ToArray():[];
        ChartMarkers=[..historical,..paper,..actual];Changed(nameof(ChartMarkers));
    }
}
