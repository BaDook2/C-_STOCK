using System.Text.Json;
using System.Diagnostics;
using Stock.App;
using Stock.Core.Research.Oversold;
using Microsoft.Data.Sqlite;
using Stock.Core.Trading;

internal static partial class Program
{
    private static async Task RealtimeRepairTests()
    {
        var unavailable=ChartTradeHistory.Read(()=>throw new SqliteException("database disk image is malformed",11),out var failed);
        Check(failed&&unavailable.Count==0,"SQLite corruption error 11 is isolated from realtime chart loading without inventing fills");
        var known=ChartTradeHistory.Read(()=>new[]{("trade-marker:1","{}")},out failed);
        Check(!failed&&known.Count==1,"healthy chart history remains available");
        using(var db=new TradingStore(System.IO.Path.Combine(Dir,"prefix-index-check.db")))
        {
            db.State("trade-marker:a",new{Value=1});db.State("trade-marker-extra:a",new{Value=2});db.State("다른키",new{Value=3});
            Check(db.States("trade-marker:").Single().Key=="trade-marker:a","binary state prefix range excludes unrelated historical rows and locale keys");
        }
        var config=new OversoldConfig();Check(RealtimeWarmup.RequiredBars(config)>=999,"recent warmup preserves MA999 history requirements");
        Check(RealtimeWarmup.SourceInterval(60,n=>n is 1 or 5 or 60)==60&&RealtimeWarmup.SourceInterval(120,n=>n is 1 or 60)==60&&RealtimeWarmup.SourceInterval(7,n=>n is 1 or 5 or 60)==1,"native interval selection avoids one-minute downloads while exactly dividing the selected timeframe");
        Check(RealtimeWarmup.SourceBars(999,60,60)>=999*7/6&&RealtimeWarmup.SourceBars(999,120,60)>=999*7/3,"recent source row budget accounts for session-close remainders and multiple native bars per target bar");
        int pages=0;var timer=Stopwatch.StartNew();
        using var client=Client(Exchange.KRX,async request=>
        {
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Check(body.RootElement.GetProperty("tic_scope").GetString()=="60","realtime hourly request uses official sixty-minute bars");pages++;
            return Json("""{"return_code":0,"stk_min_pole_chart_qry":[{"cntr_tm":"20260928100000","cur_prc":"100","open_pric":"100","high_pric":"101","low_pric":"99","trde_qty":"10"},{"cntr_tm":"20260928090000","cur_prc":"100","open_pric":"100","high_pric":"101","low_pric":"99","trde_qty":"10"}]}""","unused-continuation");
        });
        await client.ConnectAsync();var bars=await client.RecentMinuteHistory("005930",60,2,CancellationToken.None);timer.Stop();
        Check(pages==1&&bars.Length==2,"recent native warmup stops after sufficient rows even when API advertises another page");
        Console.WriteLine($"RECENT_HISTORY pages={pages}, target=60min, elapsed={timer.ElapsedMilliseconds}ms (fixture; broker rate limit retained)");
    }
}
