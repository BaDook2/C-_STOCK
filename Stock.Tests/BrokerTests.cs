using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static HttpResponseMessage Json(string json,string? next=null){var r=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};if(next!=null){r.Headers.Add("cont-yn","Y");r.Headers.Add("next-key",next);}return r;}
    private static KiwoomBroker Client(Exchange market,Func<HttpRequestMessage,Task<HttpResponseMessage>> send)=>new(new(){AppKey="fake-key",SecretKey="fake-secret"},market,new Handler(async r=>
    {
        Check(r.RequestUri!.Host=="mockapi.kiwoom.com"&&r.Method==HttpMethod.Post,"mock host POST only");
        if(r.RequestUri.AbsolutePath=="/oauth2/token"){using var body=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());Check(body.RootElement.GetProperty("secretkey").GetString()=="fake-secret","Kiwoom token secretkey schema");return Json("{\"return_code\":0,\"token\":\"fake-token\",\"expires_dt\":\"20990101000000\"}");}
        Check(r.Headers.Authorization?.ToString()=="Bearer fake-token","bearer token without raw secrets");
        if(r.Headers.GetValues("api-id").Single()=="ka00001")return Json("{\"return_code\":0,\"acctNo\":\"1234567890\"}");return await send(r);
    }));
    private static async Task BrokerTests()
    {
        foreach(var market in new[]{Exchange.KRX,Exchange.NASDAQ,Exchange.NYSE,Exchange.AMEX})
        {
            var seen=0;using var client=Client(market,async request=>
            {seen++;var us=market!=Exchange.KRX;var id=request.Headers.GetValues("api-id").Single();Check(id==(seen==1?(us?"ust20000":"kt10000"):seen==2?(us?"ust20001":"kt10001"):(us?"ust20003":"kt10003")),"buy/sell/cancel API ID "+market);
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());if(seen<3){Check(body.RootElement.GetProperty("ord_qty").ValueKind==JsonValueKind.String,"quantity is string");Check(body.RootElement.GetProperty("trde_tp").GetString()==(us?"00":"0"),"limit order code");if(us)Check(body.RootElement.GetProperty("stex_tp").GetString()==new Instrument("AAPL",market).Code,"US exchange code");}return Json("{\"return_code\":0,\"ord_no\":\"0001234\"}");});
            await client.ConnectAsync();Check(client.MaskedAccount.EndsWith("7890"),"account read and masked");var order=new OrderRequest(market==Exchange.KRX?"005930":"AAPL",1,market==Exchange.KRX?70000:2,true,market);await client.SubmitAsync(order);await client.SubmitAsync(order with{Buy=false});await client.CancelAsync("0001234",order.Symbol);
        }
        foreach(var bad in new[]{"{}","invalid","{\"return_code\":0}"}){using var c=Client(Exchange.NASDAQ,_=>Task.FromResult(Json(bad)));await c.ConnectAsync();await ThrowsAsync<UnknownOrderException>(()=>c.SubmitAsync(new("AAPL",1,2,true,Exchange.NASDAQ)),"malformed order remains unknown");}
        using(var c=Client(Exchange.NASDAQ,_=>throw new TaskCanceledException())){await c.ConnectAsync();await ThrowsAsync<UnknownOrderException>(()=>c.SubmitAsync(new("AAPL",1,2,true,Exchange.NASDAQ)),"timeout never retries POST");}
        using(var c=Client(Exchange.NASDAQ,_=>Task.FromResult(Json("{\"return_code\":10}")))){await c.ConnectAsync();await ThrowsAsync<BrokerRejectedException>(()=>c.SubmitAsync(new("AAPL",1,2,true,Exchange.NASDAQ)),"explicit Kiwoom rejection");}
        var pages=0;using(var c=Client(Exchange.NASDAQ,r=>{pages++;if(pages==2)Check(r.Headers.GetValues("next-key").Single()=="next"&&r.Headers.GetValues("cont-yn").Single()=="Y","header based continuation");return Task.FromResult(Json("{\"return_code\":0,\"result_list\":[{\"ord_no\":\"0001\",\"stk_cd\":\"AAPL\",\"frgn_stk_nm\":\"Apple\",\"slby_tp_nm\":\"매수\",\"ord_qty\":\"10\",\"ord_uv\":\"2.5\",\"cntr_qty\":\"3\",\"ord_remnq\":\"7\",\"cncl_qty\":\"0\",\"ord_stat_nm\":\"부분체결\"}]}",pages==1?"next":null));}))
        {await c.ConnectAsync();var orders=await c.GetOrdersAsync(KoreaClock.Today);Check(orders.Count==2&&orders[0].Currency=="USD"&&orders[0].Status=="부분 체결","US pages and partial fills");}
        using(var c=Client(Exchange.KRX,_=>Task.FromResult(Json("{\"return_code\":0,\"acnt_ord_cntr_prps_dtl\":[]}","same")))){await c.ConnectAsync();await ThrowsAsync<InvalidOperationException>(()=>c.GetOrdersAsync(KoreaClock.Today),"repeated pagination fails closed");}
        var chartCalls=0;
        using(var c=Client(Exchange.KRX,async request=>
        {
            chartCalls++;var id=request.Headers.GetValues("api-id").Single();using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Check(request.RequestUri!.AbsolutePath=="/api/dostk/chart"&&body.RootElement.GetProperty("tic_scope").GetString()=="1"&&body.RootElement.GetProperty("upd_stkpc_tp").GetString()=="1","Kiwoom intraday history uses official chart request");
            if(id=="ka10080")return Json("""{"return_code":0,"stk_cd":"005930","stk_min_pole_chart_qry":[{"cur_prc":"-102","trde_qty":"20","cntr_tm":"20260928100100","open_pric":"-100","high_pric":"-103","low_pric":"-99"},{"cur_prc":"-100","trde_qty":"10","cntr_tm":"20260928100000","open_pric":"-99","high_pric":"-101","low_pric":"-98"}]}""");
            if(id=="ka10079")return Json("""{"return_code":0,"stk_cd":"005930","stk_tic_chart_qry":[{"cur_prc":"103","trde_qty":"3","cntr_tm":"20260928100101","open_pric":"103","high_pric":"103","low_pric":"103"},{"cur_prc":"102","trde_qty":"2","cntr_tm":"20260928100101","open_pric":"102","high_pric":"102","low_pric":"102"},{"cur_prc":"101","trde_qty":"1","cntr_tm":"20260928100100","open_pric":"101","high_pric":"101","low_pric":"101"}]}""");
            throw new InvalidOperationException("unexpected chart api "+id);
        }))
        {
            await c.ConnectAsync();var minutes=await c.MinuteHistory("005930",CancellationToken.None);var ticks=await c.TickHistory("005930",CancellationToken.None);
            Check(chartCalls==2&&minutes.Length==2&&minutes[0].Close==100&&minutes[^1].Close==102,"Kiwoom ka10080 signed minute bars are chronological");
            Check(ticks.Length==3&&ticks.Select(x=>x.Close).SequenceEqual(new decimal[]{101,102,103}),"Kiwoom ka10079 keeps equal-time raw ticks in causal order");
        }
        var minutePages=0;
        using(var c=Client(Exchange.KRX,request=>
        {
            Check(request.Headers.GetValues("api-id").Single()=="ka10080","bounded chart pagination uses minute API");minutePages++;
            var second=minutePages.ToString("00");
            return Task.FromResult(Json($$"""{"return_code":0,"stk_min_pole_chart_qry":[{"cur_prc":"100","trde_qty":"1","cntr_tm":"2026092810{{second}}00","open_pric":"100","high_pric":"100","low_pric":"100"}]}""","minute-next-"+minutePages));
        }))
        {
            await c.ConnectAsync();var minutes=await c.MinuteHistory("005930",CancellationToken.None);
            Check(minutePages==3&&minutes.Length==3,"Kiwoom minute chart returns three recent pages without waiting for the 100-page account guard");
        }
        using(var c=Client(Exchange.KRX,request=>Task.FromResult(Json("""{"return_code":0,"stk_min_pole_chart_qry":[{"cur_prc":"103","trde_qty":"1","cntr_tm":"20260928100200","open_pric":"0","high_pric":"0","low_pric":"0"},{"cur_prc":"102","trde_qty":"2","cntr_tm":"20260928100100","open_pric":"100","high_pric":"103","low_pric":"99"}]}"""))))
        {
            await c.ConnectAsync();var minutes=await c.MinuteHistory("005930",CancellationToken.None);
            Check(minutes.Length==1&&minutes[0].Close==102,"Kiwoom provisional malformed minute row does not hide other valid chart rows");
        }
        var rangedPages=0;
        using(var c=Client(Exchange.KRX,async request=>
        {
            rangedPages++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());Check(body.RootElement.GetProperty("base_dt").GetString()=="20261020","ranged Kiwoom minute history anchors the query at the screening end date");
            var day=rangedPages switch{1=>"20261020",2=>"20261010",_=>"20261001"};
            return Json($$"""{"return_code":0,"stk_min_pole_chart_qry":[{"cur_prc":"100","trde_qty":"1","cntr_tm":"{{day}}090000","open_pric":"100","high_pric":"100","low_pric":"100"}]}""",rangedPages<3?"range-next-"+rangedPages:null);
        }))
        {
            await c.ConnectAsync();var minutes=await ((IRangedMinuteHistoryBroker)c).MinuteHistory("005930",new(2026,10,1),new(2026,10,20),CancellationToken.None);
            Check(rangedPages==3&&minutes.Length==3&&DateOnly.FromDateTime(minutes[0].Time.DateTime)==new DateOnly(2026,10,1),"screening minute history follows continuation pages through the requested start date and stops there");
        }
        var hourlyPages=0;
        using(var c=Client(Exchange.KRX,async request=>
        {
            hourlyPages++;using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Check(body.RootElement.GetProperty("tic_scope").GetString()=="60"&&body.RootElement.GetProperty("base_dt").GetString()=="20261020","one-hour screening requests Kiwoom native 60-minute rows");
            return Json("""{"return_code":0,"stk_min_pole_chart_qry":[{"cur_prc":"102","trde_qty":"200","cntr_tm":"20261020090000","open_pric":"100","high_pric":"103","low_pric":"99"}]}""");
        }))
        {
            await c.ConnectAsync();var interval=(IRangedMinuteIntervalHistoryBroker)c;Check(interval.SupportsMinuteInterval(60)&&!interval.SupportsMinuteInterval(7),"Kiwoom exposes only documented native minute intervals");var hours=await interval.MinuteHistory("005930",new(2026,10,1),new(2026,10,20),60,CancellationToken.None);
            Check(hourlyPages==1&&hours.Length==1&&hours[0].Open==100&&hours[0].Close==102,"native hourly screening avoids downloading every one-minute row");
        }
        var path=Path.Combine(Dir,"orders.json");var guard=new OrderGuard(path);var fake=new FakeBroker();var buy=new OrderRequest("005930",1,70000,true);await guard.SubmitAsync(fake,buy);await ThrowsAsync<InvalidOperationException>(()=>new OrderGuard(path).SubmitAsync(fake,buy),"durable duplicate guard after restart");Check(fake.Submitted==1,"exactly one transmission");
        path=Path.Combine(Dir,"unknown.json");guard=new(path);fake=new(){Submit=_=>throw new UnknownOrderException()};await ThrowsAsync<UnknownOrderException>(()=>guard.SubmitAsync(fake,buy),"unknown persisted");guard=new(path);await ThrowsAsync<InvalidOperationException>(()=>guard.SubmitAsync(fake,buy with{Quantity=2}),"unknown blocks different order after restart");guard.AcknowledgeExternalReview();await ThrowsAsync<InvalidOperationException>(()=>guard.SubmitAsync(fake,buy),"external review never automatically resends");
        path=Path.Combine(Dir,"concurrent.json");guard=new(path);var wait=new TaskCompletionSource<OrderReceipt>();fake=new(){Submit=_=>wait.Task};var first=guard.SubmitAsync(fake,buy);await ThrowsAsync<InvalidOperationException>(()=>guard.SubmitAsync(fake,buy),"concurrent click blocked");Check(new OrderGuard(path).HasUnresolved,"write-ahead survives interrupted transmission");wait.SetResult(new("42"));await first;
        path=Path.Combine(Dir,"corrupt.json");File.WriteAllText(path,"broken");Throws<JsonException>(()=>new OrderGuard(path),"corrupt journal blocks connection");
        int sockets=0;var received=false;var statuses=new List<string>();var ds=new DataSettings{Universe="NASDAQ:AAPL",ReconnectSeconds=1};
        await using(var stream=new KiwoomMarketDataProvider(()=>Task.FromResult("fake"),[Us],ds,statuses.Add,()=>new ScriptSocket(Interlocked.Increment(ref sockets)==1)))
        {using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(5));await foreach(var e in stream.Stream(stop.Token)){received=e.Instrument==Us;stop.Cancel();break;}}
        Check(sockets>=2&&received&&statuses.Any(s=>s.Contains("단절")),"WebSocket reconnect authenticates and resubscribes");
    }
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t)=>send(r);}
    private sealed class FakeBroker:IBroker
    {public int Submitted;public Func<OrderRequest,Task<OrderReceipt>> Submit=_=>Task.FromResult(new OrderReceipt("42"));public Task ConnectAsync()=>Task.CompletedTask;public Task<OrderReceipt> SubmitAsync(OrderRequest o){Submitted++;return Submit(o);}public Task<Balance> GetBalanceAsync()=>Task.FromResult(new Balance([],0,0));public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly d)=>Task.FromResult<IReadOnlyList<BrokerOrder>>([]);}
    private sealed class ScriptSocket(bool fail):IWireSocket
    {
        private int reads;
        public Task Connect(Uri uri,CancellationToken token){if(fail)throw new IOException("fake disconnect");return Task.CompletedTask;}
        public Task Send(string data,CancellationToken token)=>Task.CompletedTask;
        public async Task<string> Receive(CancellationToken token)
        {reads++;if(reads==1)return "{\"trnm\":\"LOGIN\",\"return_code\":0}";if(reads==2)return "{\"trnm\":\"REAL\",\"data\":[{\"type\":\"FE\",\"item\":\"AAPL\",\"stexTp\":\"ND\",\"values\":{\"20\":\"223000\",\"10\":\"2\",\"15\":\"10\",\"13\":\"100\"}}]}";await Task.Delay(Timeout.Infinite,token);return "";}
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
