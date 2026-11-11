using System.Threading.Channels;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public sealed record InjesSessionJournal(string Id,DateTimeOffset At,InjesStrategyDefinition Definition,Instrument[] Instruments,
    PaperBrokerCheckpoint Ledger,StrategySignal[] Signals,string Status,InjesSessionCheckpoint? Checkpoint=null);
public sealed record InjesStreamSnapshot(InjesStrategyDefinition Definition,PaperBrokerCheckpoint Ledger,StrategyEvaluationResult[] Evaluations,string Status,bool EntriesEnabled);

/// <summary>App-owned bounded stream. Replay and live paper deliver to the identical InjesSession; no window owns execution.</summary>
public sealed class InjesStreamService(TradingStore store,Func<DateTimeOffset>? clock=null):IAsyncDisposable
{
    private readonly object gate=new();private readonly Func<DateTimeOffset> clock=clock??(()=>DateTimeOffset.UtcNow);
    private CancellationTokenSource? stop;private Task worker=Task.CompletedTask;private InjesSession? session;
    private InjesPaperExecution? paper;private Instrument[] instruments=[];private int writtenFills,writtenOrders,writtenSignals;
    private string id="",status="중지";private readonly Dictionary<string,Queue<MarketEvent>> prices=[];
    private string savedOrderState="",storageFailure="";private bool stopWhenFlat;
    public event Action<Fill>? FillConfirmed;public event Action<string>? StatusChanged;
    public InjesSession? Session {get{lock(gate)return session;}}
    public bool Running {get{lock(gate)return !worker.IsCompleted;}}
    public string Status {get{lock(gate)return status+(storageFailure.Length>0?" · 저장소 확인 필요: "+storageFailure:"");}}
    public string StorageFailure {get{lock(gate)return storageFailure;}}
    public string RunId {get{lock(gate)return id;}}
    public bool HasUnsettled {get{lock(gate)return paper?.Orders.Any(o=>o.Active)==true||paper?.Positions.Values.Any(p=>p.Quantity>0)==true;}}
    public bool HasSavedUnsettled
    {
        get{lock(gate){if(storageFailure!="")return true;try{return store.States("inje-live:").Select(x=>Serialization.Unpack<InjesSessionJournal>(x.Json)).Any(j=>j.Ledger.Orders.Any(o=>o.Active)||j.Ledger.Positions.Any(p=>p.Quantity>0));}catch(Exception error)when(error is not OutOfMemoryException){StorageFailed(error);return true;}}}
    }
    public MarketEvent[] Trades(string key){lock(gate)return prices.GetValueOrDefault(key)?.ToArray()??[];}
    public InjesStreamSnapshot? Snapshot()
    {
        lock(gate)return session==null||paper==null?null:Serialization.Unpack<InjesStreamSnapshot>(Serialization.Pack(new InjesStreamSnapshot(session.Definition,paper.Broker.Capture(),instruments.Select(i=>session.Latest(i.Key)).Where(e=>e!=null).Cast<StrategyEvaluationResult>().ToArray(),Status,session.EntriesEnabled)));
    }
    public InjesSessionJournal RestoreLatest()
    {
        lock(gate)
        {
            if(Running||HasUnsettled)throw new InvalidOperationException("실행/보유 중인 가상 계좌를 덮어쓸 수 없습니다.");
            var unresolved=store.States("inje-live:").Select(x=>Serialization.Unpack<InjesSessionJournal>(x.Json)).Where(j=>j.Ledger.Orders.Any(o=>o.Active)||j.Ledger.Positions.Any(p=>p.Quantity>0)).ToArray();
            if(unresolved.Length!=1)throw new InvalidOperationException("복원할 미청산 Inje 계좌가 없거나 여러 개입니다. 보관 원장을 먼저 확인하세요.");
            var journal=unresolved[0];if(journal.Checkpoint==null)throw new InvalidOperationException("진입 근거 checkpoint가 없습니다. 보유를 임의 인수하지 않습니다.");
            paper=new(journal.Definition.InitialCash,journal.Definition.ExecutionPolicy);paper.Broker.Restore(journal.Ledger);
            session=new(journal.Definition,journal.Instruments,paper,checkpoint:journal.Checkpoint);instruments=journal.Instruments;id=journal.Id;prices.Clear();
            SetStatus("Inje 가상 원장 복원 · 신규 매수 OFF · 보호 감시 재개 전");Persist();return journal;
        }
    }
    public void ResumeProtection(IMarketDataProvider feed)
    {
        lock(gate)
        {
            if(Running||session==null)throw new InvalidOperationException("정지 상태로 복원한 Inje 가상 계좌가 필요합니다.");
            if(storageFailure!=""&&!Persist(retry:true))throw new InvalidOperationException("저장소를 복구하고 메모리 원장의 저장 상태를 확인하세요. "+storageFailure);
            session.ResumeProtection(clock());stopWhenFlat=false;stop?.Dispose();stop=new();var token=stop.Token;var local=session;
            SetStatus("Inje 보호 감시 재개 · 신규 매수 OFF");if(!Persist())throw new InvalidOperationException(StorageFailure);worker=Task.Run(()=>Consume(local,feed,local.Definition.ExecutionPolicy.MaximumBufferedEvents,token));
        }
    }
    public void Start(InjesStrategyDefinition definition,Instrument[] instruments,IMarketDataProvider feed)
    {
        lock(gate)
        {
            if(Running||HasUnsettled||HasSavedUnsettled)throw new InvalidOperationException("이전 Inje 가상 계좌에 보유/미체결이 남아 있습니다. 기록을 확인하고 대사해야 새 계좌를 시작할 수 있습니다.");
            var d=definition.Snapshot();paper=new(d.InitialCash,d.ExecutionPolicy);session=new(d,instruments,paper);
            this.instruments=instruments.ToArray();id=Guid.NewGuid().ToString("N");writtenFills=writtenOrders=writtenSignals=0;stopWhenFlat=false;prices.Clear();
            stop?.Dispose();stop=new();var token=stop.Token;var local=session;SetStatus("원본 틱·호가 수신 대기 · 자동 진입 OFF");if(!Persist())throw new InvalidOperationException(StorageFailure);
            worker=Task.Run(()=>Consume(local,feed,d.ExecutionPolicy.MaximumBufferedEvents,token));
        }
    }
    public void SetEntriesEnabled(bool value){lock(gate){if(value&&storageFailure!="")throw new InvalidOperationException("원장 저장 상태 확인 전 신규 진입을 활성화할 수 없습니다. "+storageFailure);session?.SetEntriesEnabled(value,clock());SetStatus(value?"Inje 자동 가상 진입 ON · 실제 주문 없음":"새 가상 진입 OFF · 보유 보호 유지");}}
    public void Emergency(){lock(gate){session?.EmergencyStop(clock());SetStatus("긴급중지 · 새 주문 차단·미체결 취소 요청");Persist();stop?.Cancel();}}
    public void StopEntries(string reason="새 진입 중지")
    {lock(gate){stopWhenFlat=true;session?.SetEntriesEnabled(false,clock());foreach(var o in paper?.Orders.Where(o=>o.Active&&o.Buy).ToArray()??[])paper!.Cancel(o,clock());SetStatus(reason+" · 기존 보유 보호 유지");Persist();if(!HasUnsettled)stop?.Cancel();}}
    public void StopSymbolEntries(string key)
    {lock(gate){session?.StopSymbolEntries(key,clock());SetStatus(key+" 신규 진입 중지 · 다른 종목과 기존 보유 보호 유지");Persist();}}
    private async Task Consume(InjesSession local,IMarketDataProvider feed,int capacity,CancellationToken token)
    {
        var channel=Channel.CreateBounded<MarketEvent>(new BoundedChannelOptions(capacity){SingleReader=true,SingleWriter=true,FullMode=BoundedChannelFullMode.Wait});
        var producer=Task.Run(async()=>{try{await foreach(var e in feed.Stream(token).WithCancellation(token))await channel.Writer.WriteAsync(e,token);channel.Writer.TryComplete();}catch(Exception e){channel.Writer.TryComplete(e);}},CancellationToken.None);
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        var pulses=Task.Run(async()=>{try{while(await timer.WaitForNextTickAsync(token)){lock(gate){local.OnTime(clock());PersistIfChanged();}}}catch(OperationCanceledException){}},CancellationToken.None);
        try
        {
            await foreach(var e in channel.Reader.ReadAllAsync(token))
            {
                lock(gate)
                {
                    local.OnMarket(InjesResearchBridge.Observation(e),clock());store.InsertEvents([e]);
                    if(e.Kind==EventKind.Trade){if(!prices.TryGetValue(e.Key,out var queue))prices[e.Key]=queue=new();queue.Enqueue(e);while(queue.Count>20000)queue.Dequeue();}
                    PersistIfChanged();
                }
            }
            StopEntries("시세 스트림 종료 · 새 진입 차단");
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){StopEntries("실행 종료 · 보유/미체결 기록 유지");}
        catch(Exception e)when(e is not OutOfMemoryException){StopEntries("시세/저장 오류 · "+e.Message);}
        finally
        {
            lock(gate)stop?.Cancel();
            try{await producer.WaitAsync(TimeSpan.FromSeconds(2));}catch(OperationCanceledException){}catch(TimeoutException){lock(gate)SetStatus("시세 공급자 종료 응답 없음 · 신규 진입 OFF · 원장 보존");}
            await pulses;
            try{await feed.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));}catch(Exception error)when(error is not OutOfMemoryException){lock(gate)SetStatus("시세 연결 정리 확인 필요 · "+error.Message+" · 원장 보존");}
            lock(gate)Persist();
        }
    }
    private void PersistIfChanged()
    {
        if(paper==null||session==null)return;
        var fills=paper.Fills.ToArray();var fresh=fills.Skip(writtenFills).ToArray();
        if(writtenFills!=fills.Length||writtenOrders!=paper.Orders.Count||writtenSignals!=session.Signals.Length||savedOrderState!=OrderStateFingerprint())
        {if(Persist())foreach(var fill in fresh)FillConfirmed?.Invoke(fill);}
        if(stopWhenFlat&&!HasUnsettled)stop?.Cancel();
    }
    private void StorageFailed(Exception error)
    {
        storageFailure=error.Message;session?.SetEntriesEnabled(false,clock());foreach(var order in paper?.Orders.Where(o=>o.Active&&o.Buy).ToArray()??[])paper!.Cancel(order,clock());
        SetStatus("저장소 오류 · 신규 진입 OFF · 종료 전 저장소 점검 필요 · 메모리 원장 보존");
    }
    private bool Persist(bool retry=false)
    {
        if(session==null||paper==null)return true;if(storageFailure!=""&&!retry)return false;
        try
        {
            var journal=new InjesSessionJournal(id,clock(),session.Definition,instruments,paper.Broker.Capture(),session.Signals,Status,session.Capture());
            store.State("inje-live:"+id,journal);storageFailure="";writtenFills=paper.Fills.Count;writtenOrders=paper.Orders.Count;writtenSignals=session.Signals.Length;savedOrderState=OrderStateFingerprint();return true;
        }
        catch(Exception error)when(error is not OutOfMemoryException){StorageFailed(error);return false;}
    }
    private string OrderStateFingerprint()=>Serialization.Pack(paper?.Orders.Select(o=>new{o.Id,o.State,o.Filled,o.CancellationRequestedAt}));
    private void SetStatus(string value){status=value;StatusChanged?.Invoke(value);}
    public async ValueTask DisposeAsync()
    {Task pending;lock(gate){StopEntries("앱 종료 · 원장 보존");stop?.Cancel();pending=worker;}await pending.ConfigureAwait(false);lock(gate){stop?.Dispose();stop=null;}}
}
