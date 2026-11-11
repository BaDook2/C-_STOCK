using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void CausalityChecks()
    {
        var momentum=InjesStrategyDefinition.Create("momentum-pullback",Epoch);var runtime=StrategyRegistry.Injes.Get(momentum.StrategyId).Create(momentum);
        Evaluate(runtime,2.2m,0,Features(2.2m,gain:10));Evaluate(runtime,2.134m,1,Features(2.134m,gain:6.7m));Evaluate(runtime,2.13m,2,Features(2.13m,gain:6.5m));
        Check(Evaluate(runtime,2.13m,61,Features(2.13m,gain:6.5m)).Type==SignalType.Hold,"default momentum stability is still waiting 59 seconds after the latest low");
        Check(Evaluate(runtime,2.14m,62,Features(2.14m,gain:7)).Type==SignalType.Enter,"default momentum enters on an actual new cross exactly 60 seconds after the latest low");
        var crash=InjesStrategyDefinition.Create("crash-rebound",Epoch);var crashRuntime=StrategyRegistry.Injes.Get(crash.StrategyId).Create(crash);var windows=new Dictionary<int,WindowFeatures>(Features().Windows){{300,new(true,120,12000,2.5m,2.5m,2,1)},{600,new(true,240,18000,2.5m,2.5m,2,1)}};
        InjesFeatures Crash(decimal price)=>Features(price) with{Windows=windows};
        Evaluate(crashRuntime,2,0,Crash(2));Evaluate(crashRuntime,1.999m,1,Crash(1.999m));Check(Evaluate(crashRuntime,2,60,Crash(2)).Reason.Contains("안정 구간"),"crash default stability does not reach 60 seconds one second early");
        Check(Evaluate(crashRuntime,2,61,Crash(2)).Type==SignalType.Hold&&Evaluate(crashRuntime,2.02m,62,Crash(2.02m)).Type==SignalType.Enter,"crash default confirms a real 60-second narrow range before a later rebound event");
        var riskRuntime=StrategyRegistry.Injes.Get(crash.StrategyId).Create(crash);Evaluate(riskRuntime,2,0,Crash(2));var risk=Crash(2) with{Reference=Crash(2).Reference! with{FundamentalRisk=FundamentalRiskState.Unknown}};
        Check(Evaluate(riskRuntime,2,1,risk).Type==SignalType.CancelEntry,"newly unknown required news risk invalidates an old crash candidate rather than aging its stability clock");
        var quote=InjesStrategyDefinition.Create("quote-scalping",Epoch);SetParameters<QuoteScalpingParameters>(quote,p=>p.OrderBookDepthLevels=2);var features=Features();
        var depth=features.LastQuote! with{Bids=[new(1.99m,1000),new(1.98m,2000)],Asks=[new(2.01m,500),new(2.02m,500)]};
        Check(Evaluate(StrategyRegistry.Injes.Get(quote.StrategyId).Create(quote),2,0,features with{LastQuote=depth}).Type==SignalType.Enter,"consistent sorted two-level order book uses the declared L2 depth");
        var malformed=depth with{Bids=[new(1.99m,1000),new(2.02m,2000)]};Check(Evaluate(StrategyRegistry.Injes.Get(quote.StrategyId).Create(quote),2,0,features with{LastQuote=malformed}).Reason.Contains("L2"),"inverted L2 levels cannot manufacture a valid imbalance");
        var lowDepth=TestSession();var zero=Observation(0,quote:true);Push(lowDepth.Session,zero with{Market=zero.Market with{BidSize=0}});Push(lowDepth.Session,Observation(1));
        Check(lowDepth.Execution.Orders.Count==0&&lowDepth.Session.Latest(Stock.Key)!.Signal.BlockReasons.Contains("Risk.ExecutableDepth"),"zero executable bid depth blocks entry independently of quote freshness");
        var chase=TestDefinition();chase.CommonParameters.MaxSpreadPercent=3;var chased=TestSession(chase);Push(chased.Session,Observation(0,quote:true,ask:2.03m));Push(chased.Session,Observation(1));
        Check(chased.Execution.Orders.Count==0&&chased.Session.Latest(Stock.Key)!.Signal.BlockReasons.Contains("MaxChasePercent"),"actual normalized order price above the signal chase limit creates no buy");
        var unknown=TestSession();Push(unknown.Session,Observation(0,quote:true));Push(unknown.Session,Observation(1));unknown.Execution.Orders[0].State=OrderState.Unknown;Push(unknown.Session,Observation(2));
        Check(unknown.Session.ReconciliationRequired&&unknown.Execution.Orders.Count==1,"unknown order state latches reconciliation and cannot submit a duplicate");Throws(()=>unknown.Session.SetEntriesEnabled(true,Epoch.AddSeconds(1)),"unknown order state cannot be rearmed through the entry toggle");
        var other=new Instrument("OTHER",Exchange.NASDAQ);var d=TestDefinition();var paper=new InjesPaperExecution(1000,d.ExecutionPolicy);var session=new InjesSession(d,[Stock,other],paper);session.SetEntriesEnabled(true,Epoch.AddTicks(-1));session.StopSymbolEntries(Stock.Key,Epoch);
        Push(session,Observation(0,quote:true));Push(session,Observation(1));foreach(var e in new[]{Observation(2,quote:true),Observation(3)})Push(session,e with{Market=e.Market with{Instrument=other}});
        Check(paper.Orders.Count==1&&paper.Orders[0].Symbol==other.Key&&session.Capture().Rows.Single(r=>r.Symbol==Stock.Key).EntriesBlocked,"individual stop blocks only its symbol and preserves the block in the checkpoint");
        var engine=new InjesFeatureEngine(Stock,new(),[3]);var badTime=Observation(0) with{Market=Observation(0).Market with{TimeQuality="receive-time only"}};Check(!engine.Add(badTime,badTime.KnownAt).Accepted,"receive-time-only data cannot advance exchange-time features");
        var delayed=Observation(0) with{KnownAt=Epoch.AddMilliseconds(500)};engine.Add(delayed,delayed.KnownAt);Check(engine.Snapshot(delayed.KnownAt).LastTrade!.AvailableAt==delayed.KnownAt,"feature evidence retains the real observation availability distinct from receipt");
        var futureAvailability=Observation(1) with{Market=Observation(1).Market with{AvailableAt=Epoch.AddDays(1)}};Check(!engine.Add(futureAvailability,futureAvailability.KnownAt).Accepted,"a future raw availability timestamp cannot be bypassed by an earlier wrapper timestamp");
        var repeatedDates=Enumerable.Range(1,20).Select(i=>new SessionVolumePoint("different-id-"+i,new DateOnly(2026,9,24),UsMarketPhase.Regular,0,100,Epoch.AddDays(-1))).ToArray();var duplicateHistory=Observation(0) with{Reference=Observation(0).Reference! with{CumulativeSessionVolume=500,HistoricalSessionVolumes=repeatedDates}};var rvol=new InjesFeatureEngine(Stock,new(),[3]);rvol.Add(duplicateHistory,Epoch);Check(rvol.Snapshot(Epoch).Rvol==null,"twenty IDs from one historical date are not twenty comparison sessions");
        foreach(var memory in new[]{true,false})
        {
            using var store=new TradingStore(Path.Combine(Path.GetTempPath(),"InjeArrival-"+Guid.NewGuid().ToString("N")+".db"),memory);var at=Epoch.AddSeconds(1);var trade=Observation(2);var earlierQuote=Observation(1,quote:true);
            var inserted=new[]{trade.Market with{ReceivedTime=at,AvailableAt=at,StrategyReference=trade.Reference,TradingSession=trade.Session},earlierQuote.Market with{ReceivedTime=at,AvailableAt=at,StrategyReference=earlierQuote.Reference,TradingSession=earlierQuote.Session}};store.InsertEvents(inserted);
            var stock=new StockItem(Stock.Symbol,"Test",Stock.Exchange,Stock.Symbol);var read=InjesResearchBridge.Read(store,stock,new(2026,9,25),new(2026,9,25));
            Check(read.Select(e=>e.Market.Sequence).SequenceEqual(new long[]{2,1}),"equal availability preserves original insertion rather than exchange-time sorting (memory="+memory+")");
        }
        Throws(()=>new ScalpingStrategy().Evaluate(new(Stock,new())),"retired automatic entry algorithms contain no executable fallback");
        Throws(()=>new OversoldBacktest().Run(new(new(Stock.Symbol,"Test",Stock.Exchange,Stock.Symbol),[],"fixture",Epoch),new(),new(2026,9,25),new(2026,9,25),new()),"retired daily backtest cannot execute the former strategy");
        var longGap=new InjesReplayEngine().Run(new(TestDefinition(),[Stock],[Observation(0)],Epoch,Epoch.AddDays(30),1000,"synthetic sparse events",true,true,true));
        Check(longGap.Fills.Length==0&&longGap.Equity.Length<10,"flat sparse replay skips idle overnight clock pulses without inventing fills");
    }
}
