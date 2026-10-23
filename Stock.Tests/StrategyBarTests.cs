using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void StrategyBarTests()
    {
        foreach(var value in new[]{"0","-1","","abc","2147483648","1.5","1,2"})Throws<InvalidOperationException>(()=>BarSpecification.Parse(value,BarUnit.Tick),"invalid bar value blocked: "+value);
        Check(BarSpecification.Parse("1,000",BarUnit.Tick).Value==1000,"western thousands grouping accepted for arbitrary bar count");
        Check(BarSpecification.Parse("2147483647",BarUnit.Hour).Value==int.MaxValue,"any positive int bar value is supported without fixed choices");
        var open=DateTimeOffset.Parse("2026-09-25T09:00:00+09:00");var close=open.AddHours(6.5);var instrument=new Instrument("005930",Exchange.KRX);
        DailyMarketSession[] sessions=[new(new(2026,9,25),open,close)];var coverage=new BarCoverage(open,close,true,"TEST_FIXTURE");
        MarketEvent E(int n,decimal price=100)=>new(instrument,EventKind.Trade,open.AddSeconds(n),open.AddSeconds(n),"TEST",n){TradePrice=price,TradeSize=2,CumulativeVolume=(n+1)*2};
        var ticks=Enumerable.Range(0,205).Select(n=>E(n,100+n%7)).ToArray();
        var a=StrategyBarAggregator.FromTrades(ticks.Reverse(),instrument,new(100,BarUnit.Tick),sessions,coverage,close);
        Check(a.Bars.Length==2&&a.Incomplete==1&&a.Bars[0].Price.Volume==200&&a.Bars[0].TradingValue==ticks.Take(100).Sum(e=>e.TradePrice*e.TradeSize),"100-tick exact OHLCV and notional use individual sizes with incomplete remainder excluded");
        var equal=new[]{E(0) with {Sequence=2,CumulativeVolume=null,TradePrice=103},E(0) with {Sequence=1,CumulativeVolume=null,TradePrice=99}};
        a=StrategyBarAggregator.FromTrades(equal,instrument,new(2,BarUnit.Tick),sessions,coverage,close);
        Check(a.Bars[0].Price.Open==99&&a.Bars[0].Price.Close==103,"equal timestamps use stable sequence order");
        Check(StrategyBarAggregator.FromTrades(equal.Concat(equal),instrument,new(2,BarUnit.Tick),sessions,coverage,close).Bars.Length==1,"duplicate identical trade identity does not double count");
        Check(StrategyBarAggregator.FromTrades(ticks.Where(e=>e.Sequence!=5),instrument,new(100,BarUnit.Tick),sessions,coverage,close).Missing==1,"cumulative discontinuity blocks missing-tick session");
        Check(StrategyBarAggregator.FromTrades(ticks,instrument,new(100,BarUnit.Tick),sessions,coverage with{Complete=false},close).Bars.Length==0,"unverified raw capture cannot masquerade as complete history");
        a=StrategyBarAggregator.FromTrades(ticks,instrument,new(3,BarUnit.Minute),sessions,coverage,open.AddMinutes(3));
        Check(a.Bars.Length==1&&a.Bars[0].OpenTime==open&&a.Bars[0].ClosedAt==open.AddMinutes(3)&&a.Bars[0].Price.Volume==360,"3-minute session-aligned bucket excludes boundary trade belonging to next bucket");
        Check(StrategyBarAggregator.FromTrades(ticks,instrument,new(1,BarUnit.Minute),sessions,coverage,open.AddSeconds(59)).Bars.Length==0,"future and incomplete minute bars excluded");
        var minutes=Enumerable.Range(0,240).Select(i=>new StrategyBar(new(open.AddMinutes(i+1),100,102,99,101,10),open.AddMinutes(i),open.AddMinutes(i+1),i,1003,"TEST_FIXTURE")).ToArray();
        a=StrategyBarAggregator.FromMinutes(minutes,new(4,BarUnit.Hour),sessions,close);
        Check(a.Bars.Length==1&&a.Bars[0].Price.Volume==2400&&a.Bars[0].TradingValue==240720,"arbitrary 4-hour aggregation retains exact minute notional");
        Check(StrategyBarAggregator.FromMinutes(minutes.Where(b=>b.Sequence!=15),new(1,BarUnit.Hour),sessions,close).Missing==1,"missing minute prevents that hour but not other complete hours");
        Check(StrategyBarAggregator.FromMinutes(minutes.Select(b=>b with{TradingValue=null}),new(1,BarUnit.Hour),sessions,close).Bars.All(b=>b.TradingValue==null),"no close-times-volume proxy for missing notional");
        Check(StrategyBarAggregator.FromMinutes(minutes,new(int.MaxValue,BarUnit.Hour),sessions,close).Incomplete==1,"huge valid duration returns incomplete without arithmetic overflow");
        Check(StrategyBarAggregator.FromTrades(ticks,instrument,new(int.MaxValue,BarUnit.Hour),sessions,coverage,close).Incomplete==1,"huge valid tick-source duration returns incomplete without overflow");
        Check(StrategyBarAggregator.FromTrades(ticks,instrument,new(1,BarUnit.Minute),[],coverage,close).Bars.Length==0,"no verified session means no inferred weekday or holiday candles");
        Check(StrategyBarAggregator.FromMinutes(minutes,new(100,BarUnit.Tick),sessions,close).Missing==1,"minute source never fabricates ticks");
    }
}
