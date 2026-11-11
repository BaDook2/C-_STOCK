using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed record PreparedResearchData(BarDataset Dataset,BarSpecification? Specification,StrategyBar[]? Bars,HistoricalFacts Facts);
/// <summary>Shared acquisition for main backtest, screening and result popups. No strategy decisions live here.</summary>
public sealed class MarketHistoryService(AppServices services,Func<IAccountBroker?> account)
{
    /// <summary>Display range only: no strategy warmup and no fabricated execution ticks.</summary>
    public async Task<PriceBar[]> Chart(StockItem stock,BarSpecification specification,DateOnly from,DateOnly to,CancellationToken token)
    {
        if(account() is not IMinuteHistoryBroker minute||account()!.Exchange!=stock.Exchange)throw new InvalidOperationException("연결 계좌의 분봉 지원 시장을 확인하세요.");specification.Validate();var target=specification.Value*(specification.Unit==BarUnit.Hour?60:1);var sourceMinutes=1;
        PriceBar[] source;
        if(minute is IRangedMinuteIntervalHistoryBroker interval&&interval.SupportsMinuteInterval(target)){sourceMinutes=target;source=await interval.MinuteHistory(stock.Symbol,from,to,target,token);}else source=minute is IRangedMinuteHistoryBroker ranged?await ranged.MinuteHistory(stock.Symbol,from,to,token):await minute.MinuteHistory(stock.Symbol,token);
        token.ThrowIfCancellationRequested();foreach(var bar in source)bar.Validate();var now=services.Clock();var converted=source.DistinctBy(b=>b.Time).OrderBy(b=>b.Time).Select((bar,n)=>new StrategyBar(bar with{Time=bar.Time.AddMinutes(sourceMinutes),IsComplete=bar.IsComplete&&bar.Time.AddMinutes(sourceMinutes)<=now},bar.Time,bar.Time.AddMinutes(sourceMinutes),n+1,null,"연결 계좌 표시 봉")).ToArray();
        var sessions=converted.Select(b=>DailyQuoteMerger.Date(b.OpenTime,stock.Exchange)).Distinct().Select(d=>ResearchMarketSession.For(d,stock.Exchange)).ToArray();
        var merged=StrategyBarAggregator.FromMinutes(converted,specification,sessions,now,sourceMinutes);var result=merged.Bars.Where(b=>DailyQuoteMerger.Date(b.OpenTime,stock.Exchange)>=from&&DailyQuoteMerger.Date(b.OpenTime,stock.Exchange)<=to).Select(b=>b.Price with{Time=b.OpenTime}).ToArray();
        if(result.Length==0)throw new InvalidOperationException("해당 기간의 지정봉 자료가 없습니다. "+merged.Reason);return result;
    }
    public async Task<PreparedResearchData> Get(StockItem stock,OversoldConfig config,HistoricalFacts facts,BarSpecification? specification,DateOnly from,DateOnly to,CancellationToken token)
    {
        if(config.Injes!=null||config.Bins!=null)specification=null;
        var independent=config.Injes==null&&config.Bins==null&&account()!=null&&config.ParameterTimeframes.Count>0;
        if(independent)specification=DecisionSpecification(config);
        var daily=services.Store.State<BarDataset>("bars:"+stock.Key);
        if(daily==null||DateTimeOffset.UtcNow-daily.RetrievedAt>TimeSpan.FromMinutes(10)){daily=await services.MarketData.History(stock,token);token.ThrowIfCancellationRequested();services.Store.State("bars:"+stock.Key,daily);}
        if(specification==null||specification.Unit==BarUnit.Day)
        {
            var dailyFacts=facts.Snapshot();
            if(independent)PrepareFrames(dailyFacts,config,DailyBars(daily,dailyFacts),[],services.Clock());
            return new(daily,null,null,dailyFacts);
        }
        if(stock.Exchange!=Exchange.KRX||account() is not IMinuteHistoryBroker minute)throw new InvalidOperationException("분·시간봉은 해당 국내 시장을 지원하는 온라인 계좌 연결이 필요합니다.");
        specification.Validate();var first=RealtimeWarmup.BacktestHistoryStart(from,specification,config);var target=specification.Value*(specification.Unit==BarUnit.Hour?60:1);
        var sourceMinutes=1;PriceBar[] source;
        if(minute is IRangedMinuteIntervalHistoryBroker interval&&interval.SupportsMinuteInterval(target)){sourceMinutes=target;source=await interval.MinuteHistory(stock.Symbol,first,to,target,token);}
        else source=minute is IRangedMinuteHistoryBroker ranged?await ranged.MinuteHistory(stock.Symbol,first,to,token):await minute.MinuteHistory(stock.Symbol,token);
        token.ThrowIfCancellationRequested();var now=DateTimeOffset.UtcNow;
        var rows=source.DistinctBy(b=>b.Time).OrderBy(b=>b.Time).Select((bar,index)=>
        {
            var open=new DateTimeOffset(bar.Time.Year,bar.Time.Month,bar.Time.Day,bar.Time.Hour,bar.Time.Minute,0,bar.Time.Offset);var close=open.AddMinutes(sourceMinutes);
            return new StrategyBar(bar with{Time=close,IsComplete=bar.IsComplete&&close<=now},open,close,index+1,(bar.Open+bar.High+bar.Low+bar.Close)/4*bar.Volume,"연결 API 확정봉 · 거래대금 OHLC 평균×량 추정");
        }).ToArray();
        var copy=facts.Snapshot();var known=copy.Sessions.ToDictionary(s=>s.Date);
        foreach(var day in rows.Select(b=>DailyDates.Of(b.Price)).Distinct().Where(d=>!known.ContainsKey(d)))known[day]=ResearchMarketSession.For(day,Exchange.KRX);
        copy.MarketSessions=known.Values.OrderBy(s=>s.Open).ToArray();if(copy.Source.Length==0)copy.Source="연결 API 관측 봉의 정규장 세션 · 실제 휴장/단축장 달력 미확인";copy.Validate();
        var result=StrategyBarAggregator.FromMinutes(rows,specification,copy.Sessions,now,sourceMinutes);
        if(result.Bars.Length==0)throw new InvalidOperationException("선택한 지정봉 자료가 부족합니다. "+result.Reason);
        if(independent)PrepareFrames(copy,config,DailyBars(daily,copy),rows,now);
        return new(daily,specification,result.Bars,copy);
    }
    public static BarSpecification? DecisionSpecification(OversoldConfig config)
    {
        var specs=config.ParameterTimeframes.Values;
        var minutes=specs.Where(f=>f.Unit is Stock.Core.Research.DayTrading.TimeframeUnit.Minute or Stock.Core.Research.DayTrading.TimeframeUnit.Hour).ToArray();
        return minutes.Length==0?null:new BarSpecification(1,BarUnit.Minute);
    }
    private static void PrepareFrames(HistoricalFacts facts,OversoldConfig config,StrategyBar[] daily,StrategyBar[] minute,DateTimeOffset asOf)
    {
        var frames=config.ParameterTimeframes.Values;
        facts.StrategyFrames=frames.Append(new(1,Stock.Core.Research.DayTrading.TimeframeUnit.TradingDay)).Concat(minute.Length>0?[new Stock.Core.Research.DayTrading.TimeframeSpec(1,Stock.Core.Research.DayTrading.TimeframeUnit.Minute)]:[]).Distinct().SelectMany(f=>Stock.Core.Research.DayTrading.StrategyFrameSeries.Aggregate(f.Unit is Stock.Core.Research.DayTrading.TimeframeUnit.Minute or Stock.Core.Research.DayTrading.TimeframeUnit.Hour?minute:daily,f,asOf)).ToArray();
        var sessions=facts.Sessions.ToDictionary(s=>s.Date);foreach(var row in daily.Concat(minute)){var date=DailyDates.Of(row.Price);sessions.TryAdd(date,ResearchMarketSession.For(date,Exchange.KRX));}facts.MarketSessions=sessions.Values.OrderBy(s=>s.Open).ToArray();if(facts.Source.Length==0)facts.Source="관측 자료 날짜의 연구 세션";facts.Validate();
    }

    private static StrategyBar[] DailyBars(BarDataset dataset,HistoricalFacts facts)=>dataset.CompletedBars.Select((bar,n)=>
    {var session=facts.Sessions.FirstOrDefault(s=>s.Date==DailyDates.Of(bar))??ResearchMarketSession.For(DailyDates.Of(bar),dataset.Stock.Exchange);return new StrategyBar(bar with{Time=session.Close},session.Open,session.Close,n+1,null,dataset.Source);}).ToArray();
}
