using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Stock.Core.Trading;
namespace Stock.Core;

/// <summary>메리츠 공식 국내주식 Open API. 주문 POST는 재시도하지 않습니다.</summary>
public sealed class MeritzBroker : IAccountBroker
{
    public const string LiveHost="https://openapi.imeritz.com:9443";
    private readonly BrokerConnection credentials;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate=new(1,1);
    private DateTimeOffset last,expires;
    private string? token;
    public string Id=>"meritz";
    public string Name=>"메리츠증권";
    public bool IsLive=>true;
    public Exchange Exchange=>Exchange.KRX;
    public bool SupportsMarketOrders=>true;
    // The gateway binds an account to the app key, and does not return its account number.
    public string Account=>TradingStore.Hash(credentials.AppKey);
    public string MaskedAccount=>"계좌별 API 키 · "+Account[..8];
    public MeritzBroker(BrokerConnection credentials,HttpMessageHandler? handler=null)
    {credentials.Validate();if(credentials.Brokerage!=Brokerage.Meritz)throw new InvalidOperationException("메리츠 키가 필요합니다.");this.credentials=credentials;http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new(LiveHost),Timeout=TimeSpan.FromSeconds(20)};}
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
    {await gate.WaitAsync();try{var delay=TimeSpan.FromMilliseconds(650)-(DateTimeOffset.UtcNow-last);if(delay>TimeSpan.Zero)await Task.Delay(delay);last=DateTimeOffset.UtcNow;return await http.SendAsync(request);}finally{gate.Release();}}
    public async Task ConnectAsync()
    {
        if(token!=null&&expires>DateTimeOffset.UtcNow.AddMinutes(2))return;
        using var request=new HttpRequestMessage(HttpMethod.Post,"/oauth2/token"){Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"},{"client_id",credentials.AppKey},{"client_secret",credentials.Secret},{"scope","login"}})};
        using var response=await Send(request);
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new LiveKeyRejectedException();
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("메리츠 인증 연결 실패. 키·등록 IP·서비스 신청을 확인하세요.");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=doc.RootElement;
        var value=Text(root,"access_token");if(string.IsNullOrWhiteSpace(value))throw new LiveKeyRejectedException();
        expires=DateTimeOffset.UtcNow.AddSeconds((double)Number(root,"expires_in"));token=value;
    }
    private async Task<JsonElement> Query(string path,Dictionary<string,string>? args=null)
    {
        await ConnectAsync();var query=args==null?"":"?"+string.Join("&",args.Select(p=>p.Key+"="+Uri.EscapeDataString(p.Value)));
        using var request=Request(HttpMethod.Get,path+query);using var response=await Send(request);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("메리츠 계좌 조회 연결 실패");
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=doc.RootElement;
        if(Text(root,"rsp_cd") is not ("0000" or "0001" or "5762" or "5766" or "5820" or "5822"))throw new InvalidOperationException("메리츠 조회 실패. 계좌 권한과 조회 조건을 확인하세요.");
        return root.Clone();
    }
    private HttpRequestMessage Request(HttpMethod method,string path)
    {var r=new HttpRequestMessage(method,path);r.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);r.Headers.Add("mac_address",credentials.MacAddress);return r;}
    public async Task<Balance> GetBalanceAsync()
    {
        var deposit=(await Query("/accounts/v1/me/deposit")).GetProperty("data");var cash=Number(deposit,"tfam");
        var root=await Query("/accounts/v1/me/holdings",new(){{"nthn_rmnd_incu_yn","N"}});
        if(Text(root,"rsp_cd")=="5762"||Text(root,"tr_cont")=="1")throw new InvalidOperationException("메리츠 잔고가 완전하지 않습니다. 주문을 진행할 수 없습니다.");
        var rows=root.GetProperty("data").EnumerateArray().ToArray();
        if(rows.Any(r=>Number(r,"loan_amt")!=0))throw new InvalidOperationException("현재 메리츠 연결은 신용·대출 잔고를 지원하지 않습니다.");
        var holdings=rows.GroupBy(r=>Code(Text(r,"iscd"))).Select(g=>{var qty=g.Sum(r=>Number(r,"rmnd_qty"));var first=g.First();return new Holding(g.Key,Text(first,"isnm"),qty,qty==0?0:g.Sum(r=>Number(r,"byng_unpr")*Number(r,"rmnd_qty"))/qty,Number(first,"psnt_prc"),g.Sum(r=>Number(r,"vltn_prls_amt")));}).ToArray();
        return new(holdings,cash,cash+holdings.Sum(h=>h.Quantity*h.CurrentPrice));
    }
    public async Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)
    {
        if(date>KoreaClock.Today)throw new InvalidOperationException("미래 날짜는 조회할 수 없습니다.");
        var args=new Dictionary<string,string>{{"oder_date",date.ToString("yyyyMMdd")},{"deal_cls_code","0"},{"cntg_cls_code","0"},{"sort_stnd_code","2"}};
        List<BrokerOrder> orders=[];HashSet<string> keys=[];
        for(var page=0;page<100;page++)
        {
            var root=await Query("/trading/v1/orders/history",args);
            foreach(var r in root.GetProperty("data").EnumerateArray())orders.Add(new(date.ToString("yyyyMMdd"),Text(r,"oder_no"),Code(Text(r,"iscd")),Text(r,"kor_issu_abwr_name"),Text(r,"deal_cls_code")=="2"?"매수":"매도",Number(r,"odqt"),Number(r,"oder_unpr"),Number(r,"total_cntg_qty"),Number(r,"ncnt_qty"),Number(r,"cncl_qty")>0,Text(r,"oder_rfus_reas_code") is not ("" or "0" or "0000"),"KRW","KRX",Text(r,"oder_pros_stat_cls_code"),Text(r,"orgl_oder_no")));
            if(Text(root,"rsp_cd")!="5762")return orders;
            var key=root.GetProperty("tr_cont_key").GetString()??"";if(key.Length==0||!keys.Add(key))throw new InvalidOperationException("메리츠 연속조회가 반복되었습니다. 전체 조회로 처리하지 않습니다.");
            args["tr_cont"]="1";args["tr_cont_key"]=key;
        }
        throw new InvalidOperationException("메리츠 조회 페이지 한도에 도달했습니다.");
    }
    public async Task<decimal> CurrentPrice(string symbol)
    {
        new Instrument(symbol,Exchange).Validate();var data=(await Query("/market/v1/prices",new(){{"mrkt_div_code","J"},{"iscd",symbol}})).GetProperty("data");
        if(Code(Text(data,"shrn_iscd"))!=symbol)throw new InvalidOperationException("메리츠 시세의 종목을 확인하지 못했습니다.");var price=Number(data,"stck_prpr");if(price<=0)throw new InvalidOperationException("유효한 현재가가 없습니다.");return price;
    }
    public Task<OrderReceipt> SubmitAsync(OrderRequest order)
    {
        order.Validate();if(order.Market!=Exchange)throw new InvalidOperationException("메리츠 연결은 현재 국내주식만 지원합니다.");
        return PostOrder("/trading/v1/orders/"+(order.Buy?"buy":"sell"),new(){{"iscd","A"+order.Symbol},{"oder_unpr",order.Kind==OrderKind.Market?"0":order.Price.ToString(CultureInfo.InvariantCulture)},{"odqt",order.Quantity.ToString(CultureInfo.InvariantCulture)},{"oder_cls_code",order.Kind==OrderKind.Market?"05":"01"},{"oder_cond_cls_code","0"},{"orgl_oder_no","0"},{"whol_rctf_cncl_yn","N"},{"warn_cnfr_yn","N"},{"exch_kind_code","01"}});
    }
    public async Task<OrderReceipt> CancelAsync(string number,string symbol)
    {
        new Instrument(symbol,Exchange).Validate();var orders=await GetOrdersAsync(KoreaClock.Today);
        var original=orders.SingleOrDefault(o=>o.Number==number&&o.Symbol==symbol&&o.Remaining>0&&!o.Cancelled&&!o.Rejected)??throw new BrokerRejectedException("메리츠 취소 대상 주문을 확인하지 못했습니다.");
        return await PostOrder("/trading/v1/orders/cancel",new(){{"iscd","A"+symbol},{"oder_unpr",original.Price.ToString(CultureInfo.InvariantCulture)},{"odqt",original.Remaining.ToString(CultureInfo.InvariantCulture)},{"oder_cond_cls_code","0"},{"orgl_oder_no",number},{"whol_rctf_cncl_yn","Y"},{"warn_cnfr_yn","N"},{"exch_kind_code","01"}});
    }
    private async Task<OrderReceipt> PostOrder(string path,Dictionary<string,string> body)
    {
        await ConnectAsync();using var request=Request(HttpMethod.Post,path);request.Content=JsonContent.Create(body);
        try
        {
            using var response=await Send(request);if(!response.IsSuccessStatusCode)throw new UnknownOrderException();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=doc.RootElement;
            var code=Text(root,"rsp_cd");if(code!="0001"){if(code is "0000" or "5762" or "5766" or "5820" or "5822"||!System.Text.RegularExpressions.Regex.IsMatch(code,"^(?:[0-9]{4}|[EI]GW[0-9]{5})$"))throw new UnknownOrderException();throw new BrokerRejectedException("메리츠가 주문을 거부했습니다. 거래 조건과 계좌를 확인하세요.");}
            var data=root.GetProperty("data");if(data.ValueKind==JsonValueKind.Array)data=data.EnumerateArray().Single();
            if(Text(data,"warn_cls_code")!="0"||Text(root,"warn_cls_code") is not ("" or "0"))throw new BrokerRejectedException("메리츠 주문 경고로 접수되지 않았습니다. 증권사 앱에서 조건을 확인하세요.");
            var number=Text(data,"oder_no");if(!long.TryParse(number,out var n)||n<=0)throw new UnknownOrderException();return new(number);
        }catch(BrokerRejectedException){throw;}catch{throw new UnknownOrderException();}
    }
    private static string Code(string s)=>s.Length==7&&s[0]=='A'?s[1..]:s;
    private static string Text(JsonElement r,string name)=>r.TryGetProperty(name,out var v)?v.ToString().Trim():"";
    private static decimal Number(JsonElement r,string name)=>decimal.TryParse(Text(r,name),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:throw new InvalidOperationException("메리츠 숫자 응답이 누락되었거나 잘못되었습니다.");
    public void Dispose(){token=null;http.Dispose();gate.Dispose();}
}
