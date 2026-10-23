using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using Stock.Core.Trading;
using Stock.Core.Research;

namespace Stock.Core;
public sealed class LiveKeyRejectedException : InvalidOperationException
{public LiveKeyRejectedException():base("실제 계좌 키가 아닙니다."){} }
public sealed class KiwoomCredentials
{
    public required string AppKey { get; init; }
    public required string SecretKey { get; init; }
    public void Validate(){if(string.IsNullOrWhiteSpace(AppKey)||string.IsNullOrWhiteSpace(SecretKey))throw new InvalidOperationException("앱의 로컬 연결 설정에서 키움 App Key와 Secret을 입력하세요.");}
}
public sealed class KiwoomBroker : IAccountBroker,IExecutionHistoryBroker,IMinuteHistoryBroker,IRangedMinuteHistoryBroker,IRangedMinuteIntervalHistoryBroker,ITickHistoryBroker
{
    public string Id=>"kiwoom";
    public string Name=>"키움증권";
    public bool SupportsMarketOrders=>true;
    public const string PaperHost="https://mockapi.kiwoom.com";
    public const string LiveHost="https://api.kiwoom.com";
    public bool IsLive {get;}
    private readonly KiwoomCredentials credentials;
    private readonly HttpClient http;
    private readonly SemaphoreSlim gate=new(1,1),authGate=new(1,1);
    private DateTimeOffset previous,expires;
    private string? token;
    public Exchange Exchange { get; }
    public string Currency=>Exchange==Exchange.KRX?"KRW":"USD";
    public string Account { get; private set; }="";
    public string MaskedAccount=>Account.Length>=4?new string('*',Account.Length-4)+Account[^4..]:"확인 전";
    private bool Us=>Exchange!=Exchange.KRX;
    private string Code=>new Instrument("A",Exchange).Code;
    public KiwoomBroker(KiwoomCredentials credentials,Exchange exchange,HttpMessageHandler? handler=null,bool live=false)
    {credentials.Validate();this.credentials=credentials;Exchange=exchange;IsLive=live;http=new(handler??new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(live?LiveHost:PaperHost),Timeout=TimeSpan.FromSeconds(20)};}
    public async Task<decimal> CurrentPrice(string symbol)
    {
        new Instrument(symbol,Exchange).Validate();
        var pages=await Query(Us?"usa20100":"ka10001",Us?"/api/us/mrkcond":"/api/dostk/stkinfo",Us?(object)new{stex_tp=Code,stk_cd=symbol}:new{stk_cd=symbol});
        var price=Math.Abs(Num(pages[0],"cur_prc"));if(price<=0)throw new InvalidOperationException("증권사 현재가를 확인하지 못해 주문하지 않았습니다.");return price;
    }
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request,CancellationToken cancellation=default)
    {await gate.WaitAsync(cancellation);try{var delay=TimeSpan.FromMilliseconds(650)-(DateTimeOffset.UtcNow-previous);if(delay>TimeSpan.Zero)await Task.Delay(delay,cancellation);previous=DateTimeOffset.UtcNow;return await http.SendAsync(request,cancellation);}finally{gate.Release();}}
    public async Task<string> GetTokenAsync()
    {
        await authGate.WaitAsync();try{if(token!=null&&expires>DateTimeOffset.UtcNow.AddMinutes(5))return token;
        using var request=new HttpRequestMessage(HttpMethod.Post,"/oauth2/token"){Content=JsonContent.Create(new{grant_type="client_credentials",appkey=credentials.AppKey,secretkey=credentials.SecretKey})};request.Headers.Add("api-id","au10001");using var response=await Send(request);if(IsLive&&(response.StatusCode==System.Net.HttpStatusCode.Unauthorized||response.StatusCode==System.Net.HttpStatusCode.BadRequest))throw new LiveKeyRejectedException();if(!response.IsSuccessStatusCode)throw new InvalidOperationException("키움 인증 연결 실패. 등록 IP와 해당 환경의 키를 확인하세요.");using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());if(IsLive&&Text(doc.RootElement,"return_code")!="0")throw new LiveKeyRejectedException();Check(doc.RootElement);var value=Text(doc.RootElement,"token");if(value=="")throw new InvalidOperationException("키움 인증 토큰 누락");if(!DateTime.TryParseExact(Text(doc.RootElement,"expires_dt"),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var dt))throw new InvalidOperationException("토큰 만료시각 파싱 실패");expires=new DateTimeOffset(dt,TimeSpan.FromHours(9));if(expires<=DateTimeOffset.UtcNow)throw new InvalidOperationException("이미 만료된 토큰입니다.");token=value;return value;}finally{authGate.Release();}
    }
    public async Task ConnectAsync(){await GetTokenAsync();var page=await Query("ka00001","/api/dostk/acnt",new Dictionary<string,string>());Account=Text(page[0],"acctNo");if(!System.Text.RegularExpressions.Regex.IsMatch(Account,"^[0-9]{10}$"))throw new InvalidOperationException("키움 연결 계좌번호를 확인하지 못했습니다.");}
    private async Task<HttpRequestMessage> Request(string api,string path,object body,string next="")
    {var auth=await GetTokenAsync();var req=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(body)};req.Headers.Add("api-id",api);req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",auth);req.Headers.Add("cont-yn",next==""?"N":"Y");req.Headers.Add("next-key",next);return req;}
    private static void Check(JsonElement root)
    {var code=Text(root,"return_code");if(code!="0")throw new InvalidOperationException("키움 API 요청 거부 (코드 "+(int.TryParse(code,out var n)?n.ToString():"응답 형식 오류")+"). 키/IP/권한/요청 조건을 확인하세요.");}
    private async Task<List<JsonElement>> Query(string api,string path,object body,CancellationToken cancellation=default,int maxPages=100,bool acceptPartialAtLimit=false,Func<JsonElement,bool>? stopAfterPage=null)
    {
        if(maxPages<1)throw new ArgumentOutOfRangeException(nameof(maxPages));
        var pages=new List<JsonElement>();var seen=new HashSet<string>();var next="";
        for(int n=0;n<maxPages;n++)
        {
            cancellation.ThrowIfCancellationRequested();using var request=await Request(api,path,body,next);using var response=await Send(request,cancellation);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("키움 조회 연결 실패. 이전 자료는 최신이 아닐 수 있습니다.");
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));Check(doc.RootElement);pages.Add(doc.RootElement.Clone());
            if(stopAfterPage?.Invoke(doc.RootElement)==true)return pages;
            var more=response.Headers.TryGetValues("cont-yn",out var flags)&&flags.First().Trim()=="Y";if(!more)return pages;
            if(acceptPartialAtLimit&&pages.Count>=maxPages)return pages;
            next=response.Headers.TryGetValues("next-key",out var keys)?keys.First().Trim():"";if(next==""||!seen.Add(next))throw new InvalidOperationException("키움 연속조회 키 누락/반복. 조회 완료로 처리하지 않습니다.");
        }
        throw new InvalidOperationException("키움 조회 한도 초과. 기간을 줄이세요.");
    }

    /// <summary>Official ka10080 domestic one-minute candles. Wider minute bars are aggregated by the chart layer.</summary>
    public Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation)=>DomesticChartHistory("ka10080","stk_min_pole_chart_qry",symbol,false,cancellation);
    /// <summary>Continues ka10080 pages only until the requested screening start date is covered.</summary>
    public Task<PriceBar[]> MinuteHistory(string symbol,DateOnly from,DateOnly to,CancellationToken cancellation)
    {
        if(from>to)throw new ArgumentOutOfRangeException(nameof(from));
        if(to>KoreaClock.Today)throw new ArgumentOutOfRangeException(nameof(to));
        return DomesticChartHistory("ka10080","stk_min_pole_chart_qry",symbol,false,cancellation,30,to,new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)));
    }
    private static readonly int[] NativeMinuteIntervals=[1,3,5,10,15,30,45,60];
    public bool SupportsMinuteInterval(int minutes)=>NativeMinuteIntervals.Contains(minutes);
    /// <summary>Uses ka10080's native interval. A 1-hour screen therefore reads 60-minute rows instead of sixty 1-minute rows.</summary>
    public Task<PriceBar[]> MinuteHistory(string symbol,DateOnly from,DateOnly to,int minutes,CancellationToken cancellation)
    {
        if(from>to)throw new ArgumentOutOfRangeException(nameof(from));
        if(to>KoreaClock.Today)throw new ArgumentOutOfRangeException(nameof(to));
        if(!SupportsMinuteInterval(minutes))throw new ArgumentOutOfRangeException(nameof(minutes),"키움이 지원하는 분봉 간격이 아닙니다.");
        return DomesticChartHistory("ka10080","stk_min_pole_chart_qry",symbol,false,cancellation,30,to,new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)),minutes);
    }
    /// <summary>Official ka10079 domestic one-tick history. Every returned execution remains an individual chart point.</summary>
    public Task<PriceBar[]> TickHistory(string symbol,CancellationToken cancellation)=>DomesticChartHistory("ka10079","stk_tic_chart_qry",symbol,true,cancellation);
    private async Task<PriceBar[]> DomesticChartHistory(string api,string list,string symbol,bool tick,CancellationToken cancellation,int maxPages=3,DateOnly? baseDate=null,DateTimeOffset? oldestNeeded=null,int minuteScope=1)
    {
        if(Us)throw new InvalidOperationException("키움 틱·분봉 과거 조회는 현재 국내주식만 지원합니다.");
        new Instrument(symbol,Exchange.KRX).Validate();
        object body=tick?new{stk_cd=symbol,tic_scope="1",upd_stkpc_tp="1"}:new{stk_cd=symbol,tic_scope=minuteScope.ToString(CultureInfo.InvariantCulture),upd_stkpc_tp="1",base_dt=(baseDate??KoreaClock.Today).ToString("yyyyMMdd")};
        // A chart only needs a bounded recent window. Following every continuation can take over a minute
        // on an active symbol and eventually discard otherwise valid pages at the generic 100-page guard.
        bool ReachedStart(JsonElement page)
        {
            if(oldestNeeded==null)return false;
            foreach(var row in Rows([page],list))
                if(DateTime.TryParseExact(Text(row,"cntr_tm"),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)&&DateOnly.FromDateTime(parsed)<=DateOnly.FromDateTime(oldestNeeded.Value.DateTime))return true;
            return false;
        }
        var pages=await Query(api,"/api/dostk/chart",body,cancellation,maxPages:maxPages,acceptPartialAtLimit:true,stopAfterPage:oldestNeeded==null?null:ReachedStart);var rows=new List<(PriceBar Bar,long Order)>();long order=0;var malformed=0;
        foreach(var row in Rows(pages,list))
        {
            cancellation.ThrowIfCancellationRequested();
            if(!DateTime.TryParseExact(Text(row,"cntr_tm"),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))throw new InvalidOperationException("키움 틱·분봉 체결시각 형식 오류");
            var close=Math.Abs(Num(row,"cur_prc"));var open=Math.Abs(Num(row,"open_pric"));var high=Math.Abs(Num(row,"high_pric"));var low=Math.Abs(Num(row,"low_pric"));var volume=Math.Abs(Num(row,"trde_qty"));
            // A raw execution is one price point. Some live chart pages also contain a provisional
            // minute row whose OHLC fields are zero or internally inconsistent while the bar forms.
            // Do not invent prices and do not discard all valid pages because of that single row.
            var bar=tick?new PriceBar(new DateTimeOffset(time,TimeSpan.FromHours(9)),close,close,close,close,volume):new PriceBar(new DateTimeOffset(time,TimeSpan.FromHours(9)),open,high,low,close,volume);
            try{bar.Validate();rows.Add((bar,order++));}catch(InvalidOperationException){malformed++;}
        }
        if(rows.Count==0&&malformed>0)throw new InvalidOperationException("키움 틱·분봉 응답에 완성된 가격 봉이 없습니다. 장중 임시 봉을 제외한 뒤 다시 조회하세요.");
        // Kiwoom returns newest first. Reverse equal-timestamp ticks so the source sequence stays causal on the chart.
        var ordered=rows.OrderBy(x=>x.Bar.Time).ThenByDescending(x=>x.Order).Select(x=>x.Bar);
        return tick?ordered.ToArray():ordered.GroupBy(x=>x.Time).Select(g=>g.Last()).ToArray();
    }
    public async Task<OrderReceipt> SubmitAsync(OrderRequest order)
    {
        order.Validate();if(order.Market!=Exchange)throw new InvalidOperationException("선택 시장과 연결 시장이 다릅니다.");
        var body=new Dictionary<string,string>{{"stk_cd",order.Symbol},{"ord_qty",order.Quantity.ToString(CultureInfo.InvariantCulture)},{"ord_uv",order.Kind==OrderKind.Market?"":Serialization.Number(order.Price)},{"trde_tp",order.Kind==OrderKind.Market?(Us?"03":"3"):(Us?"00":"0")}};
        body[Us?"stex_tp":"dmst_stex_tp"]=Code;if(!Us)body["cond_uv"]="";else if(!order.Buy)body["stop_pric"]="";
        using var request=await Request(Us?(order.Buy?"ust20000":"ust20001"):(order.Buy?"kt10000":"kt10001"),Us?"/api/us/ordr":"/api/dostk/ordr",body);
        return await SendOrder(request);
    }
    private async Task<OrderReceipt> SendOrder(HttpRequestMessage request)
    {
        try{using var response=await Send(request);if(!response.IsSuccessStatusCode)throw new UnknownOrderException();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var code=Text(doc.RootElement,"return_code");if(code!="0"){if(int.TryParse(code,out var n))throw new BrokerRejectedException($"키움 주문 거부 (코드 {n}). 주문 조건·시간·잔고를 확인하세요.");throw new UnknownOrderException();}var number=Text(doc.RootElement,"ord_no");if(number=="")throw new UnknownOrderException();return new(number);}catch(BrokerRejectedException){throw;}catch{throw new UnknownOrderException();}
    }
    public async Task<OrderReceipt> CancelAsync(string number,string symbol)
    {
        new Instrument(symbol,Exchange).Validate();if(!System.Text.RegularExpressions.Regex.IsMatch(number,"^[0-9]+$"))throw new InvalidOperationException("주문번호 형식 오류");
        var body=new Dictionary<string,string>{{"orig_ord_no",number},{"stk_cd",symbol},{Us?"stex_tp":"dmst_stex_tp",Code}};if(!Us)body["cncl_qty"]="0";
        using var request=await Request(Us?"ust20003":"kt10003",Us?"/api/us/ordr":"/api/dostk/ordr",body);return await SendOrder(request); // Receipt != cancellation confirmation.
    }
    public async Task<Balance> GetBalanceAsync()
    {
        if(!Us){var pages=await Query("kt00018","/api/dostk/acnt",new{qry_tp="2",dmst_stex_tp="KRX"});var cash=await Query("kt00001","/api/dostk/acnt",new{qry_tp="2"});var holdings=Rows(pages,"acnt_evlt_remn_indv_tot").Select(r=>new Holding(StockCode(Text(r,"stk_cd")),Text(r,"stk_nm"),Num(r,"rmnd_qty"),Math.Abs(Num(r,"pur_pric")),Math.Abs(Num(r,"cur_prc")),Num(r,"evltv_prft"))).ToList();return new(holdings,Num(cash[0],"entr"),Num(cash[0],"entr")+Num(pages[0],"tot_evlt_amt"),OrderableCash:KiwoomTickParser.Number(cash[0],"100stk_ord_alow_amt") is {} available?Math.Max(0,Math.Min(available,Num(cash[0],"entr"))):null);}
        else {var pages=await Query("ust21070","/api/us/acnt",new{stex_tp=Code,stk_cd=""});var cash=await Query("ust21120","/api/us/acnt",new{cmsn_incl_tp="1",exrt_tp="0"});var cashRows=Rows(cash,"result_list").Where(r=>Text(r,"crnc_code")=="USD").ToList();if(cashRows.Count!=1)throw new InvalidOperationException("USD 예수금 행을 명확히 확인하지 못했습니다.");var holdings=Rows(pages,"result_list").Select(r=>{if(Text(r,"crnc_code")!="USD")throw new InvalidOperationException("미국 잔고의 통화가 USD가 아닙니다.");return new Holding(Text(r,"stk_cd"),Text(r,"frgn_stk_nm"),Num(r,"poss_qty"),Num(r,"frgn_stk_book_uv"),Num(r,"now_pric"),Num(r,"pl_amt"),"USD",Exchange.ToString());}).ToList();var dollars=Num(cashRows[0],"fx_entr");return new(holdings,dollars,dollars+holdings.Sum(h=>h.Quantity*h.CurrentPrice),"USD");}
    }
    public async Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)
    {
        if(date>KoreaClock.Today||date<KoreaClock.Today.AddMonths(-3))throw new InvalidOperationException("조회일은 최근 3개월 이내입니다.");var d=date.ToString("yyyyMMdd");
        var pages=await Query(Us?"ust21150":"kt00007",Us?"/api/us/acnt":"/api/dostk/acnt",Us?(object)new{ord_dt=d,query_tp="1",slby_tp="0",stex_tp=Code,stk_cd="",oppo_trde_tp="%",fr_ord_no=""}:new{ord_dt=d,qry_tp="1",stk_bond_tp="1",sell_tp="0",stk_cd="",fr_ord_no="",dmst_stex_tp="KRX"});
        return Rows(pages,Us?"result_list":"acnt_ord_cntr_prps_dtl").Select(r=>{var raw=Text(r,Us?"ord_stat_nm":"acpt_tp");var cancel=Us?Num(r,"cncl_qty")>0:Text(r,"mdfy_cncl").Contains("취소")&&Num(r,"cnfm_qty")>0;return new BrokerOrder(d,Text(r,"ord_no"),StockCode(Text(r,"stk_cd")),Text(r,Us?"frgn_stk_nm":"stk_nm"),Text(r,Us?"slby_tp_nm":"io_tp_nm").Contains("매수")?"매수":"매도",Num(r,"ord_qty"),Num(r,"ord_uv"),Num(r,"cntr_qty"),Num(r,"ord_remnq"),cancel,raw.Contains("거부")||raw.Contains("거절"),Currency,Exchange.ToString(),raw,Text(r,Us?"orig_ord_no":"ori_ord"));}).ToList();
    }
    /// <summary>Official kt00009 individual executions, keyed by trading date/order/execution number.</summary>
    public async Task<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(DateOnly date)
    {
        if(Us)throw new InvalidOperationException("자동매매 체결 대사는 현재 키움 국내주식만 지원합니다.");
        var pages=await Query("kt00009","/api/dostk/acnt",new{ord_dt=date.ToString("yyyyMMdd"),stk_bond_tp="1",mrkt_tp="0",sell_tp="0",qry_tp="1",stk_cd="",fr_ord_no="",dmst_stex_tp="KRX"});
        var result=new Dictionary<string,BrokerExecution>();
        foreach(var r in Rows(pages,"acnt_ord_cntr_prst_array"))
        {
            var quantity=Num(r,"cntr_qty");if(quantity==0)continue;
            var execution=Text(r,"cntr_no");var number=Text(r,"ord_no");var side=Text(r,"io_tp_nm");
            if(quantity<0||quantity!=decimal.Truncate(quantity)||quantity>1000000||execution==""||execution.All(c=>c=='0')||number==""||(!side.Contains("매수")&&!side.Contains("매도")))throw new InvalidOperationException("원본 체결 식별자/수량/방향을 확인할 수 없습니다.");
            if(!TimeOnly.TryParseExact(Text(r,"cntr_tm"),"HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))throw new InvalidOperationException("실제 체결시각을 확인할 수 없습니다.");
            var price=Num(r,"cntr_uv");if(price<=0)throw new InvalidOperationException("실제 체결가격이 없습니다.");
            var id=date.ToString("yyyyMMdd")+":"+number+":"+execution;var row=new BrokerExecution(id,number,StockCode(Text(r,"stk_cd")),side.Contains("매수"),(int)quantity,price,new DateTimeOffset(date.ToDateTime(time),TimeSpan.FromHours(9)));
            if(result.TryGetValue(id,out var existing)&&existing!=row)throw new InvalidOperationException("동일 체결번호의 내용이 달라 확인이 필요합니다.");result[id]=row;
        }
        return result.Values.OrderBy(x=>x.Time).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray();
    }
    private static IEnumerable<JsonElement> Rows(IEnumerable<JsonElement> pages,string key)
    {foreach(var p in pages){if(!p.TryGetProperty(key,out var rows)){if(key!="result_list"||!p.TryGetProperty("result_lsit",out rows))throw new InvalidOperationException("키움 목록 응답 누락");}if(rows.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("키움 목록 응답 형식 오류");foreach(var r in rows.EnumerateArray())yield return r;}}
    private static string StockCode(string code)=>code.Length==7&&code[0]=='A'&&char.IsDigit(code[1])?code[1..]:code;
    private static string Text(JsonElement root,string field)=>KiwoomTickParser.Text(root,field);
    private static decimal Num(JsonElement root,string field)=>KiwoomTickParser.Number(root,field)??throw new InvalidOperationException("키움 숫자 응답 누락/형식 오류: "+field);
    public void Dispose(){token=null;http.Dispose();gate.Dispose();authGate.Dispose();}
}
