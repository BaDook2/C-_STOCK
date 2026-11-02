using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private Action<string> alert = _=>{};
    private Func<KiwoomCredentials,Exchange,KiwoomBroker> createBroker=(c,e)=>new(c,e,live:true);
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource apiRequests=new();
    private DispatcherTimer? marketTimer,captureTimer;
    public static TimeSpan LiveChartRefreshInterval=>TimeSpan.FromSeconds(1);
    private bool marketRefreshing,disposed,apiPaused,liveChartDirty;
    private string marketStatus="시장 현황을 열면 5초마다 조회합니다.",chartPeriod="일봉";
    private IReadOnlyList<PriceBar> visibleDaily=[];
    private readonly List<PriceBar> tickBars=[];
    private CancellationTokenSource? tickCancellation;
    private Task? tickTask;
    private string tickStock="";
    private readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
    private TradingSettings tickSettings=new();
    public ObservableCollection<SettingField> TickFields {get;}=[];
    public string TickStatus {get;private set;}="실제 체결·호가가 필요한 전략입니다. 키 설정 후 틱을 선택하세요.";
    public string[] ChartPeriodOptions {get;}=ChartPeriods.Intervals.Select(n=>n+"틱").Concat(ChartPeriods.Intervals.Select(n=>n+"분")).Concat(["일봉","주봉","월봉"]).ToArray();
    public bool IsTick=>ChartPeriods.Intraday(ChartPeriod);
    public bool TickSelected {get=>IsTick;set{if(value)ChartPeriod="1틱";}}
    public bool DailySelected {get=>ChartPeriod=="일봉";set{if(value)ChartPeriod="일봉";}}
    public bool WeeklySelected {get=>ChartPeriod=="주봉";set{if(value)ChartPeriod="주봉";}}
    public bool MonthlySelected {get=>ChartPeriod=="월봉";set{if(value)ChartPeriod="월봉";}}
    public string ChartPeriod {get=>chartPeriod;set{if(value=="틱")value="1틱";if(chartPeriod==value||Busy||Replaying||!ChartPeriodOptions.Contains(value))return;if(ChartPeriods.Intraday(value)&&!(value.EndsWith("분")?CanUseMinutes:CanUseIntraday)){TickStatus=IntradayHelp;Changed(nameof(TickStatus));Changed(nameof(ChartMessage));return;}chartPeriod=value;Changed();Changed(nameof(IsTick));Changed(nameof(TickSelected));Changed(nameof(DailySelected));Changed(nameof(WeeklySelected));Changed(nameof(MonthlySelected));Changed(nameof(StockDetail));Changed(nameof(SelectedTickInterval));Changed(nameof(SelectedMinuteInterval));Changed(nameof(ChartMessage));ChartBars=visibleDaily;if(ChartPeriods.Intraday(value))BeginIntraday();else if(IsOnline&&credentials!=null)StartTicks();else StopTicks();Refresh();}}
    public string MarketStatus {get=>marketStatus;private set{marketStatus=value;Changed();}}
    public MarketQuote? Kospi {get;private set;}
    public MarketQuote? Kosdaq {get;private set;}
    public MarketQuote? StockQuote {get;private set;}
    public string AverageVolume {get;private set;}="종목 데이터를 가져오면 최근 20거래일 평균을 표시합니다.";
    public ObservableCollection<SavedBacktest> History {get;}=[];
    public SavedBacktest? LatestResult {get;private set;}
    private SavedBacktest? selectedRun;
    public SavedBacktest? SelectedRun {get=>selectedRun;set{selectedRun=value;Changed();Changed(nameof(HasOversoldHistory));Changed(nameof(DisplayTradeMarkers));if(Actions.TryGetValue("낙주기록상세",out var command))command.Refresh();}}
    public string RiskStockName=>selected?.Name??"먼저 종목을 선택하세요";
    public bool RiskReviewed {get=>selected!=null&&store.State<string>("review:"+selected.Key)==KoreaClock.Today.ToString();set{if(selected!=null)store.State("review:"+selected.Key,value?KoreaClock.Today.ToString():"");Changed();if(IsTick)StartTicks();}}
    public bool RiskExcluded {get=>selected!=null&&store.State<bool>("exclude:"+selected.Key);set{if(selected!=null)store.State("exclude:"+selected.Key,value);Changed();if(IsTick)StartTicks();}}
    private ChartStrategySettings WithReview(ChartStrategySettings s,StockItem? stock){s.RiskReviewed=stock!=null&&store.State<string>("review:"+stock.Key)==KoreaClock.Today.ToString();s.Excluded=stock!=null&&store.State<bool>("exclude:"+stock.Key);return s;}
    private void InitializeFeatures(Action<string>? popup,Func<KiwoomCredentials,Exchange,KiwoomBroker>? factory)
    {
        captureTimer=new DispatcherTimer{Interval=LiveChartRefreshInterval};captureTimer.Tick+=(_,_)=>{FlushCapturedTicks();RefreshLiveChart();};captureTimer.Start();alert=popup??(_=>{});if(factory!=null)createBroker=factory;
        foreach(var row in store.States("chart-run:"))try{var r=Serialization.Unpack<SavedBacktest>(row.Json);r.Id=row.Key;History.Add(r);}catch{ /* A damaged legacy record must not hide other records. */ }
        // History is retained, but no previous run is selected automatically on startup.
        SelectedRun=null;
        InitializeWatchlist();Replace(VisibleIndices,domesticIndices);
        tickSettings=store.State<TradingSettings>("image-tick-settings")??new TradingSettings{Scalping=new(){Enabled=false},Box=new(){Enabled=false},Rebound=new(){Enabled=false}};
        InitializeRecurring();InitializeOversold();
        Add("틱설정저장",()=>{foreach(var f in TickFields)f.Apply();tickSettings.Validate();store.State("image-tick-settings",tickSettings);if(ChartPeriods.Intraday(ChartPeriod))StartTicks();Status="틱 전략 조건을 저장했습니다. 실제 계좌에 자동 주문하지 않습니다.";return Task.CompletedTask;},()=>!Busy);
        marketTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};marketTimer.Tick+=async(_,_)=>{if(ActiveTab==5)await RefreshMarket();if(IsOnline&&ShowLiveTradeMarkers)await RefreshObservedTrades();};marketTimer.Start();
    }
    private CancellationToken ApiToken=>apiRequests.Token;
    private void ResumeApiRequests()
    {
        if(!apiPaused||disposed)return;apiPaused=false;marketTimer?.Start();
        if(dataset!=null)StartDailyQuotes(dataset.Stock);
    }
    private void SuspendApiRequests()
    {
        apiPaused=true;var current=apiRequests;apiRequests=new();current.Cancel();current.Dispose();marketTimer?.Stop();
        StopDailyQuotes();StopTicks();RawTicks?.Dispose();RawTicks=null;Changed(nameof(RawTicks));
    }
    private void BuildTickFields(){TickFields.Clear();foreach(var (name,target) in new (string,object)[]{("① 호가 스캘핑",tickSettings.Scalping),("② 틱 박스권",tickSettings.Box),("③ 틱 급락 반등",tickSettings.Rebound),("지지선 · 실제 틱 매물대",tickSettings.Analysis),("기존 공통 리스크",tickSettings.Risk),("기존 주문 모델",tickSettings.Execution)})foreach(var p in target.GetType().GetProperties().Where(p=>p.GetCustomAttribute<SettingAttribute>()!=null))TickFields.Add(new(target,p,true){GroupLabel=name});}
    private void ChangeMode(bool value)
    {online=value;multiTrading?.ResetToMock();ResetConfiguredPractice();if(!value&&ChartPeriods.Intraday(chartPeriod)){chartPeriod="일봉";ChartBars=visibleDaily;Changed(nameof(ChartPeriod));Changed(nameof(DailySelected));Changed(nameof(IsTick));Changed(nameof(SelectedTickInterval));Changed(nameof(SelectedMinuteInterval));Changed(nameof(ChartMessage));}paper=null;replayIndex=-1;Trades.Clear();if(!value&&WorkspaceToolTab==0)WorkspaceToolTab=1;else if(value&&WorkspaceToolTab==1)WorkspaceToolTab=0;Changed(nameof(IsOnline));Changed(nameof(IsOffline));Changed(nameof(ScreenDaily));Changed(nameof(ScreenCustomBars));Changed(nameof(BacktestDaily));Changed(nameof(BacktestCustomBars));Changed(nameof(BacktestBarDescription));Changed(nameof(ModeDescription));Changed(nameof(OrderHeading));Changed(nameof(ConnectedAccountText));Changed(nameof(PaperSummary));ReloadTradeMarkers();Refresh();if(dataset!=null)StartDailyQuotes(dataset.Stock);}
    private Task EnterOnline()
    {
        BrokerConnection? saved=null;
        try{saved=defaultConnectionLoader?.Invoke();}catch{ /* A damaged saved key falls back to the normal key dialog. */ }
        return ConfigureKeys(true,saved);
    }
    /// <summary>실전 서버의 인증과 계좌 확인이 성공한 뒤에만 온라인 모드를 허용합니다.</summary>
    public async Task PrepareCredentialReset(Brokerage? affected)
    {
        if(affected!=null&&connection?.Brokerage!=affected)return;
        // Block before awaiting feed/cancellation completion. Durable pending-order records remain.
        liveBatch?.RequestStop(null,"API 키 초기화 · 신규 주문 차단");
        var current=session;var revoke=current?.Revoke()??Task.CompletedTask;
        try{await StopMulti();}
        finally{await revoke;replayTimer.Stop();Replaying=false;StopTicks();Disconnect();credentials=null;connection=null;ChangeMode(false);}
    }
    private async Task ConfigureKeys(bool enterOnline,BrokerConnection? supplied=null)
    {
        if(session is {} oldSession)await oldSession.Revoke();await StopMulti();replayTimer.Stop();Replaying=false;StopTicks();Disconnect();ChangeMode(false);credentials=null;connection=null;
        var legacy=supplied==null&&connectionPrompt==null?prompt():null;
        var c=supplied??(connectionPrompt!=null?connectionPrompt():legacy==null?null:new BrokerConnection{Brokerage=Brokerage.Kiwoom,AppKey=legacy.AppKey,Secret=legacy.SecretKey});
        if(c==null){Changed(nameof(IsOnline));Changed(nameof(IsOffline));return;}
        IAccountBroker? candidate=null;
        try
        {
            candidate=CreateAccount(c,c.Brokerage==Brokerage.Kiwoom?selected?.Exchange??Exchange.KRX:Exchange.KRX);
            if(!candidate.IsLive)throw new LiveKeyRejectedException();
            await candidate.ConnectAsync();
            if(connectionPrompt!=null)await candidate.GetBalanceAsync();
            if(apiPaused)throw new OperationCanceledException("긴급중지로 계좌 연결을 취소했습니다.");
            connection=c;credentials=c.Brokerage==Brokerage.Kiwoom?c.Kiwoom():null;broker=candidate;candidate=null;
            if(enterOnline){ChangeMode(true);session=new(broker,LiveSettings(),store,sessionDirectory);await RefreshAccount();}
            else AccountInfo=broker.Name+" · 실제 계좌 키 확인 완료 · 오프라인 테스트 유지";
            Status=broker.Name+" 실제 계좌 키를 확인했습니다.";
            try{saveConnection?.Invoke(c);if(saveConnection!=null)Status+=" 키를 암호화해 저장했어요.";}catch{Status+=" 키 저장에는 실패했습니다. 다음 실행 때 다시 입력하세요.";alert(Status);}
            if(ChartPeriods.Intraday(ChartPeriod))StartTicks();
            Changed(nameof(ConnectedAccountText));
        }
        catch(LiveKeyRejectedException){Disconnect();credentials=null;connection=null;ChangeMode(false);Status="실제 계좌 키가 아닙니다.";alert(Status);}
        catch(InvalidOperationException ex){Disconnect();credentials=null;connection=null;ChangeMode(false);Status=ex.Message;alert(Status);}
        catch{Disconnect();credentials=null;connection=null;ChangeMode(false);Status="실제 계좌 연결을 확인하지 못했습니다. 인터넷 연결·등록 IP·키 권한을 확인하세요.";alert(Status);}
        finally{candidate?.Dispose();Changed(nameof(IsOnline));Changed(nameof(IsOffline));Changed(nameof(ConnectedAccountText));}
    }
    private IAccountBroker CreateAccount(BrokerConnection c,Exchange market)
    {
        c.Validate();if(accountFactory!=null)return accountFactory(c,market);
        return c.Brokerage switch{
            Brokerage.Kiwoom=>createBroker(c.Kiwoom(),market),
            Brokerage.KoreaInvestment=>new KisBroker(new Credentials{AppKey=c.AppKey,AppSecret=c.Secret,Account=c.Account,Product=c.Product},live:true),
            Brokerage.Meritz=>new MeritzBroker(c),
            Brokerage.Toss=>new TossBroker(c),
            _=>throw new InvalidOperationException("이 증권사는 아직 연결을 지원하지 않습니다.")
        };
    }
    public async Task RefreshMarket()
    {
        if(disposed||apiPaused||marketRefreshing||provider is not IMarketOverviewData market)return;
        marketRefreshing=true;var item=selected??dataset?.Stock;int failures=0;var marketAtStart=DomesticMarket;
        async Task Read(Func<Task<MarketQuote>> read,Action<MarketQuote> set){try{var q=await read();if(!disposed)set(q);}catch(OperationCanceledException){}catch{failures++;}}
        try
        {
            await Task.WhenAll(VisibleIndices.ToArray().Select(card=>RefreshIndexCard(card,market)).Append(item==null?Task.CompletedTask:Read(()=>market.Quote(item,ApiToken),q=>{if((selected??dataset?.Stock)?.Key==item.Key){StockQuote=q;Changed(nameof(StockQuote));}})));
            if(disposed)return;
            var d=dataset;if(d?.Bars.Length>0){var b=d.CompletedBars.TakeLast(20).ToArray();if(b.Length==0)return;AverageVolume=$"{d.Stock.Name} · 최근 {b.Length:N0}거래일 일평균 {b.Average(x=>x.Volume):N0}주 · 기준 {b[^1].KoreaTime}";Changed(nameof(AverageVolume));}
            failures+=VisibleIndices.Count(c=>c.QuoteStatus!="");MarketStatus=$"{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)):HH:mm:ss} 조회 · 5초마다 갱신 · "+(failures>0?$"{failures}개 조회 실패, 이전 값은 각 시세시각을 확인하세요.":"제공처 지연 가능 · 시세시각은 한국시간");
        }
        finally{marketRefreshing=false;if(!disposed&&marketAtStart!=DomesticMarket&&ActiveTab==5)_=RefreshMarket();}
    }
    private void StopTicks(){tickCancellation?.Cancel();liveChartDirty=false;try{FlushCapturedTicks();}catch{Status="실제 체결 저장에 실패했습니다.";}if(tickBars.Count>0&&tickStock!="")try{store.State("live-ticks:"+tickStock,tickBars.ToArray());}catch{Status="수신한 틱의 로컬 저장에 실패했습니다.";}}
    private void RefreshLiveChart()
    {
        if(!liveChartDirty||disposed||!ChartPeriods.Intraday(ChartPeriod))return;
        liveChartDirty=false;ChartBars=visibleDaily;
    }
    private void StartTicks()
    {
        StopTicks();if(disposed||apiPaused)return;
        var item=selected??dataset?.Stock;
        if(item==null){TickStatus="먼저 종목을 선택하세요.";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));return;}
        if(tickStock!=item.Key){tickBars.Clear();tickStock=item.Key;ChartBars=visibleDaily;}
        if(tickBars.Count==0)try{tickBars.AddRange(store.State<PriceBar[]>("live-ticks:"+item.Key)??[]);ChartBars=visibleDaily;}catch{tickBars.Clear();}
        if(MultiSessionExists){TickStatus="전략 다중매매의 공용 실제 피드를 차트에 함께 표시합니다.";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));return;}
        if(credentials==null||!IsOnline||broker is not KiwoomBroker){TickStatus=connection!=null?"현재 실시간 틱 수신은 키움증권 연결에서 지원합니다. 조회된 과거 틱은 계속 볼 수 있어요.":tickBars.Count==0?"틱은 실제 체결 데이터가 필요합니다. 실제 계좌 키를 설정하세요. 일봉으로 틱을 만들지 않습니다.":$"이전에 수신한 실제 틱 · 마지막 {tickBars[^1].KoreaTime} · 새 체결 수신은 실제 계좌 키가 필요합니다.";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));return;}
        tickCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,ApiToken);var source=tickCancellation;
        var snapshot=UseConfiguredStrategy?ConfiguredEngineSettings():Serialization.Unpack<TradingSettings>(Serialization.Pack(tickSettings));var auth=credentials;
        tickTask=ReadTicks(item,auth,snapshot,source);
    }
    /// <summary>다중 전략이 소유한 단일 WebSocket을 선택 종목 차트와 공유해 중복 구독을 피한다.</summary>
    private void ObserveMultiMarket(MarketEvent e)
    {
        if(disposed||e.Kind!=EventKind.Trade||e.TradePrice is not decimal price)return;
        var item=selected??dataset?.Stock;if(item?.Key!=e.Key)return;
        ObserveDailyTrade(item,e);
        if(tickStock!=item.Key){tickBars.Clear();tickStock=item.Key;}
        tickBars.Add(new(e.ExchangeTime,price,price,price,price,e.TradeSize??0));if(tickBars.Count>2000)tickBars.RemoveAt(0);
        if(tickBars.Count%64==0)try{store.State("live-ticks:"+item.Key,tickBars.ToArray());}catch{ /* 원본 이벤트는 별도 batch로 계속 저장한다. */ }
        if(ChartPeriods.Intraday(ChartPeriod))liveChartDirty=true;
        TickStatus=$"{e.ExchangeTime.ToOffset(TimeSpan.FromHours(9)):HH:mm:ss} · 다중 전략 공용 실제 체결 {tickBars.Count:N0}틱";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));
    }
    private async Task ReadTicks(StockItem item,KiwoomCredentials auth,TradingSettings snapshot,CancellationTokenSource source)
    {
        var coverage=new Stock.Core.Research.Oversold.TickCoverageTracker();
        using var tickBroker=createBroker(auth,item.Exchange);
        await using var feed=new KiwoomMarketDataProvider(tickBroker.GetTokenAsync,[new(item.Symbol,item.Exchange)],snapshot.Data,message=>dispatcher.BeginInvoke(()=>{if(!disposed&&source==tickCancellation){TickStatus=message+" · 실제 체결 대기";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));}}),live:true);
        snapshot.Advanced=null;snapshot.Pipeline=null;snapshot.Scalping.Enabled=snapshot.Box.Enabled=snapshot.Rebound.Enabled=false;
        var pipelineEvaluator=new Stock.Core.Pipeline.PipelineEvaluator();var nextRankingRefresh=DateTimeOffset.MinValue;var riskSnapshot=WithReview(settings.Clone(),item);var context=new MarketContext(new(item.Symbol,item.Exchange),snapshot);ITradingStrategy[] strategies=[];var quality=new DataQualityGate(snapshot.Data,snapshot.Execution);
        try
        {
            TickStatus="실제 틱 연결 중 · 체결이 발생하면 표시합니다.";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));
            await foreach(var e in feed.Stream(source.Token))
            {
                if(source.IsCancellationRequested)break;if(e.Key!=context.Instrument.Key)continue;
                await dispatcher.InvokeAsync(()=>{capturedTicks.Add(e);coverage.Observe(e);if(capturedTicks.Count>=128){FlushCapturedTicks();if(coverage.Current is {} verified)store.State("verified-tick-coverage:"+item.Key,verified);}});if(!quality.Accept(e,true,out _))continue;
                if(snapshot.Pipeline is {} pipelineConfig&&e.ExchangeTime>=nextRankingRefresh)
                {
                    var rows=await dispatcher.InvokeAsync(()=>{var watch=WatchGroups.Where(g=>g.IncludeInScreening).SelectMany(g=>g.Items).Select(x=>x.Stock.Key).ToHashSet();return pipelineObservations.Where(r=>r.Source==pipelineConfig.Universe.RankingSource.ToString()).Select(r=>r with{IsWatchlist=watch.Contains(r.Symbol)}).ToArray();});
                    var ranking=Stock.Core.Pipeline.RankingProviderFactory.Create(pipelineConfig.Universe.RankingSource,rows);
                    var latest=await ranking.ReadAsync(e.ExchangeTime,source.Token);snapshot.PipelineObservations=latest.ToArray();nextRankingRefresh=e.ExchangeTime.AddSeconds(pipelineConfig.Universe.UniverseRefreshIntervalSeconds);
                }
                context.Add(e);
                await dispatcher.InvokeAsync(()=>{if(disposed||source!=tickCancellation||(selected??dataset?.Stock)?.Key!=item.Key)return;ObserveDailyTrade(item,e);if(!ChartPeriods.Intraday(ChartPeriod))return;
                    if(e.Kind==EventKind.Trade&&e.TradePrice is decimal price){tickBars.Add(new(e.ExchangeTime,price,price,price,price,e.TradeSize??0));if(tickBars.Count>2000)tickBars.RemoveAt(0);liveChartDirty=true;}
                    if(snapshot.Pipeline is {} config)
                    {
                        var evaluation=pipelineEvaluator.Evaluate(context,config,snapshot.PipelineObservations);ShowPipelineEvaluation(evaluation);
                        TickStatus="Pipeline · "+(evaluation.Passed?$"후보 {evaluation.Score:N0}점 · 신호만 표시":evaluation.Stages.First(s=>!s.Passed).Reason);Changed(nameof(TickStatus));Changed(nameof(ChartMessage));return;
                    }
                    var signals=strategies.Where(s=>s.Name!="급락 반등"||riskSnapshot.RiskReviewed).Select(s=>s.Evaluate(context)).Where(s=>s.Buy==true&&!riskSnapshot.Excluded).Select(s=>s.Strategy).ToArray();TickStatus=$"{e.ExchangeTime.ToOffset(TimeSpan.FromHours(9)):HH:mm:ss} · 실제 체결 {tickBars.Count:N0}틱 · "+(signals.Length>0?string.Join(" · ",signals)+" 조건 일치 (주문하지 않음)":"시세 표시 전용 · 일봉 전략 자동 실행 없음");Changed(nameof(TickStatus));Changed(nameof(ChartMessage));
                });
            }
        }
        catch(OperationCanceledException){}
        catch{if(!disposed&&source==tickCancellation){TickStatus="틱 연결이 끊겼습니다. 키 권한·시장 운영시간을 확인하고 주기를 다시 선택하세요.";Changed(nameof(TickStatus));Changed(nameof(ChartMessage));}}
        finally{source.Dispose();if(source==tickCancellation)tickCancellation=null;}
    }
    private void DisposeFeatures(){disposed=true;apiPaused=true;StopDailyQuotes();RawTicks?.Dispose();captureTimer?.Stop();marketTimer?.Stop();apiRequests.Cancel();lifetime.Cancel();StopTicks();}
}
