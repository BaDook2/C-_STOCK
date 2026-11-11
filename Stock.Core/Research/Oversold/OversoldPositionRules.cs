namespace Stock.Core.Research.Oversold;

public sealed record PositionRuleEvaluation(string? Exit,bool TrailingActive,decimal FirstSupportLower,decimal SupportBreakPercent,decimal SupportStop,decimal Average,decimal PriorPeak,decimal TrailingPercent,decimal TrailingStop,decimal JudgmentPrice,decimal Low,decimal High);
public sealed record AdditionalEntryEvaluation(decimal PreviousFill,decimal DropPercent,decimal Threshold,decimal JudgmentPrice,bool PricePass,bool SupportPass,SupportCluster? UsedCluster,bool FinalDecision);
public sealed record ClusterAtDecision(decimal Lower,decimal Center,decimal Upper,int Count,SupportCandidate[] Supports,bool UsedForEntry);
public sealed record OversoldDecisionSnapshot(DateTimeOffset SignalTime,decimal SignalPrice,DailyIndicators Indicators,ClusterAtDecision[] Clusters,AdditionalEntryEvaluation? AdditionalEntry,PositionRuleEvaluation? PositionRules,string Reason);

/// <summary>Retired strategy result contract. Native Inje protective exits are implemented by InjesSession.</summary>
public static class OversoldPositionRules
{
    public static PositionRuleEvaluation Evaluate(OversoldConfig c,PriceBar? bar,decimal firstLower,decimal average,decimal priorPeak,int holdingDays,bool takeProfit1Filled)=>throw Stock.Core.Trading.RetiredStrategy.Error();
    public static AdditionalEntryEvaluation Additional(OversoldConfig c,decimal lastFill,decimal judgment,SupportCluster? cluster,bool otherConditions)=>throw Stock.Core.Trading.RetiredStrategy.Error();
    public static OversoldDecisionSnapshot Snapshot(DateTimeOffset time,decimal price,DailyIndicators indicators,IEnumerable<SupportCandidate> candidates,OversoldConfig c,SupportCluster? selected,AdditionalEntryEvaluation? additional,PositionRuleEvaluation? position,string reason)=>throw Stock.Core.Trading.RetiredStrategy.Error();
}
