using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.App;
using Stock.Core;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class AuditFileDialogs(string file):IStrategyFileDialogs
    {public int Imports,Exports;public string? Import(){Imports++;return file;}public string? Export(string name){Exports++;return file;}}

    private static void ButtonAuditTests(MainWindow window,string output)
    {
        var old=window.DataContext;var lines=new List<string>();
        using var db=new TradingStore(Path.Combine(Dir,"button-audit.db"));
        var config=DailyConfig();config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.UsePivotSupport=config.UseApproximateVolumeZone=false;
        db.State("oversold-config-v1",config);db.State("oversold-facts-v1",DailyFacts(5));
        var files=new AuditFileDialogs(Path.Combine(output,"button-audit-strategy.json"));var alerts=new List<string>();
        using var fakeBroker=new RecordingBroker(Brokerage.Kiwoom);
        using var vm=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db,alert:alerts.Add,strategyFileDialogs:files,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>fakeBroker,sessionDirectory:Path.Combine(Dir,"button-audit-session"));
        window.DataContext=vm;
        var closeDialogs=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};
        closeDialogs.Tick+=(_,_)=>{foreach(var dialog in Application.Current.Windows.Cast<Window>().Where(w=>w!=window).ToArray()){lines.Add("POPUP: "+dialog.GetType().Name);dialog.Close();}};
        closeDialogs.Start();
        try
        {
            void Click(int tab,string key)
            {
                vm.ActiveTab=tab;window.UpdateLayout();
                var button=Descendants(window).OfType<Button>().FirstOrDefault(b=>ReferenceEquals(b.Command,vm.Actions[key])&&b.IsVisible);
                if(button==null)throw new Exception("Visible button missing: "+key);
                Check(button.IsEnabled,"button click enabled: "+key);
                ((IInvokeProvider)new ButtonAutomationPeer(button)).Invoke();
                Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);PumpUntil(()=>!vm.Busy);window.UpdateLayout();lines.Add("CLICK: "+key);
            }
            void Tool(string header)
            {vm.ActiveTab=0;window.UpdateLayout();var tabs=(TabControl)window.FindName("WorkspaceTools");tabs.SelectedItem=tabs.Items.Cast<TabItem>().Single(t=>t.Header?.ToString()==header);window.UpdateLayout();}
            Click(0,"검색");Check(vm.SelectedStock!=null,"search button loads an observed chart in the offline fixture");
            Click(0,"관심등록");Check(vm.IsWatchRegistered,"watch registration button persists the selected stock");
            vm.Preset="버튼검사 전략";Click(2,"추가");Click(2,"저장");Click(2,"다운로드");Check(files.Exports==1&&File.Exists(Path.Combine(output,"button-audit-strategy.json")),"download button invokes file picker and writes an importable strategy JSON");
            vm.Preset="버튼검사 이름";Click(2,"이름변경");Click(2,"복구");Click(2,"불러오기");Check(files.Imports==1&&vm.Presets.Count>=2,"import button reads the selected file and adds its own strategy identity");
            Click(2,"기본값");Click(2,"복구");
            vm.From=new(2025,1,3);vm.To=new(2025,1,5);Tool("전략 백테스트");Click(0,"백테스트");Check(vm.LatestResult?.OversoldResultId!=null,"offline backtest button completes the common strategy flow");Click(0,"낙주결과상세");
            Tool("정기매수 백테스트");vm.RecurringAmount="1,000";vm.RecurringInitial="3,000";Click(0,"정기매수실행");Check(vm.RecurringCards.Count>0,"recurring backtest button renders actual calculated result cards");
            vm.ScreenFrom=new(2025,1,1);vm.ScreenTo=new(2025,1,5);Click(1,"목록");Click(1,"스크리닝");Click(1,"스크리닝결과");
            if(vm.Matches.Count>0){vm.SelectedMatch=vm.Matches[0];Click(1,"결과열기");}
            vm.ActiveTab=6;vm.SelectedWatchGroup=vm.RegistrationGroup;window.UpdateLayout();Click(6,"관심새로고침");vm.SelectedWatch=vm.WatchItems!.First();Click(6,"관심차트");
            vm.ActiveTab=6;vm.SelectedWatchGroup!.Edit.Execute(null);window.UpdateLayout();vm.GroupDraft="버튼검사 그룹";Click(6,"그룹저장");vm.SelectedWatchGroup.Edit.Execute(null);window.UpdateLayout();Click(6,"그룹편집취소");
            vm.SelectedWatch=vm.WatchItems!.First();Click(6,"관심해제");Click(6,"그룹추가");
            Click(4,"저장소점검");Check(alerts.Any(a=>a.Contains("저장소 정상"))&&StorageDiagnostics.Inspect(db.Path).Healthy,"storage inspection button reports the same checked database without modifying its data");
            Click(0,"키설정");vm.IsOnline=true;PumpUntil(()=>!vm.Busy);Click(3,"계좌조회");Click(3,"외부확인");
            Tool("전략 백테스트");Click(0,"백테스트");Check(vm.LatestResult?.OversoldResultId!=null,"online mode's backtest button remains executable and uses the same stored strategy");Click(0,"낙주결과상세");
            vm.ResearchFrom=DateTime.MinValue;vm.ResearchTo=DateTime.MaxValue.Date;vm.SelectedResearchRecord=vm.ResearchHistory.First(r=>r.Kind=="전략 백테스트");Click(4,"테스트상세");
            Tool("실제 계좌 주문");vm.Quantity="1";Click(0,"매수");Click(0,"매도");
            Check(fakeBroker.Submitted==0,"online order buttons respect rejected confirmation and never contact a real broker");
            Click(0,"긴급중지");Check(vm.Status.Contains("긴급중지"),"emergency button executes independently from ordinary order controls");
        }
        finally{closeDialogs.Stop();File.WriteAllLines(Path.Combine(output,"button-audit-clicks.txt"),lines);window.DataContext=old;}
    }
}
