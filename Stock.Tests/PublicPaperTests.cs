using System.IO;
using System.Text.Json;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void PublicPaperTests()
    {
        using var db=new TradingStore(Path.Combine(Dir,"public-paper.db"));var now=DailyTime(2,9,0);
        var cfg=DailyConfig();cfg.MinimumAverageTradingValue=0;cfg.MovingAveragePeriods="1";cfg.MinimumSupportCount=1;cfg.UsePivotSupport=cfg.UseApproximateVolumeZone=false;cfg.UseEmergencyStop=true;
        var settings=BatchSettings();settings.Execution.UseSpread=false;settings.Execution.ParticipationPercent=100;settings.Execution.FeePercent=settings.Execution.SlippagePercent=settings.Execution.TaxPercent=0;settings.Execution.LatencyMs=0;
        var warmup=Enumerable.Range(0,2).Select(i=>new StrategyBar(DailyBar(i,100),DailyTime(i,9,0),DailyTime(i),i,4000000000,"PUBLIC_TEST")).ToArray();
        var plan=new LiveBatchPlan([new(DailyStock,"진단",settings,20000){Oversold=new(cfg,new(1,BarUnit.Minute),warmup,DailyFacts(5),PublicPaper:true)}],20000,10000);
        var batch=new RealtimePaperStrategyBatch(db,()=>now);batch.Start(plan);
        CurrentMarketSnapshot Snapshot(decimal price,decimal volume)=>new(DailyStock,new(DailyStock.Name,price,price-100,price-100,now,"OPEN"),now,100,100,price,volume,volume*price,"네이버 KRX 공개 시세",TimeSpan.FromSeconds(7),ExchangeTimestamp:false);
        now=DailyTime(2,9,0).AddSeconds(59);batch.OnPublicSnapshot(Snapshot(80,1000));
        now=DailyTime(2,9,1);var bar=new StrategyBar(new(now,100,100,80,80,1000),now.AddMinutes(-1),now,10,100000,"PUBLIC_TEST");batch.OnPublicBar(DailyStock.Key,bar);
        batch.Pulse().GetAwaiter().GetResult();Check(batch.Snapshot().Rows.Single().Pending==0,"public completed-bar signal cannot order using its earlier snapshot");
        now=now.AddSeconds(1);batch.OnPublicSnapshot(Snapshot(80,1100));batch.Pulse().GetAwaiter().GetResult();
        Check(batch.Snapshot().Rows.Single() is {Pending:>0,Owned:0},"public simulation creates a virtual pending order after the closed bar and new observation");
        now=now.AddSeconds(7);var latest=Snapshot(79,2100);batch.OnPublicSnapshot(latest);batch.Pulse().GetAwaiter().GetResult();
        Check(batch.Snapshot().Rows.Single().Owned>0&&db.States("trade-marker:").Count==1,"later public observation fills the paper account without broker or quote-book data");
        var marker=db.States("trade-marker:").Select(s=>Serialization.Unpack<TradeMarker>(s.Json)).Single();
        Check(marker.Source==RealtimePaperStrategyBatch.PublicMarkerSource&&marker.Time==now&&marker.Price==79,"public paper marker labels its model and preserves observed time and price");
        batch.OnPublicSnapshot(latest);batch.OnPublicBar(DailyStock.Key,bar);Check(db.States("trade-marker:").Count==1,"repeated snapshot and completed minute do not duplicate fills");
        now=now.AddSeconds(30);batch.Pulse().GetAwaiter().GetResult();Check(!batch.Snapshot().Running,"stale public observation stops virtual orders");
        Check(db.ReadEvents(DailyStock.Key,"NAVER_OBSERVATION",DailyTime(0),now).Count==0,"public interval observations are never persisted as exchange ticks");
        using var json=JsonDocument.Parse("""[{"localDateTime":"20250106090000","currentPrice":80,"openPrice":100,"highPrice":100,"lowPrice":80,"accumulatedTradingVolume":1000}]""");
        var minute=PublicMarketData.ParsePublicMinutes(json.RootElement,DateTimeOffset.Parse("2025-01-06T09:00:30+09:00"));
        Check(minute.Length==1&&!minute[0].IsComplete&&minute[0].Volume==1000,"public minute volume is per candle and forming minute stays incomplete");
        using var broker=new RecordingBroker(Brokerage.Kiwoom);var source=new BrokerCurrentMarketData(broker,_=>100);var current=source.Current(DailyStock,CancellationToken.None).GetAwaiter().GetResult();
        Check(current.Source.Contains(broker.Name)&&current.PollInterval==TimeSpan.FromSeconds(1),"online current quote routes to connected broker with one-second cadence");
    }
    private static async Task PublicQuoteMonitor(string output)
    {
        Directory.CreateDirectory(output);using var provider=new PublicMarketData();using var pump=new SelectedQuotePump(provider);var rows=new List<object>();var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pump.Select(DailyStock,s=>{lock(rows){rows.Add(new{s.Stock.Symbol,s.Quote.Price,s.Quote.TradeTime,s.ReceivedAt,s.PollInterval,s.Source});Console.WriteLine($"PUBLIC {s.ReceivedAt:O} observed={s.Quote.TradeTime:O} price={s.Quote.Price} interval={s.PollInterval.TotalSeconds}");if(rows.Count>=4)done.TrySetResult();}},message=>done.TrySetException(new Exception(message)));
        await done.Task.WaitAsync(TimeSpan.FromSeconds(90));pump.Stop();await pump.Completion;
        var minute=await provider.MinuteHistory(DailyStock,KoreaClock.Today.AddDays(-14),KoreaClock.Today,CancellationToken.None);
        File.WriteAllText(Path.Combine(output,"observations.json"),Serialization.Pack(new{Rows=rows,MinuteCount=minute.Length,CompleteMinutes=minute.Count(b=>b.IsComplete),NoBrokerCalls=true}));
        Console.WriteLine($"PUBLIC minute history {minute.Length} bars; no account calls");
    }
}
