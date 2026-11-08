using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void DayTradingPortfolioChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var symbol=new Instrument("005930",Exchange.KRX);
        var definition=StrategyDTestConfig();definition.Configuration.PlannedInvestment=100000;
        ExecutionSettings Execution()=>new(){UseSpread=false,FeePercent=definition.Configuration.CommissionPercent,TaxPercent=0,SlippagePercent=0,LatencyMs=0,ParticipationPercent=100,AllowPartial=true,CancelRemainder=false,TimeoutMilliseconds=60000};
        StrategyDRuntime Runtime(string instance="D",string code="005930")
        {
            var seed=StrategyDSeed(open);seed.Facts.StatusRecords=seed.Facts.StatusRecords.Select(r=>r with{Symbol=code}).ToArray();seed.Facts.RiskRecords=seed.Facts.RiskRecords.Select(r=>r with{Symbol=code}).ToArray();
            return new(new(code,Exchange.KRX),definition,seed,instance);
        }
        MarketEvent TradeAt(int second,decimal price,int sequence,string code="005930",int size=1000)=>new(new(code,Exchange.KRX),EventKind.Trade,open.AddSeconds(second),open.AddSeconds(second),"TEST",sequence){TradePrice=price,TradeSize=size,CumulativeVolume=sequence*1000};
        void Feed(DayTradingPortfolioEngine engine,int second,decimal price,int sequence,string code="005930")=>engine.OnMarket(TradeAt(second,price,sequence,code),open.AddSeconds(second));
        void Approach(DayTradingPortfolioEngine engine,string code="005930")
        {Feed(engine,0,10100,1,code);Feed(engine,1,9900,2,code);Feed(engine,2,9920,3,code);Feed(engine,3,9950,4,code);}
        var paper=new DayTradingPaperAdapter("KRW",200000,Execution());var runtime=Runtime();var log=new List<DayTradingAudit>();
        var engine=new DayTradingPortfolioEngine(paper,[runtime],definition.DayTrading!.Risk,log.Add);engine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(engine);
        Check(paper.Orders.Count==1&&paper.Fills.Count==0&&engine.Snapshot().Rows[0].Quantity==0,"entry signal submits one pending order without assuming an immediate fill");
        var pending=paper.Orders[0];engine.OnMarket(TradeAt(3,9950,4),open.AddSeconds(3));
        Check(paper.Orders.Count==1&&paper.Fills.Count==0,"duplicate signal tick cannot fill or submit the same pending entry");
        Feed(engine,4,9950,5);var entered=engine.Snapshot().Rows[0];
        Check(paper.Fills.Count==1&&paper.Fills[0].Time>pending.Created&&entered.Quantity==10&&entered.Entries==1,"subsequent event confirms actual filled shares and one entry stage");
        engine.SetEntriesEnabled(false,open.AddSeconds(4));Feed(engine,5,10100,6);Feed(engine,6,10120,7);
        var tp1=engine.Snapshot().Rows[0];Check(tp1.Quantity==7&&tp1.TakeProfit1&&paper.Fills[^1].Reason=="TP1","entry checkbox off preserves TP1 protection and confirmed partial sale");
        Feed(engine,7,10160,8);Feed(engine,8,10180,9);var tp2=engine.Snapshot().Rows[0];
        Check(tp2.Quantity==4&&tp2.TakeProfit2&&tp2.Peak>=10180,"TP2 uses original ten-share basis and peak includes take-profit trigger ticks");
        Feed(engine,9,10100,10);Feed(engine,10,10090,11);
        var exited=engine.Snapshot().Rows[0];Check(exited.Quantity==0&&exited.RealizedProfit>0&&paper.Fills[^1].Reason=="TrailingStop","remaining position exits by tick trailing protection while new entries stay disabled");
        Check(log.Count(e=>e.Kind=="체결")==paper.Fills.Count&&log.Any(e=>e.Kind=="진입 해제"),"one central journal separates signal, request, confirmed fill and entry control state");
        var off=new DayTradingPaperAdapter("KRW",100000,Execution());var offEngine=new DayTradingPortfolioEngine(off,[Runtime("off")],definition.DayTrading.Risk);Approach(offEngine);
        Check(off.Orders.Count==0&&offEngine.Snapshot().RecentEvents.Any(e=>e.Kind=="매수 신호"),"automatic entries default off while genuine rebound signals remain visible");
        offEngine.SetEntriesEnabled(true,open.AddSeconds(4));Feed(offEngine,5,9960,5);
        Check(off.Orders.Count==0,"enabling automatic entries never replays a pre-activation signal");
        var sharedPaper=new DayTradingPaperAdapter("KRW",100000,Execution());var sharedEngine=new DayTradingPortfolioEngine(sharedPaper,[Runtime("shared","005930"),Runtime("shared","000660")],definition.DayTrading.Risk);
        sharedEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(sharedEngine,"005930");Approach(sharedEngine,"000660");
        Check(sharedPaper.Orders.Count==1&&sharedPaper.ReservedCash<=sharedPaper.Cash,"two simultaneous symbols compete for the same reserved account cash");
        Throws<InvalidOperationException>(()=>new DayTradingPortfolioEngine(sharedPaper,[Runtime("a"),Runtime("b")],definition.DayTrading.Risk),"same symbol cannot run duplicated sessions in a shared portfolio");
        var dayDefinition=definition.Snapshot();dayDefinition.Configuration.UseTakeProfit1=dayDefinition.Configuration.UseTakeProfit2=dayDefinition.Configuration.UseTrailingStop=false;
        var dayRuntime=new StrategyDRuntime(symbol,dayDefinition,StrategyDSeed(open),"day");var dayPaper=new DayTradingPaperAdapter("KRW",200000,Execution());var dayEngine=new DayTradingPortfolioEngine(dayPaper,[dayRuntime],dayDefinition.DayTrading!.Risk);
        dayEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(dayEngine);Feed(dayEngine,4,9950,5);
        var fillsBefore=dayPaper.Fills.Count;dayEngine.OnTime(open.AddHours(6).AddMinutes(10));
        Check(dayPaper.Orders.Count(o=>o.Active&&!o.Buy&&o.Reason=="DayLiquidation")==1&&dayPaper.Fills.Count==fillsBefore,"independent liquidation clock queues one exit without inventing a price event or fill");
        dayEngine.OnTime(open.AddHours(6).AddMinutes(11));Check(dayPaper.Orders.Count(o=>o.Active&&!o.Buy)==1,"repeated clock pulses do not duplicate an unfilled liquidation order");
        dayEngine.OnTime(open.AddHours(6.5));Check(dayEngine.Snapshot().NeedsReconciliation&&dayEngine.Snapshot().Rows[0].Error.Contains("UnexpectedOvernight"),"unfilled liquidation at close remains an explicit overnight/reconciliation fault");
        var stoppedPaper=new DayTradingPaperAdapter("KRW",100000,Execution());var stoppedEngine=new DayTradingPortfolioEngine(stoppedPaper,[Runtime("stop")],definition.DayTrading.Risk);
        stoppedEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(stoppedEngine);stoppedEngine.EmergencyStop(open.AddSeconds(3));Feed(stoppedEngine,4,9950,5);
        Check(stoppedEngine.Snapshot().EmergencyStopped&&stoppedPaper.Fills.Count==0&&stoppedPaper.Orders.All(o=>!o.Active),"emergency stop cancels pending entry and never fabricates a market liquidation");
        Throws<InvalidOperationException>(()=>stoppedEngine.SetEntriesEnabled(true,open.AddSeconds(5)),"emergency-stopped session cannot be rearmed by toggling an entry checkbox");
        var deniedPaper=new LiveGuardAdapter(new DayTradingPaperAdapter("KRW",100000,Execution()));var deniedEngine=new DayTradingPortfolioEngine(deniedPaper,[Runtime("denied")],definition.DayTrading.Risk);
        deniedEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(deniedEngine);Check(deniedPaper.Submits==0,"live execution adapter receives no order without a separate explicit authorization");
        int authorizationChecks=0;var racePaper=new LiveGuardAdapter(new DayTradingPaperAdapter("KRW",100000,Execution()));
        var race=new DayTradingPortfolioEngine(racePaper,[Runtime("race")],definition.DayTrading.Risk,liveAuthorized:()=>++authorizationChecks==1);race.SetEntriesEnabled(true,open.AddTicks(-1));Approach(race);
        Check(authorizationChecks>=2&&racePaper.Submits==0,"revoked live authorization is checked again at the exact submit boundary");
        var partialPaper=new DayTradingPaperAdapter("KRW",200000,Execution());var partialEngine=new DayTradingPortfolioEngine(partialPaper,[Runtime("partial")],definition.DayTrading.Risk);partialEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(partialEngine);
        int cumulative=4000;void Partial(int second,decimal price,int sequence,int size)
        {cumulative+=size;var tick=TradeAt(second,price,sequence) with{TradeSize=size,CumulativeVolume=cumulative};partialEngine.OnMarket(tick,tick.ReceivedTime);}
        Partial(4,9950,5,2);Partial(5,9950,6,3);Partial(6,9950,7,5);
        Check(partialEngine.Snapshot().Rows[0].Quantity==10&&partialEngine.Snapshot().Rows[0].Entries==1,"multiple partial buy fills update actual ownership but count one entry order");
        Partial(8,10100,8,1000);Partial(9,10110,9,1);
        Check(partialEngine.Snapshot().Rows[0].Quantity==9&&!partialEngine.Snapshot().Rows[0].TakeProfit1&&partialPaper.Orders.Single(o=>o.Active).Remaining==2,"partial TP1 fill remains an unfinished target and reserves only its true remaining shares");
        Partial(10,10110,10,2);
        Check(partialEngine.Snapshot().Rows[0].Quantity==7&&partialEngine.Snapshot().Rows[0].TakeProfit1&&partialPaper.Orders.All(o=>!o.Active),"TP1 completes only after cumulative confirmed target quantity fills");
        var scale=definition.Snapshot();scale.Configuration.MaxEntries=3;scale.Configuration.EntryRatios="40,30,30";scale.Configuration.RequireSupportForAdditionalEntry=false;scale.Configuration.UseEmergencyStop=false;scale.Configuration.TrailingActivation=TrailingActivation.AfterEntry;scale.Configuration.TrailingStopPercent=50;scale.DayTrading!.Exit.SupportStopEnabled=false;scale.DayTrading.Risk.RiskBudgetPerPosition=50000;
        var scalePaper=new DayTradingPaperAdapter("KRW",200000,Execution());var scaleEngine=new DayTradingPortfolioEngine(scalePaper,[new StrategyDRuntime(symbol,scale,StrategyDSeed(open),"scale")],scale.DayTrading.Risk);scaleEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(scaleEngine);Feed(scaleEngine,4,9950,5);
        var firstQty=scaleEngine.Snapshot().Rows[0].Quantity;Feed(scaleEngine,5,9400,6);
        Check(scalePaper.Orders.Count(o=>o.Active&&o.Buy)==1&&scalePaper.Orders[^1].Reason=="Entry2","configured extra entry uses last actual fill drop and is independent from a disabled support stop");
        Feed(scaleEngine,6,9400,7);Check(scaleEngine.Snapshot().Rows[0].Entries==2&&scaleEngine.Snapshot().Rows[0].Quantity>firstQty,"additional entry state advances only on the confirmed second order fill");
        var supportScale=scale.Snapshot();supportScale.Configuration.RequireSupportForAdditionalEntry=true;
        var supportPaper=new DayTradingPaperAdapter("KRW",200000,Execution());var supportEngine=new DayTradingPortfolioEngine(supportPaper,[new StrategyDRuntime(symbol,supportScale,StrategyDSeed(open),"support-scale")],supportScale.DayTrading!.Risk);supportEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(supportEngine);Feed(supportEngine,4,9950,5);Feed(supportEngine,5,9400,6);
        Check(supportPaper.Orders.Count==1,"extra-entry drop alone is insufficient when independent support confirmation is enabled");
        Check(engine.ExitSnapshots.Count>=3&&engine.ExitSnapshots.Values.Any(v=>v.Contains("TrailingStop")&&v.Contains("EntryZoneLow")),"sell requests retain original support, peak, average and triggered stop snapshot");
        var unknownAdapter=new LiveGuardAdapter(new DayTradingPaperAdapter("KRW",200000,Execution())){Real=false,ThrowSubmit=true};
        var unknownEngine=new DayTradingPortfolioEngine(unknownAdapter,[Runtime("unknown")],definition.DayTrading.Risk);unknownEngine.SetEntriesEnabled(true,open.AddTicks(-1));Approach(unknownEngine);Feed(unknownEngine,4,9960,5);
        Check(unknownAdapter.Submits==1&&unknownEngine.Snapshot().NeedsReconciliation,"unknown submit response blocks account retries and requires explicit execution reconciliation");
        var isolatedAdapter=new DayTradingPaperAdapter("KRW",200000,Execution());
        var isolated=new DayTradingPortfolioEngine(isolatedAdapter,[new FaultingRuntime(Runtime("bad")),Runtime("good","000660")],definition.DayTrading.Risk);isolated.SetEntriesEnabled(true,open.AddTicks(-1));Approach(isolated);Approach(isolated,"000660");
        Check(isolated.Snapshot().Rows.Single(r=>r.Symbol=="KRX:005930").Error.Contains("intentional")&&isolatedAdapter.Orders.Count==1&&isolatedAdapter.Orders[0].Symbol=="KRX:000660","a failed strategy symbol preserves its error while unrelated symbols continue to valid orders");
    }
    private sealed class LiveGuardAdapter(IDayTradingExecutionAdapter paper):IDayTradingExecutionAdapter
    {
        public int Submits;public bool Real=true,ThrowSubmit;public bool IsLive=>Real;public bool Synchronized=>paper.Synchronized;public string Currency=>paper.Currency;public decimal Cash=>paper.Cash;public decimal Equity=>paper.Equity;public decimal ReservedCash=>paper.ReservedCash;
        public IReadOnlyDictionary<string,Position> Positions=>paper.Positions;public IReadOnlyList<ManagedOrder> Orders=>paper.Orders;public IReadOnlyList<Fill> Fills=>paper.Fills;
        public void OnMarket(MarketEvent market)=>paper.OnMarket(market);public void Submit(ManagedOrder order){Submits++;if(ThrowSubmit)throw new InvalidOperationException("response unknown");paper.Submit(order);}public void Cancel(ManagedOrder order,DateTimeOffset now)=>paper.Cancel(order,now);
    }
    private sealed class FaultingRuntime(IDayTradingSignalRuntime inner):IDayTradingSignalRuntime
    {
        public Instrument Instrument=>inner.Instrument;public DayTradingExecutionProfile Profile=>inner.Profile;public Stock.Core.Research.Oversold.DailyMarketSession? CurrentSession=>inner.CurrentSession;public decimal CurrentPrice=>0;public int AnalysisBarOrdinal=>0;public bool ClosedAnalysis=>false;public string State=>"error";
        public FeedObservation OnMarket(MarketEvent market,DateTimeOffset now)=>throw new FormatException("intentional symbol fault");
        public IntradayEntrySignal? Evaluate(IntradayExecutionContext context)=>null;
    }
}
