using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void DailyPaperHostChecks()
    {
        var now=new DateTimeOffset(2026,11,6,10,0,0,TimeSpan.FromHours(9));var path=Path.Combine(output,"daily-host",Guid.NewGuid().ToString("N"));var source=new DailyHostMarket(()=>now);
        using(var services=new AppServices(path,source,clock:()=>now,credentials:MemoryCredentials()))
        using(var model=new MainViewModel(services))
        {
            var window=new MainWindow{DataContext=model};window.Show();model.Strategies.Selected=StrategyDiagnosticPreset.HoldOneDayName;model.Realtime.PaperAssumptions=true;model.Realtime.Automatic=true;model.Realtime.Start.Execute(null);Pump(()=>!model.Realtime.Busy&&model.Realtime.Running,20);
            var id=services.Store.State<string>("paper-live-batch-latest")!;var frame=services.Store.State<PaperCheckpointEnvelope>("paper-live-checkpoint:"+id)!;var state=Serialization.Unpack<PaperBatchCheckpoint>(frame.Payload);
            Check(model.Realtime.Automatic&&state.Plan.Items[0].Oversold!.BarSpecification.Unit==BarUnit.Day&&!state.Plan.Items[0].Settings.Execution.UseSpread,"offline daily auto start reaches the existing paper engine using observation matching rather than unavailable quote-book liquidity");
            Check(state.Rows.All(r=>r.Broker.Fills.Length==0),"adapter warmup never retrospectively fills daily research signals");model.Realtime.Stop.Execute(null);Pump(()=>!model.Realtime.Running);window.Close();
        }
        using(var services=new AppServices(path,new DailyHostMarket(()=>now),clock:()=>now,credentials:MemoryCredentials()))
        using(var model=new MainViewModel(services))
        {
            var window=new MainWindow{DataContext=model};window.Show();model.Realtime.RestoreSaved.Execute(null);Pump(()=>!model.Realtime.Busy&&model.Realtime.RecoveredPlan!=null);
            Check(!model.Realtime.Running&&!model.Realtime.Automatic&&model.VirtualExecution.Items.Count==1,"new application composition restores the saved target/preset list paused without automatically starting trading");
            model.Realtime.ResumeSaved.Execute(null);Pump(()=>!model.Realtime.Busy&&model.Realtime.Running,20);Check(!model.Realtime.Automatic,"explicit paper resume starts only protective monitoring with new entries disabled");
            now=new(2026,11,6,15,31,0,TimeSpan.FromHours(9));Pump(()=>services.Store.States("paper-live-checkpoint:").Any(s=>Serialization.Unpack<PaperBatchCheckpoint>(Serialization.Unpack<PaperCheckpointEnvelope>(s.Json).Payload).Rows[0].Strategy.LastBarTime>=now.AddMinutes(-1)),10);
            now=new(2026,11,9,9,1,0,TimeSpan.FromHours(9));Pump(()=>services.Store.States("paper-live-checkpoint:").Any(s=>Serialization.Unpack<PaperBatchCheckpoint>(Serialization.Unpack<PaperCheckpointEnvelope>(s.Json).Payload).Rows[0].Strategy.Facts.Sessions.Any(d=>d.Date==new DateOnly(2026,11,9))),12);
            Check(model.Realtime.Running&&!model.Realtime.Automatic,"daily host rolls to a newly observed session and preserves the restored entry-disable state");model.EmergencyCommand.Execute(null);foreach(var owned in window.OwnedWindows.ToArray())owned.Close();window.Close();
        }
    }
    private sealed class DailyHostMarket(Func<DateTimeOffset> clock):IPublicMarketData,ICurrentMarketData
    {
        private readonly MarketFixture inner=new(){Clock=clock};
        public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>inner.Search(name,region,token);
        public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>inner.Universe(region,count,token);
        public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>inner.Describe(stock,token);
        public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)=>inner.Current(stock,token);
        public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
        public async Task<BarDataset> History(StockItem stock,CancellationToken token)
        {
            var data=await inner.History(stock,token);var today=DailyQuoteMerger.Date(clock(),Exchange.KRX);var close=ResearchMarketSession.For(today,Exchange.KRX).Close;var bars=data.Bars.Where(b=>b.Time.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();
            if(clock()>=close&&today.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday))bars.Add(bars[^1] with{Time=close});return data with{Bars=bars.ToArray()};
        }
    }
}
