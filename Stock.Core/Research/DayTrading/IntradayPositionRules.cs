using Stock.Core.Research.Oversold;

namespace Stock.Core.Research.DayTrading;

public sealed record IntradayPositionView(int Quantity,decimal AveragePrice,decimal RemainingEntryFees,decimal PeakPrice,int MaximumQuantity,
    int HarvestBasis,bool TakeProfit1Filled,bool TakeProfit2Filled,decimal MinimumTrailingStop=0,int TakeProfit1Sold=0,int TakeProfit2Sold=0,bool TakeProfit1Started=false);
public sealed record IntradayExitDecision(string? Reason,int Quantity,string[] Triggered,decimal? SupportStop,decimal? FixedStop,decimal? TrailingStop,decimal? BreakEvenStop);

/// <summary>Generic fill-based position rules. All thresholds come from the supplied execution profile.</summary>
public static class IntradayPositionRules
{
    public static IntradayExitDecision Evaluate(OversoldConfig shared,ExitPolicy policy,IntradayPositionView position,decimal price,decimal? entryZoneLow,bool closedEvaluation=false)
    {
        if(price<=0||position.Quantity<0||position.Quantity>0&&position.AveragePrice<=0)throw new InvalidOperationException("확인 체결의 수량/가격이 올바르지 않습니다.");
        decimal? support=policy.SupportStopEnabled&&entryZoneLow>0?entryZoneLow*(1+shared.SupportBreakPercent/100):null;
        decimal? fixedStop=shared.UseEmergencyStop?position.AveragePrice*(1+shared.EmergencyStopLossPercent/100):null;
        var trailingActive=shared.UseTrailingStop&&(shared.TrailingActivation==TrailingActivation.AfterEntry||position.TakeProfit1Filled||position.TakeProfit1Started);
        decimal? trailing=trailingActive?Math.Max(position.MinimumTrailingStop,position.PeakPrice*(1-shared.TrailingStopPercent/100)):null;
        decimal? breakEven=policy.BreakEvenAfterTakeProfit1&&(position.TakeProfit1Filled||position.TakeProfit1Started)&&position.Quantity>0
            ?(position.AveragePrice+position.RemainingEntryFees/position.Quantity)/(1-(shared.CommissionPercent+shared.TaxPercent)/100):null;
        var triggered=new List<string>();
        bool mayEvaluate=policy.RiskEvaluation!=SignalEvaluationMode.OnBarClosed||closedEvaluation;
        if(position.Quantity>0&&mayEvaluate)
        {
            if(price<=support)triggered.Add("SupportStop");
            if(price<=fixedStop)triggered.Add("FixedStop");
            if(price<=trailing)triggered.Add("TrailingStop");
            if(price<=breakEven)triggered.Add("BreakEvenStop");
        }
        if(triggered.Count>0)return new(triggered[0],position.Quantity,triggered.ToArray(),support,fixedStop,trailing,breakEven);
        var none=new IntradayExitDecision(null,0,[],support,fixedStop,trailing,breakEven);
        if(position.Quantity==0||!mayEvaluate||policy.Mode==ExitMode.TrailingOnly)return none;
        var basis=position.HarvestBasis>0?position.HarvestBasis:position.MaximumQuantity;
        if(shared.UseTakeProfit1&&!position.TakeProfit1Filled&&price>=position.AveragePrice*(1+shared.TakeProfit1Percent/100))
        {
            var quantity=policy.Mode==ExitMode.FullTakeProfit?position.Quantity:Math.Min(position.Quantity,(int)decimal.Floor(basis*shared.TakeProfit1Ratio/100)-position.TakeProfit1Sold);
            if(quantity>0)return none with{Reason=policy.Mode==ExitMode.FullTakeProfit?"FullTakeProfit":"TP1",Quantity=quantity};
        }
        if(policy.Mode==ExitMode.PartialTakeProfit&&shared.UseTakeProfit2&&!position.TakeProfit2Filled&&price>=position.AveragePrice*(1+shared.TakeProfit2Percent/100))
        {
            var quantity=Math.Min(position.Quantity,(int)decimal.Floor(basis*shared.TakeProfit2Ratio/100)-position.TakeProfit2Sold);
            if(quantity>0)return none with{Reason="TP2",Quantity=quantity};
        }
        return none;
    }
    /// <summary>Cash/risk cap before entry; a stop does not guarantee that losses remain within this budget.</summary>
    public static int EntrySize(OversoldConfig shared,AccountRiskPolicy risk,decimal reference,decimal cashAvailable,decimal exposureAvailable,decimal? protectiveStop,int entryStage=0)
    {
        if(reference<=0||cashAvailable<=0||exposureAvailable<=0||protectiveStop==null||protectiveStop<=0||protectiveStop>=reference||entryStage<0||entryStage>=shared.MaxEntries)return 0;
        var perShareCost=reference*(1+shared.CommissionPercent/100);
        var loss=reference-protectiveStop.Value+reference*shared.CommissionPercent/100+protectiveStop.Value*(shared.CommissionPercent+shared.TaxPercent)/100;
        var budget=Math.Min(shared.PlannedInvestment*shared.Ratios()[entryStage]/100,Math.Min(risk.MaxOrderCash,Math.Min(cashAvailable,exposureAvailable)));
        return (int)Math.Min(int.MaxValue,Math.Max(0,decimal.Floor(Math.Min(budget/perShareCost,risk.RiskBudgetPerPosition/loss))));
    }
}

public enum DaySessionPhase { Trading, EntryClosed, Liquidating, Closed }
public sealed record DayLiquidationDecision(DaySessionPhase Phase,bool BlockNewEntries,bool CancelPendingBuys,bool LiquidateOwned,bool UnexpectedOvernight);
/// <summary>Called by an independent clock as well as replay. Never fabricates a trade or liquidation fill.</summary>
public static class DayLiquidationPolicy
{
    public static DayLiquidationDecision Evaluate(SessionPolicy policy,Oversold.DailyMarketSession session,DateTimeOffset now,int ownedQuantity,bool hasPendingBuy)
    {
        if(ownedQuantity<0)throw new InvalidOperationException("음수 보유 수량입니다.");
        var local=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now,policy.TimeZoneId);
        var sessionLocalOpen=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(session.Open,policy.TimeZoneId);
        var sessionLocalClose=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(session.Close,policy.TimeZoneId);
        var entryEnd=TimeOnly.FromDateTime(sessionLocalOpen.DateTime);
        var close=TimeOnly.FromDateTime(sessionLocalClose.DateTime);
        if(policy.NoNewEntryAfter<entryEnd||policy.LiquidationStartAt>=close||policy.NoNewEntryAfter>policy.LiquidationStartAt)
            throw new InvalidOperationException("당일매매 시각이 확인된 시장 세션 범위를 벗어납니다.");
        if(now>=session.Close)return new(DaySessionPhase.Closed,true,policy.DayOnly&&hasPendingBuy,false,policy.DayOnly&&ownedQuantity>0);
        var time=TimeOnly.FromDateTime(local.DateTime);
        if(policy.DayOnly&&time>=policy.LiquidationStartAt)return new(DaySessionPhase.Liquidating,true,hasPendingBuy,ownedQuantity>0,false);
        if(now<session.Open||time>=policy.NoNewEntryAfter)return new(DaySessionPhase.EntryClosed,true,false,false,false);
        return new(DaySessionPhase.Trading,false,false,false,false);
    }
}
