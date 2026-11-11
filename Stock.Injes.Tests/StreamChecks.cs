using System.Threading.Channels;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class ControlledFeed:IMarketDataProvider
    {
        private readonly Channel<MarketEvent> events=Channel.CreateUnbounded<MarketEvent>();
        public string Description=>"synthetic controlled test only";
        public void Send(InjesObservation observation)=>events.Writer.TryWrite(observation.Market with{StrategyReference=observation.Reference,TradingSession=observation.Session,AvailableAt=observation.KnownAt});
        public IAsyncEnumerable<MarketEvent> Stream(CancellationToken token)=>events.Reader.ReadAllAsync(token);
        public ValueTask DisposeAsync(){events.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
    private sealed class UnresponsiveFeed:IMarketDataProvider
    {
        public bool Started;public string Description=>"synthetic non-responsive provider";
        public async IAsyncEnumerable<MarketEvent> Stream([System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken token)
        {Started=true;await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;yield break;}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private static async Task Until(Func<bool> condition)
    {var started=System.Diagnostics.Stopwatch.StartNew();while(!condition()){if(started.Elapsed>TimeSpan.FromSeconds(8))throw new TimeoutException("controlled stream did not progress");await Task.Delay(5);}}
    private static async Task StreamChecks()
    {
        var directory=Path.Combine(Path.GetTempPath(),"InjesStream-"+Guid.NewGuid().ToString("N"));using var store=new TradingStore(Path.Combine(directory,"private.db"),true);
        long tick=Epoch.UtcTicks;DateTimeOffset Clock()=>new(Interlocked.Read(ref tick),TimeSpan.Zero);
        var feed=new ControlledFeed();var d=TestDefinition();d.InitialCash=1000;
        var stream=new InjesStreamService(store,Clock);stream.Start(d,[Stock],feed);stream.SetEntriesEnabled(true);
        void Send(ControlledFeed target,int n,decimal price=2,bool quote=false,decimal? bid=null,decimal? ask=null)
        {var e=Observation(n,price,quote:quote,bid:bid,ask:ask);Interlocked.Exchange(ref tick,e.KnownAt.UtcTicks);target.Send(e);}
        Send(feed,0,quote:true);Send(feed,1);await Until(()=>stream.Snapshot()?.Ledger.Orders.Length==1);Send(feed,2,quote:true);
        await Until(()=>stream.Snapshot()?.Ledger.Positions.SingleOrDefault()?.Quantity==9);
        Check(stream.Snapshot()!.Ledger.Positions.Single().OpenedAt==Epoch.AddMilliseconds(500),"controlled original stream fills only after the entry event");
        await stream.DisposeAsync();var journal=store.States("inje-live:").Select(x=>Serialization.Unpack<InjesSessionJournal>(x.Json)).Single();
        Check(journal.Checkpoint?.Rows.Single().Entry!=null&&journal.Ledger.Positions.Single().Quantity==9,"shutdown preserves confirmed holdings and entry evidence atomically in the journal");
        var incorrectAverage=Serialization.Unpack<PaperBrokerCheckpoint>(Serialization.Pack(journal.Ledger));incorrectAverage.Positions[0].AveragePrice+=1;
        Throws(()=>new InjesPaperExecution(d.InitialCash,d.ExecutionPolicy).Broker.Restore(incorrectAverage),"restore reconciles average cost against actual fills rather than trusting a positive stored number");
        var restored=new InjesStreamService(store,Clock);var saved=restored.RestoreLatest();
        Check(restored.Snapshot()!.Ledger.Cash==journal.Ledger.Cash&&restored.Snapshot()!.Ledger.Positions.Single().Quantity==9&&!restored.Session!.EntriesEnabled&&!restored.Running,"restore reconciles cash/quantity and keeps entries and feed stopped");
        var resumeFeed=new ControlledFeed();restored.ResumeProtection(resumeFeed);Send(resumeFeed,3,quote:true,bid:1.9m,ask:1.92m);Send(resumeFeed,4,1.9m);
        await Until(()=>restored.Snapshot()?.Ledger.Orders.Any(o=>!o.Buy&&o.Reason=="StopLoss")==true);Send(resumeFeed,5,quote:true,bid:1.89m,ask:1.91m);
        await Until(()=>restored.Snapshot()?.Ledger.Positions.SingleOrDefault()?.Quantity==0);
        Check(!restored.Session!.EntriesEnabled&&restored.Snapshot()!.Ledger.Orders.Count(o=>o.Buy)==1,"explicit protection resume can stop the held position without creating a new buy");
        await restored.DisposeAsync();Check(!restored.HasSavedUnsettled,"confirmed close updates the saved ledger instead of leaving a phantom unresolved account");
        var tampered=journal with{Definition=InjesStrategyDefinition.Create("support-zone",Epoch)};
        Throws(()=>{var paper=new InjesPaperExecution(tampered.Definition.InitialCash,tampered.Definition.ExecutionPolicy);paper.Broker.Restore(tampered.Ledger);_ = new InjesSession(tampered.Definition,[Stock],paper,checkpoint:journal.Checkpoint);},"saved account cannot be resumed under a different strategy definition");
        using var stalledStore=new TradingStore(Path.Combine(directory,"unresponsive.db"),true);var stalledFeed=new UnresponsiveFeed();var stalled=new InjesStreamService(stalledStore,Clock);stalled.Start(d,[Stock],stalledFeed);await Until(()=>stalledFeed.Started);
        var watch=System.Diagnostics.Stopwatch.StartNew();await stalled.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Check(watch.Elapsed<TimeSpan.FromSeconds(5)&&!stalled.Running&&stalled.Status.Contains("응답 없음"),"unresponsive provider cannot block shutdown indefinitely and its missing acknowledgement remains visible");
        using var brokenStore=new TradingStore(Path.Combine(directory,"save-failure.db"),true);var brokenFeed=new ControlledFeed();var broken=new InjesStreamService(brokenStore,Clock);Interlocked.Exchange(ref tick,Epoch.UtcTicks);broken.Start(d,[Stock],brokenFeed);broken.SetEntriesEnabled(true);
        Send(brokenFeed,0,quote:true);Send(brokenFeed,1);await Until(()=>broken.Snapshot()?.Ledger.Orders.Length==1);Send(brokenFeed,2,quote:true);await Until(()=>broken.Snapshot()?.Ledger.Positions.Any(p=>p.Quantity==9)==true);
        var backup=brokenStore.Path+".test-backup";File.Move(brokenStore.Path,backup);Directory.CreateDirectory(brokenStore.Path);
        broken.Emergency();Check(broken.StorageFailure.Length>0&&!broken.Session!.EntriesEnabled&&broken.Snapshot()!.Ledger.Positions.Single().Quantity==9,"journal save failure locks entry and preserves the confirmed in-memory ledger without throwing from emergency");
        Throws(()=>broken.SetEntriesEnabled(true),"failed journal cannot be rearmed with the automatic toggle");await broken.DisposeAsync();Check(!broken.Running&&broken.HasSavedUnsettled,"journal storage failure remains unresolved and shutdown does not crash or claim an empty saved account");
        Directory.Delete(brokenStore.Path);File.Move(backup,brokenStore.Path);
        var diskRecovery=new InjesStreamService(brokenStore,Clock);diskRecovery.RestoreLatest();Check(diskRecovery.Snapshot()!.Ledger.Positions.Single().Quantity==9&&!diskRecovery.Session!.EntriesEnabled,"last durable checkpoint remains intact after isolated storage failure and explicit repair");await diskRecovery.DisposeAsync();
    }
}
