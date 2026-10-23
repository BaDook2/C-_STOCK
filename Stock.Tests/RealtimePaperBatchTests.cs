using System.IO;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void RealtimePaperBatchTests()
    {
        using var store=new TradingStore(Path.Combine(Dir,"paper-live.db"));var now=DailyTime(2,9,0);
        var config=DailyConfig();config.UseEmergencyStop=true;config.MinimumAverageTradingValue=0;config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.UsePivotSupport=config.UseApproximateVolumeZone=false;
        var warmup=Enumerable.Range(0,2).Select(i=>new StrategyBar(DailyBar(i,100),DailyTime(i,9,0),DailyTime(i),i,4_000_000_000,"TEST_FIXTURE")).ToArray();
        var settings=BatchSettings();settings.Execution.UseSpread=false;settings.Execution.ParticipationPercent=100;settings.Execution.LatencyMs=0;settings.Execution.SlippagePercent=0;settings.Execution.FeePercent=0;settings.Execution.TaxPercent=0;
        var profile=new OversoldLiveConfiguration(config,new(1,BarUnit.Tick),warmup,DailyFacts(5));
        var plan=new LiveBatchPlan([new LiveBatchItem(DailyStock,"국내 과매도·낙주 반등",settings,20000){Oversold=profile}],20000,10000);
        var batch=new RealtimePaperStrategyBatch(store,()=>now);var recorded=new List<TradeMarker>();batch.FillRecorded+=recorded.Add;batch.Start(plan);
        void Event(int n,decimal price)
        {
            now=DailyTime(2,9,0).AddSeconds(n);var instrument=new Instrument(DailyStock.Symbol,Exchange.KRX);
            batch.OnMarket(new(instrument,EventKind.Trade,now,now,"KIWOOM",n*2+1){TradePrice=price,TradeSize=1000,CumulativeVolume=1000*(n+1),PreviousClose=100});
            batch.OnMarket(new(instrument,EventKind.Quote,now,now,"KIWOOM",n*2+2){Bid=price,Ask=price+1,BidSize=1000,AskSize=1000});
        }
        Event(0,80);batch.Pulse().GetAwaiter().GetResult();Event(1,80);batch.Pulse().GetAwaiter().GetResult();Event(2,80);batch.Pulse().GetAwaiter().GetResult();
        Check(batch.Snapshot().Rows.Single().Pending>0&&batch.Snapshot().Rows.Single().Owned==0,"realtime paper signal creates only a virtual pending order");
        Event(3,79);batch.Pulse().GetAwaiter().GetResult();var row=batch.Snapshot().Rows.Single();
        Check(row.Owned>0&&row.Pending==0&&recorded.Count==1&&recorded[0].Source==RealtimePaperStrategyBatch.MarkerSource,"next real event confirms a paper fill and stores one live-paper marker");
        Check(store.States("trade-marker:").Select(x=>Serialization.Unpack<TradeMarker>(x.Json)).Single().Time==now,"paper marker preserves exact exchange execution time");
        var tickBars=Enumerable.Range(0,4).Select(i=>new PriceBar(DailyTime(2,9,0).AddSeconds(i),80-i/3m,80-i/3m,80-i/3m,80-i/3m,1000)).ToArray();
        Check(ChartPeriods.FromTrades(tickBars,"1틱").Length==4&&ChartPeriods.FromTrades(tickBars,"1분").Length==1,"the same live paper execution timeline supports tick and minute charts");
        var marker=recorded.Single();var minute=ChartPeriods.FromTrades(tickBars,"1분");var daily=new[]{DailyBar(2,79)};var weekly=ChartPeriods.Aggregate(daily,"주봉");
        Check(CandleChart.TradeBarIndex(tickBars,marker,"1틱")>=0&&CandleChart.TradeBarIndex(minute,marker,"1분")>=0&&CandleChart.TradeBarIndex(daily,marker,"일봉")>=0&&CandleChart.TradeBarIndex(weekly,marker,"주봉")>=0,"one exact paper fill maps to tick, minute, daily and weekly chart bars");
        batch.Stop();Check(!batch.Snapshot().Running&&batch.Snapshot().Rows.Single().Owned>0,"stopping live paper blocks new orders without inventing liquidation");
    }
}
