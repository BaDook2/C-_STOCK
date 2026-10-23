using System.Globalization;
using Stock.Core.Trading;
namespace Stock.Core.Research;

public static class ChartPeriods
{
    public static readonly int[] Intervals=[1,3,5,10,15,30,60];
    public static bool Intraday(string period)=>period.EndsWith("틱")||period.EndsWith("분")||period.EndsWith("시");
    /// <summary>실제 체결만 묶고 날짜가 달라지면 새 봉을 시작합니다. 빈 구간은 생성하지 않습니다.</summary>
    public static PriceBar[] FromTrades(IReadOnlyList<PriceBar> trades,string period)
    {
        var unit=period.EndsWith("분")?"분":"틱";
        if(!int.TryParse(period.Replace(unit,""),out var n)||!Intervals.Contains(n))return [];
        var sorted=trades.OrderBy(b=>b.Time).ToArray();
        if(unit=="분")return sorted.GroupBy(b=>b.Time.ToUnixTimeSeconds()/(n*60)).Select(g=>Merge(g)).ToArray();
        return sorted.GroupBy(b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date).SelectMany(g=>g.Chunk(n).Select(Merge)).ToArray();
    }
    /// <summary>과거 분봉을 유지하면서 같은 시간 구간의 실시간 체결 봉으로 마지막 구간을 교체합니다.</summary>
    public static PriceBar[] MergeLiveTrades(IReadOnlyList<PriceBar> history,IReadOnlyList<PriceBar> liveTrades,string period)
    {
        var historical=FromTrades(history,period);var live=FromTrades(liveTrades,period);
        if(live.Length==0)return historical;if(!period.EndsWith("분"))return live;
        if(!int.TryParse(period[..^1],out var minutes)||minutes<=0)return historical;
        long Bucket(PriceBar b)=>b.Time.ToUnixTimeSeconds()/(minutes*60L);
        var merged=historical.GroupBy(Bucket).ToDictionary(g=>g.Key,g=>g.Last());
        foreach(var bar in live)merged[Bucket(bar)]=bar;
        return merged.Values.OrderBy(b=>b.Time).ToArray();
    }
    private static PriceBar Merge(IEnumerable<PriceBar> values){var a=values.ToArray();return new(a[^1].Time,a[0].Open,a.Max(b=>b.High),a.Min(b=>b.Low),a[^1].Close,a.Sum(b=>b.Volume),a.All(b=>b.IsComplete));}
    /// <summary>이미 알려진 일봉만 묶습니다. 진행 중인 주·월의 마지막 관측시각을 보존합니다.</summary>
    public static PriceBar[] Aggregate(IReadOnlyList<PriceBar> bars,string period)
    {
        if(period is not("주봉" or "월봉"))return bars.ToArray();
        DateTime Key(PriceBar b){var d=b.Time.ToOffset(TimeSpan.FromHours(9)).Date;return period=="월봉"?new(d.Year,d.Month,1):d.AddDays(-((int)d.DayOfWeek+6)%7);}
        return bars.OrderBy(b=>b.Time).GroupBy(Key).Select(Merge).ToArray();
    }
}
public static class DisplayNumber
{
    public static string Price(decimal n)=>n!=0&&Math.Abs(n)<1?(n<0?"-1 미만":"1 미만"):n.ToString("N0",CultureInfo.GetCultureInfo("ko-KR"));
}
public sealed record MarketQuote(string Name,decimal Price,decimal Change,decimal Percent,DateTimeOffset TradeTime,string State)
{
    public string PriceText=>DisplayNumber.Price(Price);
    public string ChangeText=>$"{Change:+#,##0;-#,##0;0} ({Percent:+0.00;-0.00;0.00}%)";
    public string KoreaTime=>$"{TradeTime.ToOffset(TimeSpan.FromHours(9)):MM.dd HH:mm:ss} · {(State=="OPEN"?"장중":State=="DELAYED"?"제공처 시세 · 지연 가능":"장마감 / 지연 가능")}";
}
public interface IMarketOverviewData
{
    Task<MarketQuote> Index(string code,CancellationToken token);
    Task<MarketQuote> Quote(StockItem stock,CancellationToken token);
}
public sealed record IndexPoint(DateTimeOffset Time,decimal Value);
public sealed record IndexTrend(MarketQuote Quote,decimal PreviousClose,IndexPoint[] Points);
public interface IIndexChartData
{
    Task<IndexTrend> IndexHistory(string code,CancellationToken token);
}
public sealed record WatchGroupData(string Id,string Name,StockItem[] Stocks);
public sealed class SavedBacktest
{
    public string? OversoldResultId {get;set;}
    public AdvancedStrategyConfig? AdvancedConfiguration {get;set;}
    public Stock.Core.Pipeline.PipelineAdvancedConfig? PipelineConfiguration {get;set;}
    public string Id {get;set;}="";
    public DateTimeOffset At {get;set;}
    public StockItem Stock {get;set;}=new("","",Exchange.KRX,"");
    public ChartStrategySettings Settings {get;set;}=new();
    public string Period {get;set;}="일봉";
    public string DataHash {get;set;}="";
    public decimal Initial {get;set;}
    public decimal Cash {get;set;}
    public int Quantity {get;set;}
    public decimal Profit {get;set;}
    public decimal MaxDrawdown {get;set;}
    public BarFill[] Fills {get;set;}=[];
    public BarEquity[] Equity {get;set;}=[];
    public string Title=>$"{At.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss} · {Stock.Name} · {Period}";
    public string ProfitText=>$"{Profit:+#,##0;-#,##0;0} {Stock.Currency}";
    public string ReturnText=>Initial>0?$"{Profit/Initial*100:+0.00;-0.00;0.00}%":"—";
    public string DrawdownText=>$"{MaxDrawdown:N2}%";
    public string FeesText=>$"{Fills.Sum(f=>f.Fee):N0} {Stock.Currency}";
    public string TradesText=>$"{Fills.Length:N0}건";
    public string Summary=>$"{Title}\n손익 {ProfitText} · 수익률 {ReturnText} · 최대 낙폭 {DrawdownText}\n체결 {TradesText} · 수수료 {FeesText} · 보유 {Quantity:N0}주";
}
