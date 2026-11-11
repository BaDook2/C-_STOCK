using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core.Research;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Injes;
using Stock.Core.Strategies.Bins;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private sealed class SampleMarket(StrategySample sample):IPublicMarketData
    {
        private readonly StockItem missing=sample.Stock with{Symbol="MISS",Name="원본 자료 없는 비교 종목",ProviderCode="MISS.O"};
        public Task<IReadOnlyList<StockItem>> Search(string q,string region,CancellationToken t)=>Task.FromResult<IReadOnlyList<StockItem>>([sample.Stock]);
        public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken t)=>Task.FromResult<IReadOnlyList<StockItem>>([sample.Stock,missing]);
        public Task<StockItem> Describe(StockItem stock,CancellationToken t)=>Task.FromResult(stock);
        public Task<BarDataset> History(StockItem stock,CancellationToken t)=>Task.FromResult(sample.Display with{Stock=stock});
    }
    private static void StrategySampleChecks()
    {
        BundledSampleChecks();
        var samples=StrategySampleFixtures.All();Check(samples.Length==9&&samples.Count(s=>s.Profile.SelectedSystem==StrategySystem.Inje)==4,"four Inje algorithms and all five Bin entry patterns have independent complete sample profiles");
        var summaries=new List<object>();foreach(var sample in samples)
        {
            var directory=Path.Combine(output,sample.Case);Directory.CreateDirectory(directory);var profileJson=StrategyProfileJson.Export(sample.Profile);Check(StrategyProfileJson.Export(StrategyProfileJson.Import(profileJson))==profileJson,sample.Case+": complete sample JSON imports without dropped values");File.WriteAllText(Path.Combine(directory,"profile.json"),profileJson);
            using var services=new AppServices(Path.Combine(directory,"isolated-workspace-"+Guid.NewGuid().ToString("N")),new SampleMarket(sample),credentials:MemoryCredentials(),brokerFactory:(_,_)=>throw new InvalidOperationException("Sample checks cannot connect to an account"));
            services.Store.InsertEvents(sample.Events);var configuration=sample.Profile.ExecutionConfiguration();
            OversoldResult Replay(MarketEvent[] events)=>configuration.Bins!=null?BinsResearchBridge.Run(sample.Stock,configuration,sample.Date,sample.Date,events.Select(BinsResearchBridge.Observation).ToArray(),CancellationToken.None):InjesResearchBridge.Run(sample.Stock,configuration,sample.Date,sample.Date,events.Select(InjesResearchBridge.Observation).ToArray(),CancellationToken.None);
            ScreeningObservation Screen(MarketEvent[] events)=>configuration.Bins!=null?BinsResearchBridge.Screen(sample.Stock,configuration,events.Select(BinsResearchBridge.Observation).ToArray(),CancellationToken.None):InjesResearchBridge.Screen(sample.Stock,configuration,events.Select(InjesResearchBridge.Observation).ToArray(),sample.Date,sample.Date,CancellationToken.None);
            var task=services.Backtests.RunAsync(new(sample.Display,configuration,sample.Date,sample.Date,new()),CancellationToken.None);Pump(()=>task.IsCompleted);var result=task.GetAwaiter().GetResult();var screen=Screen(sample.Events);var fills=result.Trades.SelectMany(t=>t.Executions).ToArray();
            Check(fills.Count(f=>f.Buy)==1&&fills.Count(f=>!f.Buy)==1&&result.Performance.Trades==1&&result.Quantity==0&&!result.PendingOrderAtEnd,sample.Case+": actual backtest service completes exactly one buy/sell round trip with no unresolved position/order");
            Check(fills.All(f=>f.ExecutionTime>f.SignalTime)&&fills.Where(f=>f.Buy).Sum(f=>f.Quantity)==fills.Where(f=>!f.Buy).Sum(f=>f.Quantity),sample.Case+": signals fill only on later events and confirmed entry/exit quantities reconcile");
            Check(result.InjesResult?.SyntheticFixture==true||result.BinsResult?.SyntheticFixture==true,sample.Case+": backtest result explicitly identifies synthetic source data");
            Check(screen.Candidate&&(screen.InjesEvaluation?.Signal is{RiskApproved:true,OrderAllowed:false}||screen.BinsEvaluation?.Signal?.Kind=="ScreenMatched"),sample.Case+": screening approves the matching pattern without enabling an order");
            var duplicate=Replay(sample.Events.SelectMany(e=>new[]{e,e}).ToArray());Check(duplicate.Performance.Trades==1&&duplicate.Trades.SelectMany(t=>t.Executions).Count()==2&&duplicate.Cash==result.Cash,sample.Case+": duplicate original events cannot add a buy, sell or cash movement");
            var noQuote=sample.Events.Where(e=>e.Kind==EventKind.Trade).ToArray();var blocked=Replay(noQuote);Check(blocked.Trades.Count==0&&!Screen(noQuote).Candidate,sample.Case+": missing quotes prevent both a backtest trade and screening selection");
            var missing=sample.Events.Select(e=>e with{TradingSession=null,StrategyReference=null,BinSession=null,BinMetadata=null}).ToArray();Check(Replay(missing).Trades.Count==0&&!Screen(missing).Candidate,sample.Case+": unverified session/rules cannot become optimistic entries");
            var cut=sample.Events.Where(e=>(e.AvailableAt??e.ReceivedTime)<fills[0].SignalTime).ToArray();if(cut.Any(e=>e.Kind==EventKind.Trade))Check(Replay(cut).Trades.Count==0&&!Screen(cut).Candidate,sample.Case+": data before the first signal does not borrow a future entry");
            using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model,Width=1440,Height=1000};window.Show();model.Strategies.ImportText(profileJson);model.Strategies.Save.Execute(null);
            model.Query="TEST";model.Region="해외";model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SelectedStock?.Symbol=="TEST");model.Research.From=sample.Date.ToDateTime(TimeOnly.MinValue);model.Research.To=model.Research.From;model.Research.Run.Execute(null);Pump(()=>!model.Research.Busy);
            Check(model.Research.Result is {} view&&view.Result.Performance.Trades==1&&view.Trades.Length==2&&view.Result.Cash==result.Cash,sample.Case+": real research command displays matching buy/sell results");
            Check(services.Store.States("desktop-run:").Count==1,sample.Case+": UI saves its reproducible completed result in the isolated history");
            foreach(var owned in window.OwnedWindows.ToArray())owned.Close();model.ActiveTab=1;Dispatcher.UIThread.RunJobs();model.Screening.Market="S&P500";
            var screeningPanel=window.GetVisualDescendants().OfType<ScreeningPanel>().Single(p=>ReferenceEquals(p.DataContext,model.Screening));var dates=screeningPanel.GetVisualDescendants().OfType<CalendarDatePicker>().ToArray();dates[0].SelectedDate=sample.Date.ToDateTime(TimeOnly.MinValue);dates[1].SelectedDate=dates[0].SelectedDate;Dispatcher.UIThread.RunJobs();
            Check(model.Screening.From==dates[0].SelectedDate&&model.Screening.To==dates[1].SelectedDate&&model.Screening.From==model.Research.From,sample.Case+": visible date inputs select the actual tested period");model.Screening.TopCount=10;model.Screening.ShowAll=true;model.Screening.Start.Execute(null);Pump(()=>!model.Screening.Busy);
            Check(model.Screening.Snapshot is{Results.Length:2} snap&&snap.FinalCandidates.Length==1&&snap.FinalCandidates[0].Symbol=="TEST"&&snap.Results.Single(r=>r.Stock.Symbol=="MISS").Status.Contains("MissingData"),sample.Case+": real screening command selects the sample and rejects a symbol with no source events");
            var displayPrice=model.Screening.Rows.Single(r=>r.Code=="TEST");var observation=displayPrice.Observation.Price!.Value;
            Check(displayPrice.Price.StartsWith(observation.ToString("0.########",System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal),sample.Case+": screening preserves the observed USD fractional price");
            Check(model.Screening.Funnel.Count==4&&model.Screening.Status.Contains("완료"),sample.Case+": screening completes its four real engine stages");
            model.ActiveTab=1;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();using(var frame=window.CaptureRenderedFrame())frame!.Save(Path.Combine(directory,"screening.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            File.WriteAllText(Path.Combine(directory,"backtest.json"),Serialization.Pack(result));File.WriteAllText(Path.Combine(directory,"screening.json"),Serialization.Pack(model.Screening.Snapshot));
            var entry=fills.Single(f=>f.Buy);var exit=fills.Single(f=>!f.Buy);summaries.Add(new{sample.Name,sample.Case,System=sample.Profile.SelectedSystem.ToString(),Data="SYNTHETIC · functionality only",BacktestEntries=1,BacktestExits=1,Closed=1,ScreeningCandidate=true,UiBacktest=true,UiScreening=true,Quantity=entry.Quantity,EntryPrice=entry.Price,ExitPrice=exit.Price,RealizedPnl=result.Trades.Single().RealizedPnl,Synthetic=true,DuplicateProtected=true,MissingDataProtected=true});
            window.Close();Console.WriteLine("SAMPLE PASS: "+sample.Case);
        }
        File.WriteAllText(Path.Combine(output,"sample-summary.json"),JsonSerializer.Serialize(summaries,new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void BundledSampleChecks()
    {
        var bundled=StrategySampleCatalog.All();var fixtures=StrategySampleFixtures.All();
        Check(bundled.Length==9&&bundled.Select(p=>p.Name).Distinct().Count()==9,"release contains nine independent importable sample resources");
        for(int i=0;i<fixtures.Length;i++)
        {
            var expected=fixtures[i].Profile;var actual=bundled[i].Snapshot() with{Name=expected.Name};
            foreach(var d in actual.InjeStrategies){var reference=expected.InjeStrategies.Single(x=>x.StrategyId==d.StrategyId);d.CreatedAt=reference.CreatedAt;d.UpdatedAt=reference.UpdatedAt;}
            Check(StrategyProfileJson.Export(actual)==StrategyProfileJson.Export(expected),fixtures[i].Case+": bundled sample parameters equal the tested fixture apart from creation metadata");
        }
        using var store=new TradingStore(Path.Combine(output,"bundled-samples-"+Guid.NewGuid().ToString("N")+".db"),memoryMarketData:true);var presets=new StrategyProfileStore(store);
        var original=StrategyProfile.Create("기존 사용자 설정");var conflict=bundled[0] with{BinInitialCashUsd=12345};presets.Save(original);presets.Save(conflict);store.State("strategy-profile-selected-v1",original.Name);store.State("sample-preserve-unrelated",42);
        var old=presets.Names.ToDictionary(n=>n,n=>StrategyProfileJson.Export(presets.Get(n)));StrategySampleCatalog.EnsureInstalled(store);
        Check(presets.Names.Length==10&&old.All(p=>StrategyProfileJson.Export(presets.Get(p.Key))==p.Value)&&store.State<string>("strategy-profile-selected-v1")==original.Name&&store.State<int>("sample-preserve-unrelated")==42,"sample installation preserves colliding user values, active selection and unrelated state");
        var renamed=bundled[1] with{Name="사용자가 바꾼 샘플 이름"};presets.Rename(bundled[1].Name,renamed);var names=presets.Names;StrategySampleCatalog.EnsureInstalled(store);
        Check(names.SequenceEqual(presets.Names)&&!presets.Names.Contains(bundled[1].Name),"restarting does not duplicate samples or recreate a renamed sample");
        using var fresh=new TradingStore(Path.Combine(output,"bundled-fresh-"+Guid.NewGuid().ToString("N")+".db"),memoryMarketData:true);StrategySampleCatalog.EnsureInstalled(fresh);var freshPresets=new StrategyProfileStore(fresh);
        Check(freshPresets.Names.Length==10&&freshPresets.Names[0]=="기본 설정"&&fresh.ReadEventsInArrivalOrder("",DateTimeOffset.MinValue,DateTimeOffset.MaxValue).Count==0,"fresh installation retains the normal default and adds nine profiles without synthetic market events");
    }
}
