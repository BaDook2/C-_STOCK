using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void UiRevisionTests(MainWindow window,string output)
    {
        using var db=new TradingStore(Path.Combine(Dir,"revision-ui.db"));var provider=new CountingMarket();using var vm=new WorkspaceViewModel(_=>false,provider:provider,store:db);var previous=window.DataContext;window.DataContext=vm;
        void Do(string action){vm.Actions[action].Execute(null);PumpUntil(()=>!vm.Busy);window.UpdateLayout();}
        vm.Region="해외";vm.Query="애플";vm.ChartPeriod="주봉";Do("검색");Check(vm.ChartBars.Count>10&&vm.ChartBars.Count<600&&vm.ChartPeriod=="주봉","first search result auto-loads previously selected period");
        var notifications=0;vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.ChartBars))notifications++;};vm.OpenStock(vm.SelectedStock!).GetAwaiter().GetResult();Check(provider.Reads.Count==1&&notifications==0,"clicking loaded stock causes no clear redraw or request");
        var busyFlashes=0;vm.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(vm.Busy))busyFlashes++;};Do("관심등록");Check(busyFlashes==0,"synchronous buttons never flash whole-screen busy state");
        vm.From=new DateTime(2025,1,2);vm.To=new DateTime(2025,1,10);vm.SetChartRange(new DateTimeOffset(2025,1,20,12,0,0,TimeSpan.FromHours(9)),true);Check(vm.From==vm.To&&vm.To==new DateTime(2025,1,10),"chart start after end clamps start to end");vm.SetChartRange(new DateTimeOffset(2025,1,1,12,0,0,TimeSpan.FromHours(9)),false);Check(vm.To==vm.From&&vm.From==new DateTime(2025,1,10),"chart end before start clamps end to start");
        vm.To=new DateTime(2026,9,29);vm.From=new DateTime(2025,1,1);vm.DailySelected=true;Do("연습시작");Do("매수");Do("다음");vm.ShowTradeMarkers=true;Check(vm.TradeMarkers.Count==1&&vm.TradeMarkers[0].Source=="모의매매","paper chart shows confirmed fill only after checkbox");
        var paperMarkerToggle=Descendants(window).OfType<CheckBox>().Single(c=>Equals(c.Content,"모의매매 내역 표시"));paperMarkerToggle.IsChecked=false;Check(!vm.ShowTradeMarkers&&vm.TradeMarkers.Count==0,"paper marker checkbox hides mock fills");paperMarkerToggle.IsChecked=true;Check(vm.ShowTradeMarkers&&vm.TradeMarkers.Count==1,"paper marker checkbox restores mock fills");
        var backtest=new SavedBacktest{Id="chart-run:marker-test",At=DateTimeOffset.UtcNow,Stock=vm.SelectedStock!,Period="테스트",Fills=[new(vm.ChartBars[^1].Time,"매수",2,vm.ChartBars[^1].Close,0,0,"표식 검증")]};vm.SelectedRun=backtest;
        var backtestMarkerToggle=Descendants(window).OfType<CheckBox>().Single(c=>Equals(c.Content,"백테스트 매매 내역 표시"));backtestMarkerToggle.IsChecked=true;Check(vm.ShowBacktestTradeMarkers&&vm.DisplayTradeMarkers.Count==2&&vm.DisplayTradeMarkers.Any(m=>m.Source=="백테스트 체결"),"backtest marker checkbox adds selected run without hiding mock fills");backtestMarkerToggle.IsChecked=false;Check(!vm.ShowBacktestTradeMarkers&&vm.DisplayTradeMarkers.Count==1&&vm.DisplayTradeMarkers[0].Source=="모의매매","backtest marker toggle is independent from mock fills");backtestMarkerToggle.IsChecked=true;
        var chart=(CandleChart)window.FindName("PriceChart");window.UpdateLayout();chart.OpenRangeMenu(new Point(chart.ActualWidth/2,100));Check(chart.ContextMenu?.Items.Count==2,"right-click menu exposes start and end period actions");var startMenu=(MenuItem)chart.ContextMenu!.Items[0];startMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Check(vm.From>=new DateTime(2024,1,1)&&vm.From<=vm.To,"chart context menu is wired to backtest range");chart.ContextMenu.IsOpen=false;
        using(var restored=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db)){restored.SelectedStock=vm.SelectedStock;Check(restored.ShowTradeMarkers&&restored.ShowBacktestTradeMarkers&&restored.TradeMarkers.Count==1,"paper fills and independent display preferences survive reopening");}
        vm.ActiveTab=0;window.UpdateLayout();var combo=Descendants(window).OfType<ComboBox>().First(c=>ReferenceEquals(c.ItemsSource,vm.Regions));combo.IsDropDownOpen=true;window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);Check(combo.IsDropDownOpen&&combo.ItemContainerGenerator.ContainerFromIndex(0) is ComboBoxItem,"styled combo opens selectable item containers");combo.IsDropDownOpen=false;
        void SettleScreeningLayout(){window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();}
        vm.ActiveTab=1;SettleScreeningLayout();var sourceRow=(FrameworkElement)window.FindName("ScreenSourceRow");vm.ScreenWatchlist=true;SettleScreeningLayout();var groupScroller=Descendants(sourceRow).OfType<ScrollViewer>().Single();Check(Math.Abs(sourceRow.Height-44)<.1&&Math.Abs(sourceRow.ActualHeight-44)<.1&&Math.Abs(groupScroller.Height-44)<.1,$"watchlist groups keep a fixed 44px source row and never push screening controls downward (row {sourceRow.ActualHeight:0.0}, scroller {groupScroller.ActualHeight:0.0})");
        var directButtons=Descendants(window).OfType<Button>().Where(b=>b.TemplatedParent==null).ToArray();var directTextBoxes=Descendants(window).OfType<TextBox>().Where(t=>t.TemplatedParent==null).ToArray();Check(directButtons.All(b=>b.VerticalContentAlignment==VerticalAlignment.Center)&&directTextBoxes.All(t=>t.VerticalContentAlignment==VerticalAlignment.Center),"all rendered application buttons and text boxes center their content vertically");
        window.DataContext=previous;
    }
}
