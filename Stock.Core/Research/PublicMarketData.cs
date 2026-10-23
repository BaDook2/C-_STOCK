using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Stock.Core.Trading;

namespace Stock.Core.Research;

public sealed record StockItem(string Symbol,string Name,Exchange Exchange,string ProviderCode,string Sector="분류 확인 전")
{
    /// <summary>Domestic listing board (KOSPI/KOSDAQ) when the source identifies it.</summary>
    public string Board {get;init;}="";
    public string Region=>Exchange==Exchange.KRX?"국내":"해외";
    public string Country=>Exchange==Exchange.KRX?"한국":"미국";
    public string Currency=>Exchange==Exchange.KRX?"KRW":"USD";
    public string Key=>$"{Exchange}:{Symbol}";
    public override string ToString()=>Name;
}
public sealed record PriceBar(DateTimeOffset Time,decimal Open,decimal High,decimal Low,decimal Close,decimal Volume,bool IsComplete=true)
{
    public string KoreaTime=>Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm");
    public void Validate(){if(Open<=0||Low<=0||Close<=0||High<Math.Max(Open,Close)||Low>Math.Min(Open,Close)||Volume<0)throw new InvalidOperationException("가격 데이터 형식이 올바르지 않습니다.");}
}
public sealed record BarDataset(StockItem Stock,PriceBar[] Bars,string Source,DateTimeOffset RetrievedAt)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public PriceBar[] CompletedBars=>Bars.Where(b=>b.IsComplete).ToArray();
    public string Description=>$"{Source} · 일봉 {Bars.Length:N0}개 · 한국시간 {RetrievedAt.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm} 수집";
}
public interface IPublicMarketData
{
    Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token);
    Task<IReadOnlyList<StockItem>> Universe(string region,int perExchange,CancellationToken token);
    Task<StockItem> Describe(StockItem stock,CancellationToken token);
    Task<BarDataset> History(StockItem stock,CancellationToken token);
}
// Public, unauthenticated endpoints. No cookies, credentials, or access-control bypass.
// Failures remain explicit; synthetic quotes never replace missing market data.
/// <summary>인증키 없는 공개 검색·일봉을 공통 모델로 변환하며, 누락 가격을 생성하지 않습니다.</summary>
public sealed partial class PublicMarketData : IPublicMarketData,IMarketOverviewData,IIndexChartData,ICurrentMarketData,IDisposable
{
    private readonly HttpClient http;
    private readonly Dictionary<string,string> industries=[];
    public PublicMarketData(HttpMessageHandler? handler=null)
    {System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);http=new(handler??new HttpClientHandler()){Timeout=TimeSpan.FromSeconds(20)};http.DefaultRequestHeaders.UserAgent.ParseAdd("StockResearch/2.0");}
    private async Task<string> Get(string url,CancellationToken ct)
    {
        using var response=await http.GetAsync(url,ct);
        if(response.StatusCode==HttpStatusCode.TooManyRequests)throw new InvalidOperationException("공개 시세 조회 한도에 도달했습니다. 잠시 후 다시 시도하세요.");
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("공개 데이터 제공처가 응답하지 않습니다. 저장한 자료를 이용하거나 다시 시도하세요.");
        return await response.Content.ReadAsStringAsync(ct);
    }
    private async Task<JsonElement> Json(string url,CancellationToken ct){using var d=JsonDocument.Parse(await Get(url,ct));return d.RootElement.Clone();}
    internal static string Text(JsonElement e,string name)=>e.TryGetProperty(name,out var v)?v.ToString():"";
    public async Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("검색할 종목 이름을 입력하세요.");
        var root=await Json("https://ac.stock.naver.com/ac?q="+Uri.EscapeDataString(name.Trim())+"&target=stock&st=111",token);
        var result=new List<StockItem>();
        foreach(var r in root.GetProperty("items").EnumerateArray())
        {
            var nation=Text(r,"nationCode");if(nation is not("KOR" or "USA")||Text(r,"category")!="stock")continue;
            Exchange? exchange=nation=="KOR"?Exchange.KRX:Text(r,"typeCode") switch{"NASDAQ"=>Exchange.NASDAQ,"NYSE"=>Exchange.NYSE,"AMEX"=>Exchange.AMEX,_=>null};
            if(exchange==null)continue;var s=new StockItem(Text(r,"code"),Text(r,"name"),exchange.Value,Text(r,"reutersCode")){Board=nation=="KOR"?Text(r,"typeCode"):""};
            if(region=="전체"||s.Region==region)result.Add(s);
        }
        return result.DistinctBy(s=>s.Key).ToArray();
    }
    public async Task<IReadOnlyList<StockItem>> Universe(string region,int perExchange,CancellationToken token)
    {
        var stocks=new List<StockItem>();
        var markets=region switch
        {
            "코스피"=>new[]{"KOSPI"},
            "코스닥"=>new[]{"KOSDAQ"},
            "코스피+코스닥" or "국내"=>new[]{"KOSPI","KOSDAQ"},
            "해외"=>new[]{"NASDAQ","NYSE","AMEX"},
            _=>new[]{"KOSPI","KOSDAQ","NASDAQ","NYSE","AMEX"}
        };
        foreach(var market in markets)
        {
            bool kr=market is "KOSPI" or "KOSDAQ";
            var url=kr?$"https://m.stock.naver.com/api/stocks/marketValue/{market}?page=1&pageSize={perExchange}":$"https://api.stock.naver.com/stock/exchange/{market}/marketValue?page=1&pageSize={perExchange}";
            var root=await Json(url,token);
            foreach(var r in root.GetProperty("stocks").EnumerateArray())
            {
                if(Text(r,"stockEndType")!="stock")continue;
                var sector=r.TryGetProperty("industryCodeType",out var v)&&v.ValueKind==JsonValueKind.Object?Text(v,"industryGroupKor"):"분류 확인 전";
                stocks.Add(new(kr?Text(r,"itemCode"):Text(r,"symbolCode"),Text(r,"stockName"),kr?Exchange.KRX:Enum.Parse<Exchange>(market),Text(r,"reutersCode"),sector){Board=kr?market:""});
            }
            await Task.Delay(150,token);
        }
        return stocks.DistinctBy(s=>s.Key).ToArray();
    }
    public async Task<StockItem> Describe(StockItem stock,CancellationToken token)
    {
        if(stock.Sector is not("분류 확인 전" or "미분류" or ""))return stock;
        string sector;
        if(stock.Exchange==Exchange.KRX)
        {
            var root=await Json($"https://m.stock.naver.com/api/stock/{Uri.EscapeDataString(stock.Symbol)}/integration",token);
            var code=Text(root,"industryCode");if(code=="")return stock with{Sector="미분류"};
            if(!industries.TryGetValue(code,out sector!)){var industry=await Json($"https://m.stock.naver.com/api/stocks/industry/{Uri.EscapeDataString(code)}?page=1&pageSize=1",token);sector=Text(industry.GetProperty("groupInfo"),"name");industries[code]=sector;}
        }
        else{var root=await Json($"https://api.stock.naver.com/stock/{Uri.EscapeDataString(stock.ProviderCode)}/basic",token);sector=root.TryGetProperty("industryCodeType",out var v)&&v.ValueKind==JsonValueKind.Object?Text(v,"industryGroupKor"):"미분류";}
        return stock with{Sector=string.IsNullOrWhiteSpace(sector)?"미분류":sector};
    }
    public async Task<BarDataset> History(StockItem stock,CancellationToken token)
    {
        PriceBar[] bars;string source;
        if(stock.Exchange==Exchange.KRX)
        {
            var xml=await Get($"https://fchart.stock.naver.com/sise.nhn?timeframe=day&count=1500&requestType=0&symbol={Uri.EscapeDataString(stock.Symbol)}",token);
            bars=ParseNaver(xml);source="네이버 공개 일봉";
        }
        else
        {
            var root=await Json($"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(stock.Symbol.Replace('.','-'))}?range=5y&interval=1d",token);
            bars=ParseYahoo(root);source="Yahoo Finance 공개 일봉";
        }
        // Display the provider's latest candle, but never use an unfinished session in research.
        var now=DateTimeOffset.UtcNow;
        bars=bars.OrderBy(b=>b.Time).DistinctBy(b=>b.Time).Select(b=>b with{IsComplete=IsDailyComplete(b,stock.Exchange,now)}).ToArray();
        if(bars.Length==0)throw new InvalidOperationException("데이터가 없습니다.");
        foreach(var b in bars)b.Validate();return new(stock,bars,source,DateTimeOffset.UtcNow);
    }
    public static bool IsDailyComplete(PriceBar bar,Exchange exchange,DateTimeOffset now)
    {
        if(exchange==Exchange.KRX)return now>=new DateTimeOffset(bar.Time.ToOffset(TimeSpan.FromHours(9)).Date.AddHours(15).AddMinutes(40),TimeSpan.FromHours(9));
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var day=TimeZoneInfo.ConvertTime(bar.Time,zone).Date;
        // A conservative regular-session cutoff also keeps early-close candles out until 16:10 ET.
        var close=DateTime.SpecifyKind(day.AddHours(16).AddMinutes(10),DateTimeKind.Unspecified);
        return now>=new DateTimeOffset(close,zone.GetUtcOffset(close));
    }
    public static PriceBar[] ParseNaver(string xml)
    {
        var bars=new List<PriceBar>();foreach(Match m in Regex.Matches(xml,"<item data=\"([^\"]+)\""))
        {var p=m.Groups[1].Value.Split('|');if(p.Length!=6)continue;var dt=DateTime.ParseExact(p[0],"yyyyMMdd",CultureInfo.InvariantCulture);var n=p.Skip(1).Select(x=>decimal.Parse(x,CultureInfo.InvariantCulture)).ToArray();if(n[0]==0||n[3]==0)continue;bars.Add(new(new DateTimeOffset(dt.AddHours(15).AddMinutes(30),TimeSpan.FromHours(9)),n[0],n[1],n[2],n[3],n[4]));}return bars.ToArray();
    }
    public static PriceBar[] ParseYahoo(JsonElement root)
    {
        var result=root.GetProperty("chart").GetProperty("result");if(result.ValueKind!=JsonValueKind.Array||result.GetArrayLength()==0)throw new InvalidOperationException("해외 종목의 과거 자료가 없습니다.");
        var r=result[0];if(Text(r.GetProperty("meta"),"currency")!="USD")throw new InvalidOperationException("USD로 거래되는 미국 주식만 지원합니다.");
        var q=r.GetProperty("indicators").GetProperty("quote")[0];var stamps=r.GetProperty("timestamp");var bars=new List<PriceBar>();
        for(int i=0;i<stamps.GetArrayLength();i++){var values=new[]{"open","high","low","close","volume"}.Select(k=>q.GetProperty(k)[i]).ToArray();if(values.Any(v=>v.ValueKind==JsonValueKind.Null))continue;var n=values.Select(v=>v.GetDecimal()).ToArray();bars.Add(new(DateTimeOffset.FromUnixTimeSeconds(stamps[i].GetInt64()),n[0],n[1],n[2],n[3],n[4]));}return bars.ToArray();
    }
    public async Task<MarketQuote> Index(string code,CancellationToken token)
    {if(code is "DOW" or "NASDAQ" or "SP500")return (await IndexHistory(code,token)).Quote;if(code is not("KOSPI" or "KOSDAQ"))throw new InvalidOperationException("지원하지 않는 지수");return ParseQuote(await Json($"https://m.stock.naver.com/api/index/{code}/basic",token));}
    public async Task<IndexTrend> IndexHistory(string code,CancellationToken token)
    {
        var (symbol,name)=code switch{"KOSPI"=>("^KS11","코스피"),"KOSDAQ"=>("^KQ11","코스닥"),"DOW"=>("^DJI","다우존스"),"NASDAQ"=>("^IXIC","나스닥"),"SP500"=>("^GSPC","S&P 500"),_=>throw new InvalidOperationException("지원하지 않는 지수")};
        return ParseIndexTrend(await Json($"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=5m",token),name);
    }
    /// <summary>제공처의 실제 당일 관측값만 사용하며 결측 가격을 보간하지 않습니다.</summary>
    public static IndexTrend ParseIndexTrend(JsonElement root,string name)
    {
        var r=root.GetProperty("chart").GetProperty("result")[0];var meta=r.GetProperty("meta");
        var price=meta.GetProperty("regularMarketPrice").GetDecimal();var previous=meta.GetProperty("chartPreviousClose").GetDecimal();
        if(price<=0||previous<=0)throw new InvalidOperationException("지수 기준값을 확인하지 못했습니다.");
        var time=DateTimeOffset.FromUnixTimeSeconds(meta.GetProperty("regularMarketTime").GetInt64());
        var points=new List<IndexPoint>();
        if(r.TryGetProperty("timestamp",out var stamps)&&stamps.ValueKind==JsonValueKind.Array)
        {var closes=r.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");for(int i=0;i<Math.Min(stamps.GetArrayLength(),closes.GetArrayLength());i++)if(closes[i].ValueKind==JsonValueKind.Number&&closes[i].GetDecimal()>0)points.Add(new(DateTimeOffset.FromUnixTimeSeconds(stamps[i].GetInt64()),closes[i].GetDecimal()));}
        var quote=new MarketQuote(name,price,price-previous,(price/previous-1)*100,time,"DELAYED");
        return new(quote,previous,points.OrderBy(p=>p.Time).DistinctBy(p=>p.Time).ToArray());
    }
    public async Task<MarketQuote> Quote(StockItem stock,CancellationToken token)
    {return ParseQuote(await Json(stock.Exchange==Exchange.KRX?$"https://m.stock.naver.com/api/stock/{Uri.EscapeDataString(stock.Symbol)}/basic":$"https://api.stock.naver.com/stock/{Uri.EscapeDataString(stock.ProviderCode)}/basic",token));}
    private static MarketQuote ParseQuote(JsonElement r)
    {decimal N(string k)=>decimal.Parse(Text(r,k),NumberStyles.Number,CultureInfo.InvariantCulture);return new(Text(r,"stockName"),N("closePrice"),N("compareToPreviousClosePrice"),N("fluctuationsRatio"),DateTimeOffset.Parse(Text(r,"localTradedAt"),CultureInfo.InvariantCulture),Text(r,"marketStatus"));}
    public void Dispose()=>http.Dispose();
}
