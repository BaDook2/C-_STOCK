using System.Collections.ObjectModel;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Research.DayTrading;
using Stock.Core.Trading;
namespace Stock.Desktop;
public enum DesktopRunKind { Backtest,Screening,StrategyD,Recurring }
public sealed record DesktopHistoryEntry(string Id,DateTimeOffset At,DesktopRunKind Kind,string Name,string Stock,string Period,string Trades,string PayloadKey);
public sealed record HistoryRow(DesktopHistoryEntry Entry)
{
    public string At=>Entry.At.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss");
    public string Stock=>Entry.Stock;public string Strategy=>Entry.Name;public string Period=>Entry.Period;public string Trades=>Entry.Trades;
    public string Kind=>Entry.Kind switch{DesktopRunKind.Backtest=>"전략 백테스트",DesktopRunKind.Screening=>"일반 스크리닝",DesktopRunKind.StrategyD=>"전략 D 연구",_=>"정기매수"};
}
public sealed partial class MainViewModel
{
    private ResearchArchive? visibleBacktest;
    private bool showBacktest;
    public ResearchWorkspace Research {get;private set;}=null!;
    public MarketHistoryService HistoryService {get;private set;}=null!;
    public ObservableCollection<HistoryRow> History {get;}=[];
    public HistoryRow? SelectedHistory {get;set;}
    public DateTime? HistoryFrom {get;set;}=DateTime.Today.AddMonths(-1);
    public DateTime? HistoryTo {get;set;}=DateTime.Today;
    public AsyncCommand LoadHistoryCommand {get;private set;}=null!;
    public AsyncCommand OpenHistoryCommand {get;private set;}=null!;
    public event Action<ResearchResultViewModel>? ResultPopupRequested;
    public event Action<ScreeningWorkspace>? ScreeningHistoryRequested;
    public bool ShowBacktest
    {
        get=>showBacktest;
        set{Set(ref showBacktest,value);RefreshPaperMarkers();ChartZones=value&&visibleBacktest!=null?new ResearchResultViewModel(visibleBacktest.Result,visibleBacktest.Bars).Zones:[];Changed(nameof(ChartZones));}
    }
    private void InitializeResearch()
    {
        HistoryService=new(services,()=>Broker);Research=new(services,Strategies,HistoryService,()=>SelectedStock,()=>Online,()=>ChartCustomAvailable);Research.Notice+=Notify;Research.Completed+=archive=>
        {
            visibleBacktest=archive;ShowBacktest=true;Status=Research.Status;RefreshChart();RecordHistory(new(archive.Id,archive.At,DesktopRunKind.Backtest,archive.Name,archive.Result.Stock.Name,archive.Result.From+" ~ "+archive.Result.To,archive.Result.Performance.Trades.ToString(),archive.Id));
        };
        LoadHistoryCommand=new(()=>{try{FilterHistory();}catch(Exception ex){Notify(ex.Message);}return Task.CompletedTask;});OpenHistoryCommand=new(OpenSelectedHistory);FilterHistory();
    }
    private void RecordHistory(DesktopHistoryEntry entry){services.Store.State("desktop-history-index:"+entry.Id,entry);FilterHistory();}
    private void FilterHistory()
    {
        var from=(HistoryFrom??DateTime.MinValue).Date;var to=(HistoryTo??DateTime.MaxValue).Date;if(from>to)throw new InvalidOperationException("기록 조회 기간의 순서를 확인하세요.");History.Clear();
        // Query lightweight metadata only. Full bar/config/evidence snapshots are opened on demand.
        foreach(var item in services.Store.States("desktop-history-index:").Select(row=>Serialization.Unpack<DesktopHistoryEntry>(row.Json)).Where(a=>a.At.ToOffset(TimeSpan.FromHours(9)).Date>=from&&a.At.ToOffset(TimeSpan.FromHours(9)).Date<=to).OrderByDescending(a=>a.At))History.Add(new(item));
    }
    private Task OpenSelectedHistory()
    {
        if(SelectedHistory is not {} row)return Task.CompletedTask;
        try
        {
            var entry=row.Entry;
            switch(entry.Kind)
            {
                case DesktopRunKind.Backtest:
                    var archive=services.Store.State<ResearchArchive>(entry.PayloadKey)??throw new InvalidOperationException("기록 본문이 없습니다.");ResultPopupRequested?.Invoke(new(archive.Result,archive.Bars));break;
                case DesktopRunKind.Screening:
                    var snapshot=services.Store.State<OversoldScreeningRun>(entry.PayloadKey)??throw new InvalidOperationException("스크리닝 snapshot이 없습니다.");var screen=new ScreeningWorkspace(services,Strategies,HistoryService,()=>Online,WatchScreenTargets,()=>Research.FactsJson);screen.Notice+=Notify;screen.BrowserRequested+=model=>BrowserPopupRequested?.Invoke(model);screen.LoadSnapshot(snapshot);ScreeningHistoryRequested?.Invoke(screen);break;
                case DesktopRunKind.StrategyD:
                    var result=services.Store.State<IntradayReplayResult>(entry.PayloadKey)??throw new InvalidOperationException("전략 D 결과가 없습니다.");DayTrading.ShowResult(result);DayTradingPopupRequested?.Invoke(DayTrading,1);break;
                case DesktopRunKind.Recurring:
                    var recurring=services.Store.State<RecurringPurchaseResult>(entry.PayloadKey)??throw new InvalidOperationException("정기매수 결과가 없습니다.");var bars=services.Store.State<PriceBar[]>("desktop-recurring-bars:"+recurring.Id)??throw new InvalidOperationException("재현 봉이 없습니다.");RecurringPopupRequested?.Invoke(new(recurring,bars));break;
            }
        }
        catch(Exception ex) when(ex is not OutOfMemoryException){Notify("저장 기록을 열 수 없습니다. "+ex.Message);}return Task.CompletedTask;
    }
}
