using System.Text.Json;
using Stock.Core.Research;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Injes;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;
using BBar=Stock.Core.Strategies.Bins.BarUnit;
using BExecution=Stock.Core.Strategies.Bins.ExecutionPolicy;

internal sealed record StrategySample(string Name,string Case,StrategyProfile Profile,StockItem Stock,MarketEvent[] Events)
{
    public DateOnly Date=>new(2026,10,5);
    public BarDataset Display=>new(Stock,[new(Events[0].ExchangeTime,Events[0].LastPrice??Events[0].Bid??100,Events.Max(e=>e.TradePrice??e.Ask??0),Events.Min(e=>e.TradePrice??e.Bid??decimal.MaxValue),Events.Last(e=>e.TradePrice!=null).TradePrice!.Value,Events.Sum(e=>e.TradeSize??0))],"SYNTHETIC_SAMPLE_DISPLAY",DateTimeOffset.UtcNow);
}
/// <summary>Explicitly synthetic, reproducible source events; never registered in the user's workspace or substituted for market history.</summary>
internal static class StrategySampleFixtures
{
    public static readonly DateTimeOffset Epoch=DateTimeOffset.Parse("2026-10-05T13:30:00Z");
    private static readonly Instrument Instrument=new("TEST",Exchange.NASDAQ);
    private static StockItem Stock=>new("TEST","합성 검증 종목 · TEST",Exchange.NASDAQ,"TEST.O");
    public static StrategySample[] All()=>Inje().Concat(Bin()).ToArray();
    private static void Parameters<T>(InjesStrategyDefinition d,Action<T> edit)
    {var p=d.StrategyParameters.Deserialize<T>(InjesStrategyJson.Options)!;edit(p);d.StrategyParameters=JsonSerializer.SerializeToElement(p,InjesStrategyJson.Options);d.RefreshEffective();}
    private static IEnumerable<StrategySample> Inje()
    {
        foreach(var id in new[]{"momentum-pullback","quote-scalping","crash-rebound","support-zone"})
        {
            var d=InjesStrategyDefinition.Create(id,Epoch.AddDays(-1));d.StrategyName="샘플 · Inje · "+id;d.InitialCash=1000;
            d.CommonParameters.MinPrice=.1m;d.CommonParameters.MaxPrice=10;d.CommonParameters.MinDailyTradingValue=0;d.CommonParameters.OrderBudget=20;d.CommonParameters.TakeProfitPercent=5;d.CommonParameters.StopLossPercent=id=="quote-scalping"?5:1;d.CommonParameters.ReentryCooldownSeconds=600;d.CommonParameters.MaxSpreadPercent=3;
            d.ExecutionPolicy.ParticipationPercent=100;d.ExecutionPolicy.SlippagePercent=0;d.ExecutionPolicy.SubmissionLatencyMilliseconds=0;d.ExecutionPolicy.CancellationLatencyMilliseconds=0;
            var values=new List<(decimal Seconds,decimal Price,decimal Size,bool Quote,decimal? Bid,decimal? Ask)>();
            void Pair(int sec,decimal price,decimal size=1000){values.Add((sec,price,size,true,price-.01m,price+.01m));values.Add((sec,price,size,false,null,null));}
            switch(id)
            {
                case "momentum-pullback":
                    Parameters<MomentumPullbackParameters>(d,p=>{p.MomentumWindowSeconds=3;p.MinDayGainPercent=5;p.MinMomentumPercent=1;p.MinRVOL=1;p.RVOLLookbackSessions=1;p.MinPullbackPercent=2;p.LowStabilitySeconds=1;p.MinReboundPercent=.3m;p.ReaccelerationVolumeRatio=1;p.MaxChasePercent=2;});
                    for(int n=0;n<40;n++)Pair(n,2,100);
                    foreach(var (n,p) in new[]{(40,2.25m),(41,2.3m),(42,2.2m),(43,2.2m),(44,2.22m),(45,2.22m),(46,2.4m),(47,2.4m),(48,2.4m)})Pair(n,p,2000);break;
                case "quote-scalping":
                    Parameters<QuoteScalpingParameters>(d,p=>{p.VolumeLookbackSeconds=1;p.MinTradesPerSecond=1;p.MinSharesPerSecond=1;p.MinQuoteChangesPerSecond=0;});
                    for(int n=0;n<=6;n++)values.Add((n*.25m,2.01m,100,n%2==0,n%4==0?1.99m:2m,n%4==0?2.03m:2.04m));
                    values.Add((1.75m,1.98m,1000,false,null,null));values.Add((2,2.01m,1000,true,2,2.03m));values.Add((2.25m,2.05m,1000,false,null,null));values.Add((2.5m,2.04m,1000,true,2.03m,2.05m));break;
                case "crash-rebound":
                    Parameters<CrashReboundParameters>(d,p=>{p.MinCrashPercent=10;p.CrashLookbackSeconds=1;p.MinCrashVelocity=0;p.CrashVolumeRatio=2;p.LowStabilitySeconds=1;p.LowStabilityTolerancePercent=.2m;p.MinReboundPercent=.5m;p.MaxChasePercent=2;});
                    for(int n=0;n<5;n++)Pair(n,3,100);foreach(var (n,p) in new[]{(5,2m),(6,2m),(7,2.02m),(8,2.02m),(9,2.2m),(10,2.2m),(11,2.2m)})Pair(n,p,2000);break;
                default:
                    Parameters<SupportZoneParameters>(d,p=>{p.SupportSources="MA,Pivot";p.MovingAveragePeriods="2";p.PivotLeftBars=p.PivotRightBars=1;p.MinimumSupportCount=2;p.RequireReboundConfirmation=false;});
                    for(int n=0;n<5;n++)Pair(n,2);Pair(5,2.2m);Pair(6,2.2m);Pair(7,2.2m);break;
            }
            var session=UsSessionClock.Bounds(Epoch,UsMarketPhase.Regular,true) with{KnownAt=Epoch.AddDays(-1),AllowedOrderKinds=[OrderKind.Limit,OrderKind.Market]};
            var daily=Enumerable.Range(0,3).Select(n=>{var end=Epoch.AddDays(n-4);return new InjesKnownBar(new(end,2,n==1?2.02m:2.01m,1.99m,2,100),end,end);}).ToArray();
            var history=Enumerable.Range(0,60).Select(sec=>new SessionVolumePoint("SYNTHETIC_PAST",session.MarketDate.AddDays(-1),UsMarketPhase.Regular,sec,100,Epoch.AddDays(-1))).ToArray();
            var events=values.Select((v,n)=>{var at=Epoch.AddTicks((long)(v.Seconds*TimeSpan.TicksPerSecond));return new MarketEvent(Instrument,v.Quote?EventKind.Quote:EventKind.Trade,at,at,"SYNTHETIC_SAMPLE",n+1)
            {LastPrice=v.Price,TradePrice=v.Quote?null:v.Price,TradeSize=v.Quote?null:v.Size,Bid=v.Quote?v.Bid:null,Ask=v.Quote?v.Ask:null,BidSize=v.Quote?100000:null,AskSize=v.Quote?400:null,AvailableAt=at,TradingSession=session,
                StrategyReference=new(at){PreviousClose=id=="crash-rebound"?3:2,SessionOpen=2,CumulativeSessionVolume=10000,HistoricalSessionVolumes=history,TickSize=.01m,DailyTradingValue=1000000,AverageDailyTradingValue=500000,FundamentalRisk=FundamentalRiskState.Cleared,Halted=false,DailyBars=daily}};}).ToArray();
            var profile=StrategyProfile.Create(d.StrategyName) with{SelectedInjeId=id,InjeStrategies=StrategyProfile.Create().InjeStrategies.Select(x=>x.StrategyId==id?d:x).ToArray()};
            yield return new(d.StrategyName,id,profile,Stock,events);
        }
    }
    private static IEnumerable<StrategySample> Bin()
    {
        var profile=new CountryProfile{MaxOrderCash=2000,PlannedInvestment=1000,MaxConcurrentExposure=5000,RiskBudgetPerPosition=100,MaxDailyLoss=1000,MinEntryNetRewardPct=0,Fees=[new(new(2020,1,1),new(2030,1,1),Epoch.AddDays(-100),"SYNTHETIC_EXPLICIT_COST",.01m,.01m,0,0)]};
        foreach(var pattern in Enum.GetValues<EntryPattern>())
        {
            var range=pattern is EntryPattern.RangeLowerBounce or EntryPattern.RangeUpperBreakout;
            var c=new BinsStrategyConfig{Market=new(){CountryProfiles=new(){US=profile,KRX=profile with{MaxOrderCash=2000000,PlannedInvestment=1000000,MaxConcurrentExposure=5000000,RiskBudgetPerPosition=100000,MaxDailyLoss=1000000}}},
                Regime=new(){Timeframe=new(1,BBar.Minute),Lookback=DurationSpec.Minutes(10),MinimumBars=8,AtrPeriod=2,MinTrendMovePct=.1m,AtrTrendMultiplier=0,StructureAtrTolerance=0,RangeTouchSeparation=DurationSpec.Minutes(1)},
                Entries=new(){DowntrendRebound=new(){Enabled=pattern==EntryPattern.DowntrendRebound,EntryMode=DowntrendEntryMode.Fast,DropAtrMultiplier=0,LowStabilization=DurationSpec.Seconds(1),MinDropPct=.5m,MaxChasePct=3},
                    UptrendBreakout=new(){Enabled=pattern==EntryPattern.UptrendBreakout,Lookback=DurationSpec.Minutes(10),MaxChasePct=3},
                    UptrendPullback=new(){Enabled=pattern==EntryPattern.UptrendPullback,ImpulseLookback=DurationSpec.Minutes(10),EmaPeriod=2,MinPullbackPct=.2m,MaxEMADistancePct=2,MicroHighLookback=DurationSpec.Seconds(1),LowStabilization=DurationSpec.Seconds(1),MaxChasePct=3},
                    RangeLowerBounce=new(){Enabled=pattern==EntryPattern.RangeLowerBounce,LowStabilization=DurationSpec.Seconds(1),MaxChasePct=3},RangeUpperBreakout=new(){Enabled=pattern==EntryPattern.RangeUpperBreakout,MaxChasePct=3}},
                SharedExit=new(){StopNewEntriesBeforeClose=DurationSpec.Minutes(1),StartLiquidationBeforeClose=DurationSpec.Minutes(.5m)},Execution=new BExecution{SimulationParticipationPct=100},Quality=new(){MaximumTradeGap=DurationSpec.Seconds(2)}};
            var session=new MarketSession("SYNTHETIC_SESSION",Exchange.NASDAQ,new(2026,10,5),SessionPhase.Regular,Epoch,Epoch.AddHours(6),Epoch.AddDays(-1),true,true,[OrderKind.Limit]);
            var bars=Enumerable.Range(0,range?18:14).Select(n=>{var close=range?n%2==0?100.6m:99.4m:pattern==EntryPattern.DowntrendRebound?104-n*.2m:100+n*.2m;var end=Epoch.AddMinutes(n+1);return new CompletedBar(Instrument,new(1,BBar.Minute),session.Id,session.TradingDate,end.AddMinutes(-1),end,end,close,range?n%2==0?101:100:close+.05m,range?n%2==0?100:99:close-.05m,close,1000,"SYNTHETIC_SAMPLE");}).ToArray();
            decimal[] prices=pattern switch{EntryPattern.DowntrendRebound=>[102,100,100,100.5m,100.55m,100.55m,100.55m,98,98,98],EntryPattern.UptrendBreakout=>[102.6m,102.78m,102.79m,102.8m,102.82m,102.84m,102.84m,100,100,100],EntryPattern.UptrendPullback=>[102.2m,102m,102m,102.1m,102.35m,102.35m,102.35m,100,100,100],EntryPattern.RangeLowerBounce=>[99.1m,99.1m,99.15m,99.5m,99.5m,99.5m,99.5m,97,97,97],_=>[100.9m,101.11m,101.12m,101.13m,101.15m,101.15m,101.15m,98,98,98]};
            var rule=new PriceRule("SYNTHETIC_RULE",Epoch.AddDays(-100),Epoch.AddDays(-100),Epoch.AddDays(100),[new(0,null,.01m)]);var metadata=new InstrumentMetadata(Instrument,Epoch.AddDays(-1),session.Close.AddDays(1),true,false,true,rule);
            var events=prices.SelectMany((price,n)=>new[]{true,false}.Select(quote=>{var at=Epoch.AddSeconds((range?1080:840)+n);return new MarketEvent(Instrument,quote?EventKind.Quote:EventKind.Trade,at,at,"SYNTHETIC_SAMPLE",n*2+(quote?1:2))
                {LastPrice=price,TradePrice=quote?null:price,TradeSize=quote?null:10000,Bid=quote?price-.01m:null,Ask=quote?price+.01m:null,BidSize=quote?10000:null,AskSize=quote?10000:null,AvailableAt=at,BinSession=session,BinMetadata=metadata,BinContinuityVerified=true,BinCompletedBars=n==0&&quote?bars:[]};})).ToArray();
            yield return new("샘플 · Bin · "+pattern,pattern.ToString(),StrategyProfile.Create("샘플 · Bin · "+pattern) with{SelectedSystem=StrategySystem.Bin,Bin=c},Stock,events);
        }
    }
}
