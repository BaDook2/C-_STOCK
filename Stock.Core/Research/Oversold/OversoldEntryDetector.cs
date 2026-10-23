namespace Stock.Core.Research.Oversold;

public enum ConditionOutcome { Passed,Failed,NotEvaluated,MissingData,Disabled }
public sealed record ConditionEvaluation(string Condition,ConditionOutcome Outcome,string Value,string Threshold,string Reason,bool Diagnostic=false);
public sealed record EntryEvaluation(bool Enter,OversoldState State,string Reason,DailyIndicators Indicators,SupportCluster? Selected,SupportCluster[] Clusters,ConditionEvaluation[] Conditions,decimal? ReboundLow,DateOnly? TriggerDate)
{ public bool TechnicalCandidate {get;init;} }

/// <summary>Stateful flat-position detector shared by historical screening and execution engines. Never creates fills.</summary>
public sealed class OversoldEntryDetector
{
    private SupportCluster? approach;private decimal? approachLow;private int approachedAt=-1,observations,approachedObservation;private DateOnly? triggered;
    public decimal? ReboundLow=>approachLow;
    public DateOnly? TriggerDate=>triggered;
    public void Reset(){approach=null;approachLow=null;triggered=null;approachedAt=-1;}
    public void Observe(PriceBar? bar,int sessionIndex,OversoldConfig config)
    {
        observations++;
        if(approach==null)return;
        if(sessionIndex-approachedAt>=config.CandidateLifetimeDays)Reset();
        else if(bar!=null&&observations>approachedObservation)approachLow=Math.Min(approachLow!.Value,bar.Low);
    }
    public EntryEvaluation Evaluate(PriceBar bar,int sessionIndex,DateOnly date,OversoldConfig c,DailyIndicators indicator,IReadOnlyList<SupportCandidate> candidates,DailySecurityStatus? status,bool? fundamentalRisk,IReadOnlyList<decimal?> priorTradingValues,int lastExit=-1,decimal? previousCenter=null,int historyCount=int.MaxValue,bool currentResearch=false,bool allowPriceOnlyBacktest=false)
    {
        var all=SupportClustering.Build(candidates,c.SupportClusterRangePercent);
        var eligible=all.Where(z=>z.Count>=c.MinimumSupportCount&&z.Approached(bar.Close,c.SupportDistancePercent)).OrderBy(z=>Math.Abs(z.Center/bar.Close-1)).ThenBy(z=>z.Center).ToArray();
        SupportCluster? selected=null;OversoldState state=lastExit>=0?OversoldState.Cooldown:OversoldState.Idle;
        bool liquidityBypassed=allowPriceOnlyBacktest&&c.MinimumAverageTradingValue==0;
        bool tradingReady=liquidityBypassed||(priorTradingValues.Count>=c.AverageTradingValueLookbackDays&&priorTradingValues.All(v=>v.HasValue));
        decimal? tradingAverage=liquidityBypassed?0:tradingReady?priorTradingValues.Average(v=>v!.Value):null;
        string reason;bool enter=false;
        if(!currentResearch&&!HistoricalFacts.Tradable(status))reason="과거 일반주식·거래정지·관리 상태 미지/제외";
        else if(!currentResearch&&!tradingReady)reason="이전 실제 거래대금 자료 부족";
        else if(!currentResearch&&tradingAverage<c.MinimumAverageTradingValue)reason="최소 평균 거래대금 미충족";
        else if(indicator.Trigger==""){reason="급락 조건 미충족";if(approach==null)triggered=null;}
        else
        {
            triggered??=date;state=OversoldState.CrashTriggered;
            if(c.UseRsiFilter&&!(indicator.Rsi<=c.MaximumRsi))reason="RSI 미준비/조건 미충족";
            else if(c.UseVolumeFilter&&!(indicator.VolumeRatio>=c.CrashVolumeRatio))reason="이전 거래량 평균 미준비/조건 미충족";
            else if(!currentResearch&&fundamentalRisk!=false)reason="당시 위험 정보 미지/위험";
            else if((selected=eligible.FirstOrDefault())==null)reason="유효 지지 cluster 접근 없음";
            else if(previousCenter is {} prior&&selected.Center>prior*(1-c.MinimumNewClusterDistancePercent/100))reason="재진입의 더 낮은 지지 거리 미충족";
            else if(lastExit>=0&&sessionIndex-lastExit<c.ReentryCooldownDays&&!c.AllowEarlyReentryWithNewCluster)reason="재진입 대기기간";
            else
            {
                state=OversoldState.SupportApproached;
                if(approach==null||approach.Signature!=selected.Signature||Math.Abs(selected.Center/approach.Center-1)*100>c.ClusterChangeTolerancePercent||sessionIndex-approachedAt>=c.CandidateLifetimeDays){approach=selected;approachLow=bar.Close;approachedAt=sessionIndex;approachedObservation=observations;triggered=date;}
                if(c.RequireReboundConfirmation&&bar.Close<approachLow*(1+c.ReboundPercent/100)){state=OversoldState.WaitingForRebound;reason="최초 접근 이후 저점 반등 대기";}
                else{reason="Entry1";enter=true;}
            }
        }
        var checks=new List<ConditionEvaluation>();bool active=true;
        string N(decimal? v)=>v?.ToString("N2",System.Globalization.CultureInfo.InvariantCulture)??"자료 없음";
        void Step(string name,bool? passed,string value,string threshold,bool enabled=true)
        {
            var outcome=!currentResearch&&!active?ConditionOutcome.NotEvaluated:!enabled?ConditionOutcome.Disabled:passed==null?ConditionOutcome.MissingData:passed.Value?ConditionOutcome.Passed:ConditionOutcome.Failed;
            checks.Add(new(name,outcome,value,threshold,outcome==ConditionOutcome.Disabled?"사용 안함":outcome==ConditionOutcome.NotEvaluated?"이전 단계 제외":currentResearch&&outcome==ConditionOutcome.MissingData?"해당 자료 미확인 · 다른 가격 조건은 계속 평가":currentResearch&&outcome==ConditionOutcome.Failed?"이 조건 미충족":outcome is ConditionOutcome.Failed or ConditionOutcome.MissingData?reason:"통과"));
            if(outcome is ConditionOutcome.Failed or ConditionOutcome.MissingData)active=false;
        }
        Step("전체",true,"1","1");
        Step("상품 제외",status==null||status.Kind==HistoricalSecurityKind.Unknown?null:status.Kind==HistoricalSecurityKind.Common,status?.Kind.ToString()??"미지","일반주식 (ETF/ETN/SPAC 제외)");
        Step("정지·관리 제외",status==null||status.Suspended==null||status.Administrative==null||status.Board==HistoricalBoard.Unknown?null:HistoricalFacts.Tradable(status),status==null?"미지":$"{status.Board} / 정지 {status.Suspended} / 관리 {status.Administrative}","코스피·코스닥, 정지/관리 아님");
        Step("거래대금",tradingAverage.HasValue?tradingAverage>=c.MinimumAverageTradingValue:null,N(tradingAverage),$"평균 {c.AverageTradingValueLookbackDays:N0}봉 ≥ {c.MinimumAverageTradingValue:N0}");
        Step("Crash",historyCount<Math.Min(c.CrashLookbackDays,c.ShortCrashLookbackDays)?null:indicator.Trigger!="",N(indicator.MediumDrawdown)+" / "+N(indicator.ShortDrawdown),$"≤ {c.MinimumDrawdownPercent:N2} 또는 ≤ {c.ShortCrashPercent:N2}");
        Step("RSI",indicator.Rsi.HasValue?indicator.Rsi<=c.MaximumRsi:null,N(indicator.Rsi),$"≤ {c.MaximumRsi:N2}",c.UseRsiFilter);
        Step("Volume",indicator.VolumeRatio.HasValue?indicator.VolumeRatio>=c.CrashVolumeRatio:null,N(indicator.VolumeRatio),$"≥ {c.CrashVolumeRatio:N2}",c.UseVolumeFilter);
        foreach(var kind in Enum.GetValues<SupportKind>())
        {
            bool enabled=kind==SupportKind.MovingAverage?c.UseMovingAverageSupport:kind==SupportKind.ConfirmedPivotHigh?c.UsePivotSupport:c.UseApproximateVolumeZone;
            checks.Add(new(kind.ToString(),!currentResearch&&!active?ConditionOutcome.NotEvaluated:!enabled?ConditionOutcome.Disabled:candidates.Any(p=>p.Type==kind)?ConditionOutcome.Passed:ConditionOutcome.Failed,candidates.Count(p=>p.Type==kind).ToString("N0"),"독립 진단 · 다른 지지 유형과 AND 아님",enabled?"후보 존재":"사용 안함",true));
        }
        Step("Support 후보",candidates.Count>0,candidates.Count.ToString("N0"),"> 0");Step("Cluster",all.Length>0,all.Length.ToString("N0"),"> 0");
        Step("최소 count",all.Any(z=>z.Count>=c.MinimumSupportCount),(all.Length==0?0:all.Max(z=>z.Count)).ToString("N0"),$"≥ {c.MinimumSupportCount:N0}");
        Step("접근",eligible.Length>0,N(bar.Close),$"하단/상단 ± {c.SupportDistancePercent:N2}%");
        Step("반등",approachLow.HasValue?bar.Close>=approachLow*(1+c.ReboundPercent/100):null,N(bar.Close),N(approachLow*(1+c.ReboundPercent/100)),c.RequireReboundConfirmation);
        Step("FundamentalRisk",fundamentalRisk.HasValue?!fundamentalRisk.Value:null,fundamentalRisk?.ToString()??"미지","false");
        var technical=enter;
        if(currentResearch)
        {
            enter=technical&&HistoricalFacts.Tradable(status)&&tradingReady&&tradingAverage>=c.MinimumAverageTradingValue&&fundamentalRisk==false&&bar.IsComplete;
            if(technical)reason=checks.Any(x=>!x.Diagnostic&&x.Outcome==ConditionOutcome.Failed)?"기술 조건 일치 · 확인된 제외 조건 있음":enter?"조건 확인 완료 · 스크리닝 전용 (주문 없음)":"기술 조건 후보 · 자료 미확인/진행봉 · 자동주문 불가";
        }
        Step("최종",currentResearch&&technical&&!enter&&!checks.Any(x=>!x.Diagnostic&&x.Outcome==ConditionOutcome.Failed)?null:enter,reason,"공통 진입 판단");
        return new(enter,state,reason,indicator,selected,all,checks.ToArray(),approachLow,triggered){TechnicalCandidate=technical};
    }
}
