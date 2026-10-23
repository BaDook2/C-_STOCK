using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.Core;

/// <summary>토스증권 공식 Open API. 선택한 종합매매 계좌에만 요청하고 주문은 재전송하지 않습니다.</summary>
public sealed class TossBroker : IAccountBroker,IMinuteHistoryBroker
{
    public const string LiveHost="https://openapi.tossinvest.com";
    private readonly BrokerConnection credentials;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate=new(1,1),authGate=new(1,1);
    private string? token,accountSeq;
    private DateTimeOffset expires,last;
    public string Id=>"toss";
    public string Name=>"토스증권";
    public bool IsLive=>true;
    public Exchange Exchange=>Exchange.KRX;
    public string Account {get;private set;}="";
    public string MaskedAccount=>Account.Length<4?"미확인":new string('*',Account.Length-4)+Account[^4..];
    public bool SupportsMarketOrders=>true;
    public TossBroker(BrokerConnection credentials,HttpMessageHandler? handler=null)
    {
        credentials.Validate();if(credentials.Brokerage!=Brokerage.Toss)throw new InvalidOperationException("토스증권 키가 필요합니다.");
        this.credentials=credentials;http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new(LiveHost),Timeout=TimeSpan.FromSeconds(20)};
    }
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request,CancellationToken cancellation=default)
    {await gate.WaitAsync(cancellation);try{var delay=TimeSpan.FromMilliseconds(650)-(DateTimeOffset.UtcNow-last);if(delay>TimeSpan.Zero)await Task.Delay(delay,cancellation);last=DateTimeOffset.UtcNow;return await http.SendAsync(request,cancellation);}finally{gate.Release();}}
    public async Task ConnectAsync()
    {
        await authGate.WaitAsync();try
        {
            if(token!=null&&expires>DateTimeOffset.UtcNow.AddMinutes(2)&&accountSeq!=null)return;
            if(token==null||expires<=DateTimeOffset.UtcNow.AddMinutes(2))
            {
                using var request=new HttpRequestMessage(HttpMethod.Post,"/oauth2/token"){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"},{"client_id",credentials.AppKey},{"client_secret",credentials.Secret}})};
                using var response=await Send(request);if(response.StatusCode==HttpStatusCode.Unauthorized)throw new LiveKeyRejectedException();
                if(!response.IsSuccessStatusCode)throw new InvalidOperationException("토스증권 인증 실패. 키·허용 IP·API 이용 신청을 확인하세요.");
                using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());token=Text(doc.RootElement,"access_token");if(string.IsNullOrWhiteSpace(token))throw new LiveKeyRejectedException();expires=DateTimeOffset.UtcNow.AddSeconds((double)Number(doc.RootElement,"expires_in"));
            }
            var accounts=(await Get("/api/v1/accounts",false)).EnumerateArray().Where(a=>Text(a,"accountType")=="BROKERAGE").ToArray();
            var requested=credentials.Account.Replace("-","").Replace(" ","");
            var match=requested.Length==0?accounts:accounts.Where(a=>Text(a,"accountNo").Replace("-","").Replace(" ","")==requested).ToArray();
            if(match.Length!=1)throw new InvalidOperationException("연결할 토스증권 종합매매 계좌번호를 입력하세요. 계좌를 임의로 선택하지 않습니다.");
            Account=Text(match[0],"accountNo");accountSeq=Text(match[0],"accountSeq");if(Account==""||!long.TryParse(accountSeq,out var seq)||seq<=0)throw new InvalidOperationException("토스증권 계좌 식별자를 확인하지 못했습니다.");
        }finally{authGate.Release();}
    }
    private HttpRequestMessage Request(HttpMethod method,string path,bool account=true)
    {var r=new HttpRequestMessage(method,path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);if(account){if(accountSeq==null)throw new InvalidOperationException("계좌 연결이 필요합니다.");r.Headers.Add("X-Tossinvest-Account",accountSeq);}return r;}
    private async Task<JsonElement> Get(string path,bool account=true,CancellationToken cancellation=default)
    {
        using var r=Request(HttpMethod.Get,path,account);using var response=await Send(r,cancellation);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("토스증권 조회 실패. 연결·권한·조회 한도를 확인하세요.");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));return doc.RootElement.GetProperty("result").Clone();
    }
    public async Task<Balance> GetBalanceAsync()
    {
        await ConnectAsync();var cashRow=await Get("/api/v1/buying-power?currency=KRW");if(Text(cashRow,"currency")!="KRW")throw new InvalidOperationException("계좌 통화를 확인하지 못했습니다.");
        var cash=Number(cashRow,"cashBuyingPower");var result=await Get("/api/v1/holdings");
        var holdings=result.GetProperty("items").EnumerateArray().Where(h=>Text(h,"marketCountry")=="KR").Select(h=>new Holding(Text(h,"symbol"),Text(h,"name"),Number(h,"quantity"),Number(h,"averagePurchasePrice"),Number(h,"lastPrice"),Number(h.GetProperty("profitLoss"),"amountAfterCost"))).ToArray();
        return new(holdings,cash,cash+holdings.Sum(h=>h.Quantity*h.CurrentPrice));
    }
    public async Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)
    {
        await ConnectAsync();var orders=new Dictionary<string,BrokerOrder>();
        foreach(var status in new[]{"CLOSED","OPEN"})
        {
            string cursor="";var seen=new HashSet<string>();bool complete=false;
            for(int page=0;page<100;page++)
            {
                var root=await Get($"/api/v1/orders?status={status}&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}&limit=100"+(cursor==""?"":"&cursor="+Uri.EscapeDataString(cursor)));
                foreach(var o in root.GetProperty("orders").EnumerateArray())
                {
                    if(Text(o,"currency")!="KRW")continue;
                    var execution=o.GetProperty("execution");var qty=Number(o,"quantity");var filled=Number(execution,"filledQuantity");var state=Text(o,"status");
                    var row=new BrokerOrder(date.ToString("yyyyMMdd"),Text(o,"orderId"),Text(o,"symbol"),Text(o,"symbol"),Text(o,"side")=="BUY"?"매수":"매도",qty,OptionalNumber(o,"price")??0,filled,state is "FILLED" or "CANCELED" or "REJECTED" or "REPLACED"?0:Math.Max(0,qty-filled),state=="CANCELED",state=="REJECTED","KRW","KRX",state,AverageFillPrice:OptionalNumber(execution,"averageFilledPrice"),LastFillTime:DateTimeOffset.TryParse(Text(execution,"filledAt"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)?time:null);
                    orders[row.Number]=row;
                }
                if(status=="OPEN"||!root.GetProperty("hasNext").GetBoolean()){complete=true;break;}
                cursor=Text(root,"nextCursor");if(cursor==""||!seen.Add(cursor))throw new InvalidOperationException("토스증권 주문 내역의 다음 페이지를 확인하지 못했습니다.");
            }
            if(!complete)throw new InvalidOperationException("토스증권 주문 조회 범위를 초과했습니다.");
        }
        return orders.Values.ToArray();
    }
    public async Task<decimal> CurrentPrice(string symbol)
    {
        new Instrument(symbol,Exchange).Validate();await ConnectAsync();var rows=await Get("/api/v1/prices?symbols="+Uri.EscapeDataString(symbol),false);
        var row=rows.EnumerateArray().Single(r=>Text(r,"symbol")==symbol&&Text(r,"currency")=="KRW");var price=Number(row,"lastPrice");return price>0?price:throw new InvalidOperationException("현재가가 없습니다.");
    }
    public Task<OrderReceipt> SubmitAsync(OrderRequest order)
    {
        order.Validate();if(order.Market!=Exchange)throw new InvalidOperationException("현재 토스증권 연결은 국내주식 주문만 지원합니다.");
        var body=new Dictionary<string,object>{{"clientOrderId",Guid.NewGuid().ToString("N")},{"symbol",order.Symbol},{"side",order.Buy?"BUY":"SELL"},{"orderType",order.Kind==OrderKind.Market?"MARKET":"LIMIT"},{"timeInForce","DAY"},{"quantity",order.Quantity.ToString(CultureInfo.InvariantCulture)}};
        if(order.Kind==OrderKind.Limit)body["price"]=order.Price.ToString(CultureInfo.InvariantCulture);
        return Post("/api/v1/orders",body);
    }
    public async Task<OrderReceipt> CancelAsync(string number,string symbol)
    {
        var found=(await GetOrdersAsync(KoreaClock.Today)).SingleOrDefault(o=>o.Number==number&&o.Symbol==symbol&&o.Remaining>0&&!o.Cancelled&&!o.Rejected)??throw new BrokerRejectedException("취소 대상 주문을 확인하지 못했습니다.");
        return await Post("/api/v1/orders/"+Uri.EscapeDataString(found.Number)+"/cancel",new Dictionary<string,object>());
    }
    private async Task<OrderReceipt> Post(string path,object body)
    {
        await ConnectAsync();using var request=Request(HttpMethod.Post,path);request.Content=JsonContent.Create(body);
        try{using var response=await Send(request);using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if(!response.IsSuccessStatusCode){if((int)response.StatusCode is >=400 and <500&&doc.RootElement.TryGetProperty("error",out var error)&&Text(error,"code")!="")throw new BrokerRejectedException("토스증권이 요청을 거부했습니다. 증권사에서 거래 조건을 확인하세요.");throw new UnknownOrderException();}
            var id=Text(doc.RootElement.GetProperty("result"),"orderId");if(id=="")throw new UnknownOrderException();return new(id);
        }catch(BrokerRejectedException){throw;}catch{throw new UnknownOrderException();}
    }
    public async Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation)
    {
        new Instrument(symbol,Exchange).Validate();await ConnectAsync();var bars=new List<PriceBar>();var cursor="";var seen=new HashSet<string>();
        for(int page=0;page<10;page++)
        {
            cancellation.ThrowIfCancellationRequested();var root=await Get("/api/v1/candles?symbol="+Uri.EscapeDataString(symbol)+"&interval=1m&count=200"+(cursor==""?"":"&before="+Uri.EscapeDataString(cursor)),false,cancellation);
            foreach(var b in root.GetProperty("candles").EnumerateArray()){if(Text(b,"currency")!="KRW")throw new InvalidOperationException("분봉 통화가 다릅니다.");var bar=new PriceBar(DateTimeOffset.Parse(Text(b,"timestamp"),CultureInfo.InvariantCulture),Number(b,"openPrice"),Number(b,"highPrice"),Number(b,"lowPrice"),Number(b,"closePrice"),Number(b,"volume"));bar.Validate();bars.Add(bar);}
            cursor=Text(root,"nextBefore");if(cursor==""||!seen.Add(cursor))break;
        }
        return bars.DistinctBy(b=>b.Time).OrderBy(b=>b.Time).ToArray();
    }
    private static string Text(JsonElement e,string key)=>e.TryGetProperty(key,out var v)&&v.ValueKind!=JsonValueKind.Null?v.ToString():"";
    private static decimal Number(JsonElement e,string key)=>OptionalNumber(e,key)??throw new InvalidOperationException("토스증권 숫자 응답이 누락되었습니다.");
    private static decimal? OptionalNumber(JsonElement e,string key)=>decimal.TryParse(Text(e,key),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:null;
    public void Dispose(){token=null;http.Dispose();gate.Dispose();authGate.Dispose();}
}

public interface IMinuteHistoryBroker
{
    Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation);
}

/// <summary>Reads enough one-minute history to cover an explicit screening range.</summary>
public interface IRangedMinuteHistoryBroker
{
    Task<PriceBar[]> MinuteHistory(string symbol,DateOnly from,DateOnly to,CancellationToken cancellation);
}

/// <summary>Reads a broker-native minute interval so screening does not download every one-minute row.</summary>
public interface IRangedMinuteIntervalHistoryBroker
{
    bool SupportsMinuteInterval(int minutes);
    Task<PriceBar[]> MinuteHistory(string symbol,DateOnly from,DateOnly to,int minutes,CancellationToken cancellation);
}

/// <summary>Broker-provided historical executions represented as one PriceBar per raw tick.</summary>
public interface ITickHistoryBroker
{
    Task<PriceBar[]> TickHistory(string symbol,CancellationToken cancellation);
}
