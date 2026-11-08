using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void DayTradingDataChecks()
    {
        var instrument=new Instrument("005930",Exchange.KRX);
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));
        var session=new DailyMarketSession(new(2026,11,6),open,open.AddHours(6.5));
        MarketEvent T(int seconds,decimal price,long sequence,decimal cumulative)=>new(instrument,EventKind.Trade,open.AddSeconds(seconds),open.AddSeconds(seconds),"TEST",sequence){TradePrice=price,TradeSize=10,CumulativeVolume=cumulative};
        var a=T(0,10100,1,10);var b=T(1,9900,2,20);var c=T(5,9950,3,30);
        var guard=new CausalMarketInput(instrument,new(),[session]);
        var first=guard.Observe(a,a.ReceivedTime);Check(first.Accepted&&!first.CanEnter,"first trade is observed but cannot prove continuous capture alone");
        var second=guard.Observe(b,b.ReceivedTime);Check(second.Accepted&&second.CanEnter&&guard.Coverage?.From==open,"second cumulative-consistent trade establishes causal continuity");
        Check(guard.Observe(b,b.ReceivedTime).Decision==FeedDecision.Duplicate&&guard.Duplicates==1,"duplicate tick does not advance bars or entry state");
        Check(guard.Observe(a,b.ReceivedTime).Decision==FeedDecision.OutOfOrder,"out-of-order tick cannot alter earlier indicators");
        Check(guard.Observe(c,c.ReceivedTime.AddSeconds(3)).Decision==FeedDecision.Stale,"old exchange time blocks a newly received strategy entry");
        Check(guard.Observe(c,c.ExchangeTime.AddTicks(-1)).Decision==FeedDecision.Invalid,"future source/received timestamps rejected");
        var gap=c with{CumulativeVolume=40};Check(!guard.Observe(gap,gap.ReceivedTime).CanEnter,"cumulative trade gap invalidates continuity without treating missing volume as trades");
        var after=T(6,9960,4,50);Check(guard.Observe(after,after.ReceivedTime).CanEnter&&guard.Coverage!.From==gap.ExchangeTime,"recovery begins a new observed segment after a gap");
        var other=c with{ExchangeTime=session.Close,ReceivedTime=session.Close};Check(guard.Observe(other,other.ReceivedTime).Decision==FeedDecision.OutsideSession,"session-close event cannot create an invented post-close entry");
        var uncertain=new CausalMarketInput(instrument,new(),[session]);uncertain.Observe(a,a.ReceivedTime);
        Check(!uncertain.Observe(b with{TimeQuality="KST 날짜 추정"},b.ReceivedTime).CanEnter,"inferred exchange timestamps remain observable but cannot authorize strict entries");
        var builder=new CausalBarBuilder(new(5,TimeframeUnit.Second),100);
        Check(builder.Add(a,session,null).Length==0&&builder.Active?.Price is {Open:10100,IsComplete:false},"active second candle is explicitly provisional");
        var span=new BarCoverage(open,b.ExchangeTime,true,"TEST",1,2);
        Check(builder.Add(b,session,span).Length==0&&builder.Active!.Price.Low==9900,"current candle updates observed low without closing or fabricating ticks");
        var ended=builder.Add(c,session,new(open,c.ExchangeTime,true,"TEST",1,3));
        Check(ended.Length==1&&ended[0].OpenTime==open&&ended[0].ClosedAt==open.AddSeconds(5)&&ended[0].Price.Volume==20&&ended[0].TradingValue==200000,"second candle closes causally with exact trade volume and notional");
        Check(builder.Active!.Price.Volume==10&&builder.Active.Price.Open==9950,"boundary trade belongs exclusively to the following active candle");
        var noWatermark=builder.CloseThrough(open.AddSeconds(10),session,null);Check(noWatermark==null,"an elapsed timer without verified feed coverage cannot fabricate a closed candle");
        var noData=T(20,10000,4,40);var later=builder.Add(noData,session,new(open,noData.ExchangeTime,true,"TEST",1,4));
        Check(later.Length==1&&later[0].OpenTime==open.AddSeconds(5)&&builder.Active!.OpenTime==open.AddSeconds(20),"empty time buckets are not synthesized between observed trades");
        var ticks=new CausalBarBuilder(new(2,TimeframeUnit.Trade),10);
        ticks.Add(a,session,new(open,open,true,"TEST",1,1));var tickBar=ticks.Add(c,session,new(open,c.ExchangeTime,true,"TEST",1,3));
        Check(tickBar.Length==1&&tickBar[0].Price.Volume==20&&tickBar[0].ClosedAt==open.AddSeconds(5),"two-trade candle spans five actual seconds rather than assuming a two-second duration");
        var shortEnd=new CausalBarBuilder(new(1,TimeframeUnit.Hour),100);
        var last=T(6*3600,10000,9,100);shortEnd.Add(last,session,new(last.ExchangeTime,last.ExchangeTime,true,"TEST"));
        Check(shortEnd.CloseThrough(session.Close,session,new(last.ExchangeTime,session.Close,true,"TEST"))==null,"shortened session-end half hour never becomes a full one-hour candle");
        var limited=new CausalBarBuilder(new(1,TimeframeUnit.Minute),1);limited.Add(a,session,null);limited.Add(b,session,span);
        Check(limited.Overflowed&&limited.Active==null,"active-buffer safety overflow is explicit rather than unbounded memory or a false close");
        DayTradingIndicatorChecks();
    }
    private static StrategyDDataSeed StrategyDSeed(DateTimeOffset open,bool includeFuture=false)
    {
        var frame=new TimeframeSpec(1,TimeframeUnit.TradingDay);var bars=new List<KnownStrategyBar>();var sessions=new List<DailyMarketSession>();
        var date=DateOnly.FromDateTime(open.Date);var day=date;var prior=new List<DateOnly>();
        while(prior.Count<240){day=day.AddDays(-1);if(day.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday))prior.Add(day);}
        prior.Reverse();
        for(int index=0;index<prior.Count;index++)
        {
            var start=new DateTimeOffset(prior[index].ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9));var close=start.AddHours(6.5);
            var high=index==230?12000m:10000m;
            bars.Add(new(frame,new(new(close,10000,high,9900,10000,1000),start,close,index,10_000_000,"FIXTURE"),close));sessions.Add(new(prior[index],start,close));
        }
        sessions.Add(new(date,open,open.AddHours(6.5)));
        if(includeFuture){var close=open.AddHours(6.5);bars.Add(new(frame,new(new(close,999999,999999,9900,999999,999999),open,close,241,999999,"FUTURE"),close));}
        var facts=new HistoricalFacts{Source="TEST verified causal sessions",MarketSessions=sessions.ToArray(),
            StatusRecords=[new("005930",date,open,HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false)],RiskRecords=[new("005930",date,false,open)]};
        return new(bars.ToArray(),facts);
    }
    private static StrategyDefinition StrategyDTestConfig()
    {
        var d=StrategyDDefaults.Create(Epoch);d.Configuration.AverageTradingValueLookbackDays=2;d.Configuration.MinimumAverageTradingValue=0;
        d.Configuration.MinimumDrawdownPercent=-15;
        d.Configuration.VolumeZoneLookbackDays=2;d.Configuration.VolumeZoneMergeRangePercent=0;d.DayTrading!.Support.MaxZoneWidthPercent=3;
        d.DayTrading.AdvancedTimeframes=true;d.DayTrading.IndicatorTimeframes[IndicatorKind.Liquidity]=new(1,TimeframeUnit.TradingDay);return d;
    }
    private static void DayTradingIndicatorChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var instrument=new Instrument("005930",Exchange.KRX);
        MarketEvent T(int seconds,decimal price,long sequence)=>new(instrument,EventKind.Trade,open.AddSeconds(seconds),open.AddSeconds(seconds),"TEST",sequence){TradePrice=price,TradeSize=10,CumulativeVolume=sequence*10,High=999999};
        var definition=StrategyDTestConfig();var engine=new StrategyDDataEngine(instrument,definition,StrategyDSeed(open,true));
        engine.OnMarket(T(0,10100,1),open);engine.OnMarket(T(1,9900,2),open.AddSeconds(1));
        var observed=engine.Latest!;
        Check(observed.Supports.Length>0&&observed.Supports.All(s=>s.MovingAverage==10000&&s.MovingAveragePeriod==240),"one-minute analysis retains the exact preceding 240 completed daily closes for MA240");
        Check(observed.Crash.Medium==(9900m/12000-1)*100&&observed.Crash.Short==(9900m/10100-1)*100,"daily crash combines N-1 prior daily highs and only observed intraday highs");
        Check(observed.Crash.Session==(9900m/10100-1)*100&&observed.Crash.Session!= (9900m/999999-1)*100,"provider final-day high field is never used as a future session high");
        Check(observed.Crash.Rolling==null,"full rolling interval remains unready before continuous window capture");
        var withoutFuture=new StrategyDDataEngine(instrument,definition,StrategyDSeed(open));withoutFuture.OnMarket(T(0,10100,1),open);withoutFuture.OnMarket(T(1,9900,2),open.AddSeconds(1));
        Check(Serialization.Pack(observed)==Serialization.Pack(withoutFuture.Latest),"future completed daily suffix changes neither present MA, crash, volume zone nor facts");
        Check(observed.SecurityTradable==true&&observed.FundamentalRisk==false&&observed.AverageTradingValue==10_000_000,"point-in-time status/risk and independent daily liquidity use known input only");
        var zone=new VolumeZone(9900,10000,10100,100,"TEST",ProfilePrecision.Traded,open);
        var matches=StrategyDSupports.Overlap([(240,10000m)],new(1,TimeframeUnit.TradingDay),[zone],definition.DayTrading!.Support,open);
        Check(matches.Length==1&&matches[0].GapPercent==0,"MA inside volume zone has zero overlap gap");
        Check(StrategyDSupports.Overlap([(120,10000m),(240,10000m)],new(1,TimeframeUnit.TradingDay),[],definition.DayTrading.Support,open).Length==0,"two moving averages alone never satisfy D heterogeneous support");
        Check(StrategyDSupports.Overlap([(240,9000m)],new(1,TimeframeUnit.TradingDay),[zone],definition.DayTrading.Support,open).Length==0,"distant MA and zone cannot become valid merely by support count");
        Check(StrategyDSupports.Overlap([(240,10000m)],new(1,TimeframeUnit.TradingDay),[zone with{Lower=8000,Upper=12000}],definition.DayTrading.Support,open).Length==0,"overwide high-volume zone is explicitly excluded");
        Check(StrategyDSupports.Overlap([(240,10000m)],new(1,TimeframeUnit.TradingDay),[zone with{KnownAt=open.AddHours(1)}],definition.DayTrading.Support,open).Length==0,"future-known volume zone cannot appear in earlier support snapshot");
        var volume=new TradeVolumeProfile(instrument,10,8,100);volume.Add(T(0,9900,1),open);volume.Add(T(1,10000,2),open);volume.Add(T(2,20000,3),open);
        Check(volume.Volume==30&&volume.Bins().Length<=8&&volume.EffectiveBinSize>=10,"trade profile expands bins within bounded capacity without losing exact executed volume");
        volume.Expire(open.AddSeconds(1));Check(volume.Volume==20&&volume.RetainedEvents==2,"rolling trade profile subtracts expired actual executions");
        var limited=new TradeVolumeProfile(instrument,100,8,1);limited.Add(T(0,9900,1),open);limited.Add(T(1,10000,2),open);
        Check(limited.Overflowed&&limited.RetainedEvents==1,"trade profile memory overflow is surfaced without claiming complete precision");
        var tradeConfig=definition.Snapshot();tradeConfig.DayTrading!.Support.ProfileSource=VolumeProfileSource.TradeBased;
        var missing=new StrategyDDataEngine(instrument,tradeConfig,StrategyDSeed(open));missing.OnMarket(T(0,10100,1),open);missing.OnMarket(T(1,9900,2),open.AddSeconds(1));
        Check(missing.Latest!.Supports.Length==0,"missing historical trade profile never silently falls back to approximate daily volume");
        foreach(var combination in new[]{RuleCombination.Any,RuleCombination.All})
        {
            var d=definition.Snapshot();d.DayTrading!.Crash.DailyIntradayCombination=combination;d.DayTrading.Crash.RollingHighEnabled=false;d.DayTrading.Crash.SessionDrawdownPercent=-4;
            var e=new StrategyDDataEngine(instrument,d,StrategyDSeed(open));e.OnMarket(T(0,10100,1),open);e.OnMarket(T(1,9900,2),open.AddSeconds(1));
            Check(e.Latest!.Crash.Passed==(combination==RuleCombination.Any),"daily-only crash respects daily/intraday "+combination+" combination");
        }
        var intraday=definition.Snapshot();intraday.DayTrading!.Crash.DailyEnabled=false;intraday.DayTrading.Crash.RollingHighEnabled=false;intraday.DayTrading.Crash.SessionDrawdownPercent=-1;
        var intradayEngine=new StrategyDDataEngine(instrument,intraday,StrategyDSeed(open));intradayEngine.OnMarket(T(0,10100,1),open);intradayEngine.OnMarket(T(1,9900,2),open.AddSeconds(1));
        Check(intradayEngine.Latest!.Crash.Passed==true,"enabled intraday-only crash can trigger independently of daily rules");
        var none=definition.Snapshot();none.DayTrading!.Crash.MediumEnabled=false;none.DayTrading.Crash.RollingHighEnabled=false;none.Configuration.ShortCrashPercent=-99;none.DayTrading.Crash.SessionDrawdownPercent=-99;none.DayTrading.Crash.RollingDrawdownPercent=-99;
        var noneEngine=new StrategyDDataEngine(instrument,none,StrategyDSeed(open));noneEngine.OnMarket(T(0,10100,1),open);noneEngine.OnMarket(T(1,9900,2),open.AddSeconds(1));
        Check(noneEngine.Latest!.Crash.Passed==false,"no actual crash does not become a signal from enabled switches alone");
        var closedEngine=new StrategyDDataEngine(instrument,definition,StrategyDSeed(open));
        closedEngine.OnMarket(T(0,10100,1),open);closedEngine.OnMarket(T(1,9900,2),open.AddSeconds(1));closedEngine.OnMarket(T(59,9920,3),open.AddSeconds(59));
        closedEngine.OnMarket(T(60,11000,4),open.AddSeconds(60));
        Check(closedEngine.LatestClosedAnalysis!.Price==9920&&closedEngine.Latest!.Price==11000,"bar-close analysis never takes the following boundary trade as the preceding bar close");
        var stopSeed=StrategyDSeed(open);stopSeed.Facts.RiskRecords=[new("005930",new(2026,11,6),false,open.AddHours(1))];
        var unknownRisk=new StrategyDDataEngine(instrument,definition,stopSeed);unknownRisk.OnMarket(T(0,10100,1),open);
        Check(unknownRisk.Latest!.FundamentalRisk==null,"future-known clearance cannot clear a present unknown fundamental risk");
    }
}
