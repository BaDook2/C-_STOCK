using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
public static class ResearchMarketSession
{
    public static DailyMarketSession For(DateOnly date,Exchange exchange)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById(exchange==Exchange.KRX?"Korea Standard Time":"Eastern Standard Time");
        var open=date.ToDateTime(exchange==Exchange.KRX?new TimeOnly(9,0):new TimeOnly(9,30));var close=date.ToDateTime(exchange==Exchange.KRX?new TimeOnly(15,30):new TimeOnly(16,0));
        return new(date,new(open,zone.GetUtcOffset(open)),new(close,zone.GetUtcOffset(close)));
    }
}
