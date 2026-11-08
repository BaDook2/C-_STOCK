using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;

internal static partial class Program
{
    private static void IntradayPositionRuleChecks()
    {
        var definition=StrategyDTestConfig();var c=definition.Configuration;var policy=definition.DayTrading!.Exit;
        c.SupportBreakPercent=-1;c.EmergencyStopLossPercent=-2;c.TrailingStopPercent=1;c.TrailingActivation=TrailingActivation.AfterEntry;
        var position=new IntradayPositionView(100,100,1,110,100,0,false,false);
        var stops=IntradayPositionRules.Evaluate(c,policy,position,95,100);
        Check(stops.Reason=="SupportStop"&&stops.Quantity==100&&stops.Triggered.Length==3,"simultaneous support/fixed/trailing stops produce one full-exit decision and retain all trigger reasons");
        var support=stops.SupportStop;var trailing=IntradayPositionRules.Evaluate(c,policy,position with{PeakPrice=105,MinimumTrailingStop=108.9m},107,100);
        Check(support==99&&trailing.TrailingStop==108.9m,"entry support remains frozen and stored trailing floor cannot move down when peak basis resets");
        foreach(var active in new[]{"Support","Fixed","Trailing"})
        {
            var copy=c.Clone();var p=new ExitPolicy{SupportStopEnabled=active=="Support"};copy.UseEmergencyStop=active=="Fixed";copy.UseTrailingStop=active=="Trailing";
            var d=IntradayPositionRules.Evaluate(copy,p,position,95,100);
            Check(d.Reason==active+"Stop","protective stops can run independently: "+active);
        }
        var full=new ExitPolicy{Mode=ExitMode.FullTakeProfit,SupportStopEnabled=false};c.UseTrailingStop=false;
        Check(IntradayPositionRules.Evaluate(c,full,position,102,null).Quantity==100,"full take profit closes all owned quantity at its configured target");
        var partial=new ExitPolicy{Mode=ExitMode.PartialTakeProfit,SupportStopEnabled=false};
        var tp1=IntradayPositionRules.Evaluate(c,partial,position,102,null);
        var tp2=IntradayPositionRules.Evaluate(c,partial,position with{Quantity=70,HarvestBasis=100,TakeProfit1Filled=true},103,null);
        Check(tp1.Reason=="TP1"&&tp1.Quantity==30&&tp2.Reason=="TP2"&&tp2.Quantity==30,"partial targets both use maximum acquired position basis rather than shrinking remaining quantity");
        var tiny=IntradayPositionRules.Evaluate(c,partial,position with{Quantity=1,MaximumQuantity=1},105,null);
        Check(tiny.Reason==null&&tiny.Quantity==0,"zero-share partial target never creates a fake fill or marks the stage complete");
        var only=new ExitPolicy{Mode=ExitMode.TrailingOnly,SupportStopEnabled=false};c.UseTrailingStop=true;
        Check(IntradayPositionRules.Evaluate(c,only,position with{PeakPrice=120},125,null).Reason==null,"trailing-only does not silently use fixed take-profit thresholds");
        c.UseTrailingStop=false;var be=new ExitPolicy{BreakEvenAfterTakeProfit1=true,SupportStopEnabled=false};
        var breakEven=IntradayPositionRules.Evaluate(c,be,position with{TakeProfit1Filled=true},100,null);
        Check(breakEven.Reason=="BreakEvenStop"&&breakEven.BreakEvenStop>100,"TP1 break-even protection includes remaining acquisition and estimated exit fees");
        var sampled=new ExitPolicy{SupportStopEnabled=true,RiskEvaluation=SignalEvaluationMode.OnBarClosed};
        Check(IntradayPositionRules.Evaluate(c,sampled,position,95,100).Reason==null&&IntradayPositionRules.Evaluate(c,sampled,position,95,100,true).Reason=="SupportStop","risk evaluation period is independent from entry signal evaluation");
        var risk=definition.DayTrading.Risk;risk.RiskBudgetPerPosition=100;var cashSized=IntradayPositionRules.EntrySize(c,risk,100,1000,10000,99);
        Check(cashSized>0&&cashSized<=99&&cashSized*100*(1+c.CommissionPercent/100)<=1000,"entry shares honor both actual cash and configured stop-distance risk budget");
        Check(IntradayPositionRules.EntrySize(c,risk,100,1000,10000,100)==0&&IntradayPositionRules.EntrySize(c,risk,100,1000,10000,null)==0,"invalid or absent initial protective stop cannot authorize an unbounded entry");
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var session=new DailyMarketSession(new(2026,11,6),open,open.AddHours(6.5));var day=definition.DayTrading.Session;
        Check(DayLiquidationPolicy.Evaluate(day,session,open.AddHours(5.9),10,false).Phase==DaySessionPhase.Trading,"before cutoff, independent day controller permits regular trading");
        var entryEnd=DayLiquidationPolicy.Evaluate(day,session,open.AddHours(6),10,false);
        Check(entryEnd.BlockNewEntries&&!entryEnd.LiquidateOwned,"entry cutoff does not itself fabricate a liquidation");
        var liquidation=DayLiquidationPolicy.Evaluate(day,session,open.AddHours(6).AddMinutes(10),10,true);
        Check(liquidation.CancelPendingBuys&&liquidation.LiquidateOwned,"liquidation clock cancels pending buys and requests owned-quantity exit without waiting for a new tick");
        Check(DayLiquidationPolicy.Evaluate(day,session,session.Close,10,false).UnexpectedOvernight,"unfilled end-of-session position remains explicit unexpected overnight state");
        Check(!DayLiquidationPolicy.Evaluate(day,session,session.Close,0,false).UnexpectedOvernight,"confirmed flat account has no invented overnight warning");
    }
}
