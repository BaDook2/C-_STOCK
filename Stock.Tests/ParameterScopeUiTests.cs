using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void ParameterScopeUiTests(MainWindow window,string output)
    {
        using var db=new TradingStore(Path.Combine(Dir,"parameter-scope-ui.db"));
        var config=DailyConfig();config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.UsePivotSupport=config.UseApproximateVolumeZone=false;
        var original=Serialization.Pack(config);db.State("oversold-config-v1",config);db.State("oversold-facts-v1",DailyFacts(5));
        using var vm=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db);var old=window.DataContext;window.DataContext=vm;vm.ActiveTab=2;
        void Do(string action){vm.Actions[action].Execute(null);PumpUntil(()=>!vm.Busy);window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
        ParameterField Field(string key)=>vm.OversoldUsageGroups.SelectMany(s=>s.Groups).SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key==key);
        void Expand(){for(int pass=0;pass<4;pass++){foreach(var e in Descendants(window).OfType<Expander>().ToArray())e.IsExpanded=true;window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}}
        var properties=ParameterCatalog.Properties(config).ToArray();var fields=vm.OversoldUsageGroups.SelectMany(s=>s.Groups).SelectMany(g=>g.Fields).ToArray();
        Check(properties.Length==59&&fields.Length==59&&fields.Select(f=>f.Metadata.Key).Distinct().Count()==59&&properties.Select(p=>p.Name).Order().SequenceEqual(fields.Select(f=>f.Metadata.Key).Order())&&OversoldParameterMetadata.Keys.Order().SequenceEqual(properties.Select(p=>p.Name).Order()),"all 59 config parameters occur in exactly one explicit metadata scope");
        Check(vm.OversoldUsageGroups.Select(s=>s.Count).SequenceEqual(new[]{8,51,0})&&vm.OversoldUsageGroups.Select(s=>s.Metadata.Scope).SequenceEqual(new[]{ParameterUsageScope.Backtest,ParameterUsageScope.CommonStrategy,ParameterUsageScope.Online}),"scope counts are backtest8 common51 online0 without invented settings");
        var assumptions=new[]{"InitialCapital","CommissionPercent","TaxPercent","SlippagePercent","AnnualRiskFreePercent","AnnualTradingDays","TouchPolicy","UsePriceOnlyBacktest"};Check(fields.Where(f=>f.Metadata.UsageScope==ParameterUsageScope.Backtest).Select(f=>f.Metadata.Key).Order().SequenceEqual(assumptions.Order()),"only model cash costs touch data assumption and statistics are backtest-specific");
        Check(Field("EmergencyStopLossPercent").Metadata.UsageScope==ParameterUsageScope.CommonStrategy&&Field("TouchPolicy").Metadata.UsageDescription.Contains("언제나 종가")&&Field("AnnualTradingDays").Metadata.UsageDescription.Contains("Sharpe"),"exit thresholds are distinct from daily detection and performance assumptions");
        Check(vm.OversoldUsageGroups[2].Metadata.EmptyMessage.Contains("미지원")&&vm.OversoldUsageGroups[1].Metadata.Status.Contains("적용되지")&&vm.IsOffline&&vm.MultiTrading.RunCaption=="종목별 모의 백테스트","common category does not imply live support; offline batch stays model-only");
        Check(Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"))==original&&fields.All(f=>ReferenceEquals(f,vm.OversoldGroups.SelectMany(g=>g.Fields).Single(x=>x.Metadata.Key==f.Metadata.Key))),"scope projection shares original fields without rewriting saved config");

        Field("CommissionPercent").Value="0.015";Field("InitialCapital").Value="12,000";Field("MinimumDrawdownPercent").Value="-21.125";Field("TouchPolicy").Value="CloseOnly";Field("RequireReboundConfirmation").BoolValue=true;Field("EntryRatios").Value="50,30,20";
        vm.Preset="사용범위 검사";Do("추가");var edited=Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"));
        Check(db.State<OversoldConfig>("oversold-preset:사용범위 검사") is {CommissionPercent:.015m,InitialCapital:12000,MinimumDrawdownPercent:-21.125m,TouchPolicy:DailyTouchPolicy.CloseOnly,RequireReboundConfirmation:true,EntryRatios:"50,30,20"},"text numeric bool enum and list edits across scopes preserve existing preset storage");
        Do("기본값");Check(Field("CommissionPercent").Value=="0.015"&&Field("InitialCapital").Value=="10,000,000"&&Field("MinimumDrawdownPercent").Value=="-20"&&Field("TouchPolicy").Value=="ConservativeHighLow"&&Field("EntryRatios").Value=="40,30,30","defaults rebuild all scope bindings without altering original defaults");
        Do("복구");Do("낙주설정저장");Check(Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"))==edited,"recover rebuilds scope bindings with exactly the saved values");
        using(var restored=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db))Check(restored.OversoldUsageGroups.SelectMany(s=>s.Groups).SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="MinimumDrawdownPercent").Value=="-21.125"&&restored.OversoldUsageGroups.Count==3,"restarting restores edited config into three usage scopes");
        Field("InitialCapital").Value="100";Do("낙주설정저장");Check(vm.Status.Contains("예정 투자금")&&Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"))==edited,"cross-scope capital/investment validation preserves last saved config");Field("InitialCapital").Value="12,000";
        Field("CommissionPercent").Value="11";Do("낙주설정저장");Check(Field("CommissionPercent").Error!=""&&Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"))==edited,"range validation remains visible and prevents invalid persisted values");Field("CommissionPercent").Value="0.015";Do("낙주설정저장");
        Expand();var fee=Descendants(window).OfType<TextBox>().Single(t=>t.DataContext==Field("CommissionPercent"));fee.ApplyTemplate();window.UpdateLayout();var readout=fee.Template.FindName("PercentReadout",fee) as TextBlock;
        Check(fee.Text=="0.02"&&Field("CommissionPercent").Value=="0.015","regrouped percentage input displays two decimals without losing source precision");fee.Focus();Keyboard.Focus(fee);Check(fee.Text=="0.02"&&Field("CommissionPercent").Value=="0.015","regrouped percentage editing keeps fixed display and untouched precise source");Keyboard.ClearFocus();
        bool helped=false;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};timer.Tick+=(_,_)=>{foreach(var help in Application.Current.Windows.OfType<ParameterHelpDialog>().ToArray()){help.UpdateLayout();var texts=Descendants(help).OfType<TextBlock>().Select(t=>t.Text).ToArray();helped=texts.Any(t=>t=="사용범위 · 백테스트 전용")&&texts.Any(t=>t.Contains("실제 장중 체결"));help.Close();}};timer.Start();try{Descendants(window).OfType<Button>().Single(b=>Equals(b.Content,"?")&&b.DataContext==Field("TouchPolicy")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}finally{timer.Stop();}Check(helped&&Field("TouchPolicy").Metadata.ShortDescription.Contains("사용범위"),"tooltip and shared detail help expose central scope and exact touch-policy semantics");

        // ScreenOversold starts flat on the last date; model assumptions must not alter that day's entry signal.
        var data=new DailyFixture().History(DailyStock,default).GetAwaiter().GetResult();var day=DailyDates.Of(data.CompletedBars[^1]);var facts=DailyFacts(5);
        var baseline=new OversoldBacktest().Run(data,config,day,day,facts);var varied=config.Clone();varied.InitialCapital=20000;varied.CommissionPercent=2;varied.TaxPercent=3;varied.SlippagePercent=4;varied.AnnualRiskFreePercent=5;varied.AnnualTradingDays=250;varied.TouchPolicy=DailyTouchPolicy.CloseOnly;
        var modified=new OversoldBacktest().Run(data,varied,day,day,facts);Check(baseline.Signals.Any(s=>s.Action=="Buy")&&Serialization.Pack(baseline.Signals)==Serialization.Pack(modified.Signals)&&modified.Trades.Count==0,"documented one-day screening is unchanged by valid backtest assumptions and creates no fills");

        var width=window.Width;var height=window.Height;
        foreach(var size in new[]{(1540d,1040d),(1160d,760d)})
        {
            window.Width=size.Item1;window.Height=size.Item2;Expand();var panel=Descendants(window).OfType<OversoldParameterPanel>().Single();
            var cards=Descendants(panel).OfType<Border>().Where(b=>b.DataContext is ParameterField&&b.MinHeight==145).ToArray();var panelWidth=panel.ActualWidth;
            Check(cards.Length==59&&cards.All(c=>{var p=c.TransformToAncestor(panel).Transform(new Point());return p.X>=0&&p.X+c.ActualWidth<=panelWidth+1;}),"all scoped cards fit horizontally at "+size.Item1);
            Check(Descendants(window).OfType<ScrollViewer>().Any(s=>s.IsVisible&&s.ScrollableHeight>0),"long scope settings remain vertically scrollable at "+size.Item1);
            foreach(var e in Descendants(panel).OfType<Expander>().Where(e=>e.DataContext is ParameterGroup).ToArray())e.IsExpanded=false;
            Descendants(panel).OfType<Expander>().Single(e=>e.Header is string s&&s.StartsWith("시점별 검증 자료")).IsExpanded=false;
            Descendants(window).OfType<ScrollViewer>().Single(s=>ReferenceEquals(s.Content,panel)).ScrollToTop();Render((FrameworkElement)window.Content,Path.Combine(output,$"parameter-scopes-{size.Item1}.png"));
            var empty=Descendants(panel).OfType<TextBlock>().Single(t=>t.Text==vm.OversoldUsageGroups[2].Metadata.EmptyMessage);empty.BringIntoView();Render((FrameworkElement)window.Content,Path.Combine(output,$"parameter-scopes-online-{size.Item1}.png"));
        }
        window.DataContext=old;window.Width=width;window.Height=height;
    }
}
