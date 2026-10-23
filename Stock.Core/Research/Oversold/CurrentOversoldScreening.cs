using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed partial class OversoldScreeningEngine
{
    /// <summary>Price-only sequential warmup for today's research. Missing facts remain unknown and cannot authorize entry.</summary>
    public ScreeningObservation EvaluateCurrent(StockItem stock,IReadOnlyList<StrategyBar> input,OversoldConfig requested,
        HistoricalFacts historical,DateTimeOffset asOf,DailySecurityStatus? currentStatus=null,bool? currentRisk=null,CancellationToken token=default,DateOnly? from=null,DateOnly? to=null)
    {
        var c=requested.Clone();c.Validate();var facts=historical.Snapshot();
        if(stock.Exchange!=Exchange.KRX)return ScreeningObservation.Missing(stock,"해외 제외 · 국내 일반주식 전략");
        var today=DailyQuoteMerger.Date(asOf,stock.Exchange);
        var end=to??today;var begin=from??end;
        if(begin>end||end>today)throw new InvalidOperationException("스크리닝 시작일·종료일을 확인하세요.");
        var bars=input.Where(b=>DailyDates.Of(b.Price)<=end&&(b.Price.IsComplete?b.ClosedAt<=asOf:true)).OrderBy(b=>b.Price.Time).ToArray();
        if(bars.Length==0)return ScreeningObservation.Missing(stock,"자료 부족 · 일봉 없음");
        if(bars.Select(b=>DailyDates.Of(b.Price)).Distinct().Count()!=bars.Length)return ScreeningObservation.Missing(stock,"자료 부족 · 일봉 날짜 중복");
        var cache=new OversoldIndicatorCache(c);var detector=new OversoldEntryDetector();
        ISupportProvider[] supports=[new MovingAverageSupportProvider(),new ConfirmedPivotHighProvider(),new ApproximateVolumeZoneProvider()];
        EntryEvaluation? result=null;var values=new List<decimal?>();var matches=new List<DateOnly>();StrategyBar? representative=null,lastMatch=null;EntryEvaluation? representativeEvaluation=null,lastMatchEvaluation=null;
        for(int i=0;i<bars.Length;i++)
        {
            token.ThrowIfCancellationRequested();var bar=bars[i];var day=DailyDates.Of(bar.Price);var last=i==bars.Length-1;
            var indicator=cache.Append(bar.Price);var candidates=supports.SelectMany(p=>p.Get(cache.History,c)).ToArray();
            detector.Observe(bar.Price,i,c);
            // Historical facts are used only after their own publication time; today's snapshot never backfills prior dates.
            var at=bar.Price.IsComplete?bar.ClosedAt:asOf;
            var status=day==today&&last&&currentStatus is {KnownAt:var known}&&known<=asOf&&currentStatus.Date==today?currentStatus:facts.Status(stock.Symbol,day,at);
            var risk=day==today&&last?currentRisk??facts.Risk(stock.Symbol,day,at):facts.Risk(stock.Symbol,day,at);
            result=detector.Evaluate(bar.Price,i,day,c,indicator,candidates,status,risk,values.TakeLast(c.AverageTradingValueLookbackDays).ToArray(),historyCount:cache.History.Count,currentResearch:true);
            values.Add(bar.TradingValue??facts.TradingValue(stock.Symbol,day,at));
            if(day>=begin&&day<=end)
            {
                representative=bar;representativeEvaluation=result;
                var excluded=result.Conditions.Any(x=>!x.Diagnostic&&x.Condition!="최종"&&x.Outcome==ConditionOutcome.Failed);
                if(result.TechnicalCandidate&&!excluded){matches.Add(day);lastMatch=bar;lastMatchEvaluation=result;}
            }
        }
        if(representative==null)return ScreeningObservation.Missing(stock,"선택 기간에 관측된 일봉이 없습니다.");
        var final=lastMatch??representative;var e=lastMatchEvaluation??representativeEvaluation!;
        var candidate=matches.Count>0;
        // This is a research result, including preview signals. No current screening result is an order authorization.
        var statusText=(candidate?$"기간 내 기술 후보 {matches.Count:N0}일 · "+e.Reason:e.TechnicalCandidate?"확인된 제외 조건 · 후보 제외":e.Reason)+(final.Price.IsComplete?" · 확정 일봉 기준":" · 당일 진행봉 (백테스트 제외)");
        return new(stock,final.Price.IsComplete?final.ClosedAt:asOf,final.Price.Close,e.Indicators.MediumDrawdown,e.Indicators.Rsi,e.Indicators.VolumeRatio,e.Selected?.Count??0,candidate,statusText,Serialization.Pack(e),bars[0].OpenTime,final.ClosedAt){MatchingDates=matches.ToArray()};
    }
}
