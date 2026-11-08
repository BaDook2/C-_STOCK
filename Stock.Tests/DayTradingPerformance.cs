using System.Diagnostics;
using System.IO;
using Stock.Core.Research.DayTrading;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void DayTradingPerformance(string output)
    {
        Directory.CreateDirectory(output);var measures=new List<object>();var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));
        foreach(var symbols in new[]{10,25,50,100})
        {
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            var memory=GC.GetTotalMemory(true);var allocated=GC.GetTotalAllocatedBytes();var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
            var gc0=GC.CollectionCount(0);var gc1=GC.CollectionCount(1);var gc2=GC.CollectionCount(2);var watch=Stopwatch.StartNew();
            var definition=StrategyDTestConfig();var seed=StrategyDSeed(open);var runtimes=new List<StrategyDRuntime>();
            for(int s=0;s<symbols;s++)
            {
                var symbol=(s+1).ToString("D6");var facts=seed.Facts.Snapshot();facts.StatusRecords=facts.StatusRecords.Select(f=>f with{Symbol=symbol}).ToArray();facts.RiskRecords=facts.RiskRecords.Select(f=>f with{Symbol=symbol}).ToArray();
                runtimes.Add(new(new(symbol,Exchange.KRX),definition,new(seed.Bars,facts),"perf"));
            }
            var startup=watch.Elapsed.TotalMilliseconds;var broker=new DayTradingPaperAdapter("KRW",10_000_000,new ExecutionSettings{UseSpread=false});
            var engine=new DayTradingPortfolioEngine(broker,runtimes,definition.DayTrading!.Risk);watch.Restart();
            const int eventsPerSymbol=1000;
            for(int n=0;n<eventsPerSymbol;n++)
            foreach(var runtime in runtimes)
            {
                var time=open.AddMilliseconds(n*100);var price=n==0?10100:n%100<50?9900m:9950m;
                var e=new MarketEvent(runtime.Instrument,EventKind.Trade,time,time,"PERF",n+1){TradePrice=price,TradeSize=10,CumulativeVolume=(n+1)*10};engine.OnMarket(e,time);
            }
            watch.Stop();var elapsed=watch.Elapsed.TotalMilliseconds;process.Refresh();
            var result=new{Symbols=symbols,Events=symbols*eventsPerSymbol,StartupMs=startup,ProcessingMs=elapsed,EventsPerSecond=symbols*eventsPerSymbol/Math.Max(.001,watch.Elapsed.TotalSeconds),CpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,AllocatedBytes=GC.GetTotalAllocatedBytes()-allocated,RetainedBytes=GC.GetTotalMemory(true)-memory,WorkingSet=process.WorkingSet64,GC0=GC.CollectionCount(0)-gc0,GC1=GC.CollectionCount(1)-gc1,GC2=GC.CollectionCount(2)-gc2,RetainedTrades=runtimes.Sum(r=>r.RetainedTradeEvents),QueueBacklog=0,Errors=engine.Snapshot().Rows.Count(r=>r.Error!=""),Orders=broker.Orders.Count};
            measures.Add(result);Console.WriteLine(Serialization.Pack(result));
            Check(result.Errors==0&&result.Orders==0&&result.RetainedTrades<=symbols*eventsPerSymbol,"bounded synthetic D observation at "+symbols+" symbols with automatic entries off");
        }
        File.WriteAllText(Path.Combine(output,"performance.json"),Serialization.Pack(measures));
    }
}
