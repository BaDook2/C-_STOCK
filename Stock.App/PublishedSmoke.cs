using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;
internal static class PublishedSmoke
{
    internal static async Task Run(Application app,string output,bool publicData=false)
    {
        Directory.CreateDirectory(output);
        try
        {
            using var store=new TradingStore(Path.Combine(output,"workspace.db"));using var vm=new WorkspaceViewModel(_=>false,provider:publicData?new PublicMarketData():new FixtureMarketData(),store:store);
            var window=new MainWindow(vm){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0};window.Show();
            async Task Do(string name){vm.Actions[name].Execute(null);await Until(()=>!vm.Busy);}
            await Do("검색");if(vm.SearchResults.Count==0)throw new InvalidOperationException("Korean name search failed: "+vm.Status);await vm.OpenStock(vm.SearchResults[0]);if(vm.ChartBars.Count<80)throw new InvalidOperationException("Domestic chart failed: "+vm.Status);
            var domesticDataInfo=vm.DataInfo;var domesticLastBar=vm.ChartBars[^1].Time;await Do("관심등록");vm.ShowCloud=true;await Screenshot(window,Path.Combine(output,"domestic-chart.png"));
            await Do("목록");await Do("스크리닝");var domesticScreen=vm.ScreenInfo;var domesticMatches=vm.Matches.Count;vm.ActiveTab=1;await Screenshot(window,Path.Combine(output,"domestic-screening.png"));vm.ActiveTab=0;
            vm.From=new DateTime(2025,1,1);await Do("백테스트");if(!vm.Status.Contains("완료"))throw new InvalidOperationException("Backtest failed: "+vm.Status);var domesticTrades=vm.Trades.Count;
            vm.Region="해외";vm.Query="애플";await Do("검색");await vm.OpenStock(vm.SearchResults[0]);if(vm.ChartBars.Count<80)throw new InvalidOperationException("US chart failed: "+vm.Status);await Screenshot(window,Path.Combine(output,"overseas-chart.png"));
            var overseasDataInfo=vm.DataInfo;var overseasLastBar=vm.ChartBars[^1].Time;await Do("관심등록");await Do("백테스트");var foreignTrades=vm.Trades.Count;await Screenshot(window,Path.Combine(output,"backtest-result-inline.png"));
            await Do("목록");await Do("스크리닝");var overseasScreen=vm.ScreenInfo;var overseasMatches=vm.Matches.Count;vm.ActiveTab=1;await Screenshot(window,Path.Combine(output,"overseas-screening.png"));vm.ActiveTab=0;
            await Do("연습시작");var before=vm.ChartBars.Count;await Do("매수");await Do("다음");if(vm.ChartBars.Count!=before+1)throw new InvalidOperationException("Replay failed");
            await Screenshot(window,Path.Combine(output,"practice.png"));
            vm.ChartPeriod="주봉";await Screenshot(window,Path.Combine(output,"weekly.png"));vm.ChartPeriod="월봉";await Screenshot(window,Path.Combine(output,"monthly.png"));vm.ChartPeriod="일봉";
            vm.ActiveTab=5;await Until(()=>vm.Kospi!=null&&vm.Kosdaq!=null&&vm.StockQuote!=null&&vm.VisibleIndices.All(c=>c.HasChart));await Screenshot(window,Path.Combine(output,"market-overview.png"));
            vm.OverseasMarket=true;await Until(()=>vm.VisibleIndices.All(c=>c.HasChart));await Screenshot(window,Path.Combine(output,"overseas-indices.png"));vm.DomesticMarket=true;
            vm.ActiveTab=6;await Until(()=>vm.WatchItems!.All(e=>e.Price!="—"));await Screenshot(window,Path.Combine(output,"watchlist.png"));
            vm.Region="국내";vm.Query="삼성전자";await Do("검색");vm.RegistrationGroup=vm.WatchGroups[1];await Do("관심등록");vm.ScreenWatchlist=true;foreach(var g in vm.WatchGroups)g.IncludeInScreening=true;await Do("스크리닝");var watchScreen=vm.ScreenInfo;if(!watchScreen.Contains("중복 1개 제외"))throw new InvalidOperationException("Watch screening dedup failed: "+watchScreen);vm.ActiveTab=1;await Screenshot(window,Path.Combine(output,"watch-screening.png"));
            vm.ActiveTab=0;vm.Region="해외";vm.Query="애플";await Do("검색");await vm.OpenStock(vm.SearchResults[0]);await Do("정기매수초기화");vm.From=new DateTime(2025,1,1);vm.To=DateTime.Today;await Do("정기매수실행");
            if(vm.RecurringCards.Count!=6||vm.ChartEquity.Count==0)throw new InvalidOperationException("Recurring backtest failed: "+vm.Status);
            var recurringSessions=vm.ChartEquity.Count;await Screenshot(window,Path.Combine(output,"recurring-purchases.png"));
            vm.ActiveTab=2;vm.StrategyChoice="Pipeline";await Do("파이프라인Mock");
            if(!vm.OversoldUsageGroups.Select(s=>s.Count).SequenceEqual(new[]{8,51,0}))throw new InvalidOperationException("Parameter usage scope grouping failed");
            window.UpdateLayout();await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            foreach(var expander in Find(window).OfType<Expander>().Where(e=>e.DataContext is ParameterGroup g&&g.Name is "분할매수" or "모형 계좌 · 체결 비용").ToArray())expander.IsExpanded=true;
            window.UpdateLayout();await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
            var ratioBox=Find(window).OfType<TextBox>().Single(b=>b.DataContext is ParameterField f&&f.Metadata.Key=="EntryRatios");
            var feeField=vm.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="CommissionPercent");var originalFee=feeField.Value;
            var feeBox=Find(window).OfType<TextBox>().Single(b=>ReferenceEquals(b.DataContext,feeField));feeBox.Focus();System.Windows.Input.Keyboard.ClearFocus();
            var fixedEditorVerified=ratioBox.Text=="40.00, 30.00, 30.00"&&feeBox.Text=="0.02"&&feeField.Value==originalFee&&vm.OversoldGroups.Sum(g=>g.Rows!.Count)==26;
            if(!fixedEditorVerified)throw new InvalidOperationException("Published fixed decimal input or explicit rows failed");
            var boundary=FixedDecimalEdit.Type("1.00",1,0,"2",ParameterInputKind.Decimal);var integerInsertionVerified=boundary.Text=="12.00"&&boundary.Caret==2;if(!integerInsertionVerified)throw new InvalidOperationException("Integer boundary insertion failed");
            var screeningSnapshotVerified=vm.LatestScreening!=null&&store.State<OversoldScreeningRun>(vm.LatestScreening.Id)!=null&&vm.LatestScreening.Funnel().All(f=>f.Total==vm.LatestScreening.Targets.Length);
            if(!screeningSnapshotVerified)throw new InvalidOperationException("Screening snapshot failed");
            var screeningDialog=new ScreeningResultDialog(()=>vm.LatestScreening,vm.ScreeningTradeDetails){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};screeningDialog.Show();await Screenshot(screeningDialog,Path.Combine(output,"screening-result-published.png"));screeningDialog.Close();
            ratioBox.BringIntoView();await Screenshot(window,Path.Combine(output,"fixed-editor-published.png"));
            if(vm.StrategyChoice!="Oversold"||vm.Actions["파이프라인Mock"].CanExecute(null)||!vm.CanOpenMulti||vm.MultiTrading.RunCaption!="종목별 모의 백테스트")throw new InvalidOperationException("Retired strategy and offline mock boundary failed");
            await Screenshot(window,Path.Combine(output,"only-oversold-settings.png"));vm.UseConfiguredStrategy=false;
            var connection=new ConnectionDialog(){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0};connection.Show();if(Find(connection).OfType<RadioButton>().Count(b=>b.GroupName=="Brokerage")!=4)throw new InvalidOperationException("Broker choice layout failed");await Screenshot(connection,Path.Combine(output,"broker-selection.png"));connection.Close();
            var root=(FrameworkElement)window.Content;var tabs=Find(root).OfType<TabControl>().First();for(int n=0;n<tabs.Items.Count;n++){tabs.SelectedIndex=n;await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);await Screenshot(window,Path.Combine(output,$"workspace-{n}.png"));}
            await Until(()=>vm.RawTicks is {Busy:false});var raw=vm.RawTicks!;bool rawNativeRendered=false;
            if(!publicData)
            {
                var instrument=new Instrument(vm.SelectedStock!.Symbol,vm.SelectedStock.Exchange);var start=new DateTimeOffset(2026,9,29,9,0,0,TimeSpan.FromHours(9));
                var fixture=Enumerable.Range(0,2000).Select(i=>new MarketEvent(instrument,EventKind.Trade,start.AddMilliseconds(i*17),start.AddMilliseconds(i*17),"TEST_FIXTURE",i){TradePrice=100+i%31*.01m,TradeSize=1}).ToArray();
                await RawTickIngestion.Import(store,new HistoricalMarketDataProvider(fixture),default);await raw.LoadSources();await raw.Reset();
                await Screenshot(window,Path.Combine(output,"raw-tick-native.png"));var rawView=Find(root).OfType<RawTickChartView>().First();
                rawNativeRendered=raw.Rows.Length==2000&&rawView.Chart.Plot.RenderManager.RenderCount>0&&rawView.Chart.Plot.GetPlottables().OfType<ScottPlot.Plottables.SignalXY>().Any();
                if(!rawNativeRendered)throw new InvalidOperationException("Published native ScottPlot render failed");
            }
            else if(raw.Rows.Length!=0)throw new InvalidOperationException("Public smoke must not fabricate raw ticks");
            var dailyTrades=publicData?0:await DailySmoke(window,store,output);
            File.WriteAllText(Path.Combine(output,"result.json"),Serialization.Pack(new{Success=true,OversoldTrades=dailyTrades,PublicData=publicData,Executable=Environment.ProcessPath,WorkingDirectory=Environment.CurrentDirectory,DomesticDataInfo=domesticDataInfo,DomesticLastBar=domesticLastBar,OverseasDataInfo=overseasDataInfo,OverseasLastBar=overseasLastBar,DomesticTrades=domesticTrades,ForeignTrades=foreignTrades,DomesticScreen=domesticScreen,DomesticMatches=domesticMatches,OverseasScreen=overseasScreen,OverseasMatches=overseasMatches,WatchScreen=watchScreen,RecurringSessions=recurringSessions,OnlyOversold=vm.StrategyChoice=="Oversold",FixedEditorVerified=fixedEditorVerified,IntegerInsertionVerified=integerInsertionVerified,ScreeningSnapshotVerified=screeningSnapshotVerified,ParameterUsageScopes=vm.OversoldUsageGroups.Select(s=>new{s.Metadata.Name,s.Count}).ToArray(),LegacyCommandsBlocked=!vm.Actions["틱백테스트"].CanExecute(null),RawNativeRendered=rawNativeRendered,RawTicks=raw.Rows.Length,BrokerTilesRendered=true,NoBrokerCalls=true}));window.Close();app.Shutdown(0);
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.json"),Serialization.Pack(new{Success=false,ErrorType=ex.GetType().Name,Message=ex.Message}));app.Shutdown(1);}
    }
    private static async Task<int> DailySmoke(MainWindow window,TradingStore store,string output)
    {
        var provider=new FixtureMarketData();var stock=(await provider.Search("삼성전자","국내",default))[0];var data=await provider.History(stock,default);
        DateTimeOffset Open(PriceBar b)=>new(b.Time.ToOffset(TimeSpan.FromHours(9)).Date.AddHours(9),TimeSpan.FromHours(9));
        var facts=new HistoricalFacts{Source="TEST_FIXTURE synthetic facts/calendar; never ordinary app data",MarketSessions=data.Bars.Select(b=>new DailyMarketSession(DailyDates.Of(b),Open(b),b.Time)).ToArray(),StatusRecords=data.Bars.Select(b=>new DailySecurityStatus(stock.Symbol,DailyDates.Of(b),Open(b).AddMinutes(-1),HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false)).ToArray(),TradingValues=data.Bars.Select(b=>new DailyTradingValue(stock.Symbol,DailyDates.Of(b),b.Time,4_000_000_000m)).ToArray(),RiskRecords=data.Bars.Select(b=>new FundamentalRiskRecord(stock.Symbol,DailyDates.Of(b),false,Open(b).AddMinutes(-1))).ToArray()};
        store.State("oversold-config-v1",new OversoldConfig{UseRsiFilter=false,UseVolumeFilter=false,MovingAveragePeriods="1",MinimumSupportCount=1,UsePivotSupport=false,UseApproximateVolumeZone=false,MinimumDrawdownPercent=-.001m,ShortCrashPercent=-.001m,MaxHoldingDays=3,MaxEntries=1,EntryRatios="100"});store.State("oversold-facts-v1",facts);
        using var vm=new WorkspaceViewModel(_=>false,provider:provider,store:store);var old=window.DataContext;window.DataContext=vm;vm.Actions["검색"].Execute(null);await Until(()=>!vm.Busy);vm.From=data.Bars[30].Time.ToOffset(TimeSpan.FromHours(9)).Date;vm.To=data.Bars[70].Time.ToOffset(TimeSpan.FromHours(9)).Date;vm.Actions["낙주백테스트"].Execute(null);await Until(()=>!vm.Busy);
        var result=vm.SelectedRun?.OversoldResultId is {} id?store.State<OversoldResult>(id):null;if(result==null||result.Trades.Count==0||vm.OversoldCards.Count!=6)throw new InvalidOperationException("Published oversold UI backtest failed: "+vm.Status);
        await Screenshot(window,Path.Combine(output,"oversold-published.png"));var details=new OversoldResultDialog(result){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};details.Show();await Screenshot(details,Path.Combine(output,"oversold-published-details.png"));details.Close();window.DataContext=old;return result.Trades.Count;
    }
    internal static async Task Screenshot(Window window,string path){await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();await window.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);var root=(FrameworkElement)window.Content;root.UpdateLayout();var drawing=new DrawingVisual();using(var context=drawing.RenderOpen())context.DrawRectangle(new VisualBrush(root),null,new Rect(0,0,root.ActualWidth,root.ActualHeight));var image=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);encoder.Save(file);}
    private static async Task Until(Func<bool> done){var until=DateTime.UtcNow.AddMinutes(2);while(!done()){if(DateTime.UtcNow>until)throw new TimeoutException();await Task.Delay(30);}}
    private static IEnumerable<DependencyObject> Find(DependencyObject p){for(var n=0;n<VisualTreeHelper.GetChildrenCount(p);n++){var c=VisualTreeHelper.GetChild(p,n);yield return c;foreach(var v in Find(c))yield return v;}}
}
// Test-only fixture. The ordinary application never uses generated prices.
public sealed class FixtureMarketData : IPublicMarketData,IMarketOverviewData,IIndexChartData
{
    public async Task<IndexTrend> IndexHistory(string code,CancellationToken token){var quote=await Index(code,token);var start=DateTimeOffset.UtcNow.AddMinutes(-150);return new(quote,quote.Price-10,Enumerable.Range(0,31).Select(i=>new IndexPoint(start.AddMinutes(i*5),quote.Price-10+i/3m+(decimal)Math.Sin(i/3.0)*3)).ToArray());}
    public Task<MarketQuote> Index(string code,CancellationToken token)=>Task.FromResult(new MarketQuote(code=="KOSPI"?"코스피 · 검증 자료":"코스닥 · 검증 자료",code=="KOSPI"?2500:800,10,1,DateTimeOffset.UtcNow,"OPEN"));
    public Task<MarketQuote> Quote(StockItem stock,CancellationToken token)=>Task.FromResult(new MarketQuote(stock.Name+" · 검증 자료",150,2,1,DateTimeOffset.UtcNow,"OPEN"));
    public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([region=="해외"?new("AAPL","애플",Exchange.NASDAQ,"AAPL.O","전화 및 소형 장치"):new("005930","삼성전자",Exchange.KRX,"005930","반도체와반도체장비")]);
    public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>Search("",region,token);
    public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>Task.FromResult(stock);
    public Task<BarDataset> History(StockItem stock,CancellationToken token)
    {
        var bars=Enumerable.Range(0,600).Select(i=>{var close=(100m+i*.1m+(decimal)Math.Sin(i/12.0)*8)*(stock.Currency=="KRW"?100:1);return new PriceBar(new DateTimeOffset(2024,1,1,15,30,0,TimeSpan.FromHours(9)).AddDays(i),close-1,close+2,close-2,close,1000000);}).ToArray();return Task.FromResult(new BarDataset(stock,bars,"TEST_FIXTURE (합성 검증 전용)",DateTimeOffset.UtcNow));
    }
}
