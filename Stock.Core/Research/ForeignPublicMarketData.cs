using Stock.Core.Trading;
using System.Globalization;
using System.Text.Json;
namespace Stock.Core.Research;

public sealed partial class PublicMarketData
{
    /// <summary>Provider monthly range returns daily OHLCV; yearly range would silently return weekly bars.</summary>
    public static PriceBar[] ParseNaverForeignDaily(JsonElement root)
    {
        if(!root.TryGetProperty("priceInfos",out var rows)||rows.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("네이버 해외 일봉 형식이 올바르지 않습니다.");
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");var result=new List<PriceBar>();
        foreach(var row in rows.EnumerateArray())
        {
            if(!DateTime.TryParseExact(Text(row,"localDate"),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))continue;
            decimal N(string key)=>Number(Text(row,key))??throw new InvalidOperationException("해외 일봉 필드 없음: "+key);
            var close=DateTime.SpecifyKind(date.AddHours(16),DateTimeKind.Unspecified);
            var bar=new PriceBar(new(close,zone.GetUtcOffset(close)),N("openPrice"),N("highPrice"),N("lowPrice"),N("closePrice"),N("accumulatedTradingVolume"));
            bar.Validate();result.Add(bar);
        }
        return result.OrderBy(b=>b.Time).DistinctBy(b=>b.Time).ToArray();
    }
    private async Task<IReadOnlyList<StockItem>> IndexConstituents(string selected,CancellationToken token)
    {
        var result=new List<StockItem>();
        foreach(var code in selected=="S&P500"?new[]{".INX"}:selected=="나스닥100"?new[]{".NDX"}:new[]{".INX",".NDX"})
        {
            var seen=new HashSet<string>();
            for(var page=1;page<=10;page++)
            {
                var root=await Json($"https://api.stock.naver.com/index/{code}/stocks?page={page}&pageSize=100",token);
                if(root.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("미국 지수 구성 종목 목록 형식을 확인하세요.");
                if(root.GetArrayLength()==0)break;
                foreach(var row in root.EnumerateArray())
                {
                    if(Text(row,"stockEndType")!="stock")continue;
                    var exchange=Text(row.GetProperty("stockExchangeType"),"name") switch{"NASDAQ"=>Exchange.NASDAQ,"NYSE"=>Exchange.NYSE,"AMEX"=>Exchange.AMEX,_=>(Exchange?)null};
                    var id=Text(row,"reutersCode");var symbol=Text(row,"symbolCode");
                    if(exchange==null||id.Length==0||symbol.Length==0)continue;
                    if(!seen.Add(id))throw new InvalidOperationException("지수 구성 종목 페이지가 반복돼 조회를 중단했습니다.");
                    var sector=row.TryGetProperty("industryCodeType",out var industry)&&industry.ValueKind==JsonValueKind.Object?Text(industry,"industryGroupKor"):"미분류";
                    result.Add(new(symbol,Text(row,"stockName"),exchange.Value,id,sector){Board=exchange.ToString()!,Indices=[code==".INX"?"SP500":"NASDAQ100"]});
                }
                if(root.GetArrayLength()<100)break;
                if(page==10)throw new InvalidOperationException("지수 구성 종목 조회 페이지 한도를 초과했습니다.");
            }
        }
        return result.GroupBy(s=>s.Key).Select(g=>g.First() with{Indices=g.SelectMany(s=>s.Indices).Distinct().ToArray()}).ToArray();
    }
}
