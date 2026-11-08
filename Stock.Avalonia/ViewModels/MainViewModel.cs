using System.Collections.ObjectModel;
using Stock.Core.Research;
namespace Stock.Desktop;
public sealed partial class MainViewModel:Bindable
{
    private readonly AppServices services;
    private string status="종목 이름으로 검색해 시작하세요. 계좌키와 사용자 DB는 원본 WPF와 공유하지 않습니다.";
    private int activeTab;
    public int ActiveTab {get=>activeTab;set=>Set(ref activeTab,value);}
    public StrategyEditor Strategies {get;}
    public MainViewModel(AppServices services){this.services=services;AsyncCommand.Failed+=CommandFailed;InitializeMarket();InitializeChartTimeframe();Strategies=new(services.Store);Strategies.Notice+=Notify;InitializeResearch();InitializeDayTrading();InitializeWatch();InitializeScreening();InitializeMarketOverview();InitializeRecurring();InitializeAccounts();InitializeRealtime();InitializeVirtual();InitializePresentation();}
    private void CommandFailed(Exception error){if(!disposed)Notify("작업에 실패했습니다. "+(error is Stock.Core.Trading.StorageException or InvalidOperationException?error.Message:error.GetType().Name));}
    public string Status {get=>status;private set=>Set(ref status,value);}
}
