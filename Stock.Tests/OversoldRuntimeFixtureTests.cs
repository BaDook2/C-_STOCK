using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    /// <summary>Isolated fake broker only; no actual account credentials or network orders.</summary>
    private static void OversoldRuntimeFixtureTests()
    {
        var c=DailyConfig();c.MinimumAverageTradingValue=0;c.MovingAveragePeriods="1";c.MinimumSupportCount=1;c.UsePivotSupport=c.UseApproximateVolumeZone=false;
        var facts=DailyFacts(5);var stock=new Instrument(DailyStock.Symbol,DailyStock.Exchange);
        var warm=Enumerable.Range(0,2).Select(i=>new StrategyBar(DailyBar(i,100),DailyTime(i,9,0),DailyTime(i),i,4_000_000_000,"TEST_FIXTURE")).ToArray();
        var runtime=new OversoldLiveStrategy(stock,new(c,new(1,BarUnit.Tick),warm,facts));var independent=new OversoldLiveStrategy(stock,new(c,new(1,BarUnit.Tick),warm,facts));var settings=Settings();var context=new MarketContext(stock,settings);
        MarketEvent Event(int n)=>new(stock,EventKind.Trade,DailyTime(2,9,0).AddSeconds(n),DailyTime(2,9,0).AddSeconds(n),"TEST",n){TradePrice=80,TradeSize=10,CumulativeVolume=10*(n+1)};
        for(int n=0;n<2;n++){var e=Event(n);runtime.OnMarket(e);context.Add(e);}Check(runtime.Evaluate(context).Buy==null&&runtime.Position.Quantity==0,"completed-bar runtime cannot use same tick as its own signal fill");
        var next=Event(2);runtime.OnMarket(next);context.Add(next);var signal=runtime.Evaluate(context);var snapshot=runtime.Snapshot;Check(signal.Buy==true&&snapshot!=null&&snapshot.SignalTime<next.ExchangeTime,"isolated runtime emits prior completed-bar signal at later observed price");
        using var fake=new RecordingBroker(Brokerage.Kiwoom);fake.SubmitAsync(new(DailyStock.Symbol,10,80,true)).GetAwaiter().GetResult();Check(fake.Submitted==1&&runtime.Position.Quantity==0,"fake order acceptance alone does not update positions or entry count");
        var first=new BrokerExecution("fixture-fill-1","fixture-order-1",DailyStock.Symbol,true,10,80,next.ExchangeTime);runtime.OnExecution(first,"Entry1",snapshot);runtime.OnExecution(first,"Entry1",snapshot);
        Check(runtime.Position is {Quantity:10,Entries:1}&&independent.Position.Quantity==0,"duplicate execution id is idempotent and independent runtime state remains empty");
        runtime.OnExecution(first with{Id="fixture-fill-2",Quantity=5,Price=83},"Entry1",snapshot);Check(runtime.Position is {Quantity:15,Entries:1,Average:81},"partial fills accumulate weighted price without counting same entry stage twice");
        Throws<InvalidOperationException>(()=>runtime.OnExecution(first with{Id="fixture-invalid",Symbol="000660"},"Entry1",snapshot),"mismatched broker execution cannot alter isolated runtime");
        var broken=new OversoldLiveStrategy(stock,new(c,new(1,BarUnit.Tick),warm,facts));var brokenContext=new MarketContext(stock,settings);
        for(int n=0;n<3;n++){var trade=Event(n);broken.OnMarket(trade);brokenContext.Add(trade);}
        Check(broken.Evaluate(brokenContext).Buy==true,"verified tick segment yields the next-tick entry signal");
        var gap=Event(3) with{CumulativeVolume=100};broken.OnMarket(gap);brokenContext.Add(gap);
        Check(broken.Evaluate(brokenContext).Buy==null&&broken.Snapshot==null,"cumulative gap invalidates pending live signal before any order");
        var minute=new OversoldLiveStrategy(stock,new(c,new(1,BarUnit.Minute),warm,facts));var minuteContext=new MarketContext(stock,settings);long minuteSequence=50;
        MarketEvent MinuteEvent(int second,decimal price,decimal cumulative){var time=DailyTime(2,9,0).AddSeconds(second);return new(stock,EventKind.Trade,time,time,"TEST_MINUTE",++minuteSequence){TradePrice=price,TradeSize=10,CumulativeVolume=cumulative};}
        foreach(var trade in new[]{MinuteEvent(0,100,10),MinuteEvent(59,80,20),MinuteEvent(60,80,30),MinuteEvent(61,80,40)}){minute.OnMarket(trade);minuteContext.Add(trade);}
        Check(minute.Evaluate(minuteContext).Buy==true&&minute.Snapshot?.SignalTime==DailyTime(2,9,1),"one-minute live strategy closes the session-aligned minute and releases its signal only on a later trade");
        var hourly=new OversoldLiveStrategy(stock,new(c,new(1,BarUnit.Hour),warm,facts));var hourContext=new MarketContext(stock,settings);long hourSequence=100;
        MarketEvent HourEvent(int minute,int second,decimal price,decimal cumulative){var time=DailyTime(2,9,0).AddMinutes(minute).AddSeconds(second);return new(stock,EventKind.Trade,time,time,"TEST_HOUR",++hourSequence){TradePrice=price,TradeSize=10,CumulativeVolume=cumulative};}
        foreach(var trade in new[]{HourEvent(0,0,100,10),HourEvent(59,59,80,20),HourEvent(60,0,80,30),HourEvent(60,1,80,40)}){hourly.OnMarket(trade);hourContext.Add(trade);}
        Check(hourly.Evaluate(hourContext).Buy==true&&hourly.Snapshot?.SignalTime==DailyTime(2,10,0),"one-hour live strategy closes the session-aligned hour and releases its signal only on a later trade");
    }
}
