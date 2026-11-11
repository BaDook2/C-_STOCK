using Stock.Core.Research;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;
using Signal=Stock.Core.Strategies.Injes.StrategySignal;

internal static partial class Program
{
    private static void StrategyIntegrationChecks()
    {
        InjesStrategyDefinition QuoteDefinition(decimal fee)
        {
            var d=InjesStrategyDefinition.Create("quote-scalping",Epoch);d.InitialCash=1000;d.CommonParameters.OrderBudget=20;d.CommonParameters.MaxSpreadPercent=3;
            d.CommonParameters.StopLossPercent=5;d.CommonParameters.TakeProfitPercent=10;d.ExecutionPolicy.FeePercent=fee;d.ExecutionPolicy.SlippagePercent=0;return d;
        }
        (InjesSession Session,InjesPaperExecution Paper) WarmQuote(decimal fee)
        {
            var d=QuoteDefinition(fee);var paper=new InjesPaperExecution(d.InitialCash,d.ExecutionPolicy);var session=new InjesSession(d,[Stock],paper);session.SetEntriesEnabled(true,Epoch.AddTicks(-1));
            for(int n=0;n<=128;n++)
            {
                var e=Observation(n,2.01m,100,quote:n%2==0,bid:n%4==0?1.99m:2m,ask:n%4==0?2.03m:2.04m);
                if(n%2==0)e=e with{Market=e.Market with{AskSize=400}};
                Push(session,e);
            }
            return(session,paper);
        }
        var costly=WarmQuote(5);Check(costly.Paper.Orders.Count==0&&costly.Session.Signals.Any(s=>s.BlockReasons.Contains("ExpectedNet <= 0")),"real quote runtime plus account gate rejects spread smaller than round-trip costs");
        var live=WarmQuote(.05m);Check(live.Paper.Orders.Count==1&&live.Paper.Orders[0].PassiveLimit&&live.Paper.Fills.Count==0,"full active quote stream posts a passive bid without inventing a fill");
        Push(live.Session,Observation(129,1.98m,20));Check(live.Paper.Positions[Stock.Key].Quantity==2&&live.Paper.Orders.All(o=>o.Buy),"partial buy owns two shares and waits for the remaining-buy cancel before selling");
        Push(live.Session,Observation(130,quote:true,bid:2m,ask:2.03m));var sell=live.Paper.Orders.Single(o=>!o.Buy);
        Check(sell.Quantity==2&&sell.State==OrderState.Accepted&&sell.PassiveLimit,"quote target sells exactly confirmed quantity after buy cancellation acknowledgement");
        Push(live.Session,Observation(131,2.04m,100));Check(live.Paper.Positions[Stock.Key].Quantity==0&&live.Paper.Fills.Count==2&&live.Paper.Fills[^1].Price==2.03m,"round trip realizes only after subsequent strict-through sell volume at conservative target price");
        Check(live.Session.Signals.First(s=>s.SignalType==SignalType.Enter).UsedObservations.Any(p=>p.Kind=="Quote")&&live.Session.Signals.First(s=>s.SignalType==SignalType.Enter).UsedObservations.Any(p=>p.Kind=="Trade"),"entry audit includes actual trade and quote event timestamps, sources and sequences");
        var d=InjesStrategyDefinition.Create("support-zone",Epoch);SetParameters<SupportZoneParameters>(d,p=>{p.SupportSources="MA,Pivot";p.MovingAveragePeriods="2";p.PivotLeftBars=p.PivotRightBars=1;});
        var history=Enumerable.Range(0,3).Select(i=>new InjesKnownBar(new PriceBar(Epoch.AddDays(i-4),2,i==1?2.02m:2.01m,1.99m,2,100),Epoch.AddDays(i-4),Epoch.AddDays(i-4))).ToArray();
        var reference=Features().Reference! with{DailyBars=history,AverageDailyTradingValue=500000,TickSize=.01m};
        var context=Features(2) with{Reference=reference};var runtime=StrategyRegistry.Injes.Get(d.StrategyId).Create(d);var direct=Evaluate(runtime,2,0,context);
        Check(direct.Type==SignalType.Enter&&direct.Metrics["supportCount"]==2,"support OFF-confirmation mode enters on first approach with two independent known daily supports");
        runtime.OnPositionClosed();Check(Evaluate(runtime,2,1,context).Type==SignalType.Hold,"closed support trade cannot reuse the same uninterrupted approach episode");
        Evaluate(runtime,3,2,context);Check(Evaluate(runtime,2,3,context).Type==SignalType.Enter,"leaving and newly reapproaching creates a fresh support episode");
        SetParameters<SupportZoneParameters>(d,p=>p.RequireReboundConfirmation=true);var bounce=StrategyRegistry.Injes.Get(d.StrategyId).Create(d);
        Check(Evaluate(bounce,2,0,context).Type==SignalType.Hold,"support rebound ON waits after first approach");Evaluate(bounce,1.99m,1,context);
        Check(Evaluate(bounce,2.05m,2,context).Type==SignalType.Enter,"support rebound uses only the low observed after approach");
        var entry=new Signal("inje",d.StrategyId,Stock.Key,"test",SignalType.Enter,2,Epoch,"support",Epoch,Epoch,Epoch,"TEST",1,[],new Dictionary<string,decimal?>{{"clusterLower",2}},true,[],"1");
        Check(Evaluate(bounce,1.93m,3,context,position:new(Stock){Quantity=2,AveragePrice=2,OpenedAt=Epoch},entry:entry).Reason=="SupportBreak","support exit uses the frozen entry lower bound rather than a new daily cluster");
        var unsupported=Features(2) with{Reference=reference with{DailyBars=history.Select(b=>b with{KnownAt=default}).ToArray()}};
        Check(Evaluate(StrategyRegistry.Injes.Get(d.StrategyId).Create(d),2,0,unsupported).Type==SignalType.Hold,"bars with missing KnownAt cannot create support evidence");
    }
}
