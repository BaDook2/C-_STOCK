using System.Diagnostics;
using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
using Stock.Core.Research.Oversold;
internal static partial class Program
{
    private static void CompactCalculationChecks()
    {
        var c=new OversoldConfig{MovingAveragePeriods="1,3,20"};var random=new Random(721);var history=new List<PriceBar>();var ma=new MovingAverageSupportProvider();var zones=new ApproximateVolumeZoneProvider();
        for(var i=0;i<300;i++)
        {
            var price=70000+random.Next(-10000,10001);history.Add(new(Epoch.AddMinutes(i),price,price+random.Next(0,1200),price-random.Next(0,1200),price,random.Next(0,10000)));
            var actual=ma.Get(history,c);var expected=c.MaPeriods().Where(n=>history.Count>=n).Select(n=>history.TakeLast(n).Average(b=>b.Close));
            if(!actual.Select(s=>s.Center).SequenceEqual(expected))throw new Exception("MA prefix differs from sequential sum at "+i);
            if(i%20!=0)continue;
            var dense=zones.Allocate(history,c);var size=zones.EffectiveBinSize;var bins=new SortedDictionary<decimal,decimal>();
            foreach(var bar in history.TakeLast(c.VolumeZoneLookbackDays)){var first=decimal.Floor(bar.Low/size);var last=decimal.Floor(bar.High/size);var allocation=bar.Volume/(last-first+1);decimal assigned=0;for(var k=first;k<=last;k++){var amount=k==last?bar.Volume-assigned:allocation;bins[k*size]=bins.GetValueOrDefault(k*size)+amount;assigned+=amount;}}
            if(!dense.SequenceEqual(bins.Select(b=>new ApproximateBin(b.Key,b.Key+size,b.Value))))throw new Exception("Dense volume differs from reference at "+i);
        }
        Check(true,"300 sequential MA prefixes and 15 volume-bin histories match original decimal results exactly");
        var precise=new List<PriceBar>{new(Epoch,1m,1m,1m,.1234567890123456789012345678m,1)};var fallback=new MovingAverageSupportProvider();Check(fallback.Get(precise,c).Single().Center==precise[0].Close,"high-precision MA uses the original arithmetic path");
    }
    private static void ScreeningPerformanceChecks(string output)
    {
        Directory.CreateDirectory(output);CompactCalculationChecks();
        var sessions=Enumerable.Range(0,28).Select(i=>new DateOnly(2026,9,1).AddDays(i)).Where(d=>d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).Select(d=>new DailyMarketSession(d,new(d.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new(d.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)))).ToArray();
        var bars=sessions.SelectMany((s,day)=>Enumerable.Range(0,390).Select(i=>{var close=70000m+(decimal)Math.Sin((day*390+i)/50d)*3000;var open=s.Open.AddMinutes(i);return new StrategyBar(new(open.AddMinutes(1),close,close+100,close-100,close,10000),open,open.AddMinutes(1),day*390+i,700000000,"PERFORMANCE_FIXTURE");})).ToArray();
        var c=new OversoldConfig();var facts=new HistoricalFacts{MarketSessions=sessions,Source="PERFORMANCE_FIXTURE"};
        // Same 20-session history and defaults as a one-minute screen, without broker/network variance.
        var watch=Stopwatch.StartNew();var allocated=GC.GetTotalAllocatedBytes(true);
        var row=new OversoldScreeningEngine().EvaluateObserved(DailyStock,bars,c,facts,sessions[^1].Close,from:sessions[^5].Date,to:sessions[^1].Date);
        watch.Stop();var result=new{Bars=bars.Length,Milliseconds=watch.Elapsed.TotalMilliseconds,AllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,SnapshotHash=TradingStore.Hash(Serialization.Pack(row)),row.Status};
        File.WriteAllText(Path.Combine(output,"performance.json"),Serialization.Pack(result));File.WriteAllText(Path.Combine(output,"snapshot.json"),Serialization.Pack(row));Console.WriteLine(Serialization.Pack(result));
    }
}
