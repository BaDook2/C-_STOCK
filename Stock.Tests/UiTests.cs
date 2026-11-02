using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Trading;
using Stock.Core;
using Stock.Core.Research;
using System.Net;
using System.Net.Http;
internal static partial class Program
{
    private static void UiSmoke(string output)
    {
        Directory.CreateDirectory(output);SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app=new Stock.App.App{ManualWindowStartup=true};app.InitializeComponent();using var store=new TradingStore(Path.Combine(Dir,"ui-v2.db"));var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:store);
        var window=new MainWindow(vm){ShowActivated=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=0};window.Show();
        void Do(string key){vm.Actions[key].Execute(null);PumpUntil(()=>!vm.Busy);}
        Do("검색");Check(vm.SelectedStock?.Name=="삼성전자","Korean name search selection");Do("데이터");Check(vm.ChartBars.Count==600,"chart uses provider bars");
        Check(vm.WatchGroups.Select(g=>g.Name).SequenceEqual(new[]{"그룹명1","그룹명2"}),"default watch groups use requested names");Do("관심등록");Do("관심등록");window.UpdateLayout();var filledStar=Descendants(window).OfType<TextBlock>().FirstOrDefault(t=>t.Text=="★");Check(vm.WatchGroups[0].Items.Count==1&&vm.IsWatchRegistered&&vm.WatchRegistrationStar=="★"&&filledStar?.Foreground is SolidColorBrush {Color.R:49,Color.G:130,Color.B:246},"watch registration deduplicates and renders a blue filled star");
        vm.RegistrationGroup=vm.WatchGroups[1];Check(!vm.IsWatchRegistered&&vm.WatchRegistrationStar=="☆","watch star follows the selected registration group");Do("관심등록");Check(vm.WatchGroups[1].Items.Count==1&&vm.IsWatchRegistered,"same stock can belong to separate groups");
        vm.WatchGroups[1].Edit.Execute(null);vm.GroupDraft="그룹명1";Do("그룹저장");Check(vm.EditingGroup&&vm.WatchGroups[1].Name=="그룹명2","duplicate group name rejected without changing name");vm.GroupDraft="장기 관심";Do("그룹저장");Check(!vm.EditingGroup&&vm.WatchGroups[1].Name=="장기 관심","inline group rename saved");
        vm.SelectedWatch=vm.WatchItems![0];Do("관심해제");Check(vm.WatchGroups[1].Items.Count==0&&vm.WatchGroups[0].Items.Count==1,"watch removal affects only selected group");
        using(var savedWatch=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:store)){Check(savedWatch.WatchGroups[0].Items[0].Name=="삼성전자"&&savedWatch.WatchGroups[1].Name=="장기 관심","watch stocks and renamed groups persist after reopening");}
        vm.SelectedWatchGroup=vm.WatchGroups[0];vm.SelectedWatch=vm.WatchItems![0];Do("관심차트");Check(vm.ActiveTab==0&&vm.ChartBars.Count==600,"watch entry opens matching chart");
        vm.Preset="UI 프리셋";Do("추가");Check(vm.Presets.Contains("UI 프리셋"),"strategy preset added");
        vm.From=new DateTime(2025,1,1);Do("백테스트");Check(vm.LatestResult?.OversoldResultId!=null&&vm.Status.Contains("완료"),"single daily strategy completes and records missing facts through UI");
        Do("연습시작");var count=vm.ChartBars.Count;Do("매수");Check(vm.Trades.Count==0,"manual practice does not fill same candle");Do("다음");Check(vm.Trades.Count==1&&vm.ChartBars.Count==count+1,"practice next-open fill and hidden future");
        vm.Region="해외";vm.Query="애플";Do("검색");Check(vm.SelectedStock?.Name=="애플"&&vm.ChartBars.Count==600,"search immediately loads first result");Do("데이터");Do("목록");Check(vm.Sectors.Contains("반도체와반도체장비"),"screening sector taxonomy is loaded from the selected domestic market rather than the stock-search region");Do("스크리닝");Check(vm.LatestScreening!=null&&vm.ScreenInfo.Contains("완료")&&vm.LatestScreening.Targets.All(s=>s.Exchange==Exchange.KRX),"screening evaluates only the selected domestic market even while the chart search is overseas");
        vm.LiveOrderLimitText="bad";Throws<InvalidOperationException>(()=>{_ = vm.LiveOrderLimit;},"invalid live limit cannot fall back to old value");vm.LiveOrderLimitText="500";vm.SelectedStock=vm.SelectedStock;Check(vm.LiveOrderLimit==500,"same-currency selection preserves custom order cap");
        if(vm.Matches.Count>0){vm.ActiveTab=1;vm.SelectedMatch=vm.Matches[0];Do("결과열기");var detail=Application.Current.Windows.OfType<ScreeningChartDialog>().Single();Check(vm.ActiveTab==1&&detail.Snapshot.Stock.Key==vm.SelectedMatch.Stock.Key,"screening selection opens a separate chart dialog");detail.Close();}
        vm.ShowCloud=true;vm.ShowMa=true;vm.IsOnline=true;PumpUntil(()=>!vm.Busy);Check(!vm.Actions["매수"].CanExecute(null),"online cannot order without account");Check(vm.IsOffline,"cancelled key dialog returns offline");
        Check(vm.Actions["키설정"].CanExecute(null),"key settings enabled offline");
        Check(vm.History.Count>0&&vm.LatestResult==null&&vm.SelectedRun==null,"saved history remains available while a later search clears the visible backtest run");
        vm.ChartPeriod="주봉";Check(vm.ChartBars.Count<600&&vm.ChartBars.Count>70,"weekly chart aggregates daily history");vm.ChartPeriod="월봉";Check(vm.ChartBars.Count<30,"monthly chart aggregates daily history");vm.ChartPeriod="틱";Check(vm.ChartPeriod=="월봉"&&!vm.CanUseIntraday,"offline blocks live-only intervals");vm.ChartPeriod="일봉";
        using(var reopened=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:store)){Check(reopened.History.Count==vm.History.Count&&reopened.History.Any(r=>r.OversoldResultId!=null)&&reopened.SelectedRun==null&&!reopened.ShowBacktestTradeMarkers,"previous runs remain in history but never auto-select or auto-display after reopening");}
        string popup="";var key=new KiwoomCredentials{AppKey="fixture",SecretKey="fixture"};
        using(var invalid=new WorkspaceViewModel(_=>false,()=>key,new FixtureMarketData(),store,s=>popup=s,(c,e)=>new(c,e,new Handler(_=>Task.FromResult(Json("""{"return_code":8001}"""))),live:true)))
        {invalid.IsOnline=true;PumpUntil(()=>!invalid.Busy);Check(invalid.IsOffline&&popup=="실제 계좌 키가 아닙니다.","invalid live key shows exact popup and returns offline");}
        popup="";
        using(var unavailable=new WorkspaceViewModel(_=>false,()=>key,new FixtureMarketData(),store,s=>popup=s,(c,e)=>new(c,e,new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))),live:true)))
        {unavailable.IsOnline=true;PumpUntil(()=>!unavailable.Busy);Check(unavailable.IsOffline&&popup.Contains("연결"),"server failure is not falsely labeled invalid key");}
        using(var verified=new WorkspaceViewModel(_=>false,()=>key,new FixtureMarketData(),store,s=>popup=s,(c,e)=>new(c,e,new Handler(request=>Task.FromResult(Json(request.Headers.GetValues("api-id").Single() switch{
            "au10001"=>"""{"return_code":0,"token":"fixture","expires_dt":"20990101000000"}""",
            "ka00001"=>"""{"return_code":0,"acctNo":"1234567890"}""",
            "kt00018"=>"""{"return_code":0,"tot_evlt_amt":"0","acnt_evlt_remn_indv_tot":[]}""",
            "kt00001"=>"""{"return_code":0,"entr":"10000"}""",
            "kt00007"=>"""{"return_code":0,"acnt_ord_cntr_prps_dtl":[]}""",
            _=>throw new InvalidOperationException("unexpected request: no orders permitted")
        }))),live:true)))
        {verified.Actions["키설정"].Execute(null);PumpUntil(()=>!verified.Busy);Check(verified.IsOffline&&verified.AccountInfo.Contains("확인 완료"),"key setting in offline mode validates without switching mode");verified.IsOnline=true;PumpUntil(()=>!verified.Busy);Check(verified.IsOnline&&verified.Actions["키설정"].CanExecute(null),"validated real account enters online with key button enabled");Check(!verified.Actions["매수"].CanExecute(null),"verified account still requires selected stock before ordering");}
        using(var historyStore=new TradingStore(Path.Combine(Dir,"intraday-ui.db")))
        using(var historyBroker=new ChartHistoryBroker())
        using(var historyVm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:historyStore,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>historyBroker))
        {
            Check(!historyVm.CanUseIntraday&&!historyVm.CanUseMinutes,"API-dependent tick and minute controls are disabled before connection");
            historyVm.Query="삼성전자";historyVm.Actions["검색"].Execute(null);PumpUntil(()=>!historyVm.Busy);historyVm.IsOnline=true;PumpUntil(()=>!historyVm.Busy);
            Check(historyVm.CanUseIntraday&&historyVm.CanUseMinutes,"connected broker capabilities enable tick and minute controls");
            historyVm.ChartPeriod="1틱";PumpUntil(()=>!historyVm.Busy);Check(historyVm.ActiveTab==0&&historyVm.ChartPeriod=="1틱"&&historyVm.ChartBars.Count==3&&historyBroker.TickReads==1,"tick selection renders directly on stock chart without opening raw tick tab");
            historyVm.ChartPeriod="5분";PumpUntil(()=>!historyVm.Busy);Check(historyVm.ActiveTab==0&&historyVm.ChartBars.Count==24&&historyBroker.MinuteReads==1,"broker one-minute history aggregates and renders as selected minute bars");
            historyVm.Actions["관심등록"].Execute(null);PumpUntil(()=>!historyVm.Busy);historyVm.ScreenWatchlist=true;foreach(var group in historyVm.WatchGroups)group.IncludeInScreening=true;historyVm.ScreenDaily=false;historyVm.ScreenBarValue="1";historyVm.ScreenBarUnit="시";historyVm.ScreenFrom=new DateTime(2026,10,20);historyVm.ScreenTo=new DateTime(2026,10,20);historyVm.Actions["스크리닝"].Execute(null);PumpUntil(()=>!historyVm.Busy);
            var hourResult=historyVm.LatestScreening!.Results.Single();var hourEvaluation=hourResult.Evaluation;
            Check(hourEvaluation!=null&&hourResult.DataFrom!=null&&hourResult.Status!="자료 부족 · 제공 분봉의 확정 구간·실제 거래대금 정보가 없어 전략용 봉으로 사용할 수 없습니다."&&hourEvaluation.Conditions.Any(c=>c.Condition=="온라인 분봉 거래대금"&&c.Diagnostic),"online one-hour screening aggregates broker minutes and discloses estimated notional instead of reporting no bars · "+hourResult.Status);
            Check(hourEvaluation!.Conditions.Single(c=>c.Condition=="상품 제외").Outcome==Stock.Core.Research.Oversold.ConditionOutcome.MissingData&&hourEvaluation.Conditions.Single(c=>c.Condition=="Crash").Outcome!=Stock.Core.Research.Oversold.ConditionOutcome.NotEvaluated&&historyVm.ScreenInfo.Contains("조회 실패/봉 없음 0개"),"online one-hour research continues technical evaluation while keeping unresolved status explicit");
            var detailTask=historyVm.LoadScreeningChart(historyVm.SelectedStock!,historyVm.LatestScreening);PumpUntil(()=>detailTask.IsCompleted);var hourChart=detailTask.GetAwaiter().GetResult();Check(hourChart.Initial.Period=="1시"&&hourChart.Initial.Bars.Length>0&&hourChart.Initial.Bars.All(b=>DateOnly.FromDateTime(b.Time.ToOffset(TimeSpan.FromHours(9)).DateTime)==new DateOnly(2026,10,20)),"online screening detail reuses the selected one-hour bars and screening date range instead of defaulting to daily");
            Check(historyBroker.RangeReads==1&&historyBroker.RangeFrom==new DateOnly(2026,10,20)&&historyBroker.RangeTo==new DateOnly(2026,10,20),"online minute screening requests the explicit snapshot date range and its detail reuses the bounded cache");
            historyVm.ScreenFrom=new DateTime(2026,10,1);historyVm.ScreenTo=new DateTime(2026,10,20);historyVm.Actions["스크리닝"].Execute(null);PumpUntil(()=>!historyVm.Busy);var fullTask=historyVm.LoadScreeningChart(historyVm.SelectedStock!,historyVm.LatestScreening);PumpUntil(()=>fullTask.IsCompleted);var fullChart=fullTask.GetAwaiter().GetResult();
            Check(fullChart.Initial.Period=="1시"&&fullChart.Initial.Bars.Length==84&&DateOnly.FromDateTime(fullChart.Initial.Bars[0].Time.ToOffset(TimeSpan.FromHours(9)).DateTime)==new DateOnly(2026,10,1)&&DateOnly.FromDateTime(fullChart.Initial.Bars[^1].Time.ToOffset(TimeSpan.FromHours(9)).DateTime)==new DateOnly(2026,10,20),"one-hour screening detail contains every complete hourly bar across the requested start/end period");Check(historyBroker.RangeReads==2&&historyBroker.RangeFrom==new DateOnly(2026,10,1)&&historyBroker.RangeTo==new DateOnly(2026,10,20),"a different screening range performs one new bounded request and its detail reuses that result");
            var fullDialog=new ScreeningChartDialog(fullChart){ShowActivated=false,Left=-20000,WindowStartupLocation=WindowStartupLocation.Manual};fullDialog.Show();fullDialog.UpdateLayout();var fullCandle=Descendants(fullDialog).OfType<CandleChart>().Single();Check(fullCandle.VisibleBarCount==fullChart.Initial.Bars.Length&&fullCandle.ScreeningOverviewLocked,"screening detail initially fits and locks the complete requested hourly range");var fullRoot=(FrameworkElement)fullDialog.Content;var fullImage=new RenderTargetBitmap((int)fullRoot.ActualWidth,(int)fullRoot.ActualHeight,96,96,PixelFormats.Pbgra32);fullImage.Render(fullRoot);var fullPng=new PngBitmapEncoder();fullPng.Frames.Add(BitmapFrame.Create(fullImage));using(var imageFile=File.Create(Path.Combine(output,"screening-hour-full-range.png")))fullPng.Save(imageFile);fullCandle.ZoomAt(fullCandle.ActualWidth/2,120);Check(fullCandle.VisibleBarCount==fullChart.Initial.Bars.Length,"screening overview ignores wheel zoom until right-click focus");fullCandle.ToggleScreeningReviewView(fullCandle.ActualWidth/2);Check(fullCandle.VisibleBarCount<fullChart.Initial.Bars.Length&&fullCandle.Bars?.Count==fullChart.Initial.Bars.Length&&fullCandle.ScreeningInteractionEnabled,"screening right-click focus zooms into the full source series without discarding period bars");fullCandle.ToggleScreeningReviewView(fullCandle.ActualWidth/2);Check(fullCandle.VisibleBarCount==fullChart.Initial.Bars.Length&&fullCandle.ScreeningOverviewLocked,"screening second right click restores the complete locked range");fullDialog.Close();
            historyVm.From=new DateTime(2026,10,20);historyVm.To=new DateTime(2026,10,20);historyVm.BacktestDaily=false;Check(historyVm.BacktestCustomBars&&historyVm.BacktestBarUnitOptions.Select(x=>x.Label).SequenceEqual(new[]{"분봉","시간봉"})&&historyVm.Actions["백테스트"].CanExecute(null),"connected minute-capable account enables common minute/hour backtest selectors");historyVm.BacktestBarUnit="분";Check(historyVm.BacktestBarValues.Length==59&&historyVm.BacktestBarValues[^1]=="59","backtest minute selector offers 1 through 59");historyVm.BacktestBarValue="59";historyVm.BacktestBarUnit="시";Check(historyVm.BacktestBarValues.Length==12&&historyVm.BacktestBarValues[^1]=="12"&&historyVm.BacktestBarValue=="12","backtest hour selector offers 1 through 12 and keeps the selected value valid");historyVm.BacktestBarUnit="분";historyVm.BacktestBarValue="5";historyVm.Actions["백테스트"].Execute(null);PumpUntil(()=>!historyVm.Busy);var minuteHistory=historyVm.LatestResult;var minuteResult=minuteHistory?.OversoldResultId is {} minuteId?historyStore.State<Stock.Core.Research.Oversold.OversoldResult>(minuteId):null;
            Check(minuteResult?.BarSpecification==new Stock.Core.Research.Oversold.BarSpecification(5,Stock.Core.Research.Oversold.BarUnit.Minute)&&historyVm.ChartPeriod=="5분"&&historyVm.ChartBars.Count==78&&historyVm.OversoldMessage.Contains("다음 완성봉 시가"),"five-minute UI backtest uses official ranged minutes, renders the tested bars, and discloses next-bar execution");
            historyVm.BacktestBarUnit="시";historyVm.BacktestBarValue="1";historyVm.Actions["백테스트"].Execute(null);PumpUntil(()=>!historyVm.Busy);var hourHistory=historyVm.LatestResult;var hourBacktest=hourHistory?.OversoldResultId is {} hourId?historyStore.State<Stock.Core.Research.Oversold.OversoldResult>(hourId):null;Check(hourBacktest?.BarSpecification==new Stock.Core.Research.Oversold.BarSpecification(1,Stock.Core.Research.Oversold.BarUnit.Hour)&&historyVm.ChartPeriod=="1시"&&historyVm.ChartBars.Count==6,"one-hour UI backtest aggregates official minutes, renders six complete KRX hour bars, and preserves the hour specification");
        }
        using(var stopStore=new TradingStore(Path.Combine(Dir,"emergency-chart-ui.db")))
        using(var blockingBroker=new BlockingChartHistoryBroker())
        using(var stopVm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:stopStore,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>blockingBroker))
        {
            stopVm.Query="삼성전자";stopVm.Actions["검색"].Execute(null);PumpUntil(()=>!stopVm.Busy);stopVm.IsOnline=true;PumpUntil(()=>!stopVm.Busy);
            stopVm.ChartPeriod="1분";PumpUntil(()=>blockingBroker.Started.Task.IsCompleted&&stopVm.Busy&&stopVm.ChartLoading);
            Check(stopVm.Actions["긴급중지"].CanExecute(null),"emergency stop stays enabled during minute API loading");stopVm.Actions["긴급중지"].Execute(null);
            PumpUntil(()=>blockingBroker.Cancelled.Task.IsCompleted&&!stopVm.Busy&&!stopVm.ChartLoading);
            Check(stopVm.Status.Contains("긴급중지")&&stopVm.TickStatus.Contains("중지"),"emergency stop cancels API wait and immediately clears chart loading");
        }
        vm.ActiveTab=5;PumpUntil(()=>vm.Kospi!=null&&vm.Kosdaq!=null&&vm.StockQuote!=null);Check(vm.AverageVolume.Contains("20거래일"),"market overview shows both indexes and selected stock average volume");
        Check(vm.VisibleIndices.Count==2&&vm.VisibleIndices.All(c=>c.HasChart),"domestic index cards contain observed trends");vm.OverseasMarket=true;PumpUntil(()=>vm.VisibleIndices.All(c=>c.HasChart));Check(vm.VisibleIndices.Select(c=>c.Code).SequenceEqual(new[]{"DOW","NASDAQ","SP500"}),"overseas index selector loads three US indexes");vm.DomesticMarket=true;
        vm.ActiveTab=6;PumpUntil(()=>vm.WatchItems![0].State!="시세 확인 전");Check(vm.WatchItems![0].Price!="—","watchlist loads public quote for saved stock");vm.ActiveTab=0;vm.WorkspaceToolTab=2;window.UpdateLayout();
        var buttonArea=(FrameworkElement)window.FindName("BacktestButtons");var resultArea=(FrameworkElement)window.FindName("InlineTestResult");Check(buttonArea.ActualWidth>0&&resultArea.ActualWidth>0,"strategy execution controls and inline result both render inside the right strategy tab");
        Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var registration=Descendants(window).OfType<ComboBox>().Single(c=>ReferenceEquals(c.ItemsSource,vm.WatchGroups));Check(Descendants(registration).OfType<TextBlock>().Any(t=>t.Text==vm.RegistrationGroup!.Name),"watch group selector renders updated group name rather than internal type");
        window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var root=(FrameworkElement)window.Content;var tabs=Descendants(root).OfType<TabControl>().First();
        for(int i=0;i<tabs.Items.Count;i++){tabs.SelectedIndex=i;root.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(root),null,new Rect(0,0,root.ActualWidth,root.ActualHeight));var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(drawing);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,$"workspace-screen-{i}.png"));png.Save(file);}
        Check(tabs.Items.Count==8,"eight WPF tabs render including watchlist and raw tick");Check(Descendants(root).OfType<RadioButton>().Count(r=>r.GroupName=="Mode")==2,"two mode radio buttons");
        ConnectionUiTests(window,output);UiRevisionTests(window,output);PipelineRecurringUiTests(window,output);MultiTradeUiTests(window,output);OversoldUiTests(window,output);OnlyOversoldUiTests(window,output);ParameterScopeUiTests(window,output);FixedEditorUiTests(window,output);ScreeningUiTests(window,output);CurrentDailyUiTests(window,output);
        window.Close();app.Shutdown();
    }
    private sealed class ChartHistoryBroker:IAccountBroker,IMinuteHistoryBroker,IRangedMinuteHistoryBroker,ITickHistoryBroker
    {
        public int TickReads,MinuteReads,RangeReads;public DateOnly? RangeFrom,RangeTo;public string Id=>"fixture-chart";public string Name=>"키움증권 테스트";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account=>"1234567890";public string MaskedAccount=>"******7890";public bool SupportsMarketOrders=>true;
        public Task ConnectAsync()=>Task.CompletedTask;
        public Task<decimal> CurrentPrice(string symbol)=>Task.FromResult(100m);
        public Task<Balance> GetBalanceAsync()=>Task.FromResult(new Balance([],10_000_000,10_000_000));
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerOrder>>([]);
        public Task<OrderReceipt> SubmitAsync(OrderRequest order)=>throw new InvalidOperationException("test broker does not submit");
        public Task<OrderReceipt> CancelAsync(string number,string symbol)=>throw new InvalidOperationException("test broker does not cancel");
        public Task<PriceBar[]> TickHistory(string symbol,CancellationToken cancellation){TickReads++;return Task.FromResult(new[]{Bar(0,100),Bar(1,101),Bar(2,102)});}
        public Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation){MinuteReads++;return Task.FromResult(Enumerable.Range(0,120).Select(i=>Bar(i*60,100+i%10)).ToArray());}
        public Task<PriceBar[]> MinuteHistory(string symbol,DateOnly from,DateOnly to,CancellationToken cancellation)
        {
            RangeReads++;RangeFrom=from;RangeTo=to;var result=new List<PriceBar>();var price=100m;
            for(var day=from;day<=to;day=day.AddDays(1))
            {
                if(day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)continue;
                var open=new DateTimeOffset(day.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9));
                for(var minute=0;minute<390;minute++){var value=price+(minute%12);result.Add(new(open.AddMinutes(minute),value,value,value,value,1));}price++;
            }
            return Task.FromResult(result.ToArray());
        }
        public void Dispose(){}
        private static PriceBar Bar(int seconds,decimal price)=>new(new DateTimeOffset(2026,10,20,9,0,0,TimeSpan.FromHours(9)).AddSeconds(seconds),price,price,price,price,1);
    }
    private sealed class BlockingChartHistoryBroker:IAccountBroker,IMinuteHistoryBroker
    {
        public TaskCompletionSource<bool> Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Cancelled {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Id=>"fixture-blocking-chart";public string Name=>"키움증권 지연 테스트";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account=>"1234567890";public string MaskedAccount=>"******7890";public bool SupportsMarketOrders=>true;
        public Task ConnectAsync()=>Task.CompletedTask;
        public Task<decimal> CurrentPrice(string symbol)=>Task.FromResult(100m);
        public Task<Balance> GetBalanceAsync()=>Task.FromResult(new Balance([],10_000_000,10_000_000));
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerOrder>>([]);
        public Task<OrderReceipt> SubmitAsync(OrderRequest order)=>throw new InvalidOperationException("test broker does not submit");
        public Task<OrderReceipt> CancelAsync(string number,string symbol)=>throw new InvalidOperationException("test broker does not cancel");
        public async Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation){Started.TrySetResult(true);try{await Task.Delay(Timeout.Infinite,cancellation);return [];}catch(OperationCanceledException){Cancelled.TrySetResult(true);throw;}}
        public void Dispose(){}
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var n in Descendants(c))yield return n;}}
    private static void PumpUntil(Func<bool> done,int seconds=30){var end=DateTime.UtcNow.AddSeconds(seconds);while(!done()){if(DateTime.UtcNow>end)throw new TimeoutException("UI action timeout");var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);Thread.Sleep(5);}}
}

