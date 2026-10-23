using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Stock.Core;

// Fixed broker hosts, no redirects, and no automatic POST retries.
public sealed class KisBroker : IAccountBroker
{
    public const string PaperHost = "https://openapivts.koreainvestment.com:29443";
    public const string LiveHost = "https://openapi.koreainvestment.com:9443";
    public string Id=>"kis";
    public string Name=>"한국투자증권";
    public bool IsLive {get;}
    public Trading.Exchange Exchange=>Trading.Exchange.KRX;
    public bool SupportsMarketOrders=>true;
    public string Account=>credentials.Account+credentials.Product;
    public string MaskedAccount=>"******"+Account[^4..];
    private readonly Dictionary<string,string> orderBranches=[];
    private readonly HttpClient http;
    private readonly Credentials credentials;
    private readonly SemaphoreSlim requests = new(1, 1);
    private DateTimeOffset lastRequest;
    private DateTimeOffset tokenExpires;
    private string? token;
    public KisBroker(Credentials credentials, HttpMessageHandler? handler = null,bool live=false)
    {
        credentials.Validate();
        this.credentials = credentials;IsLive=live;
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = new Uri(live?LiveHost:PaperHost), Timeout = TimeSpan.FromSeconds(20) };
    }
    public async Task ConnectAsync()
    {
        if (token != null && tokenExpires > DateTimeOffset.UtcNow.AddMinutes(2)) return;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/tokenP")
        { Content = JsonContent.Create(new { grant_type = "client_credentials", appkey = credentials.AppKey, appsecret = credentials.AppSecret }) };
        using var response = await SendAsync(request);
        if (!response.IsSuccessStatusCode){if(IsLive&&response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)throw new LiveKeyRejectedException();throw new InvalidOperationException("한국투자 인증 실패. 키·등록 IP·서비스 신청 상태를 확인하세요.");}
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var value = Text(json.RootElement, "access_token");
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("인증 응답에 토큰이 없습니다. 로컬 키 설정을 확인하세요.");
        var seconds = Number(json.RootElement, "expires_in");
        tokenExpires = DateTimeOffset.UtcNow.AddSeconds(seconds > 0 ? (double)seconds : 3600);
        token = value;
    }
    private HttpRequestMessage Request(HttpMethod method, string path, string tr)
    {
        if (token == null || tokenExpires <= DateTimeOffset.UtcNow.AddSeconds(30))
            throw new InvalidOperationException("연결이 없거나 토큰이 만료되었습니다. 다시 연결하세요.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("appkey", credentials.AppKey);
        request.Headers.Add("appsecret", credentials.AppSecret);
        request.Headers.Add("tr_id", IsLive&&tr.StartsWith("V",StringComparison.Ordinal)?"T"+tr[1..]:tr);
        request.Headers.Add("custtype", "P");
        return request;
    }
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        await requests.WaitAsync();
        try
        {
            var delay = TimeSpan.FromMilliseconds(650) - (DateTimeOffset.UtcNow - lastRequest);
            if (delay > TimeSpan.Zero) await Task.Delay(delay);
            lastRequest = DateTimeOffset.UtcNow;
            return await http.SendAsync(request);
        }
        finally { requests.Release(); }
    }
    public async Task<OrderReceipt> SubmitAsync(OrderRequest order)
    {
        order.Validate();
        if(order.Market!=Exchange)throw new InvalidOperationException("한국투자 주문은 현재 국내주식만 지원합니다.");
        using var request = Request(HttpMethod.Post, "/uapi/domestic-stock/v1/trading/order-cash", order.Buy ? "VTTC0012U" : "VTTC0011U");
        var body = AccountParams();
        body["PDNO"] = order.Symbol;
        body["ORD_DVSN"] = order.Kind==Trading.OrderKind.Market?"01":"00";
        body["ORD_QTY"] = order.Quantity.ToString(CultureInfo.InvariantCulture);
        body["ORD_UNPR"] = order.Kind==Trading.OrderKind.Market?"0":order.Price.ToString(CultureInfo.InvariantCulture);
        body["EXCG_ID_DVSN_CD"] = "KRX";
        body["SLL_TYPE"] = order.Buy ? "" : "01";
        body["CNDT_PRIC"] = "";
        request.Content = JsonContent.Create(body);
        try
        {
            using var response = await SendAsync(request);
            // HTTP failures may arrive after the broker has accepted an order.
            if (!response.IsSuccessStatusCode) throw new UnknownOrderException();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            if (Text(root, "rt_cd") == "1") throw new BrokerRejectedException("증권사가 주문을 거부했습니다. 주문 조건·잔고·거래시간을 확인하세요.");
            if (Text(root, "rt_cd") != "0") throw new UnknownOrderException();
            var number = Text(root.GetProperty("output"), "ODNO");
            if (string.IsNullOrWhiteSpace(number)) throw new UnknownOrderException();
            orderBranches[number]=Text(root.GetProperty("output"),"KRX_FWDG_ORD_ORGNO");
            return new OrderReceipt(number);
        }
        catch (BrokerRejectedException) { throw; }
        catch { throw new UnknownOrderException(); }
    }
    private Dictionary<string, string> AccountParams() => new()
    { ["CANO"] = credentials.Account, ["ACNT_PRDT_CD"] = credentials.Product };
    private async Task<List<JsonElement>> PagesAsync(string path, string tr, Dictionary<string, string> args, Action<JsonElement>? inspect = null)
    {
        args["CTX_AREA_FK100"] = "";
        args["CTX_AREA_NK100"] = "";
        var results = new List<JsonElement>();
        var previous = new HashSet<string>();
        for (int page = 0; page < 100; page++)
        {
            string query = string.Join("&", args.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
            using var request = Request(HttpMethod.Get, path + "?" + query, tr);
            if (page > 0) request.Headers.Add("tr_cont", "N");
            using var response = await SendAsync(request);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("조회 연결 오류. 표시된 기존 자료는 최신 자료가 아닙니다.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            if (Text(root, "rt_cd") != "0") throw new InvalidOperationException("증권사 조회 실패. 연결 설정·조회일·서비스 상태를 확인하세요.");
            foreach (var row in root.GetProperty("output1").EnumerateArray()) results.Add(row.Clone());
            inspect?.Invoke(root);
            var continuation = response.Headers.TryGetValues("tr_cont", out var values) ? values.FirstOrDefault()?.Trim() : "";
            if (continuation is not ("F" or "M")) return results;
            args["CTX_AREA_FK100"] = Text(root, "ctx_area_fk100");
            args["CTX_AREA_NK100"] = Text(root, "ctx_area_nk100");
            if (!previous.Add(args["CTX_AREA_FK100"] + "|" + args["CTX_AREA_NK100"]))
                throw new InvalidOperationException("연속조회 키가 반복되었습니다. 조회 완료로 처리하지 않습니다.");
        }
        throw new InvalidOperationException("조회 페이지 한도에 도달했습니다. 전체 조회를 완료하지 못했습니다.");
    }
    public async Task<Balance> GetBalanceAsync()
    {
        var args = AccountParams();
        foreach (var pair in new Dictionary<string, string> { ["AFHR_FLPR_YN"]="N", ["OFL_YN"]="", ["INQR_DVSN"]="02", ["UNPR_DVSN"]="01", ["FUND_STTL_ICLD_YN"]="N", ["FNCG_AMT_AUTO_RDPT_YN"]="N", ["PRCS_DVSN"]="00" }) args.Add(pair.Key, pair.Value);
        decimal cash = 0, total = 0;
        var rows = await PagesAsync("/uapi/domestic-stock/v1/trading/inquire-balance", "VTTC8434R", args, root =>
        {
            var summary = root.GetProperty("output2");
            if (summary.GetArrayLength() == 0) throw new InvalidOperationException("잔고 요약이 누락되었습니다.");
            cash = Number(summary[0], "dnca_tot_amt"); total = Number(summary[0], "tot_evlu_amt");
        });
        return new Balance(rows.Select(r => new Holding(Text(r,"pdno"), Text(r,"prdt_name"), Number(r,"hldg_qty"), Number(r,"pchs_avg_pric"), Number(r,"prpr"), Number(r,"evlu_pfls_amt"))).ToList(), cash, total);
    }
    public async Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)
    {
        if (date > KoreaClock.Today || date < KoreaClock.Today.AddMonths(-3)) throw new InvalidOperationException("조회일은 오늘부터 최근 3개월 이내로 선택하세요.");
        var args = AccountParams();
        foreach (var pair in new Dictionary<string, string> { ["INQR_STRT_DT"]=date.ToString("yyyyMMdd"), ["INQR_END_DT"]=date.ToString("yyyyMMdd"), ["SLL_BUY_DVSN_CD"]="00", ["PDNO"]="", ["CCLD_DVSN"]="00", ["INQR_DVSN"]="00", ["INQR_DVSN_3"]="00", ["ORD_GNO_BRNO"]="", ["ODNO"]="", ["INQR_DVSN_1"]="", ["EXCG_ID_DVSN_CD"]="KRX" }) args.Add(pair.Key,pair.Value);
        var rows = await PagesAsync("/uapi/domestic-stock/v1/trading/inquire-daily-ccld", "VTTC0081R", args);
        foreach(var row in rows)orderBranches[Text(row,"odno")]=Text(row,"ord_gno_brno");
        return rows.Select(r => new BrokerOrder(Text(r,"ord_dt"),Text(r,"odno"),Text(r,"pdno"),Text(r,"prdt_name"),Text(r,"sll_buy_dvsn_cd") == "02" ? "매수" : "매도",Number(r,"ord_qty"),Number(r,"ord_unpr"),Number(r,"tot_ccld_qty"),Number(r,"rmn_qty"),Text(r,"cncl_yn") == "Y",Number(r,"rjct_qty") > 0)).ToList();
    }
    private static string Text(JsonElement root, string name) => root.TryGetProperty(name, out var item) ? item.ToString().Trim() : "";
    public async Task<decimal> CurrentPrice(string symbol)
    {
        new Trading.Instrument(symbol,Exchange).Validate();
        using var request=Request(HttpMethod.Get,"/uapi/domestic-stock/v1/quotations/inquire-price?FID_COND_MRKT_DIV_CODE=J&FID_INPUT_ISCD="+Uri.EscapeDataString(symbol),"FHKST01010100");
        using var response=await SendAsync(request);response.EnsureSuccessStatusCode();
        using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=doc.RootElement;
        if(Text(root,"rt_cd")!="0")throw new InvalidOperationException("한국투자 현재가 조회 실패");
        var price=Number(root.GetProperty("output"),"stck_prpr");if(price<=0)throw new InvalidOperationException("유효한 현재가를 확인하지 못했습니다.");return price;
    }
    public async Task<OrderReceipt> CancelAsync(string number,string symbol)
    {
        new Trading.Instrument(symbol,Exchange).Validate();
        var orders=await GetOrdersAsync(KoreaClock.Today);
        if(!orders.Any(o=>o.Number==number&&o.Symbol==symbol&&o.Remaining>0&&!o.Cancelled&&!o.Rejected)||!orderBranches.TryGetValue(number,out var branch)||string.IsNullOrEmpty(branch))throw new BrokerRejectedException("취소할 주문과 주문지점을 확인하지 못했습니다.");
        var body=AccountParams();body["KRX_FWDG_ORD_ORGNO"]=branch;body["ORGN_ODNO"]=number;body["ORD_DVSN"]="00";body["RVSE_CNCL_DVSN_CD"]="02";body["ORD_QTY"]="0";body["ORD_UNPR"]="0";body["QTY_ALL_ORD_YN"]="Y";body["EXCG_ID_DVSN_CD"]="KRX";
        using var request=Request(HttpMethod.Post,"/uapi/domestic-stock/v1/trading/order-rvsecncl","VTTC0013U");request.Content=JsonContent.Create(body);
        try{using var response=await SendAsync(request);if(!response.IsSuccessStatusCode)throw new UnknownOrderException();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=doc.RootElement;if(Text(root,"rt_cd")=="1")throw new BrokerRejectedException("한국투자 취소 요청 거부");if(Text(root,"rt_cd")!="0")throw new UnknownOrderException();var receipt=Text(root.GetProperty("output"),"ODNO");if(string.IsNullOrWhiteSpace(receipt))throw new UnknownOrderException();return new(receipt);}catch(BrokerRejectedException){throw;}catch{throw new UnknownOrderException();}
    }
    private static decimal Number(JsonElement root, string name) => decimal.TryParse(Text(root,name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : throw new InvalidOperationException("한국투자 숫자 응답이 누락되었거나 잘못되었습니다.");
    public void Dispose() { token=null;http.Dispose(); requests.Dispose(); }
}
