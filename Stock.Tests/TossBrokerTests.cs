using System.Net;
using System.Net.Http;
using System.Text.Json;
using Stock.Core;
using Stock.Core.Trading;
internal static partial class Program
{
    private static async Task TossBrokerTests()
    {
        var posts=0;var pages=0;
        using var broker=new TossBroker(Connection(Brokerage.Toss),new Handler(async r=>
        {
            Check(r.RequestUri!.Host=="openapi.tossinvest.com","Toss uses fixed official live host");
            var path=r.RequestUri.AbsolutePath;
            if(path=="/oauth2/token"){var body=await r.Content!.ReadAsStringAsync();Check(body.Contains("grant_type=client_credentials")&&body.Contains("client_secret="),"Toss official OAuth form schema");return Json("""{"access_token":"fixture","expires_in":86400}""");}
            if(path=="/api/v1/accounts")return Json("""{"result":[{"accountNo":"99999999","accountSeq":91,"accountType":"BROKERAGE"},{"accountNo":"12345678","accountSeq":42,"accountType":"BROKERAGE"}]}""");
            Check(r.Headers.GetValues("X-Tossinvest-Account").Single()=="42","Toss every account request targets selected account only");
            if(path=="/api/v1/buying-power")return Json("""{"result":{"currency":"KRW","cashBuyingPower":"1000000"}}""");
            if(path=="/api/v1/holdings")return Json("""{"result":{"items":[]}}""");
            if(r.Method==HttpMethod.Get&&path=="/api/v1/orders")
            {
                if(r.RequestUri.Query.Contains("CLOSED")){pages++;return Json(pages==1?"""{"result":{"orders":[],"nextCursor":"page 2","hasNext":true}}""":"""{"result":{"orders":[],"nextCursor":null,"hasNext":false}}""");}
                return Json("""{"result":{"orders":[{"orderId":"id-1","symbol":"005930","side":"BUY","status":"PARTIAL_FILLED","quantity":"5","price":null,"currency":"KRW","execution":{"filledQuantity":"2","averageFilledPrice":"70000","filledAt":"2026-09-28T10:00:00+09:00"}}],"hasNext":false,"nextCursor":null}}""");
            }
            if(r.Method==HttpMethod.Post&&path=="/api/v1/orders"){posts++;using var doc=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());Check(!doc.RootElement.TryGetProperty("price",out _)&&doc.RootElement.GetProperty("quantity").GetString()=="1"&&doc.RootElement.GetProperty("clientOrderId").GetString()!.Length==32,"Toss market request omits price and uses stable request identifier");return Json("""{"result":{"orderId":"new-id"}}""");}
            throw new Exception("Unexpected fixture request");
        }));
        await broker.ConnectAsync();Check(broker.Account=="12345678","Toss validates requested account rather than first account");Check((await broker.GetBalanceAsync()).Cash==1000000,"Toss available cash converted without currency mixing");
        var orders=await broker.GetOrdersAsync(new DateOnly(2026,9,28));Check(pages==2&&orders.Single().AverageFillPrice==70000&&orders.Single().LastFillTime!=null,"Toss pagination and actual fill metadata retained");await broker.SubmitAsync(new("005930",1,70000,true,Kind:OrderKind.Market));Check(posts==1,"Toss one confirmed submission uses one POST");
        using var ambiguous=new TossBroker(new(){Brokerage=Brokerage.Toss,AppKey="fixture",Secret="fixture"},new Handler(r=>Task.FromResult(Json(r.RequestUri!.AbsolutePath=="/oauth2/token"?"""{"access_token":"fixture","expires_in":86400}""":"""{"result":[{"accountNo":"1","accountSeq":1,"accountType":"BROKERAGE"},{"accountNo":"2","accountSeq":2,"accountType":"BROKERAGE"}]}"""))));
        await ThrowsAsync<InvalidOperationException>(ambiguous.ConnectAsync,"Toss never chooses an arbitrary account when multiple are connected");
        int lostPosts=0;using var lost=new TossBroker(Connection(Brokerage.Toss),new Handler(r=>
        {var path=r.RequestUri!.AbsolutePath;if(path=="/oauth2/token")return Task.FromResult(Json("""{"access_token":"fixture","expires_in":86400}"""));if(path=="/api/v1/accounts")return Task.FromResult(Json("""{"result":[{"accountNo":"12345678","accountSeq":42,"accountType":"BROKERAGE"}]}"""));lostPosts++;throw new TaskCanceledException();}));
        await ThrowsAsync<UnknownOrderException>(()=>lost.SubmitAsync(new("005930",1,70000,true)),"Toss lost order reply remains unknown");Check(lostPosts==1,"Toss unknown submission never automatically retries");
        var candlePages=0;
        using var history=new TossBroker(Connection(Brokerage.Toss),new Handler(r=>
        {
            var path=r.RequestUri!.AbsolutePath;
            if(path=="/oauth2/token")return Task.FromResult(Json("""{"access_token":"fixture","expires_in":86400}"""));
            if(path=="/api/v1/accounts")return Task.FromResult(Json("""{"result":[{"accountNo":"12345678","accountSeq":42,"accountType":"BROKERAGE"}]}"""));
            if(path!="/api/v1/candles")throw new Exception("Unexpected history request");
            candlePages++;Check(r.Method==HttpMethod.Get&&r.RequestUri.Query.Contains("interval=1m")&&!r.Headers.Contains("X-Tossinvest-Account"),"Toss minute history uses market data read endpoint");
            if(candlePages==2)Check(r.RequestUri.Query.Contains("%2B09"),"Toss minute pagination preserves timezone plus sign");
            return Task.FromResult(Json(candlePages==1?
                """{"result":{"candles":[{"timestamp":"2026-09-28T09:01:00+09:00","currency":"KRW","openPrice":"100","highPrice":"103","lowPrice":"99","closePrice":"102","volume":"20"}],"nextBefore":"2026-09-28T09:00:00+09:00"}}""":
                """{"result":{"candles":[{"timestamp":"2026-09-28T09:00:00+09:00","currency":"KRW","openPrice":"99","highPrice":"101","lowPrice":"98","closePrice":"100","volume":"10"}],"nextBefore":null}}"""));
        }));
        var candles=await history.MinuteHistory("005930",CancellationToken.None);
        Check(candlePages==2&&candles.Length==2&&candles[0].Close==100&&candles[^1].Close==102,"Toss minute pages are merged in chronological order");
    }
}
