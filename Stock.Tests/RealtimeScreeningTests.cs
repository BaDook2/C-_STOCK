using Stock.Core;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void RealtimeScreeningTests()
    {
        var now=DailyTime(2,9,0);
        var config=DailyConfig();
        config.MinimumAverageTradingValue=0;
        config.MovingAveragePeriods="1";
        config.MinimumSupportCount=1;
        config.UsePivotSupport=config.UseApproximateVolumeZone=false;
        var warmup=Enumerable.Range(0,2).Select(i=>new StrategyBar(DailyBar(i,100),DailyTime(i,9,0),DailyTime(i),i,4_000_000_000,"TEST_FIXTURE")).ToArray();
        var settings=BatchSettings();
        settings.Execution.StaleSeconds=30;
        var profile=new OversoldLiveConfiguration(config,new(1,BarUnit.Tick),warmup,DailyFacts(5),ResearchOnly:true);
        var item=new LiveBatchItem(DailyStock,"국내 과매도·낙주 반등",settings,20000){Oversold=profile};
        var service=new RealtimeOversoldScreeningService(()=>now);
        var signals=new List<RealtimeScreeningSignal>();
        service.SignalRaised+=signals.Add;
        service.Start(new([item],"국내 과매도·낙주 반등",new(1,BarUnit.Tick)));

        void Event(int n,decimal price=80)
        {
            now=DailyTime(2,9,0).AddSeconds(n);
            var instrument=new Instrument(DailyStock.Symbol,Exchange.KRX);
            service.OnMarket(new(instrument,EventKind.Trade,now,now,"KIWOOM",n+1)
            {TradePrice=price,TradeSize=1000,CumulativeVolume=1000*(n+1),PreviousClose=100});
            service.Pulse();
        }

        Event(0);Event(1);Event(2);
        var snapshot=service.Snapshot();
        Check(signals.Count==1&&!signals[0].AutoTradingEligible&&snapshot.Rows.Single() is {BuySignal:true,StrategyState:"매수 신호 발생"},"realtime screening displays a shared-engine buy signal while automatic trading is off");
        Check(snapshot.Rows.Single().Proximity is >=0 and <=100&&snapshot.Rows.Single().LastAnalyzedAt!=null,"realtime screening publishes bounded condition proximity and last analysis time");

        service.SetAutoTrading(true);
        service.Pulse();
        Check(signals.Count==1,"enabling automatic trading never replays an already observed signal");
        Event(3);
        Check(signals.Count==1,"the signal present at activation remains observation-only on later ticks");
        service.Stop();

        now=DailyTime(2,9,0).AddSeconds(-1);
        var armed=new RealtimeOversoldScreeningService(()=>now);var armedSignals=new List<RealtimeScreeningSignal>();armed.SignalRaised+=armedSignals.Add;
        armed.Start(new([item],"국내 과매도·낙주 반등",new(1,BarUnit.Tick)));armed.SetAutoTrading(true);
        void ArmedEvent(int n)
        {
            now=DailyTime(2,9,0).AddSeconds(n);var instrument=new Instrument(DailyStock.Symbol,Exchange.KRX);
            armed.OnMarket(new(instrument,EventKind.Trade,now,now,"KIWOOM",n+1){TradePrice=80,TradeSize=1000,CumulativeVolume=1000*(n+1),PreviousClose=100});armed.Pulse();
        }
        ArmedEvent(0);ArmedEvent(1);ArmedEvent(2);
        Check(armedSignals.Count==1&&armedSignals[0].AutoTradingEligible,"a signal formed strictly after activation becomes automatic-trading eligible");
        armed.SetAutoTrading(false);ArmedEvent(3);
        Check(!armed.Snapshot().AutoTradingEnabled,"unchecking automatic trading immediately blocks later entry eligibility while monitoring continues");
        armed.FeedFault("fixture disconnect");snapshot=armed.Snapshot();
        Check(!snapshot.Running&&!snapshot.AutoTradingEnabled&&snapshot.ConnectionState=="연결 끊김"&&snapshot.Rows.Single().DataState.Contains("신규 주문 차단"),"feed failure stops observation, disables automatic trading and exposes a blocked data state");
        armed.Stop();Check(!armed.Snapshot().Running,"realtime screening stop ends observation without creating a liquidation order");

        Throws<InvalidOperationException>(()=>service.Start(new([],"전략",new(1,BarUnit.Minute))),"realtime screening rejects an empty universe");
        Throws<InvalidOperationException>(()=>service.Start(new(Enumerable.Repeat(item,101).Select((x,i)=>x with{Stock=x.Stock with{Symbol=(i+1).ToString("D6")}}).ToArray(),"전략",new(1,BarUnit.Minute))),"realtime screening caps the monitored universe at one hundred symbols");
    }
}
