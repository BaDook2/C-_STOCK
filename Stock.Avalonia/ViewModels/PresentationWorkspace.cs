using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.Desktop;
/// <summary>WPF visual presentation state only. Broker/strategy decisions stay in their existing services.</summary>
public sealed partial class MainViewModel
{
    private int workspaceToolTab=1;
    private bool showMa=true,showCloud;
    public int WorkspaceToolTab {get=>workspaceToolTab;set=>Set(ref workspaceToolTab,value);}
    public bool IsOffline=>!Online;
    public bool IsOnline=>Online;
    public string ModeDescription=>Online?"연결한 증권사의 시세를 사용합니다. 주문 전 계좌와 한도를 확인합니다.":"인증키 없이 실제 과거 가격으로 연습해요. 주문은 가상 계좌에서만 처리됩니다.";
    public string ConnectedAccountText=>Accounts.Mode;
    public string DisplayPrice=>PriceText=="—"?"—":PriceText+" "+(SelectedStock?.Exchange==Exchange.KRX?"원":"USD");
    public string ChartDataInfo=>dataset?.Description??"";
    public string ChartWatchStar=>WatchStar.StartsWith('★')?"★ 관심등록":"☆ 관심등록";
    public bool ShowMa {get=>showMa;set=>Set(ref showMa,value);}
    public bool ShowCloud {get=>showCloud;set=>Set(ref showCloud,value);}
    public string VirtualTradingHelp=>Online?"연결 증권사 시세로 가상 전략을 실행합니다. 실제 주문은 전송하지 않습니다.":"네이버 KRX 공개 현재가·확정 일봉으로 가상 전략을 실행합니다. 일봉 종가 신호는 다음 거래일의 새 관측 이후 주문하며 시가 체결을 보장하지 않습니다.";
    public bool CanUseDisplayDays=>!ChartPeriods.Intraday(ChartPeriod);
    public string[] VisibleChartPeriods=>Online?Periods:["일봉"];
    public bool ShowNormalScreen {get=>screenMode==0;set{if(value){screenMode=0;Changed();Changed(nameof(ShowRealtimeScreen));}}}
    public bool ShowRealtimeScreen {get=>screenMode==1;set{if(value){screenMode=1;Changed();Changed(nameof(ShowNormalScreen));}}}
    private int screenMode;
    private bool domesticMarket=true;
    public bool DomesticMarket {get=>domesticMarket;set{if(value&&Set(ref domesticMarket,value)){Changed(nameof(OverseasMarket));Changed(nameof(VisibleIndices));}}}
    public bool OverseasMarket {get=>!domesticMarket;set{if(value&&Set(ref domesticMarket,false)){Changed(nameof(DomesticMarket));Changed(nameof(VisibleIndices));}}}
    public MarketIndexViewModel[] VisibleIndices=>MarketIndices.Where(i=>DomesticMarket?i.Code is "KOSPI" or "KOSDAQ":i.Code is not ("KOSPI" or "KOSDAQ")).ToArray();
    public System.Collections.ObjectModel.ObservableCollection<string> UiLogs {get;}=[];
    public AsyncCommand CancelWork {get;private set;}=null!;
    public AsyncCommand InspectStorage {get;private set;}=null!;
    private void InitializePresentation()
    {
        CancelWork=new(()=>{operation?.Cancel();Research.Stop();Screening.Stop();Recurring.Stop();return Task.CompletedTask;},()=>Busy||Research.Busy||Screening.Busy||!Recurring.Run.CanExecute(null));
        Research.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ResearchWorkspace.Busy))CancelWork.Refresh();};
        Screening.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ScreeningWorkspace.Busy))CancelWork.Refresh();};
        Recurring.Run.CanExecuteChanged+=(_,_)=>CancelWork.Refresh();
        InspectStorage=new(async()=>{var report=await Task.Run(()=>StorageDiagnostics.Inspect(Path.Combine(services.DataDirectory,"workspace.db")));Notify(report.Healthy?"저장소 점검 완료 · 무결성과 저장 형식 정상":"저장소 점검 오류 · "+(report.Error??string.Join(", ",report.Integrity))+" · 형식 오류 "+report.JsonIssues.Count+"개");});
        PropertyChanged+=(_,e)=>
        {
            if(e.PropertyName==nameof(Busy))CancelWork.Refresh();
            if(e.PropertyName==nameof(Status)){UiLogs.Insert(0,DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss")+"  "+Status);while(UiLogs.Count>100)UiLogs.RemoveAt(UiLogs.Count-1);}
            if(e.PropertyName==nameof(PriceText))Changed(nameof(DisplayPrice));
            if(e.PropertyName==nameof(WatchStar))Changed(nameof(ChartWatchStar));
            if(e.PropertyName==nameof(ChartBars))Changed(nameof(ChartDataInfo));
            if(e.PropertyName==nameof(Online)){Changed(nameof(IsOffline));Changed(nameof(IsOnline));Changed(nameof(ModeDescription));Changed(nameof(ConnectedAccountText));Changed(nameof(VirtualTradingHelp));Changed(nameof(VisibleChartPeriods));WorkspaceToolTab=Online?0:1;}
        };
    }
}
