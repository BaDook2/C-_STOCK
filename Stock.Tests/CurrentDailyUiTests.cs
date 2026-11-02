using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private sealed class CurrentUiFixture:IPublicMarketData,ICurrentMarketData
    {
        public readonly DateTimeOffset Now=DateTimeOffset.UtcNow;
        public int HistoryCalls,QuoteCalls;
        public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([DailyStock]);
        public Task<IReadOnlyList<StockItem>> Universe(string region,int perExchange,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([DailyStock]);
        public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>Task.FromResult(stock);
        public Task<BarDataset> History(StockItem stock,CancellationToken token)
        {
            HistoryCalls++;var date=Now.ToOffset(TimeSpan.FromHours(9)).Date;
            return Task.FromResult(new BarDataset(stock,Enumerable.Range(0,30).Select(i=>new PriceBar(new DateTimeOffset(date.AddDays(i-29).AddHours(15.5),TimeSpan.FromHours(9)),100,110,80,i==29?84:100,100,i!=29)).ToArray(),"TEST_FIXTURE",Now));
        }
        public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
        public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)
        {QuoteCalls++;return Task.FromResult(new CurrentMarketSnapshot(stock,new(stock.Name,85,-15,-15,Now,"OPEN"),Now,100,110,79,12345,100001,"TEST_FIXTURE · 공개 지연",TimeSpan.FromSeconds(3)));}
    }
    private static void CurrentDailyUiTests(MainWindow window,string output)
    {
        using var db=new TradingStore(Path.Combine(Dir,"current-daily-ui.db"));var cfg=DailyConfig();cfg.MinimumSupportCount=1;cfg.MinimumAverageTradingValue=0;cfg.MovingAveragePeriods="1";cfg.UsePivotSupport=cfg.UseApproximateVolumeZone=false;cfg.UsePriceOnlyBacktest=true;db.State("oversold-config-v1",cfg);
        var provider=new CurrentUiFixture();using var vm=new WorkspaceViewModel(_=>false,provider:provider,store:db);var previous=window.DataContext;window.DataContext=vm;vm.ActiveTab=0;
        void Do(string key){vm.Actions[key].Execute(null);PumpUntil(()=>!vm.Busy);window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
        Do("검색");PumpUntil(()=>vm.ChartBars.LastOrDefault()?.Volume==12345);window.UpdateLayout();
        Check(vm.ChartBars[^1] is {Close:85,Low:79,Volume:12345,IsComplete:false}&&vm.PriceText.Contains("85")&&vm.ChangeText.Contains("15"),"one-second WPF timer updates selected price, daily candle and cumulative volume together");
        var liveDailyChange=vm.ChangeText;
        vm.WeeklySelected=true;Check(vm.ChartBars[^1].Close==85&&vm.PriceText.Contains("85")&&vm.ChangeText==liveDailyChange,"weekly view keeps the live header change anchored to the previous daily close");
        vm.MonthlySelected=true;Check(vm.ChartBars[^1].Close==85&&vm.ChangeText==liveDailyChange,"monthly view keeps the same previous-close change instead of comparing monthly candles");vm.DailySelected=true;
        Check(vm.PriceDate.Contains("시세")&&vm.PriceDate.Contains("진행 중")&&vm.DataInfo.Contains("조회 3초")&&!vm.PriceDate.Contains("15:30"),"header distinguishes quote observation from fixed daily close axis and shows the offline three-second cadence");
        Check(provider.HistoryCalls==1&&provider.QuoteCalls==1,"live display refresh never downloads full history each second");
        var quoteDate=DateOnly.FromDateTime(provider.Now.ToOffset(TimeSpan.FromHours(9)).Date);vm.From=quoteDate.AddDays(-20).ToDateTime(TimeOnly.MinValue);vm.To=quoteDate.AddDays(-10).ToDateTime(TimeOnly.MinValue);Do("낙주백테스트");
        typeof(WorkspaceViewModel).GetMethod("RefreshDailyDisplay",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(vm,null);
        Check(vm.ShowBacktestTradeMarkers&&vm.ChartBars.Count>0&&vm.ChartBars.All(b=>DailyDates.Of(b)>=quoteDate.AddDays(-20)&&DailyDates.Of(b)<=quoteDate.AddDays(-10)),"one-second quote refresh preserves the completed backtest chart interval and visible trade-marker scale");
        vm.ShowBacktestTradeMarkers=false;typeof(WorkspaceViewModel).GetMethod("RefreshDailyDisplay",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(vm,null);
        Check(vm.ChartBars.Any(b=>DailyDates.Of(b)>quoteDate.AddDays(-10)),"hiding backtest markers restores the ordinary live daily horizon");
        vm.ShowBacktestTradeMarkers=true;
        Check(vm.ChartBars.Count>0&&vm.ChartBars.All(b=>DailyDates.Of(b)>=quoteDate.AddDays(-20)&&DailyDates.Of(b)<=quoteDate.AddDays(-10)),"re-enabling backtest markers restores its tested interval and visible annotations");
        var chart=Descendants(window).OfType<CandleChart>().Single();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        typeof(CandleChart).GetField("visible",flags)!.SetValue(chart,27);typeof(CandleChart).GetField("offset",flags)!.SetValue(chart,2);
        var existingBars=chart.Bars!; chart.SetCurrentValue(CandleChart.BarsProperty,existingBars.Append(existingBars[^1] with{Time=existingBars[^1].Time.AddDays(1)}).ToArray());
        Check((int)typeof(CandleChart).GetField("visible",flags)!.GetValue(chart)! ==27&&(int)typeof(CandleChart).GetField("offset",flags)!.GetValue(chart)! ==3,"appending a candle preserves zoom and anchored panned viewport");
        chart.GetBindingExpression(CandleChart.BarsProperty)!.UpdateTarget();
        vm.OversoldFactsJson="invalid draft, not applied";Do("관심등록");vm.ScreenWatchlist=true;foreach(var g in vm.WatchGroups)g.IncludeInScreening=true;Do("스크리닝");
        Check(vm.LatestScreening?.Results.Single().Evaluation!=null&&vm.Matches.Count==1,"current daily UI screens without facts JSON, even with unapplied invalid historical draft");
        var row=vm.LatestScreening!.Results.Single();Check(row.Candidate&&!row.Evaluation!.Enter&&row.Evaluation.Conditions.Single(x=>x.Condition=="현재 당일 거래대금").Value=="100,001","UI retains exact current turnover as diagnostic, not prior average or order approval");
        vm.ActiveTab=0;window.UpdateLayout();Render((FrameworkElement)window.Content,Path.Combine(output,"current-daily-chart.png"));
        var original=vm.ChartBars.ToArray();vm.Region="해외";Check(vm.ChartBars.Count==0,"changing selected market cancels and clears live daily display");
        typeof(WorkspaceViewModel).GetMethod("RefreshDailyDisplay",flags)!.Invoke(vm,null);Check(vm.ChartBars.Count==0,"late display tick cannot restore a deselected symbol");
        window.DataContext=previous;
    }
}



