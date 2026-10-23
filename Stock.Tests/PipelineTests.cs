using System.IO;
using Stock.App;
using Stock.Core.Pipeline;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void PipelineTests()
    {
        var doc=new PipelineConfiguration();var a=doc.Resolve(StrategyConfigMode.Standard);doc.Advanced=a;
        Check(Serialization.Pack(a)==Serialization.Pack(doc.Resolve(StrategyConfigMode.Advanced)),"pipeline Standard and Advanced resolve identical final config");
        Check(a.Sections().Count()==9&&a.Sections().SelectMany(x=>ParameterCatalog.Properties(x.Config)).All(p=>ParameterCatalog.For(p).Group!=""&&ParameterCatalog.For(p).DetailedDescription!=""),"all nine pipeline groups have shared metadata");
        var bad=new PipelineAdvancedConfig();bad.Universe.MinPrice=6;Throws<InvalidOperationException>(bad.Validate,"pipeline price cross validation");bad=new();bad.TickScalping.StopLossPercent=1;Throws<InvalidOperationException>(bad.Validate,"pipeline stop loss must be negative");bad=new();bad.Farmer.PartialHarvestTriggerPercent=300;Throws<InvalidOperationException>(bad.Validate,"pipeline harvest ordering validation");
        var csv="Symbol,At,SearchRank,PreviousRank,PreviousRankAt,MarketCap\nNASDAQ:AAPL,2026-09-25T13:30:00Z,5,20,2026-09-25T13:29:00Z,\"10,000,000\"";
        var rows=CsvImportProvider.Parse(csv);Check(rows[0].MarketCap==10000000,"CSV handles quoted grouped amounts");var provider=new CsvImportProvider(rows);Check(provider.ReadAsync(Epoch.AddSeconds(-1),default).Result.Count==0,"ranking provider never exposes future snapshots");Check(provider.ReadAsync(Epoch,default).Result.Single().SearchRank==5,"ranking provider selects last known snapshot");Throws<InvalidOperationException>(()=>CsvImportProvider.Parse(csv.Replace("13:30:00Z","13:30:00")),"CSV requires explicit timezone");
        var baseSettings=Settings();var settings=PipelinePresetMapper.ToEngine(a,baseSettings);var context=new MarketContext(Us,settings);
        foreach(var e in DemoMarketDataProvider.Sample(Us,900))context.Add(e);
        var absent=new PipelineEvaluator().Evaluate(context,a,[]);Check(!absent.Passed&&absent.Stages[0].Missing,"pipeline reports missing data rather than invented rankings");
        var observation=new SymbolObservation{Symbol=Us.Key,At=context.Now,SearchRank=3,PreviousRank=30,PreviousRankAt=context.Now.AddMinutes(-1),MarketCap=1000000,IsWatchlist=true};
        var f=new PipelineFeatures(Us,context.Now,2,observation){RankVelocity=27,DayTurnover=10000000,RelativeVolume=10,FiveMinuteRelativeVolume=10,DayChange=10,Gap=0,Drawdown=30,VwapDistance=0,LowDistance=2,ResistanceDistance=10,Spread=.1m,BidDepth=10000,AskDepth=10000,Imbalance=.5m,ExecutionStrength=300,Volatility=1,VolumeDecay=2,VolumeRate=100,Support=1.95m};
        IPipelineFilter[] filters=[new UniverseFilter(),new AttentionFilter(),new PriceActionFilter(),new MicrostructureFilter()];var pass=new PipelineEvaluation(f,filters.Select(x=>x.Evaluate(f,a)).ToArray());Check(pass.Passed,"each pipeline stage accepts a fully observed matching candidate");
        var stale=new PipelineEvaluator().Evaluate(context,a,[observation with{At=context.Now.AddHours(-1)}]);Check(stale.Stages[0].Missing,"expired ranking snapshots are not treated as fresh");
        var paper=new PaperBroker("USD",10000,settings.Execution);var tick=new TickScalpingPipelineStrategy();var entry=tick.Evaluate(pass,a,paper);Check(entry?.Buy==true&&entry.Kind==OrderKind.Limit,"tick pipeline prefers configured limit entry");
        paper.Positions[Us.Key]=new(Us){Quantity=10,AveragePrice=2,Last=2,Owner="Pipeline:틱떼기",OpenedAt=context.Now.AddSeconds(-10)};tick.Evaluate(pass,a,paper);var lower=f with{Price=1.97m};Check(tick.Evaluate(pass with{Features=lower},a,paper)?.Buy==false,"tick pipeline executes configured negative stop");
        a.Strategy=PipelineStrategy.Farmer;a.Farmer.FarmerSignalOnlyMode=false;var farmer=new FarmerPipelineStrategy();paper.Positions.Clear();Check(farmer.Evaluate(pass,a,paper)?.Buy==true,"farmer enters at configured initial portfolio allocation");
        paper.Positions[Us.Key]=new(Us){Quantity=10,AveragePrice=2,Last=3.1m,Owner="Pipeline:농부매매",OpenedAt=context.Now.AddDays(-1)};
        var harvest=farmer.Evaluate(pass with{Features=f with{Price=3.1m}},a,paper);Check(harvest is {Buy:false,Quantity:5}&&harvest.Reason=="농부 일부 수확","farmer partial harvest uses configured ratio");
        Check(farmer.Evaluate(pass with{Features=f with{Price=6.1m}},a,paper)?.Quantity==10,"farmer full harvest sells remaining position");
        a.Portfolio.ProfitTransferEnabled=true;paper.Fills.Add(new("p",Epoch,Us,false,10,3,0,1000,"Pipeline:농부매매"));Check(PortfolioPolicy.CoreReserve(a,paper)==500&&paper.Cash==10000,"Core transfer is reserved virtual profit without money movement");
        var huge=new PipelineOrder(true,1000,2,OrderKind.Limit,"Pipeline:틱떼기","test",1000);Check(PortfolioPolicy.Reject(huge,f,a,paper)!=null,"portfolio blocks new exposure beyond penny budget");
        using var store=new TradingStore(Path.Combine(Dir,"pipeline.db"));var state=new StrategyConfiguration{StrategyType=StrategyType.Pipeline,Pipeline=doc};store.State("pipeline",state);Check(store.State<StrategyConfiguration>("pipeline")?.Pipeline.Advanced.Universe.MaxPrice==a.Universe.MaxPrice,"pipeline all-stage config uses existing SQLite state");
        var same=doc.Resolve(StrategyConfigMode.Standard);var left=PipelinePresetMapper.ToEngine(same,Settings());left.Backtest.Instrument=Us.Key;var right=left.Clone();var events=DemoMarketDataProvider.Sample(Us,400);var x=new BacktestRunner(store).Run(left,events,default);var y=new BacktestRunner(store).Run(right,events,default);Check(Serialization.Pack(x.Trades)==Serialization.Pack(y.Trades)&&Serialization.Pack(x.Equity)==Serialization.Pack(y.Equity),"pipeline replay remains deterministic through existing engine");
        Check(NumericDisplay.Format(1000000m)=="1,000,000"&&NumericDisplay.Format(.015m)=="0.015","numeric grouping preserves fractional parameter precision");
    }
    private static void RecurringTests()
    {
        var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930");var start=DateTimeOffset.Parse("2026-09-23T15:30:00+09:00");
        var data=new BarDataset(stock,[new(start,100,100,100,100,1000),new(start.AddDays(5),100,100,100,100,1000),new(start.AddDays(6),100,100,100,100,1000)],"test",start);
        var config=new RecurringPurchaseConfig{From=start.Date,To=start.AddDays(6).Date,Amount=150,InitialAmount=200,CommissionPercent=0};
        var r=RecurringPurchaseBacktest.Run(data,config,default);Check(r.Contributions==650&&r.Quantity==6&&r.Cash==50&&r.Value==650&&r.ReturnPercent==0,"recurring contributions and leftover cash reconcile");Check(r.MaxDrawdown==0,"external recurring deposits do not create artificial drawdown");
        config.Frequency=PurchaseFrequency.Weekly;r=RecurringPurchaseBacktest.Run(data,config,default);Check(r.Purchases.Length==2&&r.Contributions==500,"weekly purchases use first available trading day including holiday gaps");
        config.Frequency=PurchaseFrequency.Monthly;r=RecurringPurchaseBacktest.Run(data,config,default);Check(r.Purchases.Length==1&&r.Contributions==350,"monthly purchase occurs once per calendar month");
        config.CommissionPercent=1;r=RecurringPurchaseBacktest.Run(data,config,default);Check(r.Quantity==3&&r.Fees==3&&r.Cash==47&&r.Value==347,"recurring fee and affordable integer quantity applied");
        config.CommissionPercent=0;config.InitialAmount=0;config.Amount=100;config.Timing=PurchaseTiming.Close;var rising=data with{Bars=[new(start,100,200,100,200,1000)]};r=RecurringPurchaseBacktest.Run(rising,config,default);Check(r.Purchases.Length==0&&r.Cash==100,"close purchase cannot afford a share at opening price");config.Timing=PurchaseTiming.Open;r=RecurringPurchaseBacktest.Run(rising,config,default);Check(r.Quantity==1&&r.Value==200,"opening purchase marks final equity at closing price");
        Throws<InvalidOperationException>(()=>RecurringPurchaseBacktest.Run(data with{Bars=data.Bars.Select(b=>b with{IsComplete=false}).ToArray()},config,default),"recurring excludes all incomplete candles");
        var falling=data with{Bars=[new(start,100,100,100,100,1000),new(start.AddDays(5),50,50,50,50,1000)]};config.Frequency=PurchaseFrequency.Daily;r=RecurringPurchaseBacktest.Run(falling,config,default);Check(r.MaxDrawdown==50&&r.Contributions==200&&r.Value==150,"recurring drawdown strips deposit masking after a price fall");
        config.From=config.To.AddDays(1);Throws<InvalidOperationException>(config.Validate,"recurring invalid dates rejected");
    }
}
