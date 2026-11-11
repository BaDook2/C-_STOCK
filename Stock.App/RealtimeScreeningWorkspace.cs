using System.Collections.ObjectModel;
using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.App;

public sealed class RealtimeScreeningDisplayRow(
    string Code,string Name,string Price,string Change,string Proximity,string Conditions,
    string Unmet,string StrategyState,string LastAnalysis,string DataState,string OrderState,string Error):Observable
{
    public string Code {get;}=Code;
    public string Name {get;}=Name;
    private string[] values=[Price,Change,Proximity,Conditions,Unmet,StrategyState,LastAnalysis,DataState,OrderState,Error];
    public string Price=>values[0]; public string Change=>values[1]; public string Proximity=>values[2];
    public string Conditions=>values[3]; public string Unmet=>values[4]; public string StrategyState=>values[5];
    public string LastAnalysis=>values[6]; public string DataState=>values[7]; public string OrderState=>values[8]; public string Error=>values[9];
    public void Update(RealtimeScreeningDisplayRow row)
    {if(!values.SequenceEqual(row.values)){values=row.values;Changed(null);}}
}

public sealed partial class WorkspaceViewModel
{
    private bool realtimeScreenMode,realtimeScreenAutoTrade,showAllRealtimeScreening;
    private RealtimeScreeningDisplayRow? selectedRealtimeScreeningRow;
    private string realtimeScreenInfo="Inje · 원본 미국 체결·호가·당시 자료가 필요합니다. 실제 자동 주문은 잠겨 있습니다.",realtimeScreenConnection="중지";
    public ObservableCollection<RealtimeScreeningDisplayRow> RealtimeScreeningRows {get;}=[];
    public bool ShowAllRealtimeScreening {get=>showAllRealtimeScreening;set{showAllRealtimeScreening=value;Changed();RefreshInjesScreen();}}
    public RealtimeScreeningDisplayRow? SelectedRealtimeScreeningRow {get=>selectedRealtimeScreeningRow;set{selectedRealtimeScreeningRow=value;Changed();}}
    public bool RegularScreeningMode {get=>!realtimeScreenMode;set{if(value)RealtimeScreeningMode=false;}}
    public bool RealtimeScreeningMode {get=>realtimeScreenMode;set{if(realtimeScreenMode==value)return;if(RealtimeScreeningRunning||RealtimeScreeningProtectionActive){Changed();return;}realtimeScreenMode=value;Changed();Changed(nameof(RegularScreeningMode));Changed(nameof(ShowScreenCustomBars));Changed(nameof(ScreenDaily));Changed(nameof(ScreenCustomBars));NotifyRealtimeState();}}
    public bool RealtimeAutoTrade
    {
        get=>realtimeScreenAutoTrade;
        set
        {
            if(value==realtimeScreenAutoTrade)return;
            try{if(value&&!RealtimeMockSupported)throw new InvalidOperationException("MissingData: 미국 원본 체결·호가 공급자 연결이 필요합니다. 실제 주문은 전송하지 않습니다.");injesStream?.SetEntriesEnabled(value);realtimeScreenAutoTrade=value;Changed();}
            catch(InvalidOperationException error){alert(error.Message);Changed();}
        }
    }
    public bool RealtimeScreeningRunning=>injesStream?.Running==true;
    public bool RealtimeScreeningProtectionActive=>injesStream?.HasUnsettled==true;
    public bool CanStartRealtimeScreening=>RealtimeScreeningMode&&!RealtimeScreeningRunning&&!RealtimeScreeningProtectionActive&&CanConfigure;
    public bool CanAutoTradeRealtimeScreening=>RealtimeMockSupported;
    public bool CanStopRealtimeScreening=>RealtimeScreeningRunning||RealtimeScreeningProtectionActive;
    public bool RealtimeScreeningStrategyChanging=>false;
    public string RealtimeScreenInfo {get=>realtimeScreenInfo;private set{realtimeScreenInfo=value;Changed();}}
    public string RealtimeScreenConnection {get=>realtimeScreenConnection;private set{realtimeScreenConnection=value;Changed();}}
    private void InitializeRealtimeScreening()
    {
        Actions["실시간스크리닝시작"]=new ActionCommand(()=>Run(StartInjesScreen),()=>CanStartRealtimeScreening);
        Actions["실시간스크리닝중지"]=new ActionCommand(()=>StopRealtimeScreening(false),()=>CanStopRealtimeScreening);
    }
    private void ChangeRealtimeScreeningStrategy(string name,OversoldConfig configuration)
    {
        if(injesStream?.Running!=true)return;injesStream.StopEntries("전략 선택 변경 · 이전 선택의 보유 보호 유지");realtimeScreenAutoTrade=false;Changed(nameof(RealtimeAutoTrade));RefreshInjesScreen();
    }
    private Task StopRealtimeScreening(bool emergency)
    {
        if(injesStream!=null){if(emergency)injesStream.Emergency();else injesStream.StopEntries();var history=StoreInjesScreeningHistory(emergency?"긴급중지":"신규 진입 중지");if(history!=null)AddScreeningHistory(history);}
        realtimeScreenAutoTrade=false;Changed(nameof(RealtimeAutoTrade));RefreshInjesScreen();return Task.CompletedTask;
    }
    private void DisposeRealtimeScreening()
    {
        injesTimer?.Stop();var history=StoreInjesScreeningHistory("앱 종료");if(history!=null)AddScreeningHistory(history);injesStream?.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    private void NotifyRealtimeState()
    {
        Changed(nameof(RealtimeScreeningRunning));Changed(nameof(RealtimeScreeningProtectionActive));Changed(nameof(CanStartRealtimeScreening));Changed(nameof(CanStopRealtimeScreening));Actions.GetValueOrDefault("실시간스크리닝시작")?.Refresh();Actions.GetValueOrDefault("실시간스크리닝중지")?.Refresh();Refresh();
    }
}
