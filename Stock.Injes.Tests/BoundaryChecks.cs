using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void BoundaryChecks()
    {
        var other=new Instrument("OTHER",Exchange.NASDAQ);
        (InjesSession Session,InjesPaperExecution Paper) Multi(InjesStrategyDefinition d)
        {var paper=new InjesPaperExecution(1000,d.ExecutionPolicy);var session=new InjesSession(d,[Stock,other],paper);session.SetEntriesEnabled(true,Epoch.AddTicks(-1));return(session,paper);}
        InjesObservation ForOther(int n,bool quote=false)
        {var e=Observation(n,quote:quote);return e with{Market=e.Market with{Instrument=other}};}
        var limited=TestDefinition();limited.CommonParameters.MaxConcurrentPositions=1;var multi=Multi(limited);
        Push(multi.Session,Observation(0,quote:true));Push(multi.Session,Observation(1));Push(multi.Session,ForOther(2,true));Push(multi.Session,ForOther(3));
        Check(multi.Paper.Orders.Count==1&&multi.Session.Latest(other.Key)!.Signal.BlockReasons.Contains("Risk.ConcurrentPositions"),"an unfilled first-symbol buy reserves the account position slot across symbols");
        var risk=TestDefinition();risk.ExecutionPolicy.RiskBudgetCash=.3m;var budgeted=Multi(risk);
        Push(budgeted.Session,Observation(0,quote:true));Push(budgeted.Session,Observation(1));Push(budgeted.Session,ForOther(2,true));Push(budgeted.Session,ForOther(3));
        Check(budgeted.Paper.Orders.Count==1&&budgeted.Session.Latest(other.Key)!.Signal.Metrics["remainingRiskBudget"]<.03m,"remaining risk budget reserves pending buy losses before a second symbol sizes an order");
        var exposure=TestDefinition();exposure.ExecutionPolicy.MaximumExposureCash=1;var noRoom=TestSession(exposure);
        Push(noRoom.Session,Observation(0,quote:true));Push(noRoom.Session,Observation(1));
        Check(noRoom.Execution.Orders.Count==0&&noRoom.Session.Latest(Stock.Key)!.Signal.BlockReasons.Contains("Risk.QuantityZero"),"remaining exposure below one share creates no order and an explicit sizing reason");
        var off=TestSession();off.Session.SetEntriesEnabled(false,Epoch);Push(off.Session,Observation(0,quote:true));Push(off.Session,Observation(1));
        Check(off.Execution.Orders.Count==0&&off.Session.Latest(Stock.Key)!.Signal is {RiskApproved:true,OrderAllowed:false},"screening risk approval remains visible while entry authorization is OFF");
        var haltedPending=TestSession();Push(haltedPending.Session,Observation(0,quote:true));Push(haltedPending.Session,Observation(1));
        Push(haltedPending.Session,Observation(2,quote:true) with{Reference=Observation(2).Reference! with{Halted=true}});
        Check(haltedPending.Execution.Fills.Count==0&&haltedPending.Execution.Orders[0].State==OrderState.CancelRequested,"a newly confirmed halt cancels an outstanding buy before the simulator can match that quote");
        haltedPending.Session.OnTime(Epoch.AddMilliseconds(650));Check(haltedPending.Execution.Orders[0].State==OrderState.Cancelled,"halt cancellation acknowledgement does not require a tradable market event");
        foreach(var state in new[]{"unknown kinds","halted","unknown halt","unconfirmed session","corporate action"})
        {
            var pair=TestSession();
            InjesObservation Restrict(InjesObservation e)=>state switch
            {
                "unknown kinds"=>e with{Session=e.Session! with{AllowedOrderKinds=[]}},
                "halted"=>e with{Reference=e.Reference! with{Halted=true}},
                "unknown halt"=>e with{Reference=e.Reference! with{Halted=null}},
                "unconfirmed session"=>e with{Session=e.Session! with{TradingAllowed=false}},
                _=>e with{Session=e.Session! with{CorporateActionsVerified=false}}
            };
            Push(pair.Session,Restrict(Observation(0,quote:true)));Push(pair.Session,Restrict(Observation(1)));
            Check(pair.Execution.Orders.Count==0&&!pair.Session.Latest(Stock.Key)!.Signal.RiskApproved,state+" blocks account entry rather than inferring permission");
        }
        var pre=TestSession();var preAt=Epoch.AddHours(-1);var preSession=UsSessionClock.Bounds(preAt,UsMarketPhase.PreMarket,true) with{AllowedOrderKinds=[OrderKind.Limit]};
        foreach(var e in new[]{Observation(0,quote:true),Observation(1)})
        {var at=e.KnownAt.AddHours(-1);Push(pre.Session,e with{Market=e.Market with{ExchangeTime=at,ReceivedTime=at},KnownAt=at,Session=preSession,Reference=e.Reference! with{KnownAt=at}});}
        Check(pre.Execution.Orders.Count==0&&pre.Session.Latest(Stock.Key)!.Signal.BlockReasons.Contains("Risk.EnabledPhase"),"provider-confirmed premarket still requires explicit execution-policy opt-in");
        var engine=new InjesFeatureEngine(Stock,new(),[3]);var old=Observation(0);engine.Add(old,old.KnownAt);
        var nextAt=Epoch.AddDays(1);var next=Observation(1) with{Market=Observation(1).Market with{ExchangeTime=nextAt,ReceivedTime=nextAt,Sequence=0},KnownAt=nextAt,Session=UsSessionClock.Bounds(nextAt,UsMarketPhase.Regular,true),Reference=new(nextAt){TickSize=.01m,Halted=false}};
        Check(engine.Add(next,nextAt) is {Accepted:true,Discontinuity:true}&&!engine.Snapshot(nextAt).Window(3).Complete,"confirmed new session accepts provider sequence reset and restarts coverage");
        var changedAt=nextAt.AddMilliseconds(250);var changed=next with{Market=next.Market with{ExchangeTime=changedAt,ReceivedTime=changedAt,Sequence=1,Source="SECOND",TradePrice=1},KnownAt=changedAt,Reference=null};
        engine.Add(changed,changedAt);var changedFeatures=engine.Snapshot(changedAt);
        Check(changedFeatures.Reference==null&&!changedFeatures.FreshQuote&&changedFeatures.ObservedHigh==1,"source change cannot reuse a previous source's quote, reference or session high");
        var wide=InjesSupportCalculator.Cluster([new("wide","ApproximateVolumeZone",2,2.5m,3,Epoch)],3);
        Check(wide.Length==0,"even one volume support cannot bypass the cluster maximum span");
        var stock=new StockItem(Stock.Symbol,"Test",Stock.Exchange,Stock.Symbol);var config=InjesPresetStore.Wrap(TestDefinition());
        var events=new[]{Observation(0,quote:true),Observation(1)};var screen=InjesResearchBridge.Screen(stock,config,events,new(2026,9,25),new(2026,9,25),default);
        Check(screen.Candidate&&screen.InjesEvaluation?.Signal is {RiskApproved:true,OrderAllowed:false}&&screen.Evaluation==null,"native screen evaluates the same signal without executing orders or parsing a legacy evaluation");
        var funnel=OversoldScreeningRun.Start([stock],config,null,Epoch).Append(screen).Funnel();
        Check(funnel.Length==4&&funnel.All(f=>f.Passed==1),"native screening preserves Universe, Attention, Strategy and Risk stages");
        var settings=new TradingSettings();settings.Data.Universe=Stock.Key;var legacy=new TradingEngine(settings,"USD",1000);
        foreach(var e in Enumerable.Range(0,10).Select(n=>Observation(n).Market))legacy.OnEvent(e);
        Check(legacy.Broker.Orders.Count==0,"old automatic strategy dispatch cannot create orders through the shared manual engine");
        var nativeSettings=InjesTradingSettings.Create(TestDefinition());nativeSettings.Data.Universe=Stock.Key;var native=new TradingEngine(nativeSettings,"USD",1000);
        foreach(var e in events)native.OnEvent(e.Market with{StrategyReference=e.Reference,TradingSession=e.Session,AvailableAt=e.KnownAt});
        Check(native.Broker.Orders.Count==1&&native.Broker.Orders[0].Strategy=="test-only","generic engine dispatches only its explicitly selected native algorithm");
    }
}
