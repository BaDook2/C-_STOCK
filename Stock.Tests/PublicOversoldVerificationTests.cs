using System.Text.Json;
using System.IO;
using System.Diagnostics;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static async Task PublicOversoldVerification(string output)
    {
        Directory.CreateDirectory(output);
        using var provider=new PublicMarketData();
        var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930","반도체");
        var data=await provider.History(stock,CancellationToken.None);
        var bars=data.CompletedBars;
        if(bars.Length<30)throw new InvalidOperationException("삼성전자 완성 일봉이 30개 미만입니다.");
        var from=DailyDates.Of(bars[0]);var to=DailyDates.Of(bars[^1]);
        var rows=new List<object>();
        foreach(var preset in StrategyDiagnosticPreset.BacktestVerificationPresets())
        {
            var result=new OversoldBacktest().Run(data,preset.Value,from,to,new());
            var expected=preset.Key==StrategyDiagnosticPreset.HoldOneDayName?"MaxHoldingDays":preset.Key==StrategyDiagnosticPreset.QuickProfitName?"TP1":"SupportBreak";
            var actual=result.Trades.FirstOrDefault(t=>t.ExitReason==expected);
            Check(result.Decisions.Count>0&&result.Transitions.Count>0,preset.Key+" records daily decisions and state transitions on Samsung public daily bars");
            Check(result.Performance.Trades>0&&actual!=null,preset.Key+" produces expected "+expected+" closed result on Samsung public daily bars");
            rows.Add(new{preset=preset.Key,expected,from,to,bars=bars.Length,decisions=result.Decisions.Count,transitions=result.Transitions.Count,signals=result.Signals.Count,closedTrades=result.Performance.Trades,finalEquity=result.Performance.FinalEquity,totalProfit=result.Performance.FinalEquity-preset.Value.InitialCapital,exitReasons=result.Trades.Select(t=>t.ExitReason).ToArray(),firstEntry=actual!.FirstExecutionDate,lastExit=actual.LastExecutionDate,source=data.Source});
        }
        var report=new{verifiedAt=DateTimeOffset.UtcNow,stock=stock.Name,symbol=stock.Symbol,source=data.Source,rows};
        File.WriteAllText(Path.Combine(output,"samsung-oversold-verification.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {passed} Samsung public-daily oversold verification checks");
    }

    private static void PublicScreeningPerformance(string output)
    {
        using var store=new TradingStore(Path.Combine(output,"public-screening-performance.db"));
        using var provider=new PublicMarketData();
        using var vm=new WorkspaceViewModel(_=>false,provider:provider,store:store);
        vm.Region="국내";vm.Sector="전체";vm.ScreenMarket=true;vm.ScreeningSize=10;
        vm.ScreenFrom=KoreaClock.Today.ToDateTime(TimeOnly.MinValue).AddDays(-7);
        vm.ScreenTo=KoreaClock.Today.ToDateTime(TimeOnly.MinValue);
        var watch=Stopwatch.StartNew();vm.Actions["스크리닝"].Execute(null);PumpUntil(()=>!vm.Busy,180);watch.Stop();
        var run=vm.LatestScreening??throw new InvalidOperationException("공개 데이터 일주일 스크리닝 결과가 생성되지 않았습니다. "+vm.Status);
        var report=new
        {
            verifiedAt=DateTimeOffset.UtcNow,
            from=vm.ScreenFrom,
            to=vm.ScreenTo,
            requested=vm.ScreeningSize,
            evaluated=run.Results.Length,
            candidates=run.FinalCandidates.Length,
            elapsedMilliseconds=watch.ElapsedMilliseconds,
            secondsPerSymbol=run.Results.Length==0?0:watch.Elapsed.TotalSeconds/run.Results.Length,
            status=vm.ScreenInfo,
            source="NAVER_PUBLIC_READ_ONLY"
        };
        File.WriteAllText(Path.Combine(output,"public-screening-performance.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Check(run.Results.Length>0&&run.Status=="완료","public one-week screening completes and records every evaluated row");
        Console.WriteLine($"PERFORMANCE: {run.Results.Length:N0} symbols in {watch.Elapsed.TotalSeconds:N2}s ({report.secondsPerSymbol:N2}s/symbol)");
    }
}
