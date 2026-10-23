using System.Globalization;
using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public enum BarUnit { Tick,Minute,Hour }
public sealed record BarSpecification(int Value,BarUnit Unit)
{
    public void Validate(){if(Value<=0||!Enum.IsDefined(Unit))throw new InvalidOperationException("데이터 기준은 양의 정수와 틱/분/시를 선택하세요.");}
    public static BarSpecification Parse(string value,BarUnit unit)
    {if(value==null||!System.Text.RegularExpressions.Regex.IsMatch(value,@"^(?:\d+|\d{1,3}(?:,\d{3})+)$")||!int.TryParse(value.Replace(",",""),NumberStyles.None,CultureInfo.InvariantCulture,out var n)||n<=0)throw new InvalidOperationException("데이터 기준은 1~2,147,483,647의 정수로 입력하세요.");var result=new BarSpecification(n,unit);result.Validate();return result;}
    public override string ToString()=>$"{Value:N0}{(Unit==BarUnit.Tick?"틱":Unit==BarUnit.Minute?"분":"시")}";
}
/// <summary>Explicit capture coverage: absence of records is never evidence of a zero-volume interval.</summary>
public sealed record BarCoverage(DateTimeOffset From,DateTimeOffset To,bool Complete,string Source,long FirstSequence=long.MinValue,long LastSequence=long.MaxValue);
public sealed record StrategyBar(PriceBar Price,DateTimeOffset OpenTime,DateTimeOffset ClosedAt,long Sequence,decimal? TradingValue,string Source);
public sealed record BarAggregation(StrategyBar[] Bars,int Incomplete,int Missing,string Reason);

/// <summary>Session-aligned causal OHLCV with exact traded notional and no synthetic gap bars.</summary>
public static class StrategyBarAggregator
{
    public static BarAggregation FromTrades(IEnumerable<MarketEvent> input,Instrument instrument,BarSpecification spec,IReadOnlyList<DailyMarketSession> sessions,BarCoverage coverage,DateTimeOffset asOf)
    {
        spec.Validate();ValidateSessions(sessions);
        if(!coverage.Complete||string.IsNullOrWhiteSpace(coverage.Source)||coverage.From>=coverage.To)return new([],0,1,"원본 체결의 연속 수집 구간이 검증되지 않았습니다.");
        var all=input.Where(e=>e.Instrument==instrument&&e.Kind==EventKind.Trade&&e.ExchangeTime<=asOf&&e.ReceivedTime<=asOf&&e.ExchangeTime>=coverage.From&&e.ExchangeTime<=coverage.To&&(e.ExchangeTime!=coverage.From||e.Sequence>=coverage.FirstSequence)&&(e.ExchangeTime!=coverage.To||e.Sequence<=coverage.LastSequence)).OrderBy(e=>e.ExchangeTime).ThenBy(e=>e.Sequence).ThenBy(e=>e.Source,StringComparer.Ordinal).ToArray();
        foreach(var e in all)e.Validate();
        var identities=all.GroupBy(e=>(e.ExchangeTime,e.Sequence,e.Source));
        if(identities.Any(g=>g.Distinct().Count()>1))return new([],0,1,"동일 체결 ID의 값이 충돌합니다.");
        all=identities.Select(g=>g.First()).OrderBy(e=>e.ExchangeTime).ThenBy(e=>e.Sequence).ThenBy(e=>e.Source,StringComparer.Ordinal).ToArray();
        var output=new List<StrategyBar>();int incomplete=0,missing=0;long ordinal=0;
        foreach(var session in sessions.OrderBy(s=>s.Open))
        {
            var trades=all.Where(e=>e.ExchangeTime>=session.Open&&e.ExchangeTime<session.Close).ToArray();
            if(trades.Length==0)continue;
            // Cumulative volume is only a continuity check, never a trade size.
            bool gap=false;decimal? previous=null;
            foreach(var trade in trades){if(previous is {} prev&&trade.CumulativeVolume is {} current&&current-prev!=trade.TradeSize){gap=true;break;}previous=trade.CumulativeVolume;}
            if(gap){missing++;continue;}
            if(spec.Unit==BarUnit.Tick)
            {
                int cursor=0;
                while(trades.Length-cursor>=spec.Value){var group=trades.Skip(cursor).Take(spec.Value).ToArray();output.Add(Build(group,group[0].ExchangeTime,group[^1].ExchangeTime,++ordinal,coverage.Source));cursor+=spec.Value;}
                if(cursor<trades.Length)incomplete++;
            }
            else
            {
                var requestedTicks=(decimal)spec.Value*(spec.Unit==BarUnit.Hour?TimeSpan.TicksPerHour:TimeSpan.TicksPerMinute);
                if(requestedTicks>(session.Close-session.Open).Ticks){incomplete++;continue;}
                var ticks=(long)requestedTicks;
                foreach(var group in trades.GroupBy(e=>(e.ExchangeTime-session.Open).Ticks/ticks))
                {
                    var start=session.Open.AddTicks(group.Key*ticks);var remaining=(session.Close-start).Ticks;var end=start.AddTicks(Math.Min(ticks,remaining));
                    if(start<coverage.From){incomplete++;continue;}
                    // The shortened session-end bucket is deliberately not emitted as a full requested bar.
                    if(remaining<ticks||end>asOf||end>coverage.To){incomplete++;continue;}
                    output.Add(Build(group.ToArray(),start,end,++ordinal,coverage.Source));
                }
            }
        }
        return new(output.ToArray(),incomplete,missing,missing>0?"체결 누락/수집 시작 구간 부족":output.Count==0?"완성된 봉이 없습니다.":"");
    }
    public static BarAggregation FromMinutes(IEnumerable<StrategyBar> input,BarSpecification spec,IReadOnlyList<DailyMarketSession> sessions,DateTimeOffset asOf)
    {
        spec.Validate();ValidateSessions(sessions);if(spec.Unit==BarUnit.Tick)return new([],0,1,"분봉을 틱으로 변환할 수 없습니다.");
        var bars=input.OrderBy(b=>b.OpenTime).ToArray();foreach(var b in bars)b.Price.Validate();
        if(bars.GroupBy(b=>b.OpenTime).Any(g=>g.Count()>1))return new([],0,1,"중복 원본 분봉입니다.");
        long span=checked((long)spec.Value*(spec.Unit==BarUnit.Hour?60:1));var output=new List<StrategyBar>();int incomplete=0,missing=0;long ordinal=0;
        foreach(var session in sessions.OrderBy(s=>s.Open))
        {
        if((decimal)span*TimeSpan.TicksPerMinute>(session.Close-session.Open).Ticks){if(bars.Any(b=>b.OpenTime>=session.Open&&b.OpenTime<session.Close))incomplete++;continue;}
        foreach(var group in bars.Where(b=>b.OpenTime>=session.Open&&b.OpenTime<session.Close).GroupBy(b=>(b.OpenTime-session.Open).Ticks/(span*TimeSpan.TicksPerMinute)))
        {
            var items=group.ToArray();var start=session.Open.AddTicks(group.Key*span*TimeSpan.TicksPerMinute);var length=span*TimeSpan.TicksPerMinute;
            if(length>(session.Close-start).Ticks){incomplete++;continue;}var end=start.AddTicks(length);
            if(end>asOf||items.Any(b=>!b.Price.IsComplete||b.ClosedAt>asOf)){incomplete++;continue;}
            if(items.LongLength!=span||items.Where((b,i)=>b.OpenTime!=start.AddMinutes(i)||b.ClosedAt!=b.OpenTime.AddMinutes(1)).Any()){missing++;continue;}
            decimal? value=items.All(b=>b.TradingValue.HasValue)?items.Sum(b=>b.TradingValue!.Value):null;
            output.Add(new(new(end,items[0].Price.Open,items.Max(b=>b.Price.High),items.Min(b=>b.Price.Low),items[^1].Price.Close,items.Sum(b=>b.Price.Volume)),start,end,++ordinal,value,string.Join(" / ",items.Select(b=>b.Source).Distinct())));
        }
        }
        return new(output.ToArray(),incomplete,missing,missing>0?"원본 1분봉 구간에 누락이 있습니다.":output.Count==0?"완성된 봉이 없습니다.":"");
    }
    private static StrategyBar Build(MarketEvent[] group,DateTimeOffset start,DateTimeOffset end,long ordinal,string source)
        =>new(new(end,group[0].TradePrice!.Value,group.Max(e=>e.TradePrice!.Value),group.Min(e=>e.TradePrice!.Value),group[^1].TradePrice!.Value,group.Sum(e=>e.TradeSize!.Value)),start,end,ordinal,group.Sum(e=>e.TradePrice!.Value*e.TradeSize!.Value),source);
    private static void ValidateSessions(IReadOnlyList<DailyMarketSession> sessions)
    {
        var ordered=sessions.OrderBy(s=>s.Open).ToArray();
        if(ordered.Any(s=>s.Open>=s.Close)||ordered.Zip(ordered.Skip(1)).Any(p=>p.First.Close>p.Second.Open))throw new InvalidOperationException("시장 세션의 시작/종료와 중복을 확인하세요.");
    }
}
