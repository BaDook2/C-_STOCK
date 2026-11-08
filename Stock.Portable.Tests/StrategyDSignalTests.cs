using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void StrategyDSignalChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var instrument=new Instrument("005930",Exchange.KRX);
        var definition=StrategyDTestConfig();var seed=StrategyDSeed(open);var data=new StrategyDDataEngine(instrument,definition,seed);
        var signals=new StrategyDSignalEngine(instrument,definition,seed.Facts.Sessions,"test-instance");
        StrategyDSignalState Step(int second,decimal price,long sequence,StrategyDEntryContext? position=null)
        {
            var time=open.AddSeconds(second);var trade=new MarketEvent(instrument,EventKind.Trade,time,time,"TEST",sequence){TradePrice=price,TradeSize=10,CumulativeVolume=sequence*10};
            data.OnMarket(trade,time);return signals.Observe(data.Latest!,position??new());
        }
        var approach=Step(0,10100,1);Check(approach.Lowest==10100&&approach.Signal==null,"first actual support-approach tick initializes D rebound low without buying");
        var low=Step(1,9900,2);Check(low.Lowest==9900&&low.Signal==null,"subsequent observed lower trade updates the same episode low");
        Check(Step(2,9920,3).Signal==null,"9920 does not satisfy 0.5 percent rebound from 9900");
        var entry=Step(3,9950,4);var first=entry.Signal!;
        Check(first!=null&&first.Price==9950&&first.ReboundLow==9900&&first.Time==open.AddSeconds(3),"9950 first upward crossing emits exactly one signal at the actual tick time and price");
        Check(first!.Crash.At==open&&first.Crash.Snapshot.Types.Contains("DailyMedium"),"entry retains the original crash event snapshot instead of requiring drawdown to persist forever");
        Check(Step(4,9955,5).Signal==null&&signals.Observe(data.Latest!,new()).Signal==null,"persistent true rebound and duplicate observation cannot repeat the same buy signal");
        var exit=new StrategyDEntryContext(SymbolEntries:1,StrategyEntries:1,AccountEntries:1,LastExit:open.AddSeconds(10),LastExitReason:"TP1",PriorSupport:first.Support);
        Check(Step(20,9960,6,exit).Stage==StrategyDStage.Cooldown,"same-zone reentry respects elapsed cooldown after the confirmed exit");
        Check(Step(70,9950,7,exit).Signal==null,"expired cooldown alone cannot reuse the previous rebound signal");
        Step(71,9900,8,exit);var second=Step(72,9950,9,exit).Signal;
        Check(second!=null&&second.Episode>first.Episode&&second.Id!=first.Id&&second.Support.SamePhysicalZone(first.Support,.5m),"same physical support reenters only after a new episode and new rebound crossing");
        var stop=exit with{LastExitReason="FixedStop"};Check(Step(73,9955,10,stop).Stage==StrategyDStage.Cooldown,"stop-specific wait remains longer than the profit cooldown");
        var pending=new StrategyDEntryContext(PendingBuy:true);Check(Step(74,9900,11,pending).Stage==StrategyDStage.OrderPending,"pending buy suppresses new independent entry signals");
        var held=new StrategyDEntryContext(HasPosition:true);Check(Step(75,9950,12,held).Stage==StrategyDStage.Holding,"confirmed position is distinguished from order-pending state");
        var snapshot=data.Latest!;var fresh=new StrategyDSignalEngine(instrument,definition,seed.Facts.Sessions,"different-instance");
        var at=snapshot with{Time=open.AddMinutes(5),Sequence=100,Price=10100};fresh.Observe(at,new());
        var noSupport=fresh.Observe(at with{Time=at.Time.AddSeconds(1),Sequence=101,Supports=[]},new());
        Check(noSupport.Lowest==null&&noSupport.Signal==null,"mandatory support disappearance invalidates the tracked low");
        fresh.Observe(at with{Time=at.Time.AddSeconds(2),Sequence=102,Price=9900},new());
        var gap=fresh.Observe(at with{Time=at.Time.AddSeconds(3),Sequence=103,Price=9950,Feed=at.Feed with{Discontinuity=true,CanEnter=false}},new());
        Check(gap.Stage==StrategyDStage.Blocked&&gap.Lowest==null&&gap.Signal==null,"trade gap invalidates the candidate before a rebound can create an entry");
        var risk=fresh.Observe(at with{Time=at.Time.AddSeconds(4),Sequence=104,FundamentalRisk=true},new());
        Check(risk.Stage==StrategyDStage.Blocked&&risk.Signal==null,"known fundamental risk blocks D independently of price conditions");
        var unverified=fresh.Observe(at with{Time=at.Time.AddSeconds(5),Sequence=105,SecurityTradable=null},new());
        Check(unverified.Stage==StrategyDStage.Blocked,"unknown security status is never treated as a cleared stock");
        var highCount=fresh.Observe(at with{Time=at.Time.AddSeconds(6),Sequence=106},new(SymbolEntries:definition.DayTrading!.Reentry.MaxEntriesPerSymbolPerDay));
        Check(highCount.Stage==StrategyDStage.Cooldown,"symbol daily entry count blocks signal creation before order submission");
        var late=fresh.Observe(at with{Time=open.AddHours(6),Sequence=107},new());
        Check(late.Stage==StrategyDStage.Blocked&&late.Reason.Contains("종료"),"new entry cutoff is independent of maximum holding days");
        var chaseDefinition=definition.Snapshot();chaseDefinition.DayTrading!.Support.MaxEntryDistanceFromSupportPercent=.01m;
        var chase=new StrategyDSignalEngine(instrument,chaseDefinition,seed.Facts.Sessions,"chase");
        chase.Observe(at,new());chase.Observe(at with{Time=at.Time.AddSeconds(1),Sequence=101,Price=9900},new());
        var chased=chase.Observe(at with{Time=at.Time.AddSeconds(2),Sequence=102,Price=10200},new());
        Check(chased.Signal==null&&chased.Reason.Contains("추격"),"rebound above the maximum entry support distance cannot chase a price");
        var stable=new StrategyDSignalEngine(instrument,definition,seed.Facts.Sessions,"stable");stable.Observe(at,new());
        var shifted=at.Supports.Select(s=>s with{Zone=s.Zone with{KnownAt=at.Time.AddSeconds(1),Lower=s.Zone.Lower+1,Upper=s.Zone.Upper+1,Center=s.Zone.Center+1}}).ToArray();
        var unchanged=stable.Observe(at with{Time=at.Time.AddSeconds(1),Sequence=101,Price=9900,Supports=shifted},new());
        Check(unchanged.Episode==1&&unchanged.ApproachAt==at.Time&&unchanged.Lowest==9900,"minor provider zone changes retain the same physical episode and observed low");
        var disabled=definition.Snapshot();disabled.DayTrading!.Reentry.Mode=ReentryMode.Disabled;
        var disabledEngine=new StrategyDSignalEngine(instrument,disabled,seed.Facts.Sessions,"disabled");
        Check(disabledEngine.Observe(at,exit).Stage==StrategyDStage.Cooldown,"disabled reentry cannot be bypassed by elapsed time or a changed zone");
    }
}
