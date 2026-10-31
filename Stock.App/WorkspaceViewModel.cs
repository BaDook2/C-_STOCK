using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record ScreeningRow(StockItem Stock,decimal Close,decimal Change,string Signal,string Date)
{public string Code=>Stock.Symbol;public decimal? Drawdown {get;init;} public decimal? Rsi {get;init;} public decimal? VolumeRatio {get;init;} public int SupportCount {get;init;} public string Status {get;init;}="";public string Name=>Stock.Name;public string Region=>Stock.Board switch{"KOSPI"=>"코스피","KOSDAQ"=>"코스닥",_=>Stock.Region};public string Sector=>Stock.Sector;public string Price=>$"{DisplayNumber.Price(Close)} {(Stock.Currency=="KRW"?"원":"달러")}";}
public sealed partial class WorkspaceViewModel : Observable,IDisposable
{
    private readonly IPublicMarketData provider;
    private readonly IOversoldBacktestService oversoldBacktests;
    private readonly TradingStore store;
    private readonly bool ownsStore;
    private readonly Func<string,bool> confirm;
    private readonly Func<KiwoomCredentials?> prompt;
    private readonly DispatcherTimer replayTimer;
    private CancellationTokenSource? operation;
    private ChartStrategySettings settings;
    private BarDataset? dataset;
    private PriceBar[] rangeBars=[];
    private BarPaperAccount? paper;
    private int replayIndex=-1;
    private ChartStrategySettings? replaySettings;
    private KiwoomCredentials? credentials;
    private IAccountBroker? broker;
    private BrokerConnection? connection;
    private readonly Func<BrokerConnection?>? connectionPrompt;
    private readonly Func<BrokerConnection?>? defaultConnectionLoader;
    private readonly Action<BrokerConnection>? saveConnection;
    private readonly Func<BrokerConnection,Exchange,IAccountBroker>? accountFactory;
    private readonly string sessionDirectory;
    private RemoteTradingSession? session;
    private bool busy,online,showMa=true,showCloud,replaying,emergencyStopping;
    private string region="국내",screeningMarketSelection="코스피+코스닥",query="삼성전자",status="종목 이름을 검색하고 데이터를 가져오세요.",dataInfo="인증키 없이 공개 일봉을 불러옵니다. 최초 다운로드에는 인터넷이 필요해요.",accountInfo="실제 계좌 미연결",resultInfo="과거 가격으로 전략을 시험하거나 하루씩 모의매매할 수 있어요.",screenInfo="현재 상장된 시가총액 상위 종목을 검사합니다. 전체 시장 검사는 아닙니다.",sector="전체",capital="10,000,000",quantity="1",preset="기본";
    private DateTime? from=KoreaClock.Today.AddYears(-1).ToDateTime(TimeOnly.MinValue),to=KoreaClock.Today.ToDateTime(TimeOnly.MinValue);
    private StockItem? selected;
    private ScreeningRow? selectedScreen;
    private BrokerOrder? selectedOrder;
    private string catalogRegion="";
    private int catalogSize;
    private IReadOnlyList<PriceBar> chartBars=[];
    public Dictionary<string,ActionCommand> Actions {get;}=[];
    public ObservableCollection<StockItem> SearchResults {get;}=[];
    public ObservableCollection<StockItem> Catalog {get;}=[];
    public ObservableCollection<ScreeningRow> Matches {get;}=[];
    public ObservableCollection<string> Sectors {get;}=["전체"];
    public ObservableCollection<SettingField> StrategyFields {get;}=[];
    public ObservableCollection<string> Presets {get;}=[];
    public ObservableCollection<BarFill> Trades {get;}=[];
    public ObservableCollection<string> Logs {get;}=[];
    public ObservableCollection<BrokerOrder> Orders {get;}=[];
    public ObservableCollection<Holding> Holdings {get;}=[];
    public string[] Regions {get;}=["전체","국내","해외"];
    public string[] ScreeningMarkets {get;}=["코스피","코스닥","코스피+코스닥"];
    public string ScreeningMarketSelection {get=>screeningMarketSelection;set{if(!CanConfigure||value==screeningMarketSelection||!ScreeningMarkets.Contains(value))return;screeningMarketSelection=value;Sector="전체";Changed();UpdateSectors();}}
    public int[] ScreeningSizes {get;}=[10,20,50,100];
    public int ScreeningSize {get;set;}=10;
    private bool screenWatchlist;
    public bool ScreenWatchlist {get=>screenWatchlist;set{if(!CanConfigure||screenWatchlist==value)return;screenWatchlist=value;Changed();Changed(nameof(ScreenMarket));UpdateSectors();ScreenInfo=value?"관심그룹을 여러 개 선택하세요. 중복 종목은 한 번만 검사합니다.":"시가총액 상위 종목을 검사합니다.";}}
    public bool ScreenMarket {get=>!screenWatchlist;set{if(value)ScreenWatchlist=false;}}
    public IReadOnlyList<StockItem> SelectedScreeningStocks()=>WatchGroups.Where(g=>g.IncludeInScreening).SelectMany(g=>g.Items).Select(e=>e.Stock).DistinctBy(s=>s.Key).Where(s=>MatchesScreeningMarket(s)&&(Sector=="전체"||s.Sector==Sector)).ToArray();
    private bool MatchesScreeningMarket(StockItem stock)=>stock.Exchange==Exchange.KRX&&ScreeningMarketSelection switch{"코스피"=>stock.Board=="KOSPI","코스닥"=>stock.Board=="KOSDAQ",_=>true};
    private int activeTab;
    public int ActiveTab {get=>activeTab;set{activeTab=value;Changed();if(!apiPaused&&value==5)_=RefreshMarket();if(!apiPaused&&value==6)_=RefreshWatchlist();if(value==7&&RawTicks==null)OpenRawTickChart();}}
    public bool Busy {get=>busy;private set{busy=value;Changed();Refresh();}}
    public bool CanConfigure=>!Busy&&!Replaying;
    public bool IsOnline {get=>online;set{if(!CanConfigure||online==value)return;if(value)_=Run(EnterOnline);else _=Run(async()=>{await StopMulti();Disconnect();ChangeMode(false);});}}
    public bool IsOffline {get=>!online;set{if(value)IsOnline=false;}}
    public string ModeDescription=>IsOnline?"수동 주문과 검증된 낙주 전략 실시간 실행은 선택한 실제 계좌에서만 처리합니다.":"인증키 없이 실제 과거 가격으로 연습해요. 주문은 가상 계좌에서만 처리됩니다.";
    public string ConnectedAccountText=>IsOnline&&broker!=null?$"연결 · {broker.Name}{(string.IsNullOrWhiteSpace(broker.MaskedAccount)?"":" "+broker.MaskedAccount)}":"";
    public string Region {get=>region;set{if(!CanConfigure||region==value)return;region=value;SelectedStock=null;SearchResults.Clear();Query=value=="해외"?"애플":"삼성전자";Changed();}}
    public string Query {get=>query;set{query=value;Changed();}}
    public StockItem? SelectedStock {get=>selected;set{if(!CanConfigure)return;Select(value);}}
    public string StockName=>dataset?.Stock.Name??SelectedStock?.Name??"어떤 주식을 살펴볼까요?";
    public string StockDetail=>dataset is {} d?$"{d.Stock.Region} · {d.Stock.Sector} · {(d.Stock.Currency=="KRW"?"원":"달러")} · {ChartPeriod}":SelectedStock is {} s?$"{s.Region} · {s.Sector}":"국내와 미국 주식을 한글 이름으로 검색하세요";
    private decimal? HeaderPrice=>LiveHeader?displayedDaily!.Quote.Price:ChartBars.Count>0?ChartBars[^1].Close:null;
    private decimal? HeaderPreviousClose
    {
        get
        {
            if(LiveHeader)
            {
                var previous=displayedDaily!.Quote.Price-displayedDaily.Quote.Change;
                return previous>0?previous:null;
            }
            if(dataset==null||ChartBars.Count==0)return null;
            var date=DailyQuoteMerger.Date(ChartBars[^1].Time,dataset.Stock.Exchange);
            return dataset.Bars.Where(b=>DailyQuoteMerger.Date(b.Time,dataset.Stock.Exchange)<date).OrderBy(b=>b.Time).LastOrDefault()?.Close;
        }
    }
    private decimal? HeaderChange=>HeaderPrice is {} price&&HeaderPreviousClose is {} previous&&previous>0?price-previous:null;
    public string PriceText=>HeaderPrice is {} price?$"{DisplayNumber.Price(price)} {(dataset?.Stock.Currency=="KRW"?"원":"달러")}":"—";
    public string ChangeText
    {
        get
        {
            if(LiveHeader)return displayedDaily!.Quote.ChangeText;
            if(HeaderPrice is not {} price||HeaderPreviousClose is not {} previous||previous<=0)return "";
            var change=price-previous;return $"{(change>=0?"▲":"▼")} {Math.Abs(change):N0} ({(price/previous-1)*100:+#,##0.00;-#,##0.00;0.00}%)";
        }
    }
    public string PriceColor=>HeaderChange is not {} change||change==0?"#333D4B":change>0?"#F04452":"#3182F6";
    public string PriceDate=>LiveHeader?$"{displayedDaily!.Quote.TradeTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss} {(displayedDaily.ExchangeTimestamp?"시세":"관측")} · {(displayedDaily.Quote.State=="OPEN"?"진행 중":"마지막 관측")} · 지연 가능":ChartBars.Count>0?$"{ChartBars[^1].Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd} 기준":"";
    public string Status {get=>status;private set{status=value;Changed();}}
    public string DataInfo {get=>dataInfo;private set{dataInfo=value;Changed();}}
    public string AccountInfo {get=>accountInfo;private set{accountInfo=value;Changed();}}
    public string ResultInfo {get=>resultInfo;private set{resultInfo=value;Changed();}}
    public string ScreenInfo {get=>screenInfo;private set{screenInfo=value;Changed();}}
    public string Sector {get=>sector;set{sector=value;Changed();}}
    public string Capital {get=>capital;set{capital=value;Changed();}}
    public string Quantity {get=>quantity;set{quantity=value;Changed();}}
    public string Preset {get=>preset;set{preset=value;Changed();}}
    public DateTime? From {get=>from;set{from=value;Changed();}}
    public DateTime? To {get=>to;set{to=value;Changed();}}
    public IReadOnlyList<PriceBar> ChartBars {get=>chartBars;private set{visibleDaily=value;chartBars=ChartPeriods.Intraday(ChartPeriod)?IntradayBars():ChartPeriods.Aggregate(value,ChartPeriod);Changed();Changed(nameof(PriceText));Changed(nameof(ChangeText));Changed(nameof(PriceColor));Changed(nameof(PriceDate));}}
    public bool ShowMa {get=>showMa;set{showMa=value;Changed();}}
    public bool ShowCloud {get=>showCloud;set{showCloud=value;Changed();}}
    public int FastPeriod=>settings.Fast;
    public int SlowPeriod=>settings.Slow;
    public string IndicatorLegend=>$"이평 {settings.Fast}봉 주황 · {settings.Slow}봉 보라 · 60봉 초록 / 일목 9·26·52, 선행 26일";
    public bool Replaying {get=>replaying;private set{replaying=value;Changed();Refresh();}}
    public bool AutoReplay {get=>false;set{Changed();}}
    public string OrderHeading=>IsOnline?"실제 계좌 주문":"가상 계좌 주문";
    public string PaperSummary=>HasConfiguredPractice?ConfiguredPaperSummary:paper==null?"모의매매 시작을 누르면 가상 계좌가 만들어져요.":$"현금 {paper.Cash:N0} · 보유 {paper.Quantity:N0}주 · 평가 {paper.Value:N0}\n손익 {paper.Profit:+#,##0;-#,##0;0} · {paper.PendingDescription}";
    public ScreeningRow? SelectedMatch {get=>selectedScreen;set{selectedScreen=value;Changed();Refresh();}}
    public BrokerOrder? SelectedOrder {get=>selectedOrder;set{selectedOrder=value;Changed();Refresh();}}
    public string LiveOrderLimitText {get;set;}="1,000,000";
    public decimal LiveOrderLimit
    {
        get=>decimal.TryParse(LiveOrderLimitText,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)&&value>0&&value<=1_000_000_000_000?value:throw new InvalidOperationException("1회 매수 한도를 양수로 입력하세요.");
        set{LiveOrderLimitText=NumericDisplay.Format(value);Changed(nameof(LiveOrderLimitText));}
    }
    public WorkspaceViewModel(Func<string,bool> confirm,Func<KiwoomCredentials?>? prompt=null,IPublicMarketData? provider=null,TradingStore? store=null,Action<string>? alert=null,Func<KiwoomCredentials,Exchange,KiwoomBroker>? brokerFactory=null,Func<BrokerConnection?>? connectionPrompt=null,Action<BrokerConnection>? saveConnection=null,Func<BrokerConnection,Exchange,IAccountBroker>? accountFactory=null,string? sessionDirectory=null,Func<IAccountBroker,IReadOnlyList<Instrument>,DataSettings,Action<string>,IMarketDataProvider>? multiFeedFactory=null,Func<DateTimeOffset>? multiClock=null,IOversoldBacktestService? oversoldBacktestService=null,Func<BrokerConnection?>? defaultConnectionLoader=null)
    {
        this.multiFeedFactory=multiFeedFactory;this.multiClock=multiClock??(()=>DateTimeOffset.UtcNow);this.confirm=confirm;this.prompt=prompt??(()=>null);this.provider=provider??new PublicMarketData();oversoldBacktests=oversoldBacktestService??new OversoldBacktestService();ownsStore=store==null;this.store=store??new TradingStore(Path.Combine(MainViewModel.StateDirectory,"workspace-v2.db"));settings=new();BuildFields();
        foreach(var p in this.store.State<string[]>("chart-presets")??["기본"])Presets.Add(p);
        foreach(var s in this.store.State<StockItem[]>("catalog")??[])Catalog.Add(s);UpdateSectors();
        Add("원본틱",()=>{OpenRawTickChart();return Task.CompletedTask;},()=>CanConfigure&&selected!=null);Add("검색",Search,()=>CanConfigure);Add("데이터",LoadData,()=>CanConfigure);Add("목록",LoadCatalog,()=>CanConfigure);
        Add("스크리닝",Screen,()=>CanConfigure);Add("결과열기",async()=>{Select(SelectedMatch!.Stock);await LoadData();ActiveTab=0;},()=>CanConfigure&&SelectedMatch!=null);
        Add("백테스트",()=>UseConfiguredStrategy?RunConfiguredBacktest():Backtest(),()=>CanConfigure&&dataset!=null&&!ChartPeriods.Intraday(ChartPeriod));
        Add("연습시작",()=>UseConfiguredStrategy?StartConfiguredPractice():StartPractice(),()=>CanConfigure&&dataset!=null&&IsOffline&&!ChartPeriods.Intraday(ChartPeriod));
        Add("다음",()=>{Step();return Task.CompletedTask;},()=>!Busy&&IsOffline&&HasNextPractice);
        Add("재생",()=>{Replaying=true;replayTimer!.Start();return Task.CompletedTask;},()=>!Busy&&IsOffline&&HasNextPractice&&!Replaying);
        Add("정지",()=>{replayTimer!.Stop();Replaying=false;return Task.CompletedTask;},()=>Replaying);
        Add("매수",()=>Place(true),()=>!Busy&&(IsOnline?session!=null&&!MultiSessionExists&&SelectedStock!=null:HasNextPractice&&!PaperPending));
        Add("매도",()=>Place(false),()=>!Busy&&(IsOnline?session!=null&&!MultiSessionExists&&SelectedStock!=null:PaperQuantity>0&&HasNextPractice&&!PaperPending));
        Add("키설정",()=>ConfigureKeys(IsOnline),()=>!Busy);Add("계좌조회",RefreshAccount,()=>CanConfigure&&session!=null&&IsOnline);
        Add("주문취소",Cancel,()=>!Busy&&(IsOnline?SelectedOrder!=null&&session!=null&&!MultiSessionExists:PaperPending));
        Actions["긴급중지"]=new ActionCommand(EmergencyStopNow,()=>!emergencyStopping);
        Add("외부확인",()=>{if(session!=null&&confirm("증권사 앱에서 미확인 주문과 취소 상태를 확인했나요? 신규 주문 차단을 해제합니다."))session.AcknowledgeReview();return Task.CompletedTask;},()=>CanConfigure&&session!=null&&!MultiSessionExists);
        Add("저장",()=>{Apply();foreach(var f in TickFields)f.Apply();tickSettings.Validate();SaveStrategyConfiguration();this.store.State("strategy-tick-preset:"+Preset,tickSettings);this.store.State("strategy-configuration-preset:"+Preset,strategyConfiguration);if(string.IsNullOrWhiteSpace(Preset))throw new InvalidOperationException("설정 이름을 입력하세요.");this.store.State("chart-preset:"+Preset,settings);this.store.State("chart-settings",settings);if(!Presets.Contains(Preset))Presets.Add(Preset);this.store.State("chart-presets",Presets.ToArray());Log("전략 설정 저장: "+Preset);return Task.CompletedTask;},()=>CanConfigure);
        Add("불러오기",()=>{settings=this.store.State<ChartStrategySettings>("chart-preset:"+Preset)??new();strategyConfiguration=this.store.State<StrategyConfiguration>("strategy-configuration-preset:"+Preset)??new();BuildParameterGroups();Changed(nameof(StandardMode));Changed(nameof(AdvancedMode));Changed(nameof(StrategyChoice));Changed(nameof(UseConfiguredStrategy));Changed(nameof(IsPipeline));Changed(nameof(PipelineStrategyChoice));BuildFields();return Task.CompletedTask;},()=>CanConfigure);
        Add("기본값",()=>{settings=new();strategyConfiguration=new();BuildParameterGroups();Changed(nameof(StandardMode));Changed(nameof(AdvancedMode));Changed(nameof(StrategyChoice));Changed(nameof(UseConfiguredStrategy));Changed(nameof(IsPipeline));Changed(nameof(PipelineStrategyChoice));BuildFields();return Task.CompletedTask;},()=>CanConfigure);
        Actions["취소"]=new ActionCommand(()=>{operation?.Cancel();return Task.CompletedTask;},()=>Busy);
        replayTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(800)};replayTimer.Tick+=(_,_)=>{try{Step();}catch(Exception ex){replayTimer.Stop();Replaying=false;Status=ex.Message;}};
        this.connectionPrompt=connectionPrompt;this.defaultConnectionLoader=defaultConnectionLoader;this.saveConnection=saveConnection;this.accountFactory=accountFactory;this.sessionDirectory=sessionDirectory??MainViewModel.StateDirectory;
        InitializeFeatures(alert,brokerFactory);showTradeMarkers=this.store.State<bool>("show-trade-markers");showLiveTradeMarkers=this.store.State<bool>("show-live-trade-markers");showBacktestTradeMarkers=this.store.State<bool>("show-backtest-trade-markers");ReloadTradeMarkers();InitializeOnlyOversold();
    }
    private void Add(string key,Func<Task> fn,Func<bool> can)=>Actions[key]=new ActionCommand(()=>Run(fn),can);
    private void Refresh(){Changed(nameof(CanOpenMulti));Changed(nameof(CanOpenRealtimePaper));Changed(nameof(VirtualTradingHelp));Changed(nameof(MultiSessionExists));multiTrading?.Refresh();Changed(nameof(CanConfigure));Changed(nameof(CanUseIntraday));Changed(nameof(CanUseMinutes));Changed(nameof(BacktestDaily));Changed(nameof(BacktestCustomBars));Changed(nameof(BacktestBarDescription));Changed(nameof(IntradayHelp));foreach(var c in Actions.Values)c.Refresh();}
    private async Task Run(Func<Task> fn)
    {if(executing)return;ResumeApiRequests();executing=true;operation=new();try{var task=fn();if(!task.IsCompleted)Busy=true;await task;}catch(OperationCanceledException){Status=apiPaused?"긴급중지로 진행 중인 작업을 취소했습니다.":"작업을 취소했습니다.";}catch(InvalidOperationException ex){Status=ex.Message;}catch(UnknownOrderException){Status="주문 응답을 확인하지 못했습니다. 재전송하지 말고 증권사 앱과 주문 내역을 확인하세요.";}catch(BrokerRejectedException ex){Status=ex.Message;}catch{Status="데이터 또는 계좌 연결에 실패했습니다. 잠시 후 다시 시도하세요.";}finally{operation.Dispose();operation=null;executing=false;if(Busy)Busy=false;else Refresh();}}
    private CancellationToken Token=>operation?.Token??CancellationToken.None;
    private async Task EmergencyStopNow()
    {
        if(emergencyStopping)return;emergencyStopping=true;Actions["긴급중지"].Refresh();
        operation?.Cancel();SuspendApiRequests();ChartLoading=false;if(Busy)Busy=false;TickStatus="긴급중지 · 진행 중인 시세 조회와 차트 로딩을 중단했습니다.";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));
        Status="긴급중지 처리 중 · 신규 주문과 데이터 요청을 중단했습니다.";
        try
        {
            replayTimer.Stop();Replaying=false;paper?.Cancel();configuredPaper?.EmergencyStop();await StopMulti();
            if(IsOnline&&session!=null)await session.EmergencyStop();
            Status="긴급중지 완료 · 신규 주문과 API 로딩을 중단했습니다. 실제 취소 결과는 계좌 조회에서 확인하세요.";
        }
        catch(Exception ex){Status="긴급중지는 적용했습니다. 증권사 취소 확인 중 오류: "+ex.Message;}
        finally{Changed(nameof(PaperSummary));emergencyStopping=false;Refresh();}
    }
    private void Log(string text){Status=text;Logs.Insert(0,$"{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss}  {text}");if(Logs.Count>200)Logs.RemoveAt(Logs.Count-1);}
    private void BuildFields(){StrategyFields.Clear();foreach(var p in typeof(ChartStrategySettings).GetProperties().Where(p=>p.GetCustomAttribute<SettingAttribute>()!=null))StrategyFields.Add(new(settings,p,true));Changed(nameof(FastPeriod));Changed(nameof(SlowPeriod));Changed(nameof(IndicatorLegend));}
    private ChartStrategySettings Apply(){foreach(var f in StrategyFields)f.Apply();settings.Validate();Changed(nameof(FastPeriod));Changed(nameof(SlowPeriod));Changed(nameof(IndicatorLegend));return WithReview(settings.Clone(),selected??dataset?.Stock);}
    private decimal InitialCash(){if(!decimal.TryParse(Capital,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)||n<=0||n>1_000_000_000_000)throw new InvalidOperationException("가상 시작 금액을 올바르게 입력하세요.");return n;}
    private void Select(StockItem? item){var oldCurrency=selected?.Currency;if(selected?.Key==item?.Key&&(item!=null||dataset==null))return;if(selected?.Key!=item?.Key){StopDailyQuotes();dataset=null;ResetConfiguredPractice();StopTicks();tickBars.Clear();tickHistoryStock="";minuteBars=[];minuteStock="";ChartBars=[];ChartMessage="데이터가 없습니다.";paper=null;replayIndex=-1;Trades.Clear();ResultInfo="새 종목의 데이터를 가져와 테스트를 시작하세요.";DataInfo="선택한 종목의 데이터를 가져와주세요.";}selected=item;RawTicks?.Dispose();RawTicks=null;Changed(nameof(RawTicks));StockQuote=null;AverageVolume="종목 데이터를 가져오면 최근 20거래일 평균을 표시합니다.";Changed(nameof(AverageVolume));Changed(nameof(StockQuote));Changed(nameof(RiskStockName));Changed(nameof(RiskReviewed));Changed(nameof(RiskExcluded));if(item!=null){Query=item.Name;if(oldCurrency!=item.Currency){Capital=item.Currency=="KRW"?"10,000,000":"10,000";LiveOrderLimit=item.Currency=="KRW"?1000000:1000;}}Changed(nameof(SelectedStock));Changed(nameof(IsWatchRegistered));Changed(nameof(WatchRegistrationStar));RefreshRecurringChart();Changed(nameof(StockName));Changed(nameof(StockDetail));Changed(nameof(PaperSummary));Refresh();ReloadTradeMarkers();if(IsTick)BeginIntraday();}
    private async Task Search() {ChartLoading=true;try{await SearchCore();if(selected!=null)await LoadSelectedData();}finally{ChartLoading=false;}}
    private async Task SearchCore()
    {
        var name=Query;IReadOnlyList<StockItem> found;bool cached=false;
        try{found=await provider.Search(name,Region,Token);}catch(OperationCanceledException){throw;}catch{found=Catalog.Where(s=>(Region=="전체"||s.Region==Region)&&s.Name.Contains(name,StringComparison.OrdinalIgnoreCase)).ToArray();if(found.Count==0)throw;cached=true;}
        Replace(SearchResults,found);Select(found.FirstOrDefault());Log(found.Count==0?"검색 결과가 없어요. 이름을 짧게 입력하거나 국내·해외 필터를 확인하세요.":$"{(cached?"저장된 목록에서 ":"")}{found.Count:N0}개 종목을 찾았어요. 첫 번째 종목의 차트를 엽니다.");
    }
    /// <summary>검색 결과의 클릭과 키보드 선택을 같은 데이터 조회 경로로 처리합니다.</summary>
    public Task OpenStock(StockItem stock)
    {if(!CanConfigure)return Task.CompletedTask;return Run(async()=>{if(dataset?.Stock.Key==stock.Key&&selected?.Key==stock.Key)return;ChartLoading=true;try{Select(stock);await LoadSelectedData();}finally{ChartLoading=false;}});}
    private async Task LoadData()
    {
        ChartLoading=true;
        try{if(selected==null||!string.Equals(Query.Trim(),selected.Name,StringComparison.OrdinalIgnoreCase))await SearchCore();if(selected!=null)await LoadSelectedData();}
        finally{ChartLoading=false;}
    }
    private async Task LoadSelectedData()
    {
        var item=selected!;try{item=await provider.Describe(item,Token);}catch(OperationCanceledException){throw;}catch{item=item with{Sector="미분류"};}
        Status=item.Name+"의 과거 가격을 가져오고 있어요…";
        BarDataset data;bool cached=false;try{data=await provider.History(item,Token);store.State("bars:"+item.Key,data);}catch(OperationCanceledException){throw;}catch{data=store.State<BarDataset>("bars:"+item.Key)??throw new InvalidOperationException("다운로드에 실패했고 저장된 자료도 없습니다. 인터넷 연결을 확인하세요.");cached=true;}
        selected=data.Stock;Changed(nameof(SelectedStock));Changed(nameof(IsWatchRegistered));Changed(nameof(WatchRegistrationStar));if(!Catalog.Any(s=>s.Key==item.Key)){Catalog.Add(data.Stock);store.State("catalog",Catalog.ToArray());UpdateSectors();}LoadDataset(data);StartDailyQuotes(data.Stock);if(ChartPeriod.EndsWith("분")&&broker is IMinuteHistoryBroker)await LoadMinutes();else if(ChartPeriod.EndsWith("틱")&&broker is ITickHistoryBroker)await LoadTicks();if(cached)DataInfo="저장된 캐시 · 최신 조회 실패 · "+DataInfo;Log(item.Name+(cached?" · 저장된 자료로 열었어요. 수집 시각을 확인하세요.":" · 실제 과거 일봉을 불러왔어요."));
    }
    public void LoadDataset(BarDataset data)
    {
        dataset=data;ResetConfiguredPractice();paper=null;replayIndex=-1;Trades.Clear();ResultInfo="과거 가격으로 전략을 시험하거나 하루씩 모의매매할 수 있어요.";rangeBars=data.Bars;ChartBars=rangeBars;DataInfo=data.Description+" · 마지막 봉 "+(data.Bars.LastOrDefault() is {} lastBar?(lastBar.IsComplete?lastBar.KoreaTime:lastBar.Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd")+" 진행 중 · 관측시각 확인 중"):"없음")+(data.Bars.Any(b=>!b.IsComplete)?" · 장중 봉 포함 (백테스트 제외)":"")+" · 시각은 한국시간";ChartMessage="데이터가 없습니다.";ReloadTradeMarkers();Changed(nameof(StockName));Changed(nameof(StockDetail));Changed(nameof(PaperSummary));Refresh();
    }
    private void SetRange()
    {if(dataset==null)throw new InvalidOperationException("먼저 데이터를 가져오세요.");if(From==null||To==null||From>To)throw new InvalidOperationException("시작일과 종료일을 확인하세요.");rangeBars=dataset.CompletedBars.Where(b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date<=To.Value.Date).ToArray();if(rangeBars.Count(b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date>=From.Value.Date)<2)throw new InvalidOperationException("선택 기간에 거래일이 2개 이상 필요합니다.");}
    private int StartIndex()=>Math.Max(0,Array.FindIndex(rangeBars,b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date>=From!.Value.Date));
    private async Task Backtest()
    {
        EnsureLegacyUnavailable();ResetConfiguredPractice();if(ChartPeriods.Intraday(ChartPeriod))throw new InvalidOperationException("틱 화면에서는 일봉 백테스트를 실행하지 않습니다. 일봉·주봉·월봉을 선택하세요.");var snapshot=Apply();SetRange();var start=StartIndex();var capital=InitialCash();var bars=rangeBars;var token=Token;
        paperSource="백테스트";paperRun="";paper=await Task.Run(()=>BarPaperAccount.Backtest(bars,snapshot,capital,start,token),token);replayIndex=-1;ChartBars=bars;UpdatePaper();
        var id="chart-run:"+Guid.NewGuid().ToString("N");var result=new SavedBacktest{Id=id,At=DateTimeOffset.UtcNow,Stock=dataset!.Stock,Settings=snapshot,DataHash=TradingStore.Hash(Serialization.Pack(bars)),Initial=capital,Cash=paper.Cash,Quantity=paper.Quantity,Profit=paper.Profit,MaxDrawdown=paper.MaxDrawdown,Fills=paper.Fills.ToArray(),Equity=paper.Equity.ToArray()};store.State(id,result);LatestResult=result;Changed(nameof(LatestResult));History.Insert(0,result);SelectedRun=result;Log("백테스트 완료 · 설정과 결과를 저장했어요.");
    }
    private Task StartPractice(){ResetConfiguredPractice();replaySettings=new ChartStrategySettings();SetRange();paperSource="모의매매";paperRun=Guid.NewGuid().ToString("N");paper=new(InitialCash());replayIndex=StartIndex();paper.Advance(rangeBars[replayIndex],replaySettings);ChartBars=rangeBars.Take(replayIndex+1).ToArray();Trades.Clear();UpdatePaper();Log("모의매매 시작 · 매수·매도 후 다음 거래일을 누르면 다음 시가로 체결돼요.");return Task.CompletedTask;}
    private void Step(){if(HasConfiguredPractice){StepConfiguredPractice();return;}if(paper==null||replaySettings==null||replayIndex<0)return;if(replayIndex>=rangeBars.Length-1){replayTimer.Stop();Replaying=false;return;}paper.Advance(rangeBars[++replayIndex],replaySettings);ChartBars=rangeBars.Take(replayIndex+1).ToArray();UpdatePaper();if(replayIndex>=rangeBars.Length-1){replayTimer.Stop();Replaying=false;Log("선택 기간의 모의매매가 끝났어요.");}Refresh();}
    private void UpdatePaper(){if(paper==null)return;PersistPaperFills();Replace(Trades,paper.Fills.AsEnumerable().Reverse());ResultInfo=$"손익 {paper.Profit:+#,##0;-#,##0;0} · 수익률 {paper.Profit/paper.Initial*100:+0.00;-0.00;0.00}% · 최대 낙폭 {paper.MaxDrawdown:0.00}%\n체결 {paper.Fills.Count:N0}건 · 수수료 {paper.Fees:N0} · 보유 {paper.Quantity:N0}주 (마지막 종가 평가)";Changed(nameof(PaperSummary));Refresh();}
    private void UpdateSectors(){var current=Sector;var stocks=ScreenWatchlist?WatchGroups.SelectMany(g=>g.Items).Select(e=>e.Stock):Catalog;Replace(Sectors,new[]{"전체"}.Concat(stocks.Select(s=>s.Sector).Where(s=>s!="분류 확인 전").Distinct().Order()));Sector=Sectors.Contains(current)?current:"전체";}
    private async Task LoadCatalog()
    {
        if(!ScreeningSizes.Contains(ScreeningSize))throw new InvalidOperationException("검사할 종목 수를 확인하세요.");
        var items=await provider.Universe(ScreeningMarketSelection,ScreeningSize,Token);Catalog.Clear();catalogRegion="";
        foreach(var item in items){Token.ThrowIfCancellationRequested();Catalog.Add(item);}
        catalogRegion=ScreeningMarketSelection;catalogSize=ScreeningSize;store.State("catalog",Catalog.ToArray());UpdateSectors();ScreenInfo=$"{Catalog.Count:N0}개 종목 준비 · {ScreeningMarketSelection} 시가총액 상위 목록 기준입니다.";
    }
    private async Task Screen()
    {
        EnsureLegacyUnavailable();var snapshot=UseConfiguredStrategy?settings.Clone():Apply();StockItem[] items;string source;
        if(UseConfiguredStrategy&&IsPipeline){Matches.Clear();await ScreenPipeline([],ConfiguredEngineSettings(),"검색순위 / 선택 관심그룹");return;}
        if(ScreenWatchlist)
        {
            var groups=WatchGroups.Where(g=>g.IncludeInScreening).ToArray();
            if(groups.Length==0)throw new InvalidOperationException("스크리닝할 관심그룹을 하나 이상 선택하세요.");
            store.State("screen-watch-groups",groups.Select(g=>g.Id).ToArray());items=SelectedScreeningStocks().ToArray();
            var raw=groups.SelectMany(g=>g.Items).Select(e=>e.Stock).Where(s=>MatchesScreeningMarket(s)&&(Sector=="전체"||s.Sector==Sector)).Count();
            source=$"관심그룹 {groups.Length:N0}개 · 중복 {raw-items.Length:N0}개 제외";
        }
        else{if(Catalog.Count==0||catalogRegion!=ScreeningMarketSelection||catalogSize!=ScreeningSize)await LoadCatalog();items=Catalog.Where(s=>MatchesScreeningMarket(s)&&(Sector=="전체"||s.Sector==Sector)).DistinctBy(s=>s.Key).ToArray();source=ScreeningMarketSelection+" 시가총액 상위 목록";}
        Matches.Clear();int failed=0,done=0;ScreenInfo=$"{source} · {items.Length:N0}종목 준비";
        if(items.Length==0){ScreenInfo="선택한 그룹·국가·분야 조건에 해당하는 종목이 없습니다.";return;}
        if(UseConfiguredStrategy){await ScreenConfigured(items,source);return;}
        foreach(var item in items){Token.ThrowIfCancellationRequested();try{var data=await provider.History(item,Token);store.State("bars:"+item.Key,data);var bars=data.CompletedBars;if(bars.Length<2)throw new InvalidOperationException("확정봉이 부족합니다.");var signal=ChartStrategies.Evaluate(bars,bars.Length-1,WithReview(snapshot.Clone(),item));if(signal.Buy)Matches.Add(new(item,bars[^1].Close,(bars[^1].Close/bars[^2].Close-1)*100,signal.Reason,bars[^1].KoreaTime));}catch(OperationCanceledException){throw;}catch(InvalidOperationException ex){failed++;Log(item.Name+" 조회 제외: "+ex.Message);}catch{failed++;Log(item.Name+" 조회 제외: 제공처 연결 또는 데이터 형식 오류");}ScreenInfo=$"{++done:N0}/{items.Length:N0}개 검사 · 조건 일치 {Matches.Count:N0}개 · 조회 실패 {failed:N0}개";await Task.Delay(180,Token);}
        ScreenInfo+=" · 최근 확정 일봉 / "+source;Log("스크리닝 완료. 종목을 선택하고 차트 열기를 누르세요.");
    }
    private async Task ConnectSelected()
    {await StopMulti();if(!IsOnline||connection==null)throw new InvalidOperationException("온라인 투자에서 실제 계좌 키를 설정하세요.");if(selected==null)throw new InvalidOperationException("먼저 거래할 종목을 검색해서 선택하세요.");if(connection.Brokerage!=Brokerage.Kiwoom&&selected.Exchange!=Exchange.KRX)throw new InvalidOperationException("선택한 증권사의 해외주식 주문은 아직 이 앱에서 지원하지 않습니다. 국내 종목을 선택하세요.");var candidate=CreateAccount(connection,selected.Exchange);try{await candidate.ConnectAsync();var next=new RemoteTradingSession(candidate,LiveSettings(),store,sessionDirectory);await next.Refresh(KoreaClock.Today);broker?.Dispose();broker=candidate;session=next;}catch{candidate.Dispose();throw;}await RefreshAccount();}
    private TradingSettings LiveSettings(){var kr=broker?.Exchange==Exchange.KRX;if(LiveOrderLimit<=0)throw new InvalidOperationException("1회 주문 한도를 입력하세요.");var s=new TradingSettings();s.Risk.MaxOrderCash=LiveOrderLimit;s.Risk.MaxSymbolCash=LiveOrderLimit*5;s.Risk.MaxTradeLoss=LiveOrderLimit*.1m;s.Risk.MaxDailyLoss=kr?500000:500;s.Risk.MaxDailyLossPercent=5;return s;}
    private async Task RefreshAccount(){if(session==null)return;await broker!.ConnectAsync();await session.Refresh(KoreaClock.Today);Replace(Orders,session.Orders);PersistLiveFills();Replace(Holdings,session.Balance!.Holdings);AccountInfo=$"{broker.Name} · 실제 계좌 {broker.MaskedAccount} · {session.Balance.Currency}\n현금 {session.Balance.Cash:N0} · 선택 시장 평가 {session.Balance.Total:N0}"+(session.Unknown?"\n미확인 주문 있음 · 신규 주문 차단":"");}
    private async Task Place(bool buy)
    {
        if(!int.TryParse(Quantity,NumberStyles.Number,CultureInfo.InvariantCulture,out var qty)||qty<=0||qty>1000000)throw new InvalidOperationException("수량은 1~1,000,000주로 입력하세요.");
        if(!IsOnline&&HasConfiguredPractice){PlaceConfiguredPractice(buy,qty);return;}if(!IsOnline){paper!.Queue(buy,qty,"직접 모의매매");Changed(nameof(PaperSummary));Log("다음 거래일 시가로 가상 주문을 예약했어요.");return;}
        if(selected==null||session==null||broker==null)throw new InvalidOperationException("실제 계좌 키를 설정하세요.");if(selected.Exchange!=broker.Exchange)await ConnectSelected();
        var reference=await broker!.CurrentPrice(selected.Symbol);var order=new OrderRequest(selected.Symbol,qty,reference,buy,selected.Exchange,OrderKind.Market);order.Validate();
        if(buy&&reference*qty>LiveOrderLimit)throw new InvalidOperationException("설정한 1회 주문 한도를 초과했습니다.");
        if(!confirm($"실제 돈으로 주문합니다\n{broker.Name} · 계좌 {broker.MaskedAccount}\n{selected.Name} · {(buy?"매수":"매도")} {qty:N0}주 · 시장가\n조회 가격 {DisplayNumber.Price(reference)} {selected.Currency}\n예상 금액 {reference*qty:N0} {selected.Currency}\n시장가 체결 가격은 달라질 수 있습니다. 이 계좌에만 주문할까요?"))return;
        var fresh=await broker.CurrentPrice(selected.Symbol);if(Math.Abs(fresh/reference-1)>.02m)throw new InvalidOperationException("확인 중 가격이 2% 이상 변했습니다. 주문을 다시 확인하세요.");if(buy&&fresh*qty>LiveOrderLimit)throw new InvalidOperationException("가격 변동으로 주문 한도를 초과했습니다.");
        var receipt=await session!.Submit(order with{Price=fresh});Log("실제 주문 접수 "+receipt.Number+" · 체결은 내역에서 확인하세요.");await RefreshAccount();
    }
    private async Task Cancel(){if(IsOnline){if(SelectedOrder==null||session==null)return;if(!confirm(SelectedOrder.Name+"의 미체결 주문을 취소할까요?"))return;await session.Cancel(SelectedOrder.Number,SelectedOrder.Symbol);await RefreshAccount();}else{paper?.Cancel();CancelConfiguredPractice();}Changed(nameof(PaperSummary));}
    private void Disconnect(){StopTicks();broker?.Dispose();broker=null;session=null;Orders.Clear();Holdings.Clear();AccountInfo="실제 계좌 미연결";Changed(nameof(ConnectedAccountText));}
    private static void Replace<T>(ObservableCollection<T> c,IEnumerable<T> values){var a=values.ToArray();c.Clear();foreach(var v in a)c.Add(v);}
    public void Dispose(){if(MultiSessionExists)throw new InvalidOperationException("StopMulti를 기다린 뒤 종료하세요.");DisposeFeatures();replayTimer.Stop();operation?.Cancel();broker?.Dispose();if(provider is IDisposable d)d.Dispose();try{tickWrites.GetAwaiter().GetResult();}finally{if(ownsStore)store.Dispose();}}
}
