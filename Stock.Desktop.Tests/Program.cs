using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;

internal static partial class Program
{
    private static int passed;
    private static string output="";
    [STAThread]private static int Main(string[] args)
    {
        try
        {
            output=Path.GetFullPath(args.FirstOrDefault()??"artifacts/desktop-tests");Directory.CreateDirectory(output);
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
            if(args.Contains("--design")){DesignCapture();return 0;}
            if(args.Contains("--live-public")){LivePublicChecks();return 0;}
            if(args.Contains("--workflow")){WorkflowChecks();Console.WriteLine($"PASS: {passed} extended workflow checks");return 0;}
            if(args.Contains("--states")){InteractiveStateChecks();Console.WriteLine($"PASS: {passed} interactive control-state checks");return 0;}
            if(args.Contains("--chart-interaction")){ChartInteractionChecks();Console.WriteLine($"PASS: {passed} chart interaction checks");return 0;}
            if(args.Contains("--calendar")){CalendarInteractionChecks();Console.WriteLine($"PASS: {passed} calendar interaction checks");return 0;}
            if(args.Contains("--online")){OnlineDesktopChecks();Console.WriteLine($"PASS: {passed} online UI checks; fake broker only");return 0;}
            if(args.Contains("--credentials")){CredentialChecks();Console.WriteLine($"PASS: {passed} credential checks; dummy keys only");return 0;}
            if(args.Contains("--account-orders")){AccountOrderChecks();Console.WriteLine($"PASS: {passed} account order checks; fake broker only");return 0;}
            if(args.Contains("--live-adapter")){LiveStrategyAdapterChecks();Console.WriteLine($"PASS: {passed} live strategy adapter checks; fake feed/account only");return 0;}
            if(args.Contains("--daily-checkpoint")){DailyPaperCheckpointChecks();Console.WriteLine($"PASS: {passed} daily paper checkpoint checks; synthetic observations only");return 0;}
            if(args.Contains("--transmission-guard")){OrderTransmissionGuardChecks();Console.WriteLine($"PASS: {passed} final transmission guard checks");return 0;}
            if(args.Contains("--daily-host")){DailyPaperHostChecks();Console.WriteLine($"PASS: {passed} daily host checks; synthetic provider only");return 0;}
            if(args.Contains("--annotations")){ChartAndHistoryChecks();Console.WriteLine($"PASS: {passed} annotation/history UI checks");return 0;}
            if(args.Contains("--outcome-colors")){TradeOutcomeColorChecks();Console.WriteLine($"PASS: {passed} trade outcome color checks");return 0;}
            MarketChecks();DayTradingDesktopChecks();OnlineDesktopChecks();RealtimeDesktopChecks();WorkflowChecks();ChartAndHistoryChecks();InteractiveStateChecks();ChartInteractionChecks();CalendarInteractionChecks();TradeOutcomeColorChecks();CredentialChecks();AccountOrderChecks();LiveStrategyAdapterChecks();DailyPaperCheckpointChecks();OrderTransmissionGuardChecks();DailyPaperHostChecks();Console.WriteLine($"PASS: {passed} Avalonia headless checks; fixtures only, no real broker calls");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void Check(bool condition,string message){if(!condition)throw new Exception("FAIL: "+message);passed++;Console.WriteLine("PASS: "+message);}
    private static void Pump(Func<bool> done,int seconds=12)
    {
        var timer=Stopwatch.StartNew();while(!done()){if(timer.Elapsed.TotalSeconds>seconds)throw new TimeoutException("Headless operation timed out");Dispatcher.UIThread.RunJobs();Thread.Sleep(5);}Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
    private static void MarketChecks()
    {
        var market=new MarketFixture();using var services=new AppServices(Path.Combine(output,"isolated-data",Guid.NewGuid().ToString("N")),market);using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};window.Show();Dispatcher.UIThread.RunJobs();
        Check(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Content?.ToString()=="이름으로 검색"),"real Avalonia AXAML loads styled search controls");
        model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.ChartBars.Length>0);var chart=window.GetVisualDescendants().OfType<StockChart>().Single();
        Check(model.SelectedStock?.Symbol=="005930"&&chart.RenderedCandles>0,"Korean search loads array-based ScottPlot candles without WPF");
        var dailyCount=model.ChartBars.Length;var expectedMonths=ChartPeriods.Aggregate(model.ChartBars,"월봉").Length;Check(model.ChartDisplayCount<dailyCount,"selected period limits the initial visible window while preserving older chart history");model.ChartPeriod="주봉";Pump(()=>model.ChartBars.Length<dailyCount);Check(chart.RenderedCandles==model.ChartBars.Length,"week selection aggregates the existing daily data through the shared core");
        model.ChartPeriod="월봉";Pump(()=>model.ChartBars.Length==expectedMonths);Check(model.ChangeText.Contains("1.00%"),"quote percent keeps two decimal places independently from the selected candle period");
        model.ChartPeriod="일봉";var first=model.PriceText;Pump(()=>model.PriceText!=first,4);Check(market.CurrentCalls>=2,"one-second selected-quote pump updates Avalonia binding and chart without broker API");
        using(var image=window.CaptureRenderedFrame())image!.Save(Path.Combine(output,"avalonia-chart.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        var fee=model.Strategies.Groups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="CommissionPercent");
        Check(fee.Text=="0.015"&&model.Strategies.Groups.Sum(g=>g.Fields.Length)==61,"legacy Avalonia editor preserves all61 fields and exact fee precision");
        model.Strategies.Selected=StrategyDiagnosticPreset.HoldOneDayName;model.Research.From=DateTime.Today.AddDays(-20);model.Research.To=DateTime.Today.AddDays(-1);model.Research.Run.Execute(null);Pump(()=>!model.Research.Busy,25);Check(model.Research.Result!=null,"research command returns a result: "+model.Research.Status);
        Check(model.Research.Result!.Result.Trades.Count>0&&model.ChartMarkers.Any(m=>m.Buy)&&model.ChartMarkers.Any(m=>!m.Buy),"original legacy backtest service produces real calculated fills and main-chart BUY/SELL markers in Avalonia");
        var tradePanel=new ResearchResultPanel{DataContext=model.Research.Result};var tradeWindow=new Window{Width=1250,Height=900,Content=tradePanel};tradeWindow.Show(window);Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();var tradeChart=tradePanel.FindControl<StockChart>("Chart")!;var badge=tradeChart.FindControl<Canvas>("TradeLayer")!.Children.OfType<Border>().First();var badgeTrade=((ChartMarkerGroup)badge.Tag!).Trades[0];ClickAt(tradeWindow,badge,.5);Check(model.Research.Result.Selected?.Fill.ExecutionTime==badgeTrade.Time&&ReferenceEquals(tradePanel.FindControl<DataGrid>("Trades")!.SelectedItem,model.Research.Result.Selected),"one chart-caption click selects the same execution in the backtest detail grid");tradeWindow.Close();
        var navigationTasks=new[]{new TaskCompletionSource<ResearchResultViewModel>(TaskCreationOptions.RunContinuationsAsynchronously),new TaskCompletionSource<ResearchResultViewModel>(TaskCreationOptions.RunContinuationsAsynchronously)};var stockA=model.SelectedStock!;var stockB=stockA with{Symbol="000660"};var viewA=model.Research.Result;var viewB=new ResearchResultViewModel(viewA.Result,viewA.Bars);using(var browser=new ResearchBrowserViewModel([stockA,stockB],(stock,_)=>navigationTasks[stock.Symbol==stockA.Symbol?0:1].Task,"snapshot")){browser.Initialize();var slow=browser.Go(0);var fast=browser.Go(1);navigationTasks[1].SetResult(viewB);Pump(()=>fast.IsCompleted);navigationTasks[0].SetResult(viewA);Pump(()=>slow.IsCompleted);Check(ReferenceEquals(browser.Current,viewB),"late result from a cancelled previous stock cannot replace the current popup result");}
        Check(model.History.Count>0&&services.Store.States("desktop-run:").Count>0,"test history stores the original result/configuration and period bars in the new isolated database");
        model.ShowBacktest=false;Check(model.ChartMarkers.Length==0,"main-chart backtest marker visibility is user-controlled");model.ShowBacktest=true;
        model.SelectedHistory=model.History[0];model.OpenHistoryCommand.Execute(null);Dispatcher.UIThread.RunJobs();Check(window.OwnedWindows.Any(w=>w.Content is ResearchResultPanel),"stored test opens a separate reusable result-and-chart popup");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
        model.ToggleWatch.Execute(null);Check(model.WatchStar.StartsWith("★")&&model.WatchGroups[0].Stocks.Count==1,"watch registration shares one stored group state and blue-star label");
        model.GroupDraft="장기 관심";model.RenameWatchGroup.Execute(null);Check(services.Store.State<WatchGroupRecord[]>("watch-groups-avalonia-v1")![0].Name=="장기 관심","watch group rename persists without changing the original WPF database");
        model.AddWatchGroup.Execute(null);model.SelectedWatchGroup!.IncludeInScreening=true;Check(services.Store.State<WatchGroupRecord[]>("watch-groups-avalonia-v1")!.Last().Screen,"new watch-group screening flag saves immediately");
        model.ToggleWatch.Execute(null);model.VirtualExecution.Group=model.WatchGroups[0];model.VirtualExecution.Refresh();model.VirtualExecution.Picks[0].Chosen=true;Check(model.VirtualExecution.Items.Count==1&&model.VirtualExecution.Items[0].Strategy==model.Strategies.Names[0],"virtual watchlist addition defaults to the first registered strategy without duplicating a symbol");model.VirtualExecution.Group=model.WatchGroups[^1];Check(model.VirtualExecution.Picks.Single().Chosen,"overlapping symbols in another watch group share the same selected checkbox state");model.VirtualExecution.Selected=model.VirtualExecution.Items[0];model.VirtualExecution.Remove.Execute(null);Check(model.VirtualExecution.Items.Count==0&&!model.VirtualExecution.Picks.Single().Chosen,"removing a virtual target clears its selection across watch groups");
        model.Recurring.From=DateTime.Today.AddDays(-90);model.Recurring.To=DateTime.Today.AddDays(-1);model.Recurring.Run.Execute(null);Pump(()=>window.OwnedWindows.Any(w=>w.Content is RecurringResultPanel),15);Check(services.Store.States("desktop-recurring:").Count==1,"recurring-purchase view uses the existing core and persists the exact result");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
        model.RefreshMarket.Execute(null);Pump(()=>!model.Busy&&model.MarketIndices.All(i=>i.Points.Length>0));Check(model.MarketIndices.Count==5,"market index cards use actual provider irregular-time series");
        market.TwoTargets=true;model.Screening.ShowAll=true;model.Screening.From=DateTime.Today.AddDays(-7);model.Screening.To=DateTime.Today.AddDays(-1);model.Screening.Start.Execute(null);Pump(()=>model.Screening.Snapshot?.Results.Length==1&&model.Screening.Busy,15);
        model.Screening.Selected=model.Screening.Rows[0];var selectedRow=model.Screening.Selected;Check(model.Screening.OpenResult.CanExecute(null),"completed screening stock can open its snapshot backtest before the entire run finishes");
        model.Screening.OpenResult.Execute(null);Pump(()=>window.OwnedWindows.Any(w=>w.Content is ResearchBrowserPanel),15);foreach(var owned in window.OwnedWindows.ToArray())owned.Close();market.ReleaseSecond.TrySetResult();Pump(()=>!model.Screening.Busy,15);
        Check(model.Screening.Snapshot?.Results.Length==2&&model.Screening.Funnel.Count==17&&ReferenceEquals(selectedRow,model.Screening.Selected),"shared screening evaluator publishes full funnel counts and preserves the selected row during incremental results");
        var notices=0;model.NotificationRequested+=_=>notices++;model.SelectedRange=model.Ranges[0];Dispatcher.UIThread.RunJobs();
        Check(notices==1&&model.SelectedRange.Months>0,"non-minute one-day selection is rejected with a separate notice request");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
        model.Region="해외";model.Query="애플";model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SelectedStock?.Symbol=="AAPL");Check(model.ChartMarkers.Length==0,"foreign Apple search uses the same provider and clears old backtest markers");
        model.EmergencyCommand.Execute(null);Check(model.Status.Contains("긴급중지"),"emergency cancels request and quote-pump work independently from normal commands");window.Close();
        Check(!Directory.EnumerateFiles(services.DataDirectory,"*.credentials.json",SearchOption.AllDirectories).Any(),"isolated desktop boot never imports or writes API credentials");
    }
    internal sealed class MarketFixture:IPublicMarketData,ICurrentMarketData,IIndexChartData
    {
        internal int CurrentCalls;
        internal int BasePrice=9000,QuoteBase=10100; internal bool Design;
        internal bool TwoTargets;
        internal Func<DateTimeOffset>? Clock;
        internal TaskCompletionSource? UniversePause;
        internal readonly TaskCompletionSource ReleaseSecond=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static readonly StockItem Samsung=new("005930","삼성전자",Exchange.KRX,"005930"){Board="KOSPI"};
        private static readonly StockItem Apple=new("AAPL","애플",Exchange.NASDAQ,"AAPL");
        public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([region=="해외"||region=="전체"&&(name=="애플"||name=="AAPL")?Apple:Samsung]);
        public async Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token){if(UniversePause!=null)await UniversePause.Task.WaitAsync(token);return TwoTargets?[Samsung,new("000660","SK하이닉스",Exchange.KRX,"000660"){Board="KOSPI"}]:[Samsung];}
        public Task<StockItem> Describe(StockItem item,CancellationToken token)=>Task.FromResult(item);
        public async Task<BarDataset> History(StockItem item,CancellationToken token)
        {
            if(item.Symbol=="000660")await ReleaseSecond.Task.WaitAsync(token);
            var last=DateOnly.FromDateTime((Clock?.Invoke().ToOffset(TimeSpan.FromHours(9)).DateTime??DateTime.Today).AddDays(-1));var bars=Enumerable.Range(0,600).Select(n=>{var close=BasePrice+n;return new PriceBar(ResearchMarketSession.For(last.AddDays(n-599),item.Exchange).Close,close,close+(Design?20:60),close-(Design?10:50),close+10,1000+n);}).ToArray();
            return new BarDataset(item,bars,"SYNTHETIC_UI_FIXTURE",DateTimeOffset.UtcNow);
        }
        public Task<CurrentMarketSnapshot> Current(StockItem item,CancellationToken token)
        {
            var count=Interlocked.Increment(ref CurrentCalls);var price=QuoteBase+(Design?1:count);var now=Clock?.Invoke()??DateTimeOffset.UtcNow;
            return Task.FromResult(new CurrentMarketSnapshot(item,new(item.Name,price,Design?12:100,Design?.11m:1,now,"OPEN"),now,Design?10600:10000,Design?10630:price,Design?10590:9900,50000,price*50000,"SYNTHETIC_UI_FIXTURE",TimeSpan.FromSeconds(1)));
        }
        public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
        public Task<IndexTrend> IndexHistory(string code,CancellationToken token)
        {var now=DateTimeOffset.UtcNow;return Task.FromResult(new IndexTrend(new(code,100,1,1,now,"FIXTURE"),99,[new(now.AddHours(-1),99),new(now,100)]));}
    }
}
