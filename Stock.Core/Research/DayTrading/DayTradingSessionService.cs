using System.Threading.Channels;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

/// <summary>Application lifetime, bounded serial stream consumer. Windows are observers, never execution owners.</summary>
public sealed class DayTradingSessionService:IAsyncDisposable
{
    private readonly object gate=new();
    private CancellationTokenSource? stop;
    private Task? worker;
    private DayTradingPortfolioEngine? engine;
    private DayTradingPaperAdapter? paper;
    private string connection="중지됨";
    private bool emergency;
    private StrategyDRuntime[] runtimes=[];
    private long processed;
    public long ProcessedEvents=>Interlocked.Read(ref processed);
    private readonly Queue<Fill> fills=[];
    private readonly Queue<IntradayEntrySignal> signals=[];
    private readonly Dictionary<Fill,IntradayEntrySignal?> evidence=[];
    public IntradayEntrySignal? EntryEvidence(Fill fill){lock(gate)return evidence.GetValueOrDefault(fill);}
    public bool IsRunning {get{lock(gate)return worker is {IsCompleted:false};}}
    public string Connection {get{lock(gate)return connection;}}
    public bool HasUnsettled {get{lock(gate)return paper?.Orders.Any(o=>o.Active)==true||engine?.Snapshot() is {} snapshot&&(snapshot.NeedsReconciliation||snapshot.Rows.Any(r=>r.Quantity>0));}}
    public DayTradingPortfolioSnapshot? Snapshot(){lock(gate)return engine?.Snapshot();}
    public Fill[] Fills {get{lock(gate)return fills.ToArray();}}
    public IntradayEntrySignal[] Signals {get{lock(gate)return signals.ToArray();}}
    public Stock.Core.Research.PriceBar[] AnalysisPrices(string key){lock(gate)return runtimes.FirstOrDefault(r=>r.Instrument.Key==key)?.AnalysisPrices()??[];}
    public void SetEntriesEnabled(bool enabled,DateTimeOffset now){lock(gate)engine?.SetEntriesEnabled(enabled,now);}
    public void EmergencyStop(DateTimeOffset now){lock(gate){emergency=true;engine?.EmergencyStop(now);connection="긴급중지 · 모든 새 전략주문 차단";}}
    public void Start(IMarketDataProvider feed,IReadOnlyList<StrategyDRuntime> runtimes,decimal cash,ExecutionSettings execution,AccountRiskPolicy limits,int queueCapacity,Func<DateTimeOffset>? clock=null)
    {
        lock(gate)
        {
            if(worker is {IsCompleted:false})throw new InvalidOperationException("전략 D 가상 실행이 이미 감시 중입니다.");
            if(HasUnsettled)throw new InvalidOperationException("이전 가상 계좌에 보유/미체결이 남아 있습니다. 이를 초기화한 새 계좌로 숨기지 않습니다. 기존 기록과 상태를 먼저 확인하세요.");
            if(queueCapacity<=0)throw new ArgumentOutOfRangeException(nameof(queueCapacity));
            stop?.Dispose();stop=new();emergency=false;processed=0;fills.Clear();signals.Clear();evidence.Clear();clock??=()=>DateTimeOffset.UtcNow;
            paper=new("KRW",cash,execution);engine=new(paper,runtimes,limits);
            this.runtimes=runtimes.ToArray();
            engine.FillConfirmed+=(fill,entry)=>{lock(gate){fills.Enqueue(fill);evidence[fill]=entry;while(fills.Count>10000)evidence.Remove(fills.Dequeue());}};
            engine.EntrySignalObserved+=signal=>{lock(gate){signals.Enqueue(signal);while(signals.Count>10000)signals.Dequeue();}};
            connection="연결 중 · 자동진입 꺼짐";
            var localEngine=engine;var token=stop.Token;
            worker=Task.Run(()=>Consume(feed,localEngine,queueCapacity,clock,token));
        }
    }
    private async Task Consume(IMarketDataProvider feed,DayTradingPortfolioEngine portfolio,int capacity,Func<DateTimeOffset> clock,CancellationToken token)
    {
        var channel=Channel.CreateBounded<MarketEvent>(new BoundedChannelOptions(capacity){FullMode=BoundedChannelFullMode.Wait,SingleReader=true,SingleWriter=true});
        var producer=Task.Run(async()=>
        {
            try{await foreach(var market in feed.Stream(token).WithCancellation(token))await channel.Writer.WriteAsync(market,token);channel.Writer.TryComplete();}
            catch(Exception ex){channel.Writer.TryComplete(ex);}
        },CancellationToken.None);
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        var pulse=Task.Run(async()=>{try{while(await timer.WaitForNextTickAsync(token)){lock(gate)portfolio.OnTime(clock());}}catch(OperationCanceledException){}},CancellationToken.None);
        try
        {
            await foreach(var market in channel.Reader.ReadAllAsync(token))
            {
                lock(gate){portfolio.OnMarket(market,clock());Interlocked.Increment(ref processed);if(!emergency)connection="수신 중 · 자료 품질/당시 상태 검증은 종목별 표시";}
            }
            lock(gate){portfolio.SetEntriesEnabled(false,clock());connection="시세 스트림 종료 · 신규진입 차단 · 보유/미체결 확인";}
        }
        catch(OperationCanceledException){lock(gate){portfolio.SetEntriesEnabled(false,clock());connection="종료됨 · 보유분 임의청산 없음";}}
        catch(Exception ex){lock(gate){portfolio.SetEntriesEnabled(false,clock());connection="시세 오류 · 신규진입 차단: "+ex.Message;}}
        finally
        {
            lock(gate)stop?.Cancel();
            try{await producer;}catch(OperationCanceledException){}
            await pulse;await feed.DisposeAsync();
        }
    }
    /// <summary>Explicit application shutdown; pausing entries instead keeps stream-based protective management alive.</summary>
    public async Task StopAsync()
    {
        Task? pending;lock(gate){stop?.Cancel();pending=worker;}
        if(pending!=null)await pending.ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync(){await StopAsync().ConfigureAwait(false);lock(gate){stop?.Dispose();stop=null;}}
}
