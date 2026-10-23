using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;
public sealed record MockBatchRecord(string Id,DateTimeOffset Started,StockItem[] Stocks,DateOnly From,DateOnly To,Dictionary<string,string> Results,string Status);
public sealed partial class WorkspaceViewModel
{
    private readonly Func<IAccountBroker,IReadOnlyList<Instrument>,DataSettings,Action<string>,IMarketDataProvider>? multiFeedFactory;
    private readonly Func<DateTimeOffset> multiClock;
    private bool IsMultiOrder(string number)=>session!=null&&store.State<LiveBatchJournal>("live-batch:"+session.Scope)?.Orders.Any(o=>o.Number==number)==true;
    private MultiTradeViewModel? multiTrading;private LiveStrategyBatch? liveBatch;private RealtimePaperStrategyBatch? realtimePaperBatch;private CancellationTokenSource? multiStop;private Task? multiTask;private bool mockBatchLocked,multiPaperActive;
    public bool MultiSessionExists=>multiTask is {IsCompleted:false}||mockBatchLocked;
    public string MultiStatus {get;private set;}="모의는 과거 백테스트이며, 온라인에서 실시간을 함께 체크하면 실제 시세로 가상 체결합니다. 실계좌 실행은 검증된 시점별 자료와 연속 체결 데이터가 필요합니다.";
    public bool CanOpenMulti=>CanConfigure&&(IsOffline||IsOnline&&session!=null);
    public bool CanOpenRealtimePaper=>CanConfigure&&IsOnline;
    public string VirtualTradingHelp=>IsOnline?RealtimeMockSupported?"실제 시세를 사용하지만 주문은 가상 계좌에서만 체결됩니다.":"현재 연결 계좌는 실시간 가상 전략매매 피드를 지원하지 않습니다.":"온라인 투자로 키움 국내 기본계좌를 연결하면 실시간 가상 전략매매를 시작할 수 있습니다.";
    public MultiTradeViewModel MultiTrading=>multiTrading??=new(provider,store,StartMulti,StopMulti,ReviewMulti,LoadMultiPreset,()=>IsOffline?"오프라인 · 과거 모의 백테스트":broker==null?"연결된 실제 시세 계정이 없습니다.":broker.Name+" · "+broker.MaskedAccount+" · "+(MultiSupported?"실시간 모의 또는 선택한 국내 실계좌":"이 계좌의 자동매매 피드/체결 대사는 미지원"),()=>MultiSupported,()=>IsOffline,StartMockMulti,()=>LatestScreening?.FinalCandidates.AsEnumerable()??[],StartRealtimeMockMulti,()=>RealtimeMockSupported,()=>Presets);
    private bool MultiSupported=>IsOnline&&broker?.Id=="kiwoom"&&broker.IsLive&&broker.Exchange==Exchange.KRX&&broker is IExecutionHistoryBroker&&session!=null;
    private bool RealtimeMockSupported=>IsOnline&&credentials!=null&&broker?.Id=="kiwoom"&&broker.IsLive&&broker.Exchange==Exchange.KRX&&session!=null;
    public Task RunSelectedMockBatch()=>Run(StartMockMulti);
    private async Task StartMockMulti()
    {
        if(!MultiTrading.UseMock||mockBatchLocked)throw new InvalidOperationException("이전 모의 실행을 전체 중지한 뒤 다시 시작하세요.");
        var selections=MultiTrading.SelectedStrategies();if(selections.Length is <1 or >20)throw new InvalidOperationException("실행할 종목을 1~20개 체크하세요.");
        if(selections.Any(s=>s.Stock.Exchange!=Exchange.KRX))throw new InvalidOperationException("국내 낙주 전략은 국내 일반주식에서만 백테스트할 수 있습니다.");
        if(From==null||To==null||From>To)throw new InvalidOperationException("백테스트 기간을 확인하세요.");
        var facts=oversoldFacts.Snapshot();var first=DateOnly.FromDateTime(From.Value);var last=DateOnly.FromDateTime(To.Value);var token=Token;var stocks=selections.Select(s=>s.Stock).ToArray();
        var key="mock-batch:"+Guid.NewGuid().ToString("N");var results=new Dictionary<string,string>();store.State(key,new MockBatchRecord(key,DateTimeOffset.UtcNow,stocks,first,last,results,"진행 중"));
        try
        {
            foreach(var selection in selections)
            {
                var stock=selection.Stock;var config=LoadMultiOversoldPreset(selection.Preset);
                token.ThrowIfCancellationRequested();MultiStatus=$"모의 백테스트 {results.Count+1:N0}/{stocks.Length:N0} · {stock.Name}";Changed(nameof(MultiStatus));
                try
                {
                    var data=await provider.History(stock,token);var result=await Task.Run(()=>new Stock.Core.Research.Oversold.OversoldBacktest().Run(data,config,first,last,facts,token),token);
                    store.State("oversold-facts-snapshot:"+result.FactsHash,facts);store.State("oversold-bars-snapshot:"+result.DataHash,data.CompletedBars);store.State(result.Id,result);var history=result.ToHistory();store.State(history.Id,history);History.Insert(0,history);
                    results[stock.Key]=$"매매 {result.Trades.Count:N0}건 · 최종 평가 {result.Performance.FinalEquity:N0}원";
                }
                catch(OperationCanceledException){throw;}
                catch(InvalidOperationException ex){results[stock.Key]="자료/조건 확인: "+ex.Message;}
                catch{results[stock.Key]="시세 조회 또는 계산 실패";}
                store.State(key,new MockBatchRecord(key,DateTimeOffset.UtcNow,stocks,first,last,new(results),"진행 중"));MultiTrading.ApplyMock(MultiStatus,results,false);
            }
            mockBatchLocked=true;MultiStatus=$"모의 백테스트 완료 · {stocks.Length:N0}종목 · 실제 주문 없음";store.State(key,new MockBatchRecord(key,DateTimeOffset.UtcNow,stocks,first,last,new(results),"완료"));store.State("mock-batch-latest",key);MultiTrading.ApplyMock(MultiStatus,results,true);Changed(nameof(MultiStatus));Refresh();
        }
        catch{store.State(key,new MockBatchRecord(key,DateTimeOffset.UtcNow,stocks,first,last,new(results),"중단 · 완료 종목 기록 보존"));throw;}
    }
    private TradingSettings LoadMultiPreset(string name,decimal equity)
    {
        var cfg=LoadMultiOversoldPreset(name);if(!MultiTrading.UseMock&&!cfg.UseEmergencyStop)throw new InvalidOperationException("실계좌 실행 전 낙주 비상 손절을 켜세요.");
        var a=new StrategyConfiguration{Enabled=true,StrategyType=StrategyType.Box}.Resolve();
        a.Common.Investment=Math.Min(cfg.PlannedInvestment,LiveOrderLimit);
        a.Common.TradingStart="09:00";a.Common.TradingEnd="15:20";
        a.Risk.MaxPositionPercent=100;a.Risk.MaxOpenPositions=20;a.Risk.DailyLossLimit=Math.Min(equity,1000000000000m);
        a.Execution.OrderType=OrderKind.Limit;a.Execution.LimitOrderOffsetTicks=0;a.Execution.OrderTimeoutMilliseconds=20000;
        a.Execution.Commission=cfg.CommissionPercent;a.Execution.Tax=cfg.TaxPercent;
        var s=new TradingSettings();s.Risk.AccountCapital=Math.Max(1,equity);s.Risk.MaxOrderCash=LiveOrderLimit;s.Risk.MaxSymbolCash=Math.Max(LiveOrderLimit,cfg.PlannedInvestment);
        s.Risk.StopMode=DistanceMode.Percent;s.Risk.StopValue=Math.Abs(cfg.EmergencyStopLossPercent);
        s.Risk.MaxTradeLoss=LiveOrderLimit*(Math.Abs(cfg.EmergencyStopLossPercent)+cfg.CommissionPercent)/100+1;
        s.Risk.MaxTradeLossPercent=Math.Min(100,Math.Abs(cfg.EmergencyStopLossPercent)+cfg.CommissionPercent+1);
        s.Risk.MaxDailyLoss=Math.Min(equity,1000000000000m);s.Execution.StaleSeconds=10;s.Execution.Retry=false;s.Risk.FlattenOnStop=false;
        s.Filters.ForEach(f=>f.Enabled=false);
        return StrategyPresetMapper.ToEngine(a,s);
    }
    private OversoldConfig LoadMultiOversoldPreset(string name)
    {
        var cfg=store.State<OversoldConfig>(OversoldPresetPrefix+name)??(string.Equals(name,activeOversoldPreset,StringComparison.Ordinal)?oversoldConfig.Clone():null)??throw new InvalidOperationException("등록 전략을 찾을 수 없습니다: "+name);
        cfg.Validate();return cfg.Clone();
    }
    private async Task StartRealtimeMockMulti()
    {
        if(!RealtimeMockSupported||credentials==null||!MultiTrading.UseMock||!MultiTrading.UseRealtime)throw new InvalidOperationException("키움 국내 실제 시세 연결과 모의·실시간 체크를 확인하세요.");
        if(MultiSessionExists)throw new InvalidOperationException("이전 다중매매를 전체 중지한 뒤 시작하세요.");
        if(ScreenDaily)throw new InvalidOperationException("전략 설정의 데이터 기준에서 틱 또는 분을 선택하세요. 일봉은 실시간 전략 실행 기준으로 사용할 수 없습니다.");
        var spec=BarSpecification.Parse(ScreenBarValue,ScreenBarUnit switch{"틱"=>BarUnit.Tick,"분"=>BarUnit.Minute,_=>throw new InvalidOperationException("실시간 실행은 틱 또는 분 기준만 지원합니다.")});
        var facts=oversoldFacts.Snapshot();
        var now=multiClock();var today=DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(9)).DateTime);
        if(!facts.Sessions.Any(s=>s.Date==today&&now>=s.Open&&now<s.Close))throw new InvalidOperationException("오늘의 검증된 한국 시장 세션이 없습니다.");
        var current=session!;await Task.Run(()=>current.Refresh(today));var equity=Math.Max(1,current.Balance?.Total??1);var plan=MultiTrading.CreatePlan(equity);
        if(plan.Items.Length==0)throw new InvalidOperationException("실행할 종목을 하나 이상 체크하세요.");
        var configured=new List<LiveBatchItem>();
        foreach(var item in plan.Items)
        {
            if(!HistoricalFacts.Tradable(facts.Status(item.Stock.Symbol,today,now))||facts.Risk(item.Stock.Symbol,today,now)!=false)throw new InvalidOperationException(item.Stock.Name+": 오늘의 일반주식·거래정지·관리·위험 상태를 확인할 수 없습니다.");
            var warmup=await ScreeningBars(item.Stock,spec,facts,now,Token);
            if(warmup.Length==0)throw new InvalidOperationException(item.Stock.Name+": 검증된 원본 체결로 만든 준비 봉이 없습니다.");
            if(warmup.Length>20000)throw new InvalidOperationException(item.Stock.Name+": 준비 봉이 20,000개를 넘습니다. 더 큰 틱/분 기준을 선택하세요.");
            configured.Add(item with{Oversold=new(LoadMultiOversoldPreset(item.Preset),spec,warmup,facts)});
        }
        plan=plan with{Items=configured.ToArray()};
        if(!ReferenceEquals(current,session)||!IsOnline||!MultiTrading.UseMock||!MultiTrading.UseRealtime||!RealtimeMockSupported)throw new InvalidOperationException("계좌 연결 또는 실행 모드가 변경되었습니다.");
        if(broker is not KiwoomBroker&&multiFeedFactory==null)throw new InvalidOperationException("이 연결의 실제 피드 어댑터를 확인할 수 없습니다.");
        StopTicks();liveBatch=null;var active=new RealtimePaperStrategyBatch(store,multiClock);realtimePaperBatch=active;active.FillRecorded+=_=>dispatcher.BeginInvoke(()=>{if(!disposed&&ReferenceEquals(active,realtimePaperBatch))ReloadTradeMarkers();});active.Start(plan);multiPaperActive=true;ShowTradeMarkers=true;
        multiStop=new();var token=multiStop.Token;var instruments=plan.Items.Select(i=>new Instrument(i.Stock.Symbol,i.Stock.Exchange)).ToArray();
        Action<string> statusChanged=m=>{if(m.Contains("단절")||m.Contains("오류")||m.Contains("버퍼"))active.FeedFault(m);};
        IMarketDataProvider feed;
        try{feed=multiFeedFactory?.Invoke(broker!,instruments,plan.Items[0].Settings.Data,statusChanged)??new KiwoomMarketDataProvider(((KiwoomBroker)broker!).GetTokenAsync,instruments,plan.Items[0].Settings.Data,statusChanged,live:true);}
        catch{active.Stop();multiPaperActive=false;multiStop.Dispose();multiStop=null;throw;}
        MultiStatus="실시간 모의매매 실행 중 · 실제 주문 없음";Changed(nameof(MultiStatus));MultiTrading.Apply(active.Snapshot());
        multiTask=Task.Run(async()=>
        {
            async Task Read()
            {
                var batch=new List<MarketEvent>();try{await foreach(var e in feed.Stream(token)){active.OnMarket(e);batch.Add(e);if(e.Kind==EventKind.Trade)_=dispatcher.BeginInvoke(()=>ObserveMultiMarket(e));if(batch.Count>=128){store.InsertEvents(batch);batch.Clear();}}if(!token.IsCancellationRequested)active.FeedFault("수신 종료");}
                catch(OperationCanceledException)when(token.IsCancellationRequested){}catch{active.FeedFault("연결 또는 시세 오류");}
                finally{try{if(batch.Count>0)store.InsertEvents(batch);}catch{active.FeedFault("원본 체결 저장 실패");}await feed.DisposeAsync();}
            }
            async Task Poll()
            {
                using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));try{while(await timer.WaitForNextTickAsync(token)){await active.Pulse();var snapshot=active.Snapshot();_ = dispatcher.BeginInvoke(()=>{if(disposed||!ReferenceEquals(active,realtimePaperBatch))return;MultiTrading.Apply(snapshot);MultiStatus=(snapshot.Running?"실시간 모의 실행 중 · ":"실시간 모의 중지 · ")+snapshot.Status;Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();});}}
                catch(OperationCanceledException)when(token.IsCancellationRequested){}catch{active.FeedFault("처리 오류: 새 가상 주문 중지");}
            }
            await Task.WhenAll(Read(),Poll());
        });Refresh();
    }
    private async Task StartMulti()
    {
        if(!MultiSupported||credentials==null||MultiTrading.UseMock)throw new InvalidOperationException("선택한 키움 국내 실계좌와 모의 해제를 확인하세요.");
        if(MultiSessionExists||session!.AutomationClaimed)throw new InvalidOperationException("이전 다중매매를 전체 중지한 뒤 시작하세요.");
        if(ScreenDaily)throw new InvalidOperationException("전략 설정의 데이터 기준에서 틱 또는 분을 선택하세요. 일봉으로 실시간 주문하지 않습니다.");
        var spec=BarSpecification.Parse(ScreenBarValue,ScreenBarUnit switch{"틱"=>BarUnit.Tick,"분"=>BarUnit.Minute,_=>throw new InvalidOperationException("실시간 실행은 틱 또는 분 기준만 지원합니다.")});
        var facts=oversoldFacts.Snapshot();
        var now=multiClock();var today=DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(9)).DateTime);
        if(!facts.Sessions.Any(s=>s.Date==today&&now>=s.Open&&now<s.Close))throw new InvalidOperationException("오늘의 검증된 한국 시장 세션이 없습니다.");
        var current=session;await Task.Run(()=>current.Refresh(today));var plan=MultiTrading.CreatePlan(current.Balance!.Total);
        if(plan.Items.Length==0)throw new InvalidOperationException("실행할 종목을 하나 이상 체크하세요.");
        var configured=new List<LiveBatchItem>();
        foreach(var item in plan.Items)
        {
            if(!HistoricalFacts.Tradable(facts.Status(item.Stock.Symbol,today,now))||facts.Risk(item.Stock.Symbol,today,now)!=false)throw new InvalidOperationException(item.Stock.Name+": 오늘의 일반주식·거래정지·관리·위험 상태를 확인할 수 없습니다.");
            var warmup=await ScreeningBars(item.Stock,spec,facts,now,Token);
            if(warmup.Length==0)throw new InvalidOperationException(item.Stock.Name+": 검증된 원본 체결로 만든 준비 봉이 없습니다.");
            if(warmup.Length>20000)throw new InvalidOperationException(item.Stock.Name+": 준비 봉이 20,000개를 넘습니다. 더 큰 틱/분 기준을 선택하세요.");
            configured.Add(item with{Oversold=new(LoadMultiOversoldPreset(item.Preset),spec,warmup,facts)});
        }
        plan=plan with{Items=configured.ToArray()};
        var details=string.Join("\n",plan.Items.Select(i=>$"{i.Stock.Name} / {i.Preset} / 예산 {i.Budget:N0}원 / 1회 {i.Settings.Advanced!.Common.Investment:N0}원 / {spec} / 지정가"));
        if(!confirm($"선택한 실제 계좌에서 낙주 전략 자동매매를 시작할까요?\n{broker!.Name} · {broker.MaskedAccount}\n{details}\n전체 한도 {plan.TotalBudget:N0}원 / 일 손실 한도 {plan.DailyLossLimit:N0}원\n메인 1회 주문 한도 {LiveOrderLimit:N0}원도 적용됩니다. 새 실제 체결로 완성한 봉의 신호만 사용하며, 체결·보유는 증권사 체결 대사 후 반영합니다.\n중지는 새 주문 차단과 소유 미체결 취소 요청이며 보유분은 자동 청산하지 않습니다."))return;
        if(!ReferenceEquals(current,session)||!IsOnline||MultiTrading.UseMock||!MultiSupported)throw new InvalidOperationException("계좌 또는 모드가 변경되었습니다.");
        if(broker is not KiwoomBroker&&multiFeedFactory==null)throw new InvalidOperationException("이 연결의 실제 피드 어댑터를 확인할 수 없습니다.");
        StopTicks();realtimePaperBatch=null;multiPaperActive=false;liveBatch=new(current,store,multiClock);await Task.Run(()=>liveBatch.Start(plan));multiStop=new();var token=multiStop.Token;var active=liveBatch;
        var instruments=plan.Items.Select(i=>new Instrument(i.Stock.Symbol,i.Stock.Exchange)).ToArray();
        Action<string> statusChanged=m=>{if(m.Contains("단절")||m.Contains("오류")||m.Contains("버퍼"))active.FeedFault(m);};
        IMarketDataProvider feed;
        try{feed=multiFeedFactory?.Invoke(broker,instruments,plan.Items[0].Settings.Data,statusChanged)??new KiwoomMarketDataProvider(((KiwoomBroker)broker).GetTokenAsync,instruments,plan.Items[0].Settings.Data,statusChanged,live:true);}
        catch{await Task.Run(()=>active.Stop());multiStop.Dispose();multiStop=null;throw;}
        MultiStatus="전략 다중매매 실행 중 · "+broker.MaskedAccount;Changed(nameof(MultiStatus));MultiTrading.Apply(active.Snapshot());
        multiTask=Task.Run(async()=>
        {
            async Task Read()
            {
                var batch=new List<MarketEvent>();try{await foreach(var e in feed.Stream(token)){active.OnMarket(e);batch.Add(e);if(e.Kind==EventKind.Trade)_=dispatcher.BeginInvoke(()=>ObserveMultiMarket(e));if(batch.Count>=128){store.InsertEvents(batch);batch.Clear();}}if(!token.IsCancellationRequested)active.FeedFault("수신 종료");}
                catch(OperationCanceledException)when(token.IsCancellationRequested){}catch{active.FeedFault("연결 또는 시세 오류");}
                finally{try{if(batch.Count>0)store.InsertEvents(batch);}catch{active.FeedFault("원본 체결 저장 실패");}await feed.DisposeAsync();}
            }
            async Task Poll()
            {
                using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));try{while(await timer.WaitForNextTickAsync(token)){await active.Pulse();var snapshot=active.Snapshot();_ = dispatcher.BeginInvoke(()=>{if(disposed||!ReferenceEquals(active,liveBatch))return;MultiTrading.Apply(snapshot);MultiStatus=(snapshot.Running?"실행 중 · ":"중지 · ")+snapshot.Status;Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();});}}
                catch(OperationCanceledException)when(token.IsCancellationRequested){}catch{active.FeedFault("처리 오류: 신규 주문 중지");}
            }
            await Task.WhenAll(Read(),Poll());
        });Refresh();
    }
    public async Task StopMulti(string? key=null)
    {
        if(multiPaperActive&&realtimePaperBatch is {} paper)
        {
            paper.Stop(key);
            if(key==null){multiPaperActive=false;multiStop?.Cancel();try{if(multiTask!=null)await multiTask;}catch{Status="실시간 모의 피드 종료 상태를 확인하지 못했습니다.";}finally{multiTask=null;multiStop?.Dispose();multiStop=null;}}
            if(!disposed){MultiTrading.Apply(paper.Snapshot());MultiStatus="실시간 모의 중지 · 가상 보유분 자동청산 없음 · 실제 주문 없음";Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();}return;
        }
        var active=liveBatch;if(active==null){if(mockBatchLocked){mockBatchLocked=false;MultiStatus="모의 실행 종료 · 기록은 보존됩니다.";MultiTrading.ApplyMock(MultiStatus,new Dictionary<string,string>(),false);Changed(nameof(MultiStatus));Refresh();}return;}active.RequestStop(key);
        try{await Task.Run(()=>active.Stop(key));}
        finally{if(key==null){multiStop?.Cancel();try{if(multiTask!=null)await multiTask;}catch{Status="피드 종료 상태를 확인하지 못했습니다. 신규 주문은 중지됐습니다.";}finally{multiTask=null;multiStop?.Dispose();multiStop=null;liveBatch=null;}}if(!disposed){MultiTrading.Apply(active.Snapshot());MultiStatus="중지 · 보유분 자동청산 없음 · 미체결 상태를 확인하세요.";Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();}}
    }
    private async Task ReviewMulti()
    {
        if(session==null||!MultiSupported)throw new InvalidOperationException("키움 국내 계좌가 필요합니다.");
        if(!confirm("증권사 앱에서 이전 자동 주문의 응답·체결·취소를 확인했고 해당 종목의 보유와 미체결이 없습니까? 이 확인은 주문 재전송이나 보유분 인수가 아닙니다."))return;
        var review=new LiveStrategyBatch(session,store);await Task.Run(review.ConfirmPreviousClosed);MultiStatus="이전 실행 외부 확인 완료 · 일괄수행은 별도 실행";Changed(nameof(MultiStatus));
    }
}
