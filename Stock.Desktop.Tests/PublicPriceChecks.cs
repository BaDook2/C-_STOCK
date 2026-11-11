using System.Net;
using System.Text;
using System.Text.Json;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private sealed class QuoteClock:TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.Parse("2026-11-10T02:30:00Z");
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private static readonly StockItem QuoteApple=new("AAPL","애플",Exchange.NASDAQ,"AAPL.O");
    private static readonly StockItem QuoteSamsung=new("005930","삼성전자",Exchange.KRX,"005930");
    private static JsonElement PriceJson(string value)=>JsonDocument.Parse(value).RootElement.Clone();
    private static JsonElement QuoteFrame(bool domestic=false,object? extended=null,string regularStatus="CLOSE",string? traded=null,string field="overMarketPriceInfo")
    {
        var row=new Dictionary<string,object?>{{"itemCode","005930"},{"reutersCode","AAPL.O"},{"closePrice",domestic?"10,000":"95"},{"compareToPreviousClosePrice",domestic?"100":"5"},{"fluctuationsRatio","5"},{"compareToPreviousPrice",new{code="5"}},{"localTradedAt",traded??(domestic?"2026-11-09T15:30:00+09:00":"2026-11-09T20:00:00-05:00")},{"marketStatus",regularStatus},{"currencyType",new{code=domestic?"KRW":"USD"}},{"stockExchangeType",new{delayTime=0}},{"openPrice",domestic?"10000":"95"},{"highPrice",domestic?"10100":"96"},{"lowPrice",domestic?"9900":"94"},{"accumulatedTradingVolume","1000"}};
        if(extended!=null)row[field]=extended;
        return JsonSerializer.SerializeToElement(new{pollingInterval="7000",time="20261110113000",datas=new[]{row}});
    }
    private sealed class NaverQuoteHttp:HttpMessageHandler
    {
        public int Calls;public bool Limited,Fail=false;public JsonElement Frame=QuoteFrame();public string? LastPath;public bool Auth;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Calls++;LastPath=request.RequestUri!.AbsolutePath;Auth|=request.Headers.Authorization!=null;
            var response=new HttpResponseMessage(Limited?HttpStatusCode.TooManyRequests:Fail?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK);
            if(Limited)response.Headers.RetryAfter=new(TimeSpan.FromSeconds(30));response.Content=new StringContent(Frame.GetRawText(),Encoding.UTF8,"application/json");return Task.FromResult(response);
        }
    }
    private sealed class ViewerDayHttp(QuoteClock clock):HttpMessageHandler
    {
        public int Init,Calendars,Searches,Prices;public bool Fail,Limited=false,Auth,Private;public decimal Price=351.03m;
        public TaskCompletionSource? Hold;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Auth|=request.Headers.Authorization!=null;object result;var path=request.RequestUri!.AbsolutePath;
            if(path=="/api/v3/init"){Init++;result=new{};}
            else if(path=="/api/v2/system/trading-hours/integrated"){Calendars++;result=new{us=new{today=new{date="2026-11-10",dayMarketStart="10:00:00.000",dayMarketEnd="17:50:00.000"}}};}
            else if(path=="/api/v3/search-all/wts-auto-complete"){Searches++;result=new[]{new{data=new{items=new[]{new{symbol="AAPU",market="NSQ",productCode="WRONG"},new{symbol="AAPL",market="NSQ",productCode="VERIFIED"}}}}};}
            else if(path=="/api/v2/stock-infos/VERIFIED")result=new{code="VERIFIED",symbol="AAPL",currency="USD",daytimePriceSupported=true};
            else if(path=="/api/v5/stock-prices")
            {
                Prices++;Started.TrySetResult();if(Hold!=null)await Hold.Task.WaitAsync(token);
                if(Fail||Limited){var error=new HttpResponseMessage(Limited?HttpStatusCode.TooManyRequests:HttpStatusCode.ServiceUnavailable);if(Limited)error.Headers.RetryAfter=new(TimeSpan.FromSeconds(30));return error;}
                result=new{prices=new[]{new{productCode="VERIFIED",marketCurrency="USD",session="DAY_PRE_MAIN",dt=clock.Now.AddSeconds(-1).ToString("O"),close=Price,@base=350.69m}}};
            }
            else{Private=true;throw new InvalidOperationException("Unexpected public path: "+path);}
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{result}),Encoding.UTF8,"application/json")};
        }
    }
    private static void PublicPriceChecks()
    {
        var clock=new QuoteClock();var extended=new{tradingSessionType="PRE_MARKET",overMarketStatus="OPEN",overPrice="10,200",fluctuationsRatio="2",compareToPreviousPrice=new{code="2"},localTradedAt="2026-11-10T08:20:00+09:00"};
        CurrentMarketSnapshot Parse(JsonElement frame,bool kr=true)=>PublicMarketData.ParseRealtimeStock(kr?QuoteSamsung:QuoteApple,frame,clock.Now);
        var kr=Parse(QuoteFrame(true,extended));Check(kr.Quote is{Price:10200,Percent:2,State:"OPEN"}&&kr.Source.Contains("NXT"),"StockViewer NXT premarket price, signed return and status are retained");
        Check(Parse(QuoteFrame(true,extended,field:"nxtOverMarketPriceInfo")).Quote.Price==10200,"alternate NXT provider field is supported");
        Check(Parse(QuoteFrame(true,extended with{overPrice="bad"})).Quote is{Price:10000,State:"OPEN"},"extended session status updates even without a new valid price");
        Check(Parse(QuoteFrame(true,extended with{localTradedAt="2026-11-09T14:00:00+09:00"})).Quote.Price==10000,"older NXT price cannot replace a later regular trade");
        var after=new{tradingSessionType="AFTER_MARKET",overMarketStatus="OPEN",overPrice="96",localTradedAt="2026-11-09T16:00:00-05:00"};
        var us=Parse(QuoteFrame(false,after,traded:"2026-11-09T16:00:00-05:00"),false);
        Check(us.Quote is{Price:96,State:"OPEN",Change:-4}&&us.Quote.Percent==-4,"US after-hours may start at equal timestamp and retains previous-day basis");
        Check(Parse(QuoteFrame(false,after with{tradingSessionType="PRE_MARKET",localTradedAt="2026-11-10T08:00:00-05:00"},traded:""),false).Quote.Change==1,"US premarket with missing regular time uses last regular close");
        Check(PublicMarketData.ReadPollingInterval(PriceJson("{\"pollingInterval\":\"bad\"}"))==TimeSpan.FromSeconds(1),"malformed polling metadata never suppresses a valid quote");
        bool invalid=false;try{Parse(PriceJson(QuoteFrame().GetRawText().Replace("USD","KRW")),false);}catch(InvalidOperationException){invalid=true;}Check(invalid,"mismatched public quote currency is rejected");
        var primary=new NaverQuoteHttp();var day=new ViewerDayHttp(clock);using(var data=new PublicMarketData(primary,day,clock))
        {
            var current=data.Current(QuoteApple,CancellationToken.None).GetAwaiter().GetResult();Check(current.Quote is{Price:351.03m,State:"OPEN"}&&current.Source.Contains("데이마켓")&&current.PollInterval==TimeSpan.FromSeconds(1),"offline service selects StockViewer's verified US daytime price");
            data.CurrentFast(QuoteApple,CancellationToken.None).GetAwaiter().GetResult();Check(primary.Calls==1&&day.Prices==1,"foreground and ordinary price reads share one in-flight/cache slot");
            clock.Now=clock.Now.AddSeconds(1);day.Price=352;var next=data.CurrentFast(QuoteApple,CancellationToken.None).GetAwaiter().GetResult();Check(next.Quote.Price==352&&primary.LastPath!.EndsWith("AAPL.O")&&day.Searches==1,"one-second refresh preserves opaque provider ID and reuses verified product mapping");
            day.Fail=true;clock.Now=clock.Now.AddSeconds(1);var failed=data.Current(QuoteApple,CancellationToken.None).GetAwaiter().GetResult();Check(failed.Quote is{Price:352,State:"UNKNOWN"}&&failed.Source.Contains("확인 지연"),"daytime failure preserves last valid price and reports unknown session");
            day.Fail=false;primary.Limited=true;clock.Now=clock.Now.AddSeconds(1);day.Price=353;Check(data.Current(QuoteApple,CancellationToken.None).GetAwaiter().GetResult().Quote.Price==353,"daytime remains available while Naver is rate-limited");
            clock.Now=clock.Now.AddSeconds(1);day.Price=354;data.Current(QuoteApple,CancellationToken.None).GetAwaiter().GetResult();Check(primary.Calls==4,"Naver Retry-After suppresses new primary HTTP requests while daytime continues");
            primary.Limited=false;clock.Now=DateTimeOffset.Parse("2026-11-10T08:51:00Z");var closed=data.Current(QuoteApple,CancellationToken.None).GetAwaiter().GetResult();Check(closed.Quote is{Price:354,State:"CLOSE"},"full market close retains last daytime price until a newer venue trade arrives");
            Check(!primary.Auth&&!day.Auth&&!day.Private&&day.Init==1,"anonymous quote reads never use account credentials, login or order endpoints");
        }
        var limited=new NaverQuoteHttp{Limited=true};using(var data=new PublicMarketData(limited,clock:clock))
        {
            for(int i=0;i<2;i++){try{data.Current(QuoteSamsung,CancellationToken.None).GetAwaiter().GetResult();}catch(PublicQuoteRetryException){}}
            Check(limited.Calls==1,"public Retry-After blocks duplicate requests across repeated reads");
        }
        var held=new ViewerDayHttp(clock){Hold=new(TaskCreationOptions.RunContinuationsAsynchronously)};clock.Now=DateTimeOffset.Parse("2026-11-10T02:30:00Z");using(var data=new PublicMarketData(new NaverQuoteHttp(),held,clock))using(var cancel=new CancellationTokenSource())
        {
            var pending=data.Current(QuoteApple,cancel.Token);Pump(()=>held.Started.Task.IsCompleted);cancel.Cancel();Pump(()=>pending.IsCompleted);bool cancelled=false;try{pending.GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"switch/cancel interrupts pending daytime HTTP without publishing a late quote");
        }
    }
}
