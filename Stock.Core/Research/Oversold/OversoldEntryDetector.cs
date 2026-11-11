namespace Stock.Core.Research.Oversold;

public enum ConditionOutcome { Passed,Failed,NotEvaluated,MissingData,Disabled }
public sealed record EntryDetectorState(SupportCluster? Approach,decimal? Low,int ApproachedAt,int Observations,int ApproachedObservation,DateOnly? Triggered);
public sealed record ConditionEvaluation(string Condition,ConditionOutcome Outcome,string Value,string Threshold,string Reason,bool Diagnostic=false);
public sealed record EntryEvaluation(bool Enter,OversoldState State,string Reason,DailyIndicators Indicators,SupportCluster? Selected,SupportCluster[] Clusters,ConditionEvaluation[] Conditions,decimal? ReboundLow,DateOnly? TriggerDate)
{ public bool TechnicalCandidate {get;init;} }

/// <summary>Read-only result DTOs remain available for non-strategy history rendering; the former detector is removed.</summary>
public sealed class OversoldEntryDetector
{
    public decimal? ReboundLow=>null;public DateOnly? TriggerDate=>null;
    public EntryDetectorState Capture()=>throw Stock.Core.Trading.RetiredStrategy.Error();
    public void Restore(EntryDetectorState state)=>throw Stock.Core.Trading.RetiredStrategy.Error();
    public void Reset(){}
    public void Observe(PriceBar? bar,int sessionIndex,OversoldConfig config)=>throw Stock.Core.Trading.RetiredStrategy.Error();
    public EntryEvaluation Evaluate(PriceBar bar,int sessionIndex,DateOnly date,OversoldConfig c,DailyIndicators indicator,IReadOnlyList<SupportCandidate> candidates,DailySecurityStatus? status,bool? fundamentalRisk,IReadOnlyList<decimal?> priorTradingValues,int lastExit=-1,decimal? previousCenter=null,int historyCount=int.MaxValue,bool currentResearch=false,bool allowPriceOnlyBacktest=false)=>throw Stock.Core.Trading.RetiredStrategy.Error();
}
