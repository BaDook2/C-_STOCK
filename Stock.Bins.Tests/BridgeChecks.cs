using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void BridgeChecks()
    {
        var config=Config();var profile=StrategyProfile.Create() with{SelectedSystem=StrategySystem.Bin,Bin=config};var transport=profile.ExecutionConfiguration();
        var rows=BreakoutEvents().Select((r,n)=>r with{Event=r.Event with{BinSession=r.Session,BinMetadata=r.Metadata,BinContinuityVerified=r.ContinuityVerified,BinCompletedBars=n==0?TrendBars():[]}}).ToArray();
        var stock=new StockItem(Us.Symbol,"Bin fixture",Us.Exchange,Us.Symbol);var date=Session().TradingDate;
        var result=BinsResearchBridge.Run(stock,transport,date,date,rows,CancellationToken.None);
        Check("UI-BRIDGE",result.BinsResult is {Orders.Length:1,SyntheticFixture:true}&&result.InjesResult==null&&result.Trades.Count==1&&result.Trades[0].Executions.All(e=>e.BinsEvidence!=null),"Bin UI backtest routes to Bin replay, retaining native evidence and fixture identity");
        var screen=BinsResearchBridge.Screen(stock,transport,rows,CancellationToken.None);
        Check("UI-BRIDGE",screen.Candidate&&screen.BinsEvaluation?.Signal?.Kind=="ScreenMatched"&&screen.InjesEvaluation==null,"Bin screening uses original pattern/risk approval without Inje fallback");
        var account=new BinsPaperExecution("USD",10000);var engine=new BinsStrategyEngine(config,[Us],[account],evaluationOnly:true);foreach(var bar in TrendBars())engine.LoadCompletedBar(bar,bar.KnownAt);foreach(var r in rows)Push(engine,r);
        Check("UI-BRIDGE",engine.Decisions.Any(d=>d.Kind=="ScreenMatched")&&account.Orders.Count==0&&account.Fills.Count==0,"screening evaluation cannot submit or fill even a paper order");
        var missing=BinsResearchBridge.Screen(stock,transport,rows.Select(r=>r with{Session=null,Metadata=null}).ToArray(),CancellationToken.None);
        Check("UI-BRIDGE",!missing.Candidate&&missing.BinsEvaluation?.Outcomes[0]==ConditionOutcome.MissingData,"missing provider session/rules stays MissingData");
        var futureRows=rows.Select((r,n)=>n==0?r with{Event=r.Event with{BinCompletedBars=r.Event.BinCompletedBars.Concat([TrendBars()[0] with{KnownAt=Epoch.AddDays(1)}]).ToArray()}}:r).ToArray();
        var futureScreen=BinsResearchBridge.Screen(stock,transport,futureRows,CancellationToken.None);
        Check("UI-BRIDGE",System.Text.Json.JsonSerializer.Serialize(futureScreen.BinsEvaluation,BinsJson.Options)==System.Text.Json.JsonSerializer.Serialize(screen.BinsEvaluation,BinsJson.Options),"future attached bar cannot change screening signal or final state");
        var profileJson=StrategyProfileJson.Export(profile);Check("UI-PROFILE",StrategyProfileJson.Export(StrategyProfileJson.Import(profileJson))==profileJson,"both systems and all Inje algorithms strictly roundtrip in one profile");
        Throws("UI-PROFILE",()=>{var mixed=transport.Clone();mixed.Injes=profile.InjeStrategies[0];mixed.Validate();},"mixed execution transport is rejected");
        File.WriteAllText(Path.Combine(output,"ui-bridge-result.json"),System.Text.Json.JsonSerializer.Serialize(result.BinsResult,BinsJson.Options));
    }
}
