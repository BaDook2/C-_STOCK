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
    private CancellationTokenSource? stop=null;
    private Task loop=Task.CompletedTask;
    private readonly SemaphoreSlim stopGate=new(1,1);
    private bool busy=false,disposed;
    private string status="Inje 실계좌 자동 주문은 잠겨 있습니다. 계좌 연결과 수동 주문의 기존 안전 절차는 유지합니다.";
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
        await Task.CompletedTask;
        throw new InvalidOperationException("기존 전략은 제거되었고 Inje 실계좌 자동 주문은 잠겨 있습니다. 가상 실행을 사용하세요.");
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
