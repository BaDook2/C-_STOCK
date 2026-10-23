using System.IO;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void StrategyConfigurationTests()
    {
        foreach(var type in Enum.GetValues<StrategyType>().Where(t=>t!=StrategyType.Pipeline))
        {
            var doc=new StrategyConfiguration{StrategyType=type};var standard=doc.Resolve();doc.Advanced=standard;doc.Mode=StrategyConfigMode.Advanced;
            Check(Serialization.Pack(standard)==Serialization.Pack(doc.Resolve()),type+": Standard and Advanced resolve identical parameters");
            using var db=new TradingStore(Path.Combine(Dir,"config-"+type+".db"));var events=DemoMarketDataProvider.Sample(Us,900);var left=StrategyPresetMapper.ToEngine(standard,Settings());var right=StrategyPresetMapper.ToEngine(doc.Resolve(),Settings());left.Backtest.Instrument=right.Backtest.Instrument=Us.Key;
            var a=new BacktestRunner(db).Run(left,events,CancellationToken.None);var b=new BacktestRunner(db).Run(right,events,CancellationToken.None);
            Check(a.NetPnl==b.NetPnl&&Serialization.Pack(a.Trades)==Serialization.Pack(b.Trades)&&Serialization.Pack(a.Equity)==Serialization.Pack(b.Equity),type+": identical engine backtests across modes");
            db.State("settings",doc);Check(db.State<StrategyConfiguration>("settings")?.Mode==StrategyConfigMode.Advanced,type+": configuration mode persists in existing SQLite");
        }
        var config=new StandardStrategyConfig();config.Scalping.VolumeStrength=0;Throws<InvalidOperationException>(()=>StrategyPresetMapper.ConvertToAdvanced(StrategyType.Scalping,config),"Standard strength below one rejected");
        config.Scalping.VolumeStrength=6;Throws<InvalidOperationException>(()=>StrategyPresetMapper.ConvertToAdvanced(StrategyType.Scalping,config),"Standard strength above five rejected");
        var advanced=new AdvancedStrategyConfig();advanced.Rebound.MinimumCrashPercent=50;Throws<InvalidOperationException>(advanced.Validate,"cross validation minimum crash exceeds maximum");advanced.Rebound.MinimumCrashPercent=10;advanced.Common.TradingStart="25:00";Throws<InvalidOperationException>(advanced.Validate,"invalid KST trading hour rejected");advanced.Common.TradingStart="09:00";advanced.Common.MaxHoldingSeconds=0;Throws<InvalidOperationException>(advanced.Validate,"zero holding time rejected");
        var sampleTypes=new object[]{new CommonStrategyConfig(),new ScalpStandardConfig(),new BoxStandardConfig(),new ReboundStandardConfig(),new ScalpAdvancedConfig(),new BoxAdvancedConfig(),new ReboundAdvancedConfig(),new AdvancedRiskConfig(),new AdvancedExecutionConfig()};
        Check(sampleTypes.SelectMany(ParameterCatalog.Properties).All(p=>{var m=ParameterCatalog.For(p);return m.DisplayName!=""&&m.ShortDescription!=""&&m.EffectWhenIncreased!=""&&m.EffectWhenDecreased!=""&&m.Unit!="";}),"every new parameter supplies shared label help effects and unit metadata");
        var loose=new StandardStrategyConfig();var strict=new StandardStrategyConfig();loose.Scalping.VolumeStrength=1;strict.Scalping.VolumeStrength=5;loose.Common.ExitStrength=1;strict.Common.ExitStrength=5;
        var low=StrategyPresetMapper.ConvertToAdvanced(StrategyType.Scalping,loose);var high=StrategyPresetMapper.ConvertToAdvanced(StrategyType.Scalping,strict);Check(low.Scalping.VolumeRatioThreshold<high.Scalping.VolumeRatioThreshold&&low.Scalping.ExitScoreThreshold<high.Scalping.ExitScoreThreshold,"higher strengths tighten entry and increase exit sensitivity");
        Check(high.Box.BreakdownPercent<low.Box.BreakdownPercent&&high.Box.ResistanceDistancePercent>low.Box.ResistanceDistancePercent,"higher exit strength tightens box breakdown and exits sooner near resistance");
        Check(high.Rebound.ExitRetracementPercent<low.Rebound.ExitRetracementPercent,"higher exit strength tightens rebound retracement exit");
        var heldSettings=Settings();heldSettings.Advanced=new(){StrategyType=StrategyType.Rebound};heldSettings.Advanced.Rebound.ExitRetracementPercent=1;
        var heldContext=new MarketContext(Us,heldSettings){Position=new(Us){Quantity=1,AveragePrice=2,Owner="급락 반등"}};
        var heldGate=new AdvancedStrategyGate(new ReboundStrategy());heldContext.Add(Trade(1,2.2m));heldGate.Evaluate(heldContext);heldContext.Add(Trade(2,2.17m));
        Check(heldGate.Evaluate(heldContext).Buy==false,"configured rebound exit sells on observed peak retracement");
        var date=DateTimeOffset.Parse("2026-09-28T15:30:00+09:00");var bar=new PriceBar(date,100,110,90,105,10);
        Check(!PublicMarketData.IsDailyComplete(bar,Exchange.KRX,date.AddHours(-1))&&PublicMarketData.IsDailyComplete(bar,Exchange.KRX,date.AddMinutes(10)),"chart today retained while incomplete session excluded from research");
        Check(new BarDataset(new("005930","삼성전자",Exchange.KRX,"005930"),[bar with{IsComplete=false}],"fixture",date).CompletedBars.Length==0,"partial daily bars never enter completed backtest dataset");
        var ticks=Enumerable.Range(0,15).Select(i=>bar with{Time=date.AddSeconds(i*30),Open=100+i,High=100+i,Low=100+i,Close=100+i,Volume=1}).ToArray();
        foreach(var n in ChartPeriods.Intervals){var grouped=ChartPeriods.FromTrades(ticks,n+"틱");Check(grouped.Length==(int)Math.Ceiling(15d/n)&&grouped.Sum(b=>b.Volume)==15,n+" tick grouping preserves observed volume");var minutes=ChartPeriods.FromTrades(ticks,n+"분");Check(minutes.Sum(b=>b.Volume)==15&&minutes[0].Open==100&&minutes[^1].Close==114,n+" minute grouping preserves OHLC and volume");}
        var minuteHistory=new[]{new PriceBar(Epoch,90,91,89,90,100),new PriceBar(Epoch.AddMinutes(1),91,92,90,91,100)};var liveMinute=new[]{new PriceBar(Epoch.AddMinutes(1).AddSeconds(10),95,95,95,95,2),new PriceBar(Epoch.AddMinutes(1).AddSeconds(20),96,96,96,96,3)};
        var mergedMinute=ChartPeriods.MergeLiveTrades(minuteHistory,liveMinute,"1분");Check(mergedMinute.Length==2&&mergedMinute[^1].Open==95&&mergedMinute[^1].Close==96&&mergedMinute[^1].Volume==5,"live trades replace the current historical minute bucket without double-counting old volume");
        Check(WorkspaceViewModel.LiveChartRefreshInterval==TimeSpan.FromSeconds(1),"tick minute daily and weekly live chart UI share a one-second refresh interval");
        var marker=new TradeMarker("fill","KRX:005930",date.AddDays(-1),true,1,100,"모의매매");
        Check(Stock.App.CandleChart.TradeBarIndex([bar],marker,"월봉")==0,"monthly chart includes fills before last bar date in same month");
        marker=marker with{Time=date.AddHours(-3)};
        Check(Stock.App.CandleChart.TradeBarIndex([bar],marker,"주봉")==0,"weekly chart includes earlier fill in same market week");
        var usBar=bar with{Time=DateTimeOffset.Parse("2026-09-28T13:30:00Z")};
        marker=marker with{StockKey="NASDAQ:AAPL",Time=DateTimeOffset.Parse("2026-09-28T19:59:00Z")};
        Check(Stock.App.CandleChart.TradeBarIndex([usBar],marker,"일봉")==0,"US fill after KST midnight belongs to original US trading day");
        marker=marker with{StockKey="KRX:005930",Time=date.AddSeconds(65)};
        Check(Stock.App.CandleChart.TradeBarIndex(ChartPeriods.FromTrades(ticks,"3분"),marker,"3분")==0,"minute marker follows aggregation bucket rather than closing timestamp");
        var legacy=Settings();legacy.Scalping.Enabled=true;var context=new MarketContext(Us,legacy);foreach(var e in DemoMarketDataProvider.Sample(Us,200))context.Add(e);Check(new ScalpingStrategy().Evaluate(context)==new AdvancedStrategyGate(new ScalpingStrategy()).Evaluate(context),"null Advanced retains original strategy signal exactly");
        var paperSettings=Settings();paperSettings.Execution.UseSpread=false;paperSettings.Execution.ParticipationPercent=100;paperSettings.Execution.FeePercent=0;paperSettings.Execution.TaxPercent=1;var broker=new PaperBroker("USD",1000,paperSettings.Execution);broker.Submit(new(){Id="buy",Instrument=Us,Buy=true,Quantity=10,Price=2,Kind=OrderKind.Market,Created=Epoch});broker.Process(Trade(1,2,100));broker.Submit(new(){Id="sell",Instrument=Us,Buy=false,Quantity=10,Price=3,Kind=OrderKind.Market,Created=Epoch.AddSeconds(1)});broker.Process(Trade(2,3,100));Check(broker.Fills[0].Fee==0&&broker.Fills[1].Fee==.3m&&broker.Cash==1009.7m,"execution tax charged only on simulated sell fills");
        var risk=new AdvancedStrategyConfig();var order=new ManagedOrder{Instrument=Us,Buy=true,Quantity=100,Price=3,Created=Epoch.AddSeconds(3)};Check(AdvancedPaperRisk.Check(risk,order,broker)?.Contains("비중")==true,"advanced maximum position exposure applies to paper entry");order=new(){Instrument=Us,Buy=true,Quantity=1,Price=3,Created=Epoch.AddSeconds(3)};Check(AdvancedPaperRisk.Check(risk,order,broker)?.Contains("대기")==true,"paper reentry cooldown blocks immediate rebuy");
    }
}


