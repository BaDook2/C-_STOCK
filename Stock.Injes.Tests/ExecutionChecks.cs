using Stock.Core.Research.DayTrading;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

internal static partial class Program
{
    public sealed class TestParameters { }
    private sealed class ScriptedRuntime:IStrategyRuntime
    {
        public static int Evaluations;
        public StrategyIntent Evaluate(StrategyContext c)
        {Evaluations++;return c.Position?.Quantity>0?StrategyIntent.Hold("holding"):c.Event?.Kind==EventKind.Trade?new(SignalType.Enter,"scripted test",c.Event.Sequence.ToString(),[],new Dictionary<string,decimal?>(),c.Features.Ask,OrderKind.Limit,MaxChasePercent:.5m):StrategyIntent.Hold("quote");}
        public void ResetCandidate(){}public void OnPositionClosed(){}
    }
    private static InjesStrategyDefinition TestDefinition()
    {
        if(!StrategyRegistry.Injes.All.Any(f=>f.StrategyId=="test-only"))StrategyRegistry.Injes.Register(new StrategyFactory<TestParameters>("test-only","test only",[],_=>new ScriptedRuntime(),_=>[10]));
        var d=InjesStrategyDefinition.Create("test-only",Epoch);d.CommonParameters.OrderBudget=20;d.CommonParameters.ReentryCooldownSeconds=0;d.ExecutionPolicy.FeePercent=d.ExecutionPolicy.SlippagePercent=0;d.ExecutionPolicy.MaximumExposureCash=1000;d.CommonParameters.MaxDailyLossCash=1;return d;
    }
    private static (InjesSession Session,InjesPaperExecution Execution) TestSession(InjesStrategyDefinition? value=null)
    {var d=value??TestDefinition();var execution=new InjesPaperExecution(1000,d.ExecutionPolicy);var session=new InjesSession(d,[Stock],execution);session.SetEntriesEnabled(true,Epoch.AddTicks(-1));return(session,execution);}
    private static void Push(InjesSession session,InjesObservation e)=>session.OnMarket(e,e.KnownAt);
    private static void ExecutionChecks()
    {
        var pair=TestSession();var session=pair.Session;var execution=pair.Execution;
        Push(session,Observation(0,quote:true));Push(session,Observation(1));
        Check(execution.Orders.Count==1&&execution.Fills.Count==0&&execution.Positions.Count==0,"entry signal creates BuyPending, not a holding or same-event fill");
        var evaluations=ScriptedRuntime.Evaluations;Push(session,Observation(1));Check(ScriptedRuntime.Evaluations==evaluations&&execution.Orders.Count==1,"duplicate input invokes neither strategy nor execution twice");
        Push(session,Observation(2,quote:true));Check(execution.Positions[Stock.Key].Quantity==9&&execution.Positions[Stock.Key].OpenedAt==Epoch.AddMilliseconds(500),"actual fill sets quantity and holding start");
        var underFilter=Observation(3,1.9m) with{Reference=Observation(3).Reference! with{DailyTradingValue=0,MarketCap=null,Float=null}};
        Push(session,underFilter);Check(execution.Orders.Any(o=>!o.Buy&&o.Reason=="StopLoss"),"held stop is evaluated even when entry universe filters fail");
        Push(session,Observation(4,quote:true,bid:1.89m,ask:1.91m));Check(execution.Positions[Stock.Key].Quantity==0,"protective exit waits for a subsequent executable quote");
        Push(session,Observation(5));Check(execution.Orders.Count==2&&session.Rejections.Keys.Any(k=>k.Contains("DailyLoss")),"daily loss latch blocks later entries after confirmed losing exit");
        Check(session.Signals.All(s=>s.StrategyId=="test-only"),"a session invokes only its registered selected runtime");
        var incomplete=TestSession();Push(incomplete.Session,Observation(0,quote:true) with{Reference=Observation(0).Reference! with{TickSize=null}});Push(incomplete.Session,Observation(1) with{Reference=Observation(1).Reference! with{TickSize=null}});Check(incomplete.Execution.Orders.Count==0,"unknown provider tick size never falls back to a guessed US tick");
        var optional=TestSession();Push(optional.Session,Observation(0,quote:true));Push(optional.Session,Observation(1) with{Reference=Observation(1).Reference! with{MarketCap=null,Float=null}});Check(optional.Execution.Orders.Count==1,"disabled cap/float filters accept absent optional values");
        var requiredD=TestDefinition();requiredD.CommonParameters.MaxFloat=10000000;var required=TestSession(requiredD);Push(required.Session,Observation(0,quote:true));Push(required.Session,Observation(1) with{Reference=Observation(1).Reference! with{Float=null}});Check(required.Execution.Orders.Count==0,"enabled float filter rejects an unknown float");
        var partial=TestSession();Push(partial.Session,Observation(0,quote:true));Push(partial.Session,Observation(1));
        var small=Observation(2,quote:true);Push(partial.Session,small with{Market=small.Market with{AskSize=10}});
        Check(partial.Execution.Positions[Stock.Key].Quantity==1&&partial.Execution.Orders[0].State==OrderState.PartiallyFilled,"partial fill updates only actually filled shares");
        var order=partial.Execution.Orders[0];partial.Execution.Cancel(order,Epoch.AddMilliseconds(500));
        Check(order.State==OrderState.CancelRequested&&order.Active,"cancel request remains active until acknowledgement");
        partial.Session.OnTime(Epoch.AddMilliseconds(550));Check(order.State==OrderState.CancelRequested,"cancel latency cannot be bypassed by a clock pulse");
        partial.Session.OnTime(Epoch.AddMilliseconds(650));Check(order.State==OrderState.Cancelled&&partial.Execution.Positions[Stock.Key].Quantity==1,"cancel acknowledgement releases only the unfilled remainder");
        var direct=new InjesPaperExecution(1000,new(){FeePercent=0,SlippagePercent=0});var passive=new ManagedOrder{Id="passive",Instrument=Stock,Buy=true,Price=2,Quantity=10,Kind=OrderKind.Limit,PassiveLimit=true,Created=Epoch,Strategy="quote-scalping",TimeoutMilliseconds=3000,PriceTick=.01m};direct.Submit(passive);
        direct.OnMarket(Observation(1,quote:true,bid:2,ask:2.02m).Market);Check(direct.Fills.Count==0,"passive Bid limit is not filled by posting or quote touch");
        direct.OnMarket(Observation(2,2).Market);Check(direct.Fills.Count==0,"unknown queue does not fill on an equal-price print");
        direct.OnMarket(Observation(3,1.99m,20).Market);Check(direct.Fills.Count==1&&direct.Fills[0].Quantity==2&&direct.Fills[0].Price==2,"strict-through subsequent print has capped participation and conservative limit price");
        var sizing=InjesPositionSizing.Quantity(new(){OrderBudget=100},new(){FeePercent=1,MaximumExposureCash=100},10,100,100,9);
        Check(sizing==9,"cash sizing includes buy fee and floors whole shares");
        Check(InjesPositionSizing.Quantity(new(){OrderBudget=1000},new(){RiskBudgetCash=10},10,1000,1000,null)==0,"enabled risk budget cannot bypass an unknown protective stop");
        var maxHold=TestDefinition();maxHold.CommonParameters.MaxHoldingSeconds=1;var timed=TestSession(maxHold);Push(timed.Session,Observation(0,quote:true));Push(timed.Session,Observation(1));Push(timed.Session,Observation(2,quote:true));timed.Session.OnTime(Epoch.AddMilliseconds(1250));Check(timed.Execution.Orders.Count==1,"holding clock does not start at order creation");timed.Session.OnTime(Epoch.AddMilliseconds(1500));Check(timed.Execution.Orders.Any(o=>!o.Buy&&o.Reason=="MaxHoldingSeconds"),"independent clock exits at first-fill maximum holding time");
        var stopped=TestSession();Push(stopped.Session,Observation(0,quote:true));Push(stopped.Session,Observation(1));stopped.Session.EmergencyStop(Epoch.AddMilliseconds(300));Check(!stopped.Session.EntriesEnabled&&stopped.Execution.Orders[0].State==OrderState.CancelRequested,"emergency stop locks new orders and requests cancellation");Throws(()=>stopped.Session.SetEntriesEnabled(true,Epoch.AddSeconds(1)),"emergency stop requires a new explicitly prepared session");
    }
    private static void ReplayChecks()
    {
        var d=TestDefinition();var events=new[]{Observation(0,quote:true),Observation(1),Observation(2,quote:true),Observation(3,1.9m),Observation(4,quote:true,bid:1.89m,ask:1.91m)};
        var request=new InjesReplayRequest(d,[Stock],events,Epoch,Epoch.AddSeconds(2),1000,"synthetic",true,true,true);
        var a=new InjesReplayEngine().Run(request);var b=new InjesReplayEngine().Run(request);
        Check(a.Signals.Select(s=>s.SignalId).SequenceEqual(b.Signals.Select(s=>s.SignalId)),"identical receive-order replay has deterministic signal sequence");
        var live=TestSession(d);foreach(var e in events)Push(live.Session,e);
        Check(live.Session.Signals.Select(s=>s.SignalId).SequenceEqual(a.Signals.Select(s=>s.SignalId)),"streaming and replay call the same decision engine");
        Check(a.Performance.Trades==1&&a.Performance.RealizedProfit<0&&a.Performance.OpenPositions==0,"only fully closed round trips count as trades");
        Check(a.Performance.ProfitFactor==0&&!a.Performance.InfiniteProfitFactor&&a.Performance.Expectancy<0,"losing closed trade metrics and expectancy are explicit");
        var open=new InjesReplayEngine().Run(request with{Events=events.Take(3).ToArray(),To=Epoch.AddMilliseconds(600)});Check(open.Performance.Trades==0&&open.Performance.OpenPositions==1&&open.Performance.WinRate==null,"unclosed holdings remain marked and are never counted as winning trades");
        Throws(()=>new InjesReplayEngine().Run(request with{OriginalTradeEvents=false}),"OHLCV cannot masquerade as an original tick replay");
        var invalid=InjesStrategyDefinition.Create("quote-scalping",Epoch);invalid.ExecutionPolicy.FeePercent=5;var runtime=StrategyRegistry.Injes.Get(invalid.StrategyId).Create(invalid);Check(Evaluate(runtime,2,0).Type==SignalType.Enter,"alpha signal remains separate from the account/cost approval layer");
    }
}
