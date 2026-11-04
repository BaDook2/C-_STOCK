using System.Globalization;
using System.Text.Json;
using Stock.Core.Trading;
namespace Stock.Core.Research;

public interface IPublicMinuteMarketData
{
    Task<PriceBar[]> MinuteHistory(StockItem stock,DateOnly from,DateOnly to,CancellationToken token);
}
public sealed partial class PublicMarketData:IPublicMinuteMarketData
{
    /// <summary>Public KRX minute OHLCV. Missing intervals remain missing; no tick reconstruction.</summary>
    public async Task<PriceBar[]> MinuteHistory(StockItem stock,DateOnly from,DateOnly to,CancellationToken token)
    {
        if(stock.Exchange!=Exchange.KRX)throw new InvalidOperationException("공개 분봉 가상매매는 국내 종목을 선택하세요.");
        var root=await Json($"https://api.stock.naver.com/chart/domestic/item/{Uri.EscapeDataString(stock.Symbol)}/minute?startDateTime={from:yyyyMMdd}0900&endDateTime={to:yyyyMMdd}1530",token);
        return ParsePublicMinutes(root,DateTimeOffset.UtcNow);
    }
    public static PriceBar[] ParsePublicMinutes(JsonElement root,DateTimeOffset asOf)
    {
        if(root.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("공개 분봉 응답 형식을 확인할 수 없습니다.");
        var bars=new List<PriceBar>();
        foreach(var row in root.EnumerateArray())
        {
            var time=new DateTimeOffset(DateTime.ParseExact(Text(row,"localDateTime"),"yyyyMMddHHmmss",CultureInfo.InvariantCulture),TimeSpan.FromHours(9));
            decimal N(string name)=>decimal.Parse(Text(row,name),NumberStyles.Number,CultureInfo.InvariantCulture);
            var bar=new PriceBar(time,N("openPrice"),N("highPrice"),N("lowPrice"),N("currentPrice"),N("accumulatedTradingVolume"),time.AddMinutes(1)<=asOf);
            if(bar.Open<=0||bar.Close<=0)continue;bar.Validate();bars.Add(bar);
        }
        return bars.OrderBy(b=>b.Time).DistinctBy(b=>b.Time).ToArray();
    }
}
