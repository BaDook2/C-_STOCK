using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public static class InjesTradingSettings
{
    public static TradingSettings Create(InjesStrategyDefinition definition)
    {
        var d=definition.Snapshot();var c=d.CommonParameters;var p=d.ExecutionPolicy;
        var result=new TradingSettings{Injes=d};result.Scalping.Enabled=result.Box.Enabled=result.Rebound.Enabled=false;
        result.Risk.AccountCapital=d.InitialCash;result.Risk.FixedCash=c.OrderBudget;result.Risk.MaxOrderCash=c.OrderBudget;result.Risk.MaxSymbolCash=c.OrderBudget;
        result.Risk.MaxDailyLoss=c.MaxDailyLossCash;result.Risk.MaxPositions=c.MaxConcurrentPositions;result.Risk.StopValue=c.StopLossPercent;result.Risk.TakeValue=c.TakeProfitPercent;
        result.Execution.FeePercent=p.FeePercent;result.Execution.TaxPercent=p.TaxPercent;result.Execution.SlippagePercent=p.SlippagePercent;result.Execution.TimeoutMilliseconds=c.OrderTimeoutMilliseconds;result.Execution.Retry=false;
        result.Filters.ForEach(f=>f.Enabled=false);return result;
    }
}
