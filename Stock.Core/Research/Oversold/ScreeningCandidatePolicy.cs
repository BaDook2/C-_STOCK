namespace Stock.Core.Research.Oversold;

/// <summary>Research-list eligibility only. It never changes entry signals or authorizes orders.</summary>
public static class ScreeningCandidatePolicy
{
    public static bool IsExact(EntryEvaluation evaluation)=>evaluation.TechnicalCandidate&&!evaluation.Conditions.Any(c=>!c.Diagnostic&&c.Condition!="최종"&&c.Outcome==ConditionOutcome.Failed);
    public static bool IsNear(EntryEvaluation? evaluation,OversoldConfig config)
    {
        if(!config.ShowNearCandidates||evaluation==null)return false;
        if(evaluation.Conditions.Any(c=>c.Condition is "상품 제외" or "정지·관리 제외" or "FundamentalRisk"&&c.Outcome==ConditionOutcome.Failed))return false;
        if(evaluation.Conditions.Any(c=>c.Condition is "Crash" or "RSI" or "Volume"&&c.Outcome is ConditionOutcome.MissingData or ConditionOutcome.NotEvaluated))return false;
        return RealtimeConditionProximity.Calculate(evaluation,config)>=config.MinimumCandidateProximity;
    }
}
