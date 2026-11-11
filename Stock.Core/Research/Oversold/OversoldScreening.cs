using System.Collections.Immutable;
using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed record ScreeningObservation(StockItem Stock,DateTimeOffset? ObservedAt,decimal? Price,decimal? Drawdown,decimal? Rsi,decimal? VolumeRatio,int SupportCount,bool Candidate,string Status,string EvaluationJson,DateTimeOffset? DataFrom,DateTimeOffset? DataTo)
{
    public Stock.Core.Strategies.Injes.StrategyEvaluationResult? InjesEvaluation {get;init;}
    public Stock.Core.Strategies.Bins.BinsScreenEvaluation? BinsEvaluation {get;init;}
    public decimal Proximity {get;init;}
    public bool IsNearCandidate {get;init;}
    public DateOnly[] MatchingDates {get;init;}=[];
    public EntryEvaluation? Evaluation=>EvaluationJson==""?null:Serialization.Unpack<EntryEvaluation>(EvaluationJson);
    public static ScreeningObservation Missing(StockItem stock,string reason)=>new(stock,null,null,null,null,null,0,false,reason,"",null,null);
}
public sealed record ScreeningFunnelRow(string Condition,int Passed,int Failed,int NotEvaluated,int MissingData,int Disabled,bool Diagnostic)
{public int Total=>Passed+Failed+NotEvaluated+MissingData;}
/// <summary>Value snapshots stored through TradingStore.State. Mutable config objects are never retained.</summary>
public sealed record OversoldScreeningRun(string Id,DateTimeOffset ScreeningTime,BarSpecification? BarSpecification,string ConfigJson,ImmutableArray<StockItem> Targets,ImmutableArray<ScreeningObservation> Results,string Status)
{
    public DateOnly? From {get;init;}
    public DateOnly? To {get;init;}
    public string FactsJson {get;init;}="";
    public string StrategyName {get;init;}="국내주식 과매도·낙주 반등";
    public OversoldConfig Configuration=>Serialization.Unpack<OversoldConfig>(ConfigJson);
    public ImmutableArray<StockItem> FinalCandidates=>Results.Where(r=>r.Candidate).Select(r=>r.Stock).DistinctBy(s=>s.Key).ToImmutableArray();
    public static OversoldScreeningRun Start(IEnumerable<StockItem> targets,OversoldConfig config,BarSpecification? spec,DateTimeOffset now,DateOnly? from=null,DateOnly? to=null,string? strategyName=null)=>new("oversold-screen:"+Guid.NewGuid().ToString("N"),now,spec,Serialization.Pack(config),targets.DistinctBy(s=>s.Key).ToImmutableArray(),[],"진행 중"){From=from,To=to,StrategyName=string.IsNullOrWhiteSpace(strategyName)?"국내주식 과매도·낙주 반등":strategyName.Trim()};
    public OversoldScreeningRun Append(ScreeningObservation row)
    {if(!Targets.Any(t=>t.Key==row.Stock.Key)||Results.Any(r=>r.Stock.Key==row.Stock.Key))throw new InvalidOperationException("스크리닝 대상 중복/불일치");return this with{Results=Results.Add(row)};}
    public ScreeningFunnelRow[] Funnel()
    {
        if(Configuration.Bins!=null)
        {
            string[] names=["원본 자료·품질","시장 분류·가족 선택","5가지 매수 패턴","공통 위험 심사"];
            return names.Select((name,index)=>{var values=Targets.Select(stock=>{var row=Results.FirstOrDefault(r=>r.Stock.Key==stock.Key);return row==null?ConditionOutcome.NotEvaluated:row.BinsEvaluation?.Outcomes[index]??ConditionOutcome.MissingData;}).ToArray();return new ScreeningFunnelRow(name,values.Count(v=>v==ConditionOutcome.Passed),values.Count(v=>v==ConditionOutcome.Failed),values.Count(v=>v==ConditionOutcome.NotEvaluated),values.Count(v=>v==ConditionOutcome.MissingData),0,false);}).ToArray();
        }
        if(Configuration.Injes!=null)
        {
            string[] names=["Universe · 종목 범위","Attention · 시장 관심","Strategy Signal · 선택 전략","Risk Approval · 주문 심사"];
            return names.Select((name,index)=>
            {
                var outcomes=Targets.Select(stock=>
                {
                    var row=Results.FirstOrDefault(r=>r.Stock.Key==stock.Key);if(row==null)return ConditionOutcome.NotEvaluated;
                    var e=row.InjesEvaluation;if(e==null)return ConditionOutcome.MissingData;
                    var value=index switch{0=>e.Universe,1=>e.Attention,2=>e.Strategy,_=>e.Risk};
                    return value switch{Stock.Core.Strategies.Injes.EvaluationOutcome.Passed=>ConditionOutcome.Passed,Stock.Core.Strategies.Injes.EvaluationOutcome.Failed=>ConditionOutcome.Failed,Stock.Core.Strategies.Injes.EvaluationOutcome.MissingData=>ConditionOutcome.MissingData,_=>ConditionOutcome.NotEvaluated};
                }).ToArray();
                return new ScreeningFunnelRow(name,outcomes.Count(v=>v==ConditionOutcome.Passed),outcomes.Count(v=>v==ConditionOutcome.Failed),outcomes.Count(v=>v==ConditionOutcome.NotEvaluated),outcomes.Count(v=>v==ConditionOutcome.MissingData),0,false);
            }).ToArray();
        }

        string[] steps=["전체","상품 제외","정지·관리 제외","거래대금","Crash","RSI","Volume","Support 후보","Cluster","최소 count","접근","반등","FundamentalRisk","최종","MovingAverage","ConfirmedPivotHigh","ApproximateVolumeZone"];
        return steps.Select((name,index)=>
        {
            var outcomes=Targets.Select(stock=>
            {
                var row=Results.FirstOrDefault(r=>r.Stock.Key==stock.Key);
                if(row==null)return ConditionOutcome.NotEvaluated;
                var evaluation=row.Evaluation;
                if(evaluation==null)return index==0?ConditionOutcome.MissingData:ConditionOutcome.NotEvaluated;
                return evaluation.Conditions.First(c=>c.Condition==name).Outcome;
            }).ToArray();
            return new ScreeningFunnelRow(name,outcomes.Count(v=>v is ConditionOutcome.Passed or ConditionOutcome.Disabled),outcomes.Count(v=>v==ConditionOutcome.Failed),outcomes.Count(v=>v==ConditionOutcome.NotEvaluated),outcomes.Count(v=>v==ConditionOutcome.MissingData),outcomes.Count(v=>v==ConditionOutcome.Disabled),index>=14);
        }).ToArray();
    }
}

/// <summary>Retired bar screening contract. Native screening uses InjesResearchBridge.Screen.</summary>
public sealed partial class OversoldScreeningEngine
{
    public ScreeningObservation EvaluateObserved(StockItem stock,IReadOnlyList<StrategyBar> input,OversoldConfig requested,HistoricalFacts inputFacts,DateTimeOffset asOf,DailySecurityStatus? currentStatus=null,bool? currentRisk=null,CancellationToken token=default,DateOnly? from=null,DateOnly? to=null)=>throw RetiredStrategy.Error();
    public ScreeningObservation Evaluate(StockItem stock,IReadOnlyList<StrategyBar> input,OversoldConfig requested,HistoricalFacts inputFacts,DateTimeOffset asOf,CancellationToken token=default,DateOnly? from=null,DateOnly? to=null)=>throw RetiredStrategy.Error();
}
