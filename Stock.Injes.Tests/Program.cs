using System.Text.Json;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;
using Signal=Stock.Core.Strategies.Injes.StrategySignal;

internal static partial class Program
{
    private static int passed;
    private static readonly DateTimeOffset Epoch=DateTimeOffset.Parse("2026-09-25T13:30:00Z");
    private static readonly Instrument Stock=new("TEST",Exchange.NASDAQ);
    private static void Check(bool value,string why){if(!value)throw new Exception("FAIL: "+why);Console.WriteLine("PASS: "+why);passed++;}
    private static void Throws(Action action,string why){try{action();}catch(Exception e)when(e is InvalidOperationException or JsonException){Check(true,why);return;}throw new Exception("FAIL expected error: "+why);}
    private static void SetParameters<T>(InjesStrategyDefinition d,Action<T> edit) where T:class
    {var p=d.StrategyParameters.Deserialize<T>(InjesStrategyJson.Options)!;edit(p);d.StrategyParameters=JsonSerializer.SerializeToElement(p,InjesStrategyJson.Options);d.RefreshEffective();}
    private static InjesObservation Observation(int n,decimal price=2,decimal quantity=100,bool quote=false,decimal? bid=null,decimal? ask=null)
    {
        var at=Epoch.AddMilliseconds(n*250);var session=UsSessionClock.Bounds(Epoch,UsMarketPhase.Regular,true) with{AllowedOrderKinds=[OrderKind.Limit,OrderKind.Market]};
        var e=new MarketEvent(Stock,quote?EventKind.Quote:EventKind.Trade,at,at,"TEST",n){TradePrice=quote?null:price,TradeSize=quote?null:quantity,Bid=quote?bid??1.99m:null,Ask=quote?ask??2.01m:null,BidSize=quote?1000:null,AskSize=quote?1000:null};
        return new(e,at,session,new(at){PreviousClose=2,SessionOpen=2,TickSize=.01m,DailyTradingValue=1000000,AverageDailyTradingValue=500000,FundamentalRisk=FundamentalRiskState.Cleared,Halted=false,MarketCap=1000000,Float=1000000});
    }
    private static InjesFeatures Features(decimal price=2,decimal gain=12,decimal rvol=6,decimal reacceleration=2,decimal? momentumStart=null)
    {
        var windows=new Dictionary<int,WindowFeatures>{{120,new(true,120,20000,momentumStart??price/1.05m,price,price,1)},{10,new(true,40,2000,price,price,price,1)},{40,new(true,100,5000,price,price,price,1)},{3,new(true,12,600,price*1.1m,price,price,1)},{6,new(true,24,800,price*1.1m,price,price,1)},{30,new(true,120,12000,price,price,price,1)}};
        return new(Epoch,price,gain,price,price-.01m,price+.01m,1000,500,.01m,1,2,2,reacceleration,rvol,windows,new(Epoch){PreviousClose=4,SessionOpen=3,TickSize=.01m,DailyTradingValue=1000000,FundamentalRisk=FundamentalRiskState.Cleared},UsSessionClock.Bounds(Epoch,UsMarketPhase.Regular,true),new(Stock,EventKind.Quote,Epoch,Epoch,"TEST",1){Bid=price-.01m,Ask=price+.01m,BidSize=1000,AskSize=500},Epoch,Epoch,true,true);
    }
    private static StrategyIntent Evaluate(IStrategyRuntime runtime,decimal price,int seconds,InjesFeatures? features=null,Position? position=null,Signal? entry=null)
    {var at=Epoch.AddSeconds(seconds);var e=Observation(seconds*4,price).Market;var f=(features??Features(price)) with{At=at,Price=price,LastTradeAt=at,LastQuoteAt=at};return runtime.Evaluate(new(Stock,f,e,position,false,entry,at));}
    private static int Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--catalog"){WriteCatalog(args[1]);return 0;}
        if(args.Length==2&&args[0]=="--validate-guide"){ValidateGuide(args[1]);return 0;}
        if(args.Length==2&&args[0]=="--verify-manifest"){ValidateManifest(args[1]);return 0;}
        try{DefinitionChecks();FeatureChecks();MomentumChecks();CrashChecks();QuoteChecks();SupportChecks();StrategyIntegrationChecks();ExecutionChecks();BoundaryChecks();CausalityChecks();ReplayChecks();RetirementChecks();StreamChecks().GetAwaiter().GetResult();Console.WriteLine($"PASS: {passed} Inje checks; synthetic data, isolated paper ledger, no account/API calls");return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    private static void DefinitionChecks()
    {
        Check(StrategyRegistry.Injes.All.Length==4,"exactly four independent Inje registrations");
        Check(typeof(InjesCommonParameters).GetProperties().Length==14,"exactly fourteen common strategy parameters");
        var expected=new[]{10,12,11,13};int index=0;
        foreach(var factory in StrategyRegistry.Injes.All)
        {
            var d=InjesStrategyDefinition.Create(factory.StrategyId,Epoch);d.Validate();
            Check(d.Metadata().Length==expected[index++],factory.StrategyId+" has the required advanced parameter count");
            Check(InjesStrategyJson.Export(InjesStrategyJson.Import(InjesStrategyJson.Export(d)))==InjesStrategyJson.Export(d),factory.StrategyId+" strict JSON roundtrip preserves every field");
            d.StrategyMode=StrategyMode.Standard;d.StandardParameters[factory.StandardRules[0].Key]=5;d.RefreshEffective();d.Validate();
            Check(d.EffectiveAdvancedParameters.GetProperty(JsonNamingPolicy.CamelCase.ConvertName(factory.StandardRules[0].AdvancedKey)).GetDecimal()==factory.StandardRules[0].Values[4],factory.StrategyId+" Standard is an explicit effective numeric mapping");
            d.StrategyMode=StrategyMode.Advanced;d.RefreshEffective();Check(JsonElement.DeepEquals(d.StrategyParameters,d.EffectiveAdvancedParameters),factory.StrategyId+" Advanced custom values survive mode switch");
            var editor=new InjesEditorModel(d);
            foreach(var field in editor.CommonFields.Concat(editor.ExecutionFields).Concat(editor.StrategyFields))
            {
                if(field.IsBool)field.BoolValue=!field.BoolValue;
                else if(field.IsChoice)field.Text=field.Choices.Last().Value;
                else if(field.IsList)field.Text=field.Path.EndsWith("MovingAveragePeriods")?"20,240":"MA,Pivot";
                else if(field.Text=="")field.Text="1000.1234";
                else field.Text=(decimal.Parse(field.Text,System.Globalization.CultureInfo.InvariantCulture)+(field.Metadata.InputKind==ParameterInputKind.Integer?1:.0123m)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            var edited=editor.Apply(d.StrategyName,Epoch.AddSeconds(1));var imported=InjesStrategyJson.Import(InjesStrategyJson.Export(edited));var reloaded=new InjesEditorModel(imported);
            var before=editor.CommonFields.Concat(editor.ExecutionFields).Concat(editor.StrategyFields).ToDictionary(f=>f.Path,f=>f.Text);
            foreach(var field in reloaded.CommonFields.Concat(reloaded.ExecutionFields).Concat(reloaded.StrategyFields))Check(before[field.Path]==field.Text,factory.StrategyId+" edited field matches import: "+field.Path);
            var integer=reloaded.CommonFields.Single(f=>f.Path.EndsWith("MaxConcurrentPositions"));integer.Text="1.5";Check(integer.Error!="",factory.StrategyId+" integer inputs reject fractional counts");
        }
        var original=InjesStrategyDefinition.Create("momentum-pullback",Epoch);var json=InjesStrategyJson.Export(original);
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"familyId\": \"inje\"","\"familyId\": \"bin\"")),"Bin family cannot silently execute Inje");
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"momentum-pullback\"","\"not-supported\"")),"unknown strategy ID rejected");
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"Advanced\"","\"not-a-mode\"")),"unknown enum rejected");
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"minPrice\": 1,","\"minPrice\": 1, \"MinPrice\": 2,")),"nested case-insensitive duplicate key rejected");
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"minPrice\": 1,","\"minPrice\": 1, \"misspelled\": 2,")),"unknown common field rejected");
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"minPrice\": 1,","")),"missing common parameter rejected instead of using a default");
        Throws(()=>InjesStrategyJson.Import(json.Replace("\"maxPrice\": 5","\"maxPrice\": 0")),"out-of-range input rejected");
        original.ExecutionPolicy.LiveOrdersEnabled=true;Throws(original.Validate,"live orders cannot be enabled in JSON");
        var otherRegistry=new StrategyRegistry("bin");Check(otherRegistry.All.Length==0&&StrategyRegistry.Injes.All.Length==4,"new family has an independent registry");
    }
    private static void FeatureChecks()
    {
        var engine=new InjesFeatureEngine(Stock,new(),[3,6,10,40]);
        for(int n=0;n<=40;n++){var e=Observation(n,2+n*.001m,10);Check(engine.Add(e,e.KnownAt).Accepted,"ordered trade admitted "+n);}
        var last=Observation(40,2.04m,10);Check(engine.Add(last,last.KnownAt).Duplicate,"duplicate tick is rejected without advancing features");
        var f=engine.Snapshot(last.KnownAt);Check(f.Window(3).Complete&&f.Window(3).Count==12&&f.Window(3).Volume==120,"rolling duration removes boundary trades once");
        Check(f.Momentum(3)>0&&f.Window(3).RealizedVolatility>0,"momentum and log-return volatility use observed trades");
        var future=Observation(41);Check(!engine.Add(future,last.KnownAt).Accepted,"future exchange/receive/known timestamps rejected");
        var delayed=Observation(100);Check(!engine.Add(delayed,delayed.KnownAt.AddSeconds(5)).Accepted,"stale input rejected");
        var gap=Observation(100);Check(engine.Add(gap,gap.KnownAt).Discontinuity&&!engine.Snapshot(gap.KnownAt).Window(3).Complete,"trade gap resets rolling coverage");
        Check(f.Rvol==null,"no historical same-time cumulative volume means MissingData RVOL");
        var points=Enumerable.Range(1,20).Select(i=>new SessionVolumePoint("old"+i,new DateOnly(2026,9,25).AddDays(-i),UsMarketPhase.Regular,0,100,Epoch.AddDays(-i))).ToArray();
        var first=Observation(0);var good=new InjesFeatureEngine(Stock,new(),[3]);var rich=first with{Reference=first.Reference! with{CumulativeSessionVolume=500,HistoricalSessionVolumes=points}};good.Add(rich,rich.KnownAt);
        Check(good.Snapshot(rich.KnownAt).Rvol==5,"RVOL compares twenty prior same-phase same-elapsed-time cumulative volumes");
        var quoteEngine=new InjesFeatureEngine(Stock,new(),[3]);
        for(int n=0;n<16;n++){var q=Observation(n,quote:true);quoteEngine.Add(q,q.KnownAt);}
        Check(quoteEngine.Snapshot(Epoch.AddSeconds(3.75),quoteWindowSeconds:3).QuoteChangesPerSecond==0,"quantity-only/same-price quotes do not count as quote-price changes");
        var qnext=Observation(16,quote:true,bid:2,ask:2.02m);quoteEngine.Add(qnext,qnext.KnownAt);Check(quoteEngine.Snapshot(qnext.KnownAt,quoteWindowSeconds:3).QuoteChangesPerSecond==1m/3,"actual best-price changes counted once");
        var winter=UsSessionClock.Bounds(DateTimeOffset.Parse("2026-12-01T15:00:00Z"),UsMarketPhase.Regular);
        Check(winter.Open.Hour==9&&winter.Open.Offset==TimeSpan.FromHours(-5)&&!winter.TradingAllowed,"winter DST bounds do not assume provider session permission");
        Check(UsSessionClock.Bounds(Epoch,UsMarketPhase.Regular).Open.Offset==TimeSpan.FromHours(-4),"summer Eastern daylight offset");
    }
    private static void MomentumChecks()
    {
        var d=InjesStrategyDefinition.Create("momentum-pullback",Epoch);SetParameters<MomentumPullbackParameters>(d,p=>p.LowStabilitySeconds=2);var r=StrategyRegistry.Injes.Get(d.StrategyId).Create(d);
        Check(Evaluate(r,2.2m,0).Type==SignalType.Hold,"momentum candidate registration never buys on registration tick");
        Evaluate(r,2.3m,1);Evaluate(r,2.2m,2);Evaluate(r,2.19m,3);
        Check(Evaluate(r,2.192m,4).Type==SignalType.Hold,"new low restarts required stability time");
        Check(Evaluate(r,2.20m,5).Type==SignalType.Enter,"ordered high, >=3% pullback, stable low, new rebound and volume re-entry");
        Check(Evaluate(r,2.21m,6).Type==SignalType.Hold,"same momentum rebound cannot emit a second entry");
        var lowVolume=StrategyRegistry.Injes.Get(d.StrategyId).Create(d);Evaluate(lowVolume,2.2m,0);Evaluate(lowVolume,2.1m,1);
        Check(Evaluate(lowVolume,2.11m,4,Features(2.11m,reacceleration:1)).Type==SignalType.Hold,"insufficient reacceleration blocks entry");
        var invalidated=Evaluate(lowVolume,2.12m,5,Features(2.12m,gain:0));Check(invalidated.Type==SignalType.CancelEntry,"erasing the observed day gain invalidates the momentum candidate");
        r.ResetCandidate();Check(Evaluate(r,2.3m,100).Type==SignalType.Hold,"session/gap reset starts a fresh candidate without old lows");
        var exact=StrategyRegistry.Injes.Get(d.StrategyId).Create(d);Evaluate(exact,2.2m,0,Features(2.2m,gain:10));
        Check(Evaluate(exact,2.134m,1,Features(2.134m,gain:6.7m)).Reason.Contains("눌림"),"a real +10% surge followed by a 3% pullback remains a pullback candidate");
        Check(Evaluate(exact,2.141m,3,Features(2.141m,gain:7.05m)).Type==SignalType.Enter,"registration gain threshold is not reapplied to invalidate an otherwise valid sequential pullback");
    }
    private static void CrashChecks()
    {
        var d=InjesStrategyDefinition.Create("crash-rebound",Epoch);SetParameters<CrashReboundParameters>(d,p=>{p.CrashLookbackSeconds=3;p.LowStabilitySeconds=2;});
        var factory=StrategyRegistry.Injes.Get(d.StrategyId);var r=factory.Create(d);
        Check(Evaluate(r,2,0).Type==SignalType.Hold,"50% previous-close crash registers a candidate without retrospective bounce");
        Evaluate(r,1.999m,1);Check(Evaluate(r,2,2).Type==SignalType.Hold,"new crash low resets stable seconds");
        Check(Evaluate(r,2,3).Type==SignalType.Hold,"real observed narrow range latches stability before bounce");
        Check(Evaluate(r,2.02m,4).Type==SignalType.Enter,"fresh post-stability rebound cross generates entry");
        Check(Evaluate(r,2.03m,5).Type==SignalType.Hold,"same crash bounce is not reused");
        var broken=factory.Create(d);Evaluate(broken,2,0);Check(Evaluate(broken,1.98m,1).Type==SignalType.CancelEntry,"compare frozen old crash low before updating to detect invalidation");
        var unknown=Features() with{Reference=Features().Reference! with{FundamentalRisk=FundamentalRiskState.Unknown}};
        Check(Evaluate(factory.Create(d),2,0,unknown).Reason.Contains("Unknown"),"missing news provider remains Unknown and cannot clear crash risk");
        SetParameters<CrashReboundParameters>(d,p=>p.CrashReferenceMode=CrashReferenceMode.SessionOpen);
        Check(Evaluate(factory.Create(d),2,0).Reason.Contains("대기"),"session open mode uses its own reference rather than previous close");
        SetParameters<CrashReboundParameters>(d,p=>p.MinCrashVelocity=1);
        Check(Evaluate(factory.Create(d),2,0).Type==SignalType.Hold,"enabled velocity gate requires its full observed threshold");
    }
    private static void QuoteChecks()
    {
        var d=InjesStrategyDefinition.Create("quote-scalping",Epoch);var factory=StrategyRegistry.Injes.Get(d.StrategyId);var r=factory.Create(d);
        var enter=Evaluate(r,2,0);Check(enter.Type==SignalType.Enter&&enter.PlannedPrice==1.99m,"quote strategy generates passive Bid limit, never a fill");
        Check(Evaluate(r,2,1).Type==SignalType.Hold,"quote condition episode emits once until condition resets");
        SetParameters<QuoteScalpingParameters>(d,p=>p.OrderBookDepthLevels=2);
        Check(Evaluate(factory.Create(d),2,0).Reason.Contains("L2"),"L1 is never substituted for required L2");
        var invalid=Features() with{FreshQuote=false,Bid=2.1m,Ask=2};Check(Evaluate(factory.Create(d),2,0,invalid).Type==SignalType.Hold,"crossed/stale quotes block scalping");
        var position=new Position(Stock){Quantity=3,AveragePrice=2,OpenedAt=Epoch};
        var entry=new Signal("inje",d.StrategyId,Stock.Key,"first",SignalType.Enter,2,Epoch,"entry",Epoch,Epoch,Epoch,"TEST",1,[],new Dictionary<string,decimal?>{{"entryVolumeRate",10000},{"feeRate",.0005m},{"taxRate",0},{"executionCost",.002m}},true,[],"1");
        Check(Evaluate(factory.Create(d),2,2,position:position,entry:entry).Reason=="LiquidityDecay","quote liquidity decline creates protective exit rather than another target limit");
        Check(PriceGrid.Buy(1.234m,.01m)==1.23m&&PriceGrid.Sell(1.234m,.01m)==1.24m,"side-aware price normalization uses supplied tick size");
    }
    private static void SupportChecks()
    {
        var bars=new[]{new PriceBar(Epoch.AddDays(-3),2,2.1m,2,2.05m,100000),new PriceBar(Epoch.AddDays(-2),2,2,2,2,200)};
        var bins=InjesSupportCalculator.Allocate(bars,.01m,1000);Check(bins.Sum(b=>b.Volume)==100200,"OHLCV bins preserve total volume including exact boundaries and flat bars");
        Throws(()=>InjesSupportCalculator.Allocate(bars,.0001m,10),"bin memory cap fails explicitly without silently widening a trading parameter");
        var levels=new[]{new InjesSupport("a","MA",2,2,2,Epoch),new InjesSupport("b","Pivot",2.04m,2.04m,2.04m,Epoch),new InjesSupport("c","MA",2.09m,2.09m,2.09m,Epoch)};
        var clusters=InjesSupportCalculator.Cluster(levels,3);Check(clusters.Length==2&&clusters[0].SupportCount==2,"cluster chaining cannot exceed total allowed span");
        var duplicate=InjesSupportCalculator.Cluster([new("vp","ApproximateVolumeZone",2,2,2,Epoch),new("vp","ApproximateVolumeZone",2,2.01m,2.02m,Epoch)],5);Check(duplicate[0].SupportCount==1,"bins from one volume profile count as one independent support");
        var p=new SupportZoneParameters{MovingAveragePeriods="2",PivotLeftBars=1,PivotRightBars=1,VolumeProfileLookbackBars=2};
        var history=new[]{new InjesKnownBar(new(Epoch.AddDays(-3),2,2.1m,1.9m,2,10),Epoch.AddDays(-3),Epoch.AddDays(-3)),new InjesKnownBar(new(Epoch.AddDays(-2),2,2.2m,1.9m,2,10),Epoch.AddDays(-2),Epoch.AddDays(-2)),new InjesKnownBar(new(Epoch.AddDays(-1),2,2.1m,1.9m,2,10),Epoch.AddDays(-1),Epoch.AddDays(1))};
        var data=new InjesReferenceData(Epoch){DailyBars=history,TickSize=.01m};
        Check(!InjesSupportCalculator.Calculate(data,p,Epoch,1000).Any(c=>c.SupportTypes.Contains("Pivot")),"pivot right-hand bar must be completed AND known before use");
        Check(InjesSupportCalculator.Calculate(data with{DailyBars=history.Select(x=>x with{KnownAt=x.ClosedAt}).ToArray()},p,Epoch,1000).Any(c=>c.SupportTypes.Contains("Pivot")),"confirmed pivot appears only after right-bar availability");
        p.SupportSources="MA";p.MovingAveragePeriods="240";Check(InjesSupportCalculator.Calculate(data,p,Epoch,1000).Length==0,"MA240 cannot be fabricated from insufficient daily bars");
    }
}
