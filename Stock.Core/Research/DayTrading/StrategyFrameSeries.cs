using Stock.Core.Research.Oversold;
namespace Stock.Core.Research.DayTrading;

/// <summary>Completed independent indicator series. Calendar buckets are published only after their boundary.</summary>
public sealed record KnownStrategyBar(TimeframeSpec Timeframe,StrategyBar Bar,DateTimeOffset KnownAt);

public static class StrategyFrameSeries
{
    public static KnownStrategyBar[] Aggregate(IEnumerable<StrategyBar> source,TimeframeSpec frame,DateTimeOffset asOf)
    {
        frame.Validate();var rows=source.Where(b=>b.Price.IsComplete&&b.ClosedAt<=asOf).OrderBy(b=>b.OpenTime).ToArray();
        if(rows.Length==0)return [];
        if(frame.Unit is TimeframeUnit.TradingDay)
        {
            var daily=new List<KnownStrategyBar>();
            foreach(var day in rows.GroupBy(b=>b.OpenTime.Date))
            {
                var items=day.ToArray();var first=items[0];var last=items[^1];
                if(first.OpenTime.TimeOfDay!=new TimeSpan(9,0,0)||last.ClosedAt.TimeOfDay!=new TimeSpan(15,30,0)||items.Zip(items.Skip(1)).Any(p=>p.First.ClosedAt!=p.Second.OpenTime))continue;
                decimal? value=items.All(b=>b.TradingValue.HasValue)?items.Sum(b=>b.TradingValue!.Value):null;
                daily.Add(new(frame,new(new(last.ClosedAt,first.Price.Open,items.Max(b=>b.Price.High),items.Min(b=>b.Price.Low),last.Price.Close,items.Sum(b=>b.Price.Volume)),first.OpenTime,last.ClosedAt,last.Sequence,value,last.Source),last.ClosedAt));
            }
            return daily.ToArray();
        }
        DateTimeOffset Start(StrategyBar b)
        {
            var local=b.OpenTime;var date=local.Date;
            if(frame.Unit==TimeframeUnit.Week)date=date.AddDays(-((int)date.DayOfWeek+6)%7);
            if(frame.Unit==TimeframeUnit.Month)date=new(date.Year,date.Month,1);
            if(frame.Unit is TimeframeUnit.Week or TimeframeUnit.Month)return new(date,local.Offset);
            var session=new DateTimeOffset(local.Year,local.Month,local.Day,9,0,0,local.Offset);
            var ticks=frame.Duration!.Value.Ticks;return session.AddTicks((local-session).Ticks/ticks*ticks);
        }
        var result=new List<KnownStrategyBar>();long n=0;
        foreach(var group in rows.GroupBy(Start))
        {
            var items=group.ToArray();var start=group.Key;
            var end=frame.Unit==TimeframeUnit.Week?start.AddDays(7):frame.Unit==TimeframeUnit.Month?start.AddMonths(1):start+frame.Duration!.Value;
            if(end>asOf)continue;
            if(frame.Duration is {} span&&(items[0].OpenTime!=start||items[^1].ClosedAt!=end||items.Zip(items.Skip(1)).Any(p=>p.First.ClosedAt!=p.Second.OpenTime)))continue;
            decimal? value=items.All(b=>b.TradingValue.HasValue)?items.Sum(b=>b.TradingValue!.Value):null;
            var bar=new StrategyBar(new(items[^1].ClosedAt,items[0].Price.Open,items.Max(b=>b.Price.High),items.Min(b=>b.Price.Low),items[^1].Price.Close,items.Sum(b=>b.Price.Volume)),items[0].OpenTime,items[^1].ClosedAt,++n,value,string.Join(" / ",items.Select(b=>b.Source).Distinct()));
            result.Add(new(frame,bar,end));
        }
        return result.ToArray();
    }
}
