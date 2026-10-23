using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static async Task ResearchTests()
    {
        var fixture=new Stock.App.FixtureMarketData();var stock=new StockItem("AAPL","애플",Exchange.NASDAQ,"AAPL.O","기기");var data=await fixture.History(stock,CancellationToken.None);var bars=data.Bars;
        using(var trendJson=JsonDocument.Parse("""{"chart":{"result":[{"meta":{"regularMarketPrice":110.5,"chartPreviousClose":100,"regularMarketTime":1790550000},"timestamp":[1790549400,1790549700,1790550000],"indicators":{"quote":[{"close":[101,null,110.5]}]}}]}}""")){var trend=PublicMarketData.ParseIndexTrend(trendJson.RootElement,"검증 지수");Check(trend.Points.Length==2&&trend.Points[1].Value==110.5m&&trend.PreviousClose==100,"index trend excludes missing samples without synthetic fill");Check(trend.Quote.Change==10.5m&&trend.Quote.Percent==10.5m,"index daily change uses actual previous close with full precision");}
        var weekly=ChartPeriods.Aggregate(bars.Take(4).ToArray(),"주봉");Check(weekly.Length==1&&weekly[0].Open==bars[0].Open&&weekly[0].Close==bars[3].Close&&weekly[0].Volume==bars.Take(4).Sum(b=>b.Volume)&&weekly[0].Time==bars[3].Time,"partial weekly OHLCV has no future prices or timestamp");
        var monthly=ChartPeriods.Aggregate(bars.Take(32).ToArray(),"월봉");Check(monthly.Length==2&&monthly[0].Close==bars[30].Close&&monthly[1].Open==bars[31].Open,"monthly boundary aggregation");
        Check(DisplayNumber.Price(.3812m)=="1 미만"&&DisplayNumber.Price(1234.5m)=="1,235","integer price display avoids false zero for penny stocks");
        var displaySettings=new ChartStrategySettings();var fee=new Stock.App.SettingField(displaySettings,typeof(ChartStrategySettings).GetProperty("FeePercent")!,true);Check(fee.Value=="5","fractional fees displayed as integer basis points");fee.Apply();Check(displaySettings.FeePercent==.05m,"integer display preserves internal fee precision");
        var reboundBars=new[]{bars[0] with{Open=95,High=100,Low=90,Close=95},bars[1] with{Open=85,High=90,Low=80,Close=81},bars[2] with{Open=82,High=85,Low=82,Close=83}};
        var reboundSettings=new ChartStrategySettings{TrendEnabled=false,ReboundEnabled=true,DropDays=2,DropPercent=10,BouncePercent=2};Check(!ChartStrategies.Evaluate(reboundBars,2,reboundSettings).Buy,"rebound requires manual risk review by default");reboundSettings.RiskReviewed=true;Check(ChartStrategies.Evaluate(reboundBars,2,reboundSettings).Buy,"reviewed rebound applies price conditions");reboundSettings.Excluded=true;Check(!ChartStrategies.Evaluate(reboundBars,2,reboundSettings).Buy,"manual exclusion overrides entry signal");
        var boxBars=bars.Take(6).Select(b=>b with{Open=94,High=100,Low=90,Close=94,Volume=1000}).ToArray();boxBars[5]=boxBars[5] with{Close=91,Volume=500};var boxSettings=new ChartStrategySettings{TrendEnabled=false,BoxEnabled=true,BoxDays=5,BoxDecliningVolume=true,BoxMaxVolume=900};Check(!ChartStrategies.Evaluate(boxBars,5,boxSettings).Buy,"box average volume ceiling filters entry");boxSettings.BoxMaxVolume=1000;Check(ChartStrategies.Evaluate(boxBars,5,boxSettings).Buy,"low volume lower-box entry");boxSettings.BoxTouches=2;Check(!ChartStrategies.Evaluate(boxBars,5,boxSettings).Buy,"consecutive support days count as one independent touch");
        using(var p=new PublicMarketData(new Handler(_=>Task.FromResult(Json("""{"stockName":"코스피","closePrice":"7,035.82","compareToPreviousClosePrice":"-45.10","fluctuationsRatio":"-0.64","localTradedAt":"2026-09-28T10:00:00+09:00","marketStatus":"OPEN"}""")))))
        {var q=await p.Index("KOSPI",CancellationToken.None);Check(q.Price==7035.82m&&q.Percent==-.64m&&q.KoreaTime.Contains("10:00:00"),"market quote keeps full precision and KST timestamp");}
        Check(ChartIndicators.Sma(bars,3,5)==null,"SMA requires full lookback");
        Check(ChartIndicators.Sma(bars,4,5)==bars.Take(5).Average(b=>b.Close),"SMA uses closed bars");
        var cloud=ChartIndicators.Cloud(bars,77);Check(cloud.B==ChartIndicators.Mid(bars,51,52),"Ichimoku cloud shifted 26 bars without future");
        Check(ChartIndicators.Cloud(bars,76).B==null,"Ichimoku full cloud warmup");
        var s=new ChartStrategySettings();var full=ChartStrategies.Evaluate(bars,100,s);var prefix=ChartStrategies.Evaluate(bars.Take(101).ToArray(),100,s);Check(full==prefix,"screening signal independent of later bars");
        var account=new BarPaperAccount(10000);account.Advance(bars[50],s);account.Queue(true,10,"수동");Check(account.Fills.Count==0&&account.Cash==10000,"practice order waits for next open");account.Advance(bars[51],s);Check(account.Fills.Single().Price==bars[51].Open*(1+s.SlippagePercent/100),"next open plus slippage");Check(account.Cash==10000-account.Fills[0].Price*10-account.Fills[0].Fee,"cash deducts actual cost plus fee");account.Queue(false,10,"매도");account.Advance(bars[52],s);Check(account.Quantity==0&&account.Profit==account.Fills[^1].Realized,"round trip includes both side fees");
        account.Queue(true,100000,"초과");account.Advance(bars[53] with{Volume=100},s);Check(account.Quantity<=1&&!account.HasPending,"liquidity and cash cap; remainder expires");
        var run=BarPaperAccount.Backtest(bars,s,10000,50,CancellationToken.None);var run2=BarPaperAccount.Backtest(bars,s,10000,50,CancellationToken.None);Check(run.Fills.Count>0&&Serialization.Pack(run.Fills)==Serialization.Pack(run2.Fills),"bar backtest deterministic and actual fills");Check(run.Fills[0].Time>bars[50].Time,"strategy cannot fill deciding bar");
        using(var stop=new CancellationTokenSource()){stop.Cancel();Throws<OperationCanceledException>(()=>BarPaperAccount.Backtest(bars,s,10000,50,stop.Token),"bar backtest cancellation");}
        Throws<InvalidOperationException>(()=>new ChartStrategySettings{Fast=30,Slow=20}.Validate(),"chart setting relationship validation");
        var kr=PublicMarketData.ParseNaver("<item data=\"20250102|100|110|90|105|1000\"/><item data=\"20250103|0|0|0|0|0\"/>");Check(kr.Length==1&&kr[0].Time.Offset==TimeSpan.FromHours(9)&&kr[0].Volume==1000,"Naver OHLCV parsing and KST");
        using(var p=new PublicMarketData(new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)))))await ThrowsAsync<InvalidOperationException>(()=>p.Search("삼성","국내",CancellationToken.None),"public rate limits explicit, no invented data");
        using(var p=new PublicMarketData(new Handler(_=>Task.FromResult(Json("""{"items":[{"code":"AAPL","name":"애플","typeCode":"NASDAQ","reutersCode":"AAPL.O","nationCode":"USA","category":"stock"},{"code":"005930","name":"삼성전자","typeCode":"KOSPI","reutersCode":"005930","nationCode":"KOR","category":"stock"}]}""")))))
        {var found=await p.Search("주식","해외",CancellationToken.None);Check(found.Count==1&&found[0].Name=="애플"&&found[0].Exchange==Exchange.NASDAQ,"Korean name and internal exchange resolution");}
        var marketUrls=new List<string>();using(var p=new PublicMarketData(new Handler(request=>{marketUrls.Add(request.RequestUri!.AbsoluteUri);return Task.FromResult(Json("{\"stocks\":[]}"));})))
        {
            await p.Universe("코스피",10,CancellationToken.None);Check(marketUrls.Count==1&&marketUrls[0].Contains("marketValue/KOSPI")&&!marketUrls[0].Contains("KOSDAQ"),"KOSPI screening requests only KOSPI top N");marketUrls.Clear();
            await p.Universe("코스닥",20,CancellationToken.None);Check(marketUrls.Count==1&&marketUrls[0].Contains("marketValue/KOSDAQ"),"KOSDAQ screening requests only KOSDAQ top N");marketUrls.Clear();
            await p.Universe("코스피+코스닥",50,CancellationToken.None);Check(marketUrls.Count==2&&marketUrls.Any(u=>u.Contains("marketValue/KOSPI"))&&marketUrls.Any(u=>u.Contains("marketValue/KOSDAQ"))&&marketUrls.All(u=>u.Contains("pageSize=50")),"combined screening requests top N independently from both domestic markets");
        }
        foreach(var exchange in new[]{Exchange.KRX,Exchange.NASDAQ})
        {
            int sent=0;using var live=new KiwoomBroker(new(){AppKey="test-key",SecretKey="test-secret"},exchange,new Handler(async request=>
            {
                Check(request.RequestUri!.Host=="api.kiwoom.com","live routing only via explicit live flag (fake handler)");
                var id=request.Headers.GetValues("api-id").Single();if(id=="au10001")return Json("""{"return_code":0,"token":"test","expires_dt":"20990101000000"}""");
                if(id=="ka00001")return Json("""{"return_code":0,"acctNo":"1234567890"}""");
                if(id is "ka10001" or "usa20100")return Json("""{"return_code":0,"cur_prc":"+123.00"}""");
                using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Check(body.RootElement.GetProperty("ord_uv").GetString()=="","market order omits user-entered reference price");Check(body.RootElement.GetProperty("trde_tp").GetString()==(exchange==Exchange.KRX?"3":"03"),"live market order code");sent++;return Json("""{"return_code":0,"ord_no":"001"}""");
            }),live:true);
            await live.ConnectAsync();var symbol=exchange==Exchange.KRX?"005930":"AAPL";var price=await live.CurrentPrice(symbol);Check(price==123,"broker price automatically used for risk");await live.SubmitAsync(new(symbol,1,price,true,exchange,OrderKind.Market));Check(sent==1,"one market request without automatic retry");
        }
    }
}
