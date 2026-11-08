using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class ForeignResearchHandler:HttpMessageHandler
    {
        public readonly List<string> Requests=[];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var url=request.RequestUri!.ToString();Requests.Add(url);string json;
            if(url.Contains("/chart/foreign/item/"))json="""{"priceInfos":[{"localDate":"20251103","openPrice":100,"highPrice":101,"lowPrice":98,"closePrice":99,"accumulatedTradingVolume":10000},{"localDate":"20251104","openPrice":99,"highPrice":100,"lowPrice":96,"closePrice":97,"accumulatedTradingVolume":20000}]}""";
            else if(url.Contains("/index/"))json="""[{"stockEndType":"stock","stockExchangeType":{"name":"NASDAQ"},"reutersCode":"AAPL.O","symbolCode":"AAPL","stockName":"애플"}]""";
            else json="""{"items":[{"nationCode":"USA","category":"stock","typeCode":"NASDAQ","code":"AAPL","name":"애플","reutersCode":"AAPL.O"}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});
        }
    }
    private static void ResearchExpansionTests(string output)
    {
        var c=DailyConfig();c.MinimumAverageTradingValue=0;c.MinimumSupportCount=1;c.MovingAveragePeriods="1";c.UseApproximateVolumeZone=c.UsePivotSupport=false;
        var prices=new[]{DailyBar(0,100),DailyBar(1,100),DailyBar(2,86,high:100)};var rows=prices.Select((b,i)=>new StrategyBar(b,b.Time,b.Time,i,4_000_000_000,"FIXTURE")).ToArray();
        var engine=new OversoldScreeningEngine();var exact=engine.EvaluateCurrent(DailyStock,rows,c,DailyFacts(3),DailyTime(2,16));
        Check(!exact.Candidate,"exact-only screening does not display a fourteen-percent crash against fifteen-percent trigger");
        c.ShowNearCandidates=true;c.MinimumCandidateProximity=80;var near=engine.EvaluateCurrent(DailyStock,rows,c,DailyFacts(3),DailyTime(2,16));
        Check(near.Candidate&&near.IsNearCandidate&&near.Proximity>=80&&!near.Evaluation!.Enter,"near candidate is displayed before entry and cannot authorize an order");
        c.MinimumCandidateProximity=100;Check(!engine.EvaluateCurrent(DailyStock,rows,c,DailyFacts(3),DailyTime(2,16)).Candidate,"candidate threshold changes the list independently of entry rules");
        c.MinimumCandidateProximity=80;var risk=DailyFacts(3);risk.RiskRecords=risk.RiskRecords.Select(r=>r with{IsRisk=true}).ToArray();Check(!engine.EvaluateCurrent(DailyStock,rows,c,risk,DailyTime(2,16)).Candidate,"confirmed fundamental risk excludes even a near candidate");
        var restored=Serialization.Unpack<OversoldConfig>(Serialization.Pack(c));Check(restored.ShowNearCandidates&&restored.MinimumCandidateProximity==80,"new screening settings survive JSON export/import");
        c.MinimumCandidateProximity=101;Throws<InvalidOperationException>(c.Validate,"candidate score validates zero through one hundred");c.MinimumCandidateProximity=80;
        using(var api=new PublicMarketData(new ForeignResearchHandler()))
        {
            var stock=api.Search("애플","해외",CancellationToken.None).GetAwaiter().GetResult().Single();var data=api.History(stock,CancellationToken.None).GetAwaiter().GetResult();
            Check(stock.ProviderCode=="AAPL.O"&&data.Bars.Length==2&&data.Source.Contains("네이버"),"Apple ticker/Korean search loads Naver daily OHLCV using the opaque StockViewer provider ID");
            Check(DailyDates.Of(data.Bars[0])==new DateOnly(2025,11,3)&&data.Bars[0].Time.Offset==TimeSpan.FromHours(-5),"US daily bars retain the Eastern trading date and DST offset");
            var members=api.Universe("S&P500+나스닥100",10,CancellationToken.None).GetAwaiter().GetResult();Check(members.Count==1&&members[0].Indices.Length==2,"combined index universe deduplicates shared constituents and preserves both memberships");
            var foreignRows=data.Bars.Select((b,i)=>new StrategyBar(b,b.Time,b.Time,i,null,"네이버 USD")).ToArray();
            var foreign=engine.EvaluateCurrent(stock,foreignRows,c,new(),new DateTimeOffset(2025,11,6,0,0,0,TimeSpan.Zero),from:new(2025,11,3),to:new(2025,11,4));Check(foreign.Evaluation!=null&&!foreign.Status.Contains("해외 제외"),"foreign screening uses the same price/support evaluator instead of excluding US symbols");
        }
        var apple=new StockItem("AAPL","애플",Exchange.NASDAQ,"AAPL.O");var usConfig=DailyConfig();usConfig.UsePriceOnlyBacktest=true;usConfig.MinimumAverageTradingValue=0;
        var usPrices=new[]{100m,100m,80m,82m,90m}.Select((p,i)=>new PriceBar(ResearchMarketSession.For(new DateOnly(2025,11,3).AddDays(i),Exchange.NASDAQ).Close,p,p,p,p,100)).ToArray();
        var usRun=new OversoldBacktest().Run(new(apple,usPrices,"US FIXTURE",DateTimeOffset.UtcNow),usConfig,new(2025,11,5),new(2025,11,7),new(),supportProviders:[new FixedDailySupport()]);
        var usEntry=usRun.Trades.Single().Executions.First();Check(usEntry.SignalTime==ResearchMarketSession.For(new(2025,11,5),Exchange.NASDAQ).Close&&usEntry.ExecutionTime==ResearchMarketSession.For(new(2025,11,6),Exchange.NASDAQ).Open&&usEntry.Price==82,"US daily signals fill at the next Eastern 09:30 open without KST date shifting");
        var notices=new List<string>();var historyPath=Path.Combine(Dir,"research-history-"+Guid.NewGuid().ToString("N")+".db");
        using(var db=new TradingStore(historyPath))
        using(var vm=new WorkspaceViewModel(_=>false,store:db,provider:new ParallelScreeningFixture(),alert:notices.Add,multiClock:()=>DailyTime(2,18)))
        {
            vm.RealtimeScreeningMode=true;vm.Actions["실시간스크리닝시작"].Execute(null);PumpUntil(()=>!vm.Busy);
            Check(notices.Any(n=>n.Contains("장 외 시간"))&&!vm.RealtimeScreeningRunning,"closed-market realtime start raises a popup notice and creates no session");
            var bars=new[]{DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,82),DailyBar(4,90)};
            var result=DailyRun(bars);var saved=result.ToHistory();db.State(result.Id,result);db.State("oversold-bars-snapshot:"+result.DataHash,bars);db.State(saved.Id,saved);vm.History.Add(saved);
            vm.ResearchFrom=DateTime.MinValue;vm.ResearchTo=DateTime.MaxValue.Date;
            Check(vm.ResearchHistory.Any(r=>r.Id==saved.Id),"a newly completed backtest immediately appears as one history row");
            vm.SelectedResearchRecord=vm.ResearchHistory.Single(r=>r.Id==saved.Id);vm.Actions["테스트상세"].Execute(null);PumpUntil(()=>Application.Current.Windows.OfType<ScreeningChartDialog>().Any());
            var popup=Application.Current.Windows.OfType<ScreeningChartDialog>().Single();Check(popup.Snapshot.Markers.Length==result.Trades.Sum(t=>t.Executions.Count)&&popup.Snapshot.Result?.Id==result.Id,"history opens the saved backtest chart and fill details without rerunning or fetching prices");popup.Close();
            vm.ResearchFrom=new DateTime(2000,1,1);vm.ResearchTo=new DateTime(2000,1,2);Check(vm.ResearchHistory.Count==0,"history date filters use execution dates and exclude out-of-range runs");
            var screening=OversoldScreeningRun.Start([DailyStock],c,null,DateTimeOffset.UtcNow,new(2025,1,1),new(2025,1,5),"보관 검증") with{Status="완료"};db.State(screening.Id,screening);
        }
        using(var reopenedDb=new TradingStore(historyPath))using(var reopened=new WorkspaceViewModel(_=>false,store:reopenedDb,provider:new FixtureMarketData()))
        {reopened.ResearchFrom=DateTime.MinValue;reopened.ResearchTo=DateTime.MaxValue.Date;Check(reopened.ResearchHistory.Count==2&&reopened.ResearchHistory.Any(r=>r.Kind=="스크리닝"),"backtest and screening history both survive application restart");}
    }
    private static void ResearchExpansionDemo(string output)
    {
        Directory.CreateDirectory(output);using var db=new TradingStore(Path.Combine(Dir,"research-demo.db"));var config=DailyConfig();config.ShowNearCandidates=true;config.MinimumCandidateProximity=60;db.State("oversold-config-v1",config);db.State("oversold-facts-v1",DailyFacts(5));
        var bars=new[]{DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,82),DailyBar(4,90)};var result=DailyRun(bars);var saved=result.ToHistory();db.State(result.Id,result);db.State("oversold-bars-snapshot:"+result.DataHash,bars);db.State(saved.Id,saved);
        using var vm=new WorkspaceViewModel(_=>false,store:db,provider:new DailyFixture(),alert:message=>MessageBox.Show(message,"검증 안내"));vm.ResearchFrom=DateTime.MinValue;vm.ResearchTo=DateTime.MaxValue.Date;vm.ActiveTab=4;
        var window=new MainWindow(vm);Application.Current.MainWindow=window;window.Title="STOCK 기능 검증 · 격리 DB / 실제 주문 없음";window.ShowDialog();
    }
    private static async Task ForeignPublicChecks(string output)
    {
        using var api=new PublicMarketData();using var limit=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var apple=(await api.Search("애플","해외",limit.Token)).First(s=>s.Symbol=="AAPL");var data=await api.History(apple,limit.Token);var quote=await api.Current(apple,limit.Token);
        var sp=await api.Universe("S&P500",10,limit.Token);var nasdaq=await api.Universe("나스닥100",10,limit.Token);var combined=await api.Universe("S&P500+나스닥100",10,limit.Token);
        Check(data.Bars.Length>1000&&data.CompletedBars.Length>0&&quote.Quote.Price>0,"live Apple Naver history and StockViewer-style current quote return genuine prices");
        Check(sp.Count>=490&&nasdaq.Count>=100&&combined.Count==sp.Concat(nasdaq).DistinctBy(s=>s.Key).Count(),"live S&P500 Nasdaq100 and deduplicated combined universe contain actual constituents");
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"foreign-public.json"),JsonSerializer.Serialize(new{Apple=apple.ProviderCode,Bars=data.Bars.Length,First=data.Bars[0].Time,Last=data.Bars[^1].Time,Price=quote.Quote.Price,SP500=sp.Count,Nasdaq100=nasdaq.Count,Combined=combined.Count}));
    }
}
