using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;

internal static partial class Program
{
    private sealed class FixtureStrategyFiles(string path):IStrategyFileDialogs
    {public Task<string?> Import()=>Task.FromResult<string?>(path);public Task<string?> Export(string suggested)=>Task.FromResult<string?>(path);}
    private static void DayTradingDesktopChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930");var definition=StrategyDDefaults.Create(open);
        definition.Configuration.AverageTradingValueLookbackDays=2;definition.Configuration.MinimumAverageTradingValue=0;definition.Configuration.MinimumDrawdownPercent=-15;definition.Configuration.VolumeZoneLookbackDays=2;definition.Configuration.VolumeZoneMergeRangePercent=0;definition.Configuration.PlannedInvestment=100000;
        definition.DayTrading!.Support.MaxZoneWidthPercent=3;definition.DayTrading.AdvancedTimeframes=true;definition.DayTrading.IndicatorTimeframes[IndicatorKind.Liquidity]=new(1,TimeframeUnit.TradingDay);
        var previous=new List<DateOnly>();var date=DateOnly.FromDateTime(open.Date);for(var day=date.AddDays(-1);previous.Count<240;day=day.AddDays(-1))if(day.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday))previous.Add(day);previous.Reverse();
        var sessions=previous.Append(date).Select(d=>ResearchMarketSession.For(d,Exchange.KRX)).ToArray();var seeds=previous.Select((d,index)=>
        {var session=ResearchMarketSession.For(d,Exchange.KRX);return new KnownStrategyBar(new(1,TimeframeUnit.TradingDay),new(new(session.Close,10000,index==230?12000:10000,9900,10000,1000),session.Open,session.Close,index,10000000,"FIXTURE"),session.Close);}).ToArray();
        var facts=new HistoricalFacts{Source="SYNTHETIC_FIXTURE",MarketSessions=sessions,StatusRecords=[new(stock.Symbol,date,open,HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false)],RiskRecords=[new(stock.Symbol,date,false,open)]};
        decimal[] prices=[10100,9900,9920,9950,9950,10100,10120,10160,10180,10100,10090];var bars=prices.Select((p,n)=>new StrategyBar(new(open.AddMinutes(n+1),p,p,p,p,10000),open.AddMinutes(n),open.AddMinutes(n+1),n+1,p*10000,"OHLC_FIXTURE")).ToArray();
        var path=Path.Combine(output,"D-fixture.json");File.WriteAllText(path,StrategyDefinitionJson.Export(definition));using var store=new TradingStore(Path.Combine(output,"D-isolated-"+Guid.NewGuid().ToString("N")+".db"),true);
        var notices=new List<string>();var model=new DayTradingViewModel(store,new FixtureStrategyFiles(path),notices.Add,(_,config,_,_,_,_,_)=>Task.FromResult<PreparedDayTrading[]>([new(stock,new(new(stock.Symbol,stock.Exchange),config,new(seeds,facts),"D"),bars,[])]),_=>throw new InvalidOperationException("fixture feed not requested"),()=>[stock]){StartDate=open.Date,EndDate=open.Date,SeedCash="200000"};
        model.Commands["불러오기"].Execute(null);Pump(()=>!model.Busy);model.Commands["저장"].Execute(null);Pump(()=>!model.Busy);
        var dialog=new DayTradingDialog(model,0);dialog.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using(var settingsImage=dialog.CaptureRenderedFrame())settingsImage!.Save(Path.Combine(output,"avalonia-strategy-d-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Check(!dialog.GetVisualDescendants().OfType<TabControl>().Any(),"D history viewer contains results only; settings are integrated in the strategy panel");
        model.Commands["연구"].Execute(null);Pump(()=>!model.Busy,20);Check(notices.Count==0&&model.Latest is {Signals.Length:1,Fills.Length:4},"Avalonia D dialog uses the same replay/runtime and reproduces one signal plus four fills");
        Check(model.Markers.Length==4&&model.Zones.Count(z=>!z.CrashPoint)==1&&model.Zones.Count(z=>z.CrashPoint)==1,"D Avalonia result maps exact execution timestamps and original frozen support zone");using(var image=dialog.CaptureRenderedFrame())image!.Save(Path.Combine(output,"avalonia-strategy-d.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        var outer=definition.Configuration.Clone();outer.UseCrashStrategy=true;outer.CrashStrategy=definition;
        facts.StrategyFrames=seeds;
        var daily=seeds.Select(s=>s.Bar).ToArray();
        var routed=SelectedStrategy.Backtest(stock,outer,facts,daily,bars,date,date,false,default);
        Check(routed.StrategyDResult is {Signals.Length:1,Fills.Length:4},"selected strategy switch invokes the full existing D entry and exit engine");
        var mapped=new ResearchResultViewModel(routed,bars.Select(b=>b.Price).ToArray());
        Check(mapped.Markers.Length==4&&mapped.Zones.Count(z=>z.CrashPoint)==1,"unified research results preserve actual D fills and the used rebound-low line");
        dialog.Close();model.DisposeAsync().AsTask().GetAwaiter().GetResult();Check(store.State<StrategyDefinition[]>("daytrading-definitions-v2")!.Single().IsStrategyD,"D settings persist only in the separate schema2 namespace");
    }
}
