using Avalonia.Threading;
using Stock.Core.Research;
namespace Stock.Desktop;
public sealed partial class MainViewModel
{
    public DayTradingViewModel DayTrading {get;private set;}=null!;
    private DispatcherTimer? dayTimer;
    private StockItem[] dayTargets=[];
    public event Action<DayTradingViewModel,int>? DayTradingPopupRequested;
    public AsyncCommand OpenDayTradingSettings {get;private set;}=null!;
    public AsyncCommand OpenDayTradingResearch {get;private set;}=null!;
    private void InitializeDayTrading()
    {
        var data=new DayTradingPreparation(services,HistoryService,()=>Broker);DayTrading=new(services.Store,new DeferredStrategyFileDialogs(()=>Strategies.FileDialogs),Notify,data.Prepare,data.Feed,()=>dayTargets);
        DayTrading.ResearchCompleted+=record=>RecordHistory(new(record.Id,record.At,DesktopRunKind.StrategyD,record.Name,"전략 D",record.Period,"연구 결과","daytrading-result:"+record.ResultId));
        DayTrading.PaperFillsChanged+=RefreshPaperMarkers;
        OpenDayTradingSettings=new(()=>{PrepareDayTargets();DayTradingPopupRequested?.Invoke(DayTrading,0);return Task.CompletedTask;});OpenDayTradingResearch=new(()=>{PrepareDayTargets();DayTradingPopupRequested?.Invoke(DayTrading,1);return Task.CompletedTask;});
        dayTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};dayTimer.Tick+=(_,_)=>{try{DayTrading.RefreshLive();}catch(Exception ex) when(ex is not OutOfMemoryException){dayTimer.Stop();DayTrading.BlockNewEntries("표시/기록 오류: "+ex.Message);Notify(ex.Message);}};dayTimer.Start();
    }
    private void PrepareDayTargets(){dayTargets=SelectedStock is {} stock?[stock]:[];DayTrading.StartDate=Research.From??DateTime.Today.AddDays(-7);DayTrading.EndDate=Research.To??DateTime.Today;DayTrading.FactsJson=Research.FactsJson;}
}
