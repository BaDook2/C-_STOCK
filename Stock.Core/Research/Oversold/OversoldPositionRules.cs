namespace Stock.Core.Research.Oversold;

public sealed record PositionRuleEvaluation(string? Exit,bool TrailingActive,decimal FirstSupportLower,decimal SupportBreakPercent,decimal SupportStop,decimal Average,decimal PriorPeak,decimal TrailingPercent,decimal TrailingStop,decimal JudgmentPrice,decimal Low,decimal High);
public sealed record AdditionalEntryEvaluation(decimal PreviousFill,decimal DropPercent,decimal Threshold,decimal JudgmentPrice,bool PricePass,bool SupportPass,SupportCluster? UsedCluster,bool FinalDecision);
public sealed record ClusterAtDecision(decimal Lower,decimal Center,decimal Upper,int Count,SupportCandidate[] Supports,bool UsedForEntry);
public sealed record OversoldDecisionSnapshot(DateTimeOffset SignalTime,decimal SignalPrice,DailyIndicators Indicators,ClusterAtDecision[] Clusters,AdditionalEntryEvaluation? AdditionalEntry,PositionRuleEvaluation? PositionRules,string Reason);

/// <summary>Shared position thresholds. Callers control execution timing and use only confirmed fills for position state.</summary>
public static class OversoldPositionRules
{
    public static PositionRuleEvaluation Evaluate(OversoldConfig c,PriceBar? bar,decimal firstLower,decimal average,decimal priorPeak,int holdingDays,bool takeProfit1Filled)
    {
        var low=c.TouchPolicy==DailyTouchPolicy.CloseOnly?bar?.Close:bar?.Low;var high=c.TouchPolicy==DailyTouchPolicy.CloseOnly?bar?.Close:bar?.High;
        bool trailing=c.UseTrailingStop&&(c.TrailingActivation==TrailingActivation.AfterEntry||takeProfit1Filled);
        var support=firstLower*(1+c.SupportBreakPercent/100);var trailingStop=priorPeak*(1-c.TrailingStopPercent/100);
        string? exit=bar?.Close<=support?"SupportBreak":c.UseEmergencyStop&&low<=average*(1+c.EmergencyStopLossPercent/100)?"EmergencyStop":trailing&&low<=trailingStop?"TrailingStop":holdingDays>=c.MaxHoldingDays?"MaxHoldingDays":null;
        return new(exit,trailing,firstLower,c.SupportBreakPercent,support,average,priorPeak,c.TrailingStopPercent,trailingStop,bar?.Close??0,low??0,high??0);
    }
    public static AdditionalEntryEvaluation Additional(OversoldConfig c,decimal lastFill,decimal judgment,SupportCluster? cluster,bool otherConditions)
    {var threshold=lastFill*(1+c.AdditionalEntryDropPercent/100);bool price=judgment<=threshold,support=!c.RequireSupportForAdditionalEntry||cluster!=null;return new(lastFill,c.AdditionalEntryDropPercent,threshold,judgment,price,support,cluster,price&&support&&otherConditions);}
    public static OversoldDecisionSnapshot Snapshot(DateTimeOffset time,decimal price,DailyIndicators indicators,IEnumerable<SupportCandidate> candidates,OversoldConfig c,SupportCluster? selected,AdditionalEntryEvaluation? additional,PositionRuleEvaluation? position,string reason)
        =>new(time,price,indicators,SupportClustering.Build(candidates,c.SupportClusterRangePercent).Where(z=>z.Count>=c.MinimumSupportCount).Select(z=>new ClusterAtDecision(z.Lower,z.Center,z.Upper,z.Count,z.Supports.ToArray(),selected?.Signature==z.Signature)).ToArray(),additional,position,reason);
}
