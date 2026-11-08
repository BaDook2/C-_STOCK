using Avalonia.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

/// <summary>Application-owned adapter for the existing live batch. Windows never own trading tasks.</summary>
public sealed class LiveStrategyController:Bindable,IAsyncDisposable
{
    private readonly AppServices services;
    private readonly MarketHistoryService history;
    private LiveStrategyBatch? active;
    private RemoteTradingSession? owner;
    private CancellationTokenSource? stop;
    private Task loop=Task.CompletedTask;
    private readonly SemaphoreSlim stopGate=new(1,1);
    private bool busy,disposed;
    private string status="실제 주문 활성화와 검증된 현재 종목·위험 자료가 필요합니다. 키움 국내 계좌만 지원합니다.";
    private LiveBatchStatus? snapshot;
    public AccountWorkspace Accounts {get;}
    public bool Busy=>busy;
    public bool Running=>snapshot?.Running==true;
    public bool HasProtectedPositions=>snapshot?.Rows.Any(r=>r.Owned>0||r.Pending>0)==true;
    public bool Locked=>busy||active!=null;
    public string Status=>status;
    public string TotalBudget {get;set;}="10000000";
    public string DailyLossLimit {get;set;}="100000";
    public string FactsJson {get;set;}=Serialization.Pack(new HistoricalFacts());
    public string WarmupJson {get;set;}="{}";
    public AsyncCommand StopCommand {get;}
    public AsyncCommand ReviewPrevious {get;}
    public event Action<string>? Notice;
    public event Action? FillsChanged;
    public LiveStrategyController(AppServices services,AccountWorkspace accounts,MarketHistoryService history)
    {
        this.services=services;Accounts=accounts;this.history=history;StopCommand=new(Stop);ReviewPrevious=new(Review);
        Accounts.TradingDisarmed+=RequestStop;
    }
    public Dictionary<string,string> SessionStatuses()=>snapshot?.Rows.ToDictionary(r=>r.Key,r=>r.Status+(r.Error.Length>0?" · "+r.Error:""))??[];
    public async Task Start(RealtimeRequest request)
    {
        if(disposed||Locked)throw new InvalidOperationException("기존 실제 실행을 먼저 중지·대사하세요.");
        var remote=Accounts.Session;if(remote==null||!Accounts.Authorized)throw new InvalidOperationException("실제 계좌 주문을 명시적으로 활성화하세요.");
        if(!remote.Broker.IsLive||remote.Broker.Id!="kiwoom"||remote.Broker.Exchange!=Exchange.KRX||remote.Broker is not IExecutionHistoryBroker)throw new InvalidOperationException("실제 체결 대사가 가능한 키움 국내 계좌만 지원합니다.");
        if(!LiveBatchSupport.RegularSession(services.Clock()))throw new InvalidOperationException("장 외 시간입니다. 국내 정규 연속매매 시간에 시작하세요.");
        if(request.PaperAssumptions)throw new InvalidOperationException("가상 분류·위험 가정은 실계좌에 사용할 수 없습니다.");
        owner=remote;busy=true;stop=new(services.QueryTimeout);var token=stop.Token;Publish("실계좌 준비 봉·현재 상태 확인 중 · 주문 없음");
        try
        {
            request.Specification.Validate();var facts=request.Facts.Snapshot();facts.Validate();var now=services.Clock();var today=DailyQuoteMerger.Date(now,Exchange.KRX);
            if(!facts.Sessions.Any(s=>s.Date==today&&now>=s.Open&&now<s.Close))throw new InvalidOperationException("오늘의 검증된 한국 시장 세션이 필요합니다.");
            var items=new List<LiveBatchItem>();
            foreach(var stock in request.Stocks)
            {
                token.ThrowIfCancellationRequested();if(stock.Exchange!=Exchange.KRX)throw new InvalidOperationException("국내 일반주식만 실제 전략 실행할 수 있습니다.");
                if(!HistoricalFacts.Tradable(facts.Status(stock.Symbol,today,now))||facts.Risk(stock.Symbol,today,now)!=false)throw new InvalidOperationException(stock.Name+": 오늘의 일반주식·거래정지·관리·위험 상태가 확인되지 않았습니다.");
                var selection=request.ItemConfigurations?.Single(i=>i.Stock.Key==stock.Key);var config=Serialization.Unpack<OversoldConfig>(Serialization.Pack(selection?.Configuration??request.Configuration));config.Validate();
                var budget=selection?.Budget??config.PlannedInvestment;var prepared=await history.Get(stock,config,facts,request.Specification.Unit==BarUnit.Day?null:request.Specification,today,today,token);
                // Live entry filters may not treat OHLC-average estimates as actual turnover.
                var bars=prepared.Bars??prepared.Dataset.CompletedBars.Select((bar,n)=>{var day=DailyDates.Of(bar);var session=facts.Sessions.FirstOrDefault(s=>s.Date==day)??ResearchMarketSession.For(day,Exchange.KRX);return new StrategyBar(bar with{Time=session.Close},session.Open,session.Close,n+1,null,prepared.Dataset.Source);}).ToArray();
                var external=Serialization.Unpack<Dictionary<string,StrategyBar[]>>(WarmupJson);
                if(external.TryGetValue(stock.Key,out var supplied))bars=supplied;
                else if(request.Specification.Unit==BarUnit.Day)bars=bars.Select(b=>b with{TradingValue=facts.TradingValue(stock.Symbol,DailyDates.Of(b.Price),b.ClosedAt),Source="외부 입력 일별 실제 거래대금"}).ToArray();
                else bars=bars.Select(b=>b with{TradingValue=null}).ToArray();
                if(bars.Any(b=>!b.Price.IsComplete||b.ClosedAt>now||b.OpenTime>=b.ClosedAt)||bars.Zip(bars.Skip(1)).Any(p=>p.First.ClosedAt>=p.Second.ClosedAt))throw new InvalidOperationException(stock.Name+": 준비 봉의 마감 시각·순서가 올바르지 않습니다.");
                foreach(var bar in bars)bar.Price.Validate();
                if(bars.Length==0||bars.Any(b=>b.TradingValue==null))throw new InvalidOperationException(stock.Name+": 준비 봉의 시점별 실제 거래대금 자료가 필요합니다. OHLC 추정치를 실계좌 입력으로 사용하지 않습니다.");
                var settings=RealtimeScreeningHost.PaperSettings(config,budget,publicPaper:false);settings.Data.Universe=stock.Key;
                items.Add(new(stock,selection?.Name??request.Name,settings,budget){Oversold=new(config,request.Specification,bars,LiveBatchSupport.WithObservedWarmupSessions(facts,bars))});
            }
            var plan=new LiveBatchPlan(items.ToArray(),request.TotalBudget,request.DailyLossLimit);
            var details=string.Join("\n",items.Select(i=>$"{i.Stock.Name} / {i.Preset} / 예산 {DisplayNumber.Price(i.Budget)}원"));
            if(!await Accounts.Confirm($"실제 계좌 전략 실행\n{remote.Broker.Name} {remote.Broker.MaskedAccount}\n{details}\n{request.Specification} · 전체 {DisplayNumber.Price(plan.TotalBudget)}원 / 일 손실 {DisplayNumber.Price(plan.DailyLossLimit)}원\n새 실제 체결로 완성된 봉의 전략 신호만 사용합니다. 실제 주문을 시작할까요?"))return;
            token.ThrowIfCancellationRequested();if(Accounts.Session!=remote||!Accounts.Authorized)throw new InvalidOperationException("계좌 변경/중지로 시작을 차단했습니다.");
            var instruments=items.Select(i=>new Instrument(i.Stock.Symbol,i.Stock.Exchange)).ToArray();
            var candidate=new LiveStrategyBatch(remote,services.Store,services.Clock);active=candidate;
            await Task.Run(()=>candidate.Start(plan),token).WaitAsync(token);token.ThrowIfCancellationRequested();if(Accounts.Session!=remote||!Accounts.Authorized)throw new InvalidOperationException("계좌 변경/중지로 시작을 차단했습니다.");snapshot=candidate.Snapshot();
            IMarketDataProvider feed;
            try{feed=services.LiveFeedFactory(remote.Broker,instruments,items[0].Settings.Data,message=>{if(message.Contains("단절")||message.Contains("오류")||message.Contains("버퍼"))candidate.FeedFault(message);Publish(message);});}
            catch{await candidate.Stop().WaitAsync(services.QueryTimeout);active=null;throw;}
            stop.CancelAfter(Timeout.InfiniteTimeSpan);loop=Task.Run(()=>Run(candidate,remote,feed,stop.Token));Publish("실제 전략 실행 · 새 체결 대기");
        }
        catch{if(active!=null){active.RequestStop(reason:"준비 실패 · 새 주문 금지");try{await active.Stop().WaitAsync(services.QueryTimeout);active=null;}catch{Publish("실행 준비 실패 · 주문 대사와 중지를 확인하세요.");}}throw;}
        finally{busy=false;if(active==null)owner=null;Publish(status);}
    }
    private async Task Run(LiveStrategyBatch batch,RemoteTradingSession remote,IMarketDataProvider feed,CancellationToken token)
    {
        async Task Read()
        {
            try{await foreach(var item in feed.Stream(token)){batch.OnMarket(item);}if(!token.IsCancellationRequested)batch.FeedFault("실제 시세 수신 종료");}
            catch(OperationCanceledException) when(token.IsCancellationRequested){}catch(Exception){batch.FeedFault("실제 시세 수신 오류");}
            finally{await feed.DisposeAsync();}
        }
        async Task Poll()
        {
            try
            {
                using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));while(await timer.WaitForNextTickAsync(token)){await batch.Pulse();snapshot=batch.Snapshot();Publish(snapshot.Status);Dispatcher.UIThread.Post(()=>{if(!disposed)FillsChanged?.Invoke();});}
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){}catch(Exception){batch.FeedFault("실제 계좌 처리 오류 · HTS 확인 필요");Publish("실제 계좌 처리 오류 · 새 주문 차단");}
        }
        await Task.WhenAll(Read(),Poll()).ConfigureAwait(false);
    }
    public void RequestStop(){stop?.Cancel();active?.RequestStop(reason:"계좌 잠금/모드 변경/긴급중지 · 새 주문 금지");if(active!=null)_=Stop();}
    public async Task Stop()
    {
        stop?.Cancel();active?.RequestStop();await stopGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(active is {} batch){await Task.Run(()=>batch.Stop()).WaitAsync(services.QueryTimeout).ConfigureAwait(false);snapshot=batch.Snapshot();await loop.WaitAsync(services.QueryTimeout).ConfigureAwait(false);active=null;owner=null;}
            Publish("실제 전략 중지 · 보유분 임의청산 없음 · 미체결은 계좌 내역 확인");
        }
        catch(Exception ex) when(ex is not OutOfMemoryException){Publish("실제 전략 신규 주문은 차단됐습니다. 중지/취소 대사를 완료하지 못했습니다. HTS 확인이 필요합니다.");Dispatcher.UIThread.Post(()=>Notice?.Invoke(status));}
        finally{stopGate.Release();}
    }
    private async Task Review()
    {
        var remote=Accounts.Session;if(remote==null||Locked)return;
        if(!await Accounts.Confirm("이전 실제 전략의 주문·취소·보유가 모두 정리됐음을 HTS에서 확인했나요? 계좌에서도 보유/미체결을 다시 검사합니다. 주문 재전송이나 포지션 인수는 하지 않습니다."))return;
        if(Accounts.Session!=remote)return;await new LiveStrategyBatch(remote,services.Store,services.Clock).ConfirmPreviousClosed();Publish("이전 실제 실행 대사 완료 · 새 시작은 별도 확인");
    }
    public async Task StopForCredentialReset(RemoteTradingSession session)
    {if(!ReferenceEquals(owner,session))return;await Stop().WaitAsync(services.QueryTimeout);if(Locked)throw new InvalidOperationException("실제 전략 중지·대사가 끝나지 않아 키 삭제를 보류합니다.");}
    private void Publish(string value){status=value;Dispatcher.UIThread.Post(()=>{if(disposed)return;Changed(nameof(Status));Changed(nameof(Busy));Changed(nameof(Running));Changed(nameof(Locked));Changed(nameof(HasProtectedPositions));});}
    public async ValueTask DisposeAsync(){if(disposed)return;Accounts.TradingDisarmed-=RequestStop;await Stop().ConfigureAwait(false);disposed=true;stop?.Dispose();}
}
