using System.Collections.Immutable;
using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed record ScreeningObservation(StockItem Stock,DateTimeOffset? ObservedAt,decimal? Price,decimal? Drawdown,decimal? Rsi,decimal? VolumeRatio,int SupportCount,bool Candidate,string Status,string EvaluationJson,DateTimeOffset? DataFrom,DateTimeOffset? DataTo)
{
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

/// <summary>Sequential warmup with the same entry detector as backtesting. It observes signals, never simulates fills.</summary>
public sealed partial class OversoldScreeningEngine
{
    /// <summary>
    /// Evaluates intraday bars as a research screen. Missing historical classification/risk facts stay
    /// visible as MissingData, but do not prevent the independent price/indicator/support conditions
    /// from being calculated. A result from this method is never an order authorization.
    /// </summary>
    public ScreeningObservation EvaluateObserved(StockItem stock,IReadOnlyList<StrategyBar> input,OversoldConfig requested,HistoricalFacts inputFacts,DateTimeOffset asOf,DailySecurityStatus? currentStatus=null,bool? currentRisk=null,CancellationToken token=default,DateOnly? from=null,DateOnly? to=null)
    {
        var c=requested.Clone();c.Validate();var facts=inputFacts.Snapshot();
        if(stock.Exchange!=Exchange.KRX)return ScreeningObservation.Missing(stock,"해외 제외 · 국내 일반주식 전략");
        if(from>to)throw new InvalidOperationException("스크리닝 시작일·종료일을 확인하세요.");
        var bars=input.Where(b=>b.Price.IsComplete&&b.ClosedAt<=asOf&&(to==null||DailyDates.Of(b.Price)<=to)).OrderBy(b=>b.ClosedAt).ThenBy(b=>b.Sequence).ToArray();
        if(bars.Length==0)return ScreeningObservation.Missing(stock,"자료 부족 · 완성봉 없음");
        if(bars.GroupBy(b=>(b.ClosedAt,b.Sequence)).Any(g=>g.Count()>1))return ScreeningObservation.Missing(stock,"자료 부족 · 봉 순서 중복");
        var cache=new OversoldIndicatorCache(c,true);var detector=new OversoldEntryDetector();ISupportProvider[] providers=[new MovingAverageSupportProvider(),new ConfirmedPivotHighProvider(true),new ApproximateVolumeZoneProvider()];
        var sessions=facts.Sessions.OrderBy(s=>s.Open).ToArray();EntryEvaluation? evaluation=null,lastMatchedEvaluation=null;StrategyBar? last=null,lastMatched=null;var values=new List<decimal?>();var matched=new List<DateOnly>();
        foreach(var bar in bars)
        {
            token.ThrowIfCancellationRequested();var day=Array.FindIndex(sessions,s=>bar.OpenTime>=s.Open&&bar.ClosedAt<=s.Close);
            if(day<0)return ScreeningObservation.Missing(stock,"자료 부족 · 검증된 시장 세션 없음");
            var session=sessions[day];var indicators=cache.Append(bar.Price);var candidates=providers.SelectMany(p=>p.Get(cache.History,c)).ToArray();
            detector.Observe(bar.Price,day,c);var date=DailyDates.Of(bar.Price);var at=bar.ClosedAt;
            var status=currentStatus is {KnownAt:var known}&&currentStatus.Date==date&&known<=asOf?currentStatus:facts.Status(stock.Symbol,date,at);
            var risk=currentStatus?.Date==date?currentRisk??facts.Risk(stock.Symbol,date,at):facts.Risk(stock.Symbol,date,at);
            evaluation=detector.Evaluate(bar.Price,day,session.Date,c,indicators,candidates,status,risk,values.TakeLast(c.AverageTradingValueLookbackDays).ToArray(),historyCount:cache.History.Count,currentResearch:true);
            values.Add(bar.TradingValue);if(from==null||session.Date>=from){last=bar;var excluded=evaluation.Conditions.Any(x=>!x.Diagnostic&&x.Condition!="최종"&&x.Outcome==ConditionOutcome.Failed);if(evaluation.TechnicalCandidate&&!excluded){matched.Add(session.Date);lastMatched=bar;lastMatchedEvaluation=evaluation;}}
        }
        if(last==null)return ScreeningObservation.Missing(stock,"선택 기간에 검증된 완성봉이 없습니다.");
        var chosen=lastMatched??last;var e=lastMatchedEvaluation??evaluation!;var candidate=matched.Count>0;
        var statusText=candidate?$"기간 내 기술 후보 {matched.Count:N0}개 봉 · {e.Reason}":e.TechnicalCandidate?"확인된 제외 조건 · 후보 제외":e.Reason;
        return new(stock,chosen.ClosedAt,chosen.Price.Close,e.Indicators.MediumDrawdown,e.Indicators.Rsi,e.Indicators.VolumeRatio,e.Selected?.Count??0,candidate,statusText+" · 지정봉 연구 결과 (주문 승인 아님)",Serialization.Pack(e),bars[0].OpenTime,chosen.ClosedAt){MatchingDates=matched.ToArray()};
    }

    public ScreeningObservation Evaluate(StockItem stock,IReadOnlyList<StrategyBar> input,OversoldConfig requested,HistoricalFacts inputFacts,DateTimeOffset asOf,CancellationToken token=default,DateOnly? from=null,DateOnly? to=null)
    {
        var c=requested.Clone();c.Validate();var facts=inputFacts.Snapshot();
        if(stock.Exchange!=Exchange.KRX)return ScreeningObservation.Missing(stock,"해외 제외 · 국내 일반주식 전략");
        if(from>to)throw new InvalidOperationException("스크리닝 시작일·종료일을 확인하세요.");
        var bars=input.Where(b=>b.Price.IsComplete&&b.ClosedAt<=asOf&&(to==null||DailyDates.Of(b.Price)<=to)).OrderBy(b=>b.ClosedAt).ThenBy(b=>b.Sequence).ToArray();
        if(bars.Length==0)return ScreeningObservation.Missing(stock,"자료 부족 · 완성봉 없음");
        if(bars.GroupBy(b=>(b.ClosedAt,b.Sequence)).Any(g=>g.Count()>1))return ScreeningObservation.Missing(stock,"자료 부족 · 봉 순서 중복");
        var cache=new OversoldIndicatorCache(c,true);var detector=new OversoldEntryDetector();ISupportProvider[] providers=[new MovingAverageSupportProvider(),new ConfirmedPivotHighProvider(true),new ApproximateVolumeZoneProvider()];
        var sessions=facts.Sessions.OrderBy(s=>s.Open).ToArray();EntryEvaluation? evaluation=null,lastMatchedEvaluation=null;StrategyBar? last=null,lastMatched=null;var values=new List<decimal?>();var matched=new List<DateOnly>();
        foreach(var bar in bars)
        {
            token.ThrowIfCancellationRequested();var day=Array.FindIndex(sessions,s=>bar.OpenTime>=s.Open&&bar.ClosedAt<=s.Close);
            if(day<0)return ScreeningObservation.Missing(stock,"자료 부족 · 검증된 시장 세션 없음");
            var session=sessions[day];var indicators=cache.Append(bar.Price);var candidates=providers.SelectMany(p=>p.Get(cache.History,c)).ToArray();
            detector.Observe(bar.Price,day,c);
            evaluation=detector.Evaluate(bar.Price,day,session.Date,c,indicators,candidates,facts.Status(stock.Symbol,session.Date,bar.ClosedAt),facts.Risk(stock.Symbol,session.Date,bar.ClosedAt),values.TakeLast(c.AverageTradingValueLookbackDays).ToArray(),historyCount:cache.History.Count);
            values.Add(bar.TradingValue);if(from==null||session.Date>=from){last=bar;if(evaluation.Enter){matched.Add(session.Date);lastMatched=bar;lastMatchedEvaluation=evaluation;}}
        }
        if(last==null)return ScreeningObservation.Missing(stock,"선택 기간에 검증된 완성봉이 없습니다.");
        var chosen=lastMatched??last;var e=lastMatchedEvaluation??evaluation!;
        return new(stock,chosen.ClosedAt,chosen.Price.Close,e.Indicators.MediumDrawdown,e.Indicators.Rsi,e.Indicators.VolumeRatio,e.Selected?.Count??0,matched.Count>0,matched.Count>0?$"기간 내 신호 {matched.Count:N0}일 · {e.Reason}":e.Reason,Serialization.Pack(e),bars[0].OpenTime,chosen.ClosedAt){MatchingDates=matched.ToArray()};
    }
}
