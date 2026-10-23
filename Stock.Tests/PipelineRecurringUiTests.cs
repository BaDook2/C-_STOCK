using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Pipeline;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void PipelineRecurringUiTests(MainWindow window,string output)
    {
        using var db=new TradingStore(Path.Combine(Dir,"pipeline-ui.db"));using var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db);var old=window.DataContext;window.DataContext=vm;
        void Do(string key){vm.Actions[key].Execute(null);PumpUntil(()=>!vm.Busy);window.UpdateLayout();}
        vm.Region="해외";vm.Query="애플";Do("검색");
        vm.UseConfiguredStrategy=false;vm.ActiveTab=0;vm.From=new(2025,1,1);vm.To=new(2025,8,22);vm.RecurringAmount="100";vm.RecurringInitial="3,000";vm.RecurringFee="0.015";vm.RecurringWeekly=true;vm.RecurringAtOpen=true;Do("정기매수실행");
        Check(vm.BacktestTab==1&&vm.RecurringCards.Count==6&&vm.ChartEquity.Count>0&&vm.DisplayTradeMarkers.Count>0,"recurring run populates side-by-side cards equity and buy markers");
        var count=vm.History.Count;Check(db.States("recurring-run:").Count()>0&&vm.History[0].Period.Contains("정기매수"),"recurring config detailed result and shared history are persisted");
        vm.ShowRecurringMarkers=false;Check(vm.DisplayTradeMarkers.Count==0,"recurring buy markers follow checkbox");vm.ShowRecurringMarkers=true;
        var rangeChecks=new (string Key,Func<bool> Selected)[]{("기간1",()=>vm.ChartRange1Selected),("기간3",()=>vm.ChartRange3Selected),("기간6",()=>vm.ChartRange6Selected),("기간12",()=>vm.ChartRange12Selected),("기간36",()=>vm.ChartRange36Selected),("기간60",()=>vm.ChartRange60Selected),("기간120",()=>vm.ChartRange120Selected)};
        vm.ShowCloud=true;var chart=(CandleChart)window.FindName("PriceChart");
        foreach(var range in rangeChecks){Do(range.Key);Check(range.Selected(),range.Key+" quick range keeps selected color state");}
        Check(vm.ChartWindowCount==vm.ChartBars.Count,"quick chart range fits available bars");
        foreach(var period in new[]{"일봉","주봉","월봉"})
        foreach(var range in rangeChecks){vm.ChartPeriod=period;Do(range.Key);Check(chart.RightGapSlots<=.51,$"{period} {range.Key} fills real candles to the right plot edge with Ichimoku enabled");}
        Check(Descendants(window).OfType<RadioButton>().Count(r=>r.GroupName=="ChartRange")==7,"seven quick ranges use the same selected radio styling as candle periods");
        window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Render((FrameworkElement)window.Content,Path.Combine(output,"recurring-layout.png"));
        var caption=Descendants(window).OfType<RecurringParameterCaption>().First();Check(caption.Children.Count==2,"recurring captions build label and help from shared metadata");
        vm.RecurringAmount="bad";Do("정기매수실행");Check(vm.History.Count==count&&vm.Status.Contains("숫자"),"invalid recurring amount preserves existing result and history");
        using(var reload=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db))Check(reload.RecurringInitial=="3,000"&&reload.RecurringWeekly,"recurring settings restore with numeric grouping");
        window.DataContext=old;
    }
}
