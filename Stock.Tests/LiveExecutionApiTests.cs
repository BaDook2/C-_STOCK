using System.Text.Json;
using Stock.Core;
using Stock.Core.Trading;
internal static partial class Program
{
    private static async Task LiveExecutionApiTests()
    {
        bool malformed=false;int executionCalls=0;
        using var broker=new KiwoomBroker(new(){AppKey="fixture",SecretKey="fixture"},Exchange.KRX,new Handler(async r=>
        {
            var api=r.Headers.GetValues("api-id").Single();
            if(api=="au10001")return Json("""{"return_code":0,"token":"fixture","expires_dt":"20990101000000"}""");
            if(api=="kt00018")return Json("""{"return_code":0,"tot_evlt_amt":"0","acnt_evlt_remn_indv_tot":[]}""");
            if(api=="kt00001")return Json("""{"return_code":0,"entr":"1000000","100stk_ord_alow_amt":"750000"}""");
            Check(api=="kt00009"&&r.RequestUri!.AbsolutePath=="/api/dostk/acnt","individual executions use official domestic account endpoint");executionCalls++;
            using var doc=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());var body=doc.RootElement;Check(body.GetProperty("qry_tp").GetString()=="1"&&body.GetProperty("ord_dt").GetString()=="20260930"&&body.GetProperty("dmst_stex_tp").GetString()=="KRX","execution query pins fill-only mode date and exchange");
            var fill="""{"ord_no":"0001","stk_cd":"A005930","cntr_no":"0009","cntr_qty":"4","cntr_uv":"70100","cntr_tm":"10:12:13","io_tp_nm":"현금매수"}""";
            if(malformed)fill=fill.Replace("70100","0");
            return Json("{\"return_code\":0,\"acnt_ord_cntr_prst_array\":["+fill+","+fill+"]}");
        }),live:true);
        var cash=await broker.GetBalanceAsync();Check(cash.OrderableCash==750000&&cash.Cash==1000000,"100 percent cash buying power remains distinct from deposit balance");
        var fills=await broker.GetExecutionsAsync(new(2026,9,30));Check(fills.Count==1&&fills[0].Price==70100&&fills[0].Quantity==4&&fills[0].Symbol=="005930"&&fills[0].Time.Offset==TimeSpan.FromHours(9),"actual execution ids deduplicate with exact price quantity and Korea timestamp");
        malformed=true;await ThrowsAsync<InvalidOperationException>(()=>broker.GetExecutionsAsync(new(2026,9,30)),"missing real fill price fails closed instead of using order price");Check(executionCalls==2,"read parser checks make no order requests");
    }
}
