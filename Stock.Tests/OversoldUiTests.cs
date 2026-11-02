using System.IO;
using System.Windows;
using System.Windows.Controls;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private sealed class DailyFixture:IPublicMarketData
    {
        public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([DailyStock]);
        public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>Search("","",token);
        public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>Task.FromResult(stock);
        public Task<BarDataset> History(StockItem stock,CancellationToken token)=>Task.FromResult(new BarDataset(stock,[DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,80),DailyBar(4,80,high:100)],"TEST_FIXTURE",DailyTime(5)));
    }
    private static void OversoldUiTests(MainWindow window,string output)
    {
        using var db=new TradingStore(Path.Combine(Dir,"oversold-ui.db"));var cfg=DailyConfig();cfg.MovingAveragePeriods="1";cfg.MinimumSupportCount=1;cfg.UsePivotSupport=cfg.UseApproximateVolumeZone=false;db.State("oversold-config-v1",cfg);db.State("oversold-facts-v1",DailyFacts(5));
        using var vm=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db);var old=window.DataContext;window.DataContext=vm;
        vm.Actions["검색"].Execute(null);PumpUntil(()=>!vm.Busy);
        var dailyChange=vm.ChangeText;Check(dailyChange.Contains("0.00%"),"daily fixture header uses the immediately previous daily close");
        vm.WeeklySelected=true;Check(vm.ChangeText==dailyChange,"historical weekly view preserves the previous-daily-close header baseline");vm.MonthlySelected=true;Check(vm.ChangeText==dailyChange,"historical monthly view preserves the previous-daily-close header baseline");vm.DailySelected=true;
        vm.From=new(2025,1,3);vm.To=new(2025,1,5);vm.Actions["낙주백테스트"].Execute(null);PumpUntil(()=>!vm.Busy);
        Check(vm.History.Count==1&&vm.HasOversoldHistory&&vm.OversoldCards.Count==6&&vm.LatestResult?.Fills.Length==1,"daily backtest UI executes and saves actual result");
        var saved=db.State<OversoldResult>(vm.SelectedRun!.OversoldResultId!)!;Check(saved.Trades.Count==1&&vm.ResultInfo==vm.LatestResult!.Summary&&db.State<HistoricalFacts>("oversold-facts-snapshot:"+saved.FactsHash)!=null&&db.State<PriceBar[]>("oversold-bars-snapshot:"+saved.DataHash)!=null,"daily inline summary and detailed snapshot available");
        Check(vm.ShowBacktestTradeMarkers&&vm.DisplayTradeMarkers.Any(m=>m.Source=="백테스트 체결")&&vm.ChartBars.All(b=>DailyDates.Of(b)>=new DateOnly(2025,1,3)&&DailyDates.Of(b)<=new DateOnly(2025,1,5)),"completed backtest automatically shows its executions inside the tested chart range");
        var fee=vm.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key.EndsWith("CommissionPercent"));fee.Value="0.015";vm.Actions["낙주설정저장"].Execute(null);Check(db.State<OversoldConfig>("oversold-config-v1")!.CommissionPercent==.015m,"daily parameter save retains precise fee");
        using(var reopened=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db)){Check(reopened.History.Any(r=>r.OversoldResultId!=null)&&!reopened.HasOversoldHistory&&!reopened.Actions["낙주기록상세"].CanExecute(null)&&reopened.OversoldFactsJson.Contains("TEST_FIXTURE"),"daily saved result and point-in-time facts remain available without auto-selecting the old chart run after reopen");}
        var json=vm.OversoldFactsJson;vm.OversoldFactsJson="{";vm.Actions["낙주설정저장"].Execute(null);Check(vm.Status.Contains("JSON")&&db.State<HistoricalFacts>("oversold-facts-v1")!.MarketSessions.Length==5,"invalid JSON displays explanation without replacing saved facts");vm.OversoldFactsJson=json;vm.Actions["낙주설정저장"].Execute(null);
        var oldWidth=window.Width;var oldHeight=window.Height;
        foreach(var size in new[]{(1540d,1040d),(1160d,760d)}){window.Width=size.Item1;window.Height=size.Item2;vm.ActiveTab=0;vm.WorkspaceToolTab=2;window.UpdateLayout();var chart=(CandleChart)window.FindName("PriceChart");Check(chart.ActualHeight>200,$"daily tool preserves chart size at {size} (actual {chart.ActualHeight:N0})");Render((FrameworkElement)window.Content,Path.Combine(output,$"oversold-chart-{size.Item1}.png"));}
        vm.ActiveTab=2;window.UpdateLayout();var panel=Descendants(window).OfType<OversoldParameterPanel>().Single();panel.BringIntoView();var expander=Descendants(panel).OfType<Expander>().First();expander.IsExpanded=true;window.UpdateLayout();Descendants(panel).OfType<Expander>().First(e=>e.Header?.ToString()=="종목 선별 · 급락").IsExpanded=true;window.UpdateLayout();panel.BringIntoView();Render((FrameworkElement)window.Content,Path.Combine(output,"oversold-settings.png"));
        var popup=new OversoldResultDialog(saved){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};popup.Show();popup.UpdateLayout();Render((FrameworkElement)popup.Content,Path.Combine(output,"oversold-summary.png"));Descendants(popup).OfType<TabControl>().First().SelectedIndex=1;popup.UpdateLayout();Render((FrameworkElement)popup.Content,Path.Combine(output,"oversold-trade-detail.png"));popup.Close();
        window.DataContext=old;window.Width=oldWidth;window.Height=oldHeight;
    }
}
