using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void OversoldScreeningTests()
    {
        var c=DailyConfig();c.MinimumSupportCount=1;c.MovingAveragePeriods="1";c.UsePivotSupport=c.UseApproximateVolumeZone=false;
        var facts=DailyFacts(6);PriceBar[] prices=[DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,81,high:100),DailyBar(4,82,high:100),DailyBar(5,83,high:110)];
        var bars=prices.Select((p,i)=>new StrategyBar(p,DailyTime(i,9,0),p.Time,i,4_000_000_000,"TEST_FIXTURE")).ToArray();var engine=new OversoldScreeningEngine();
        var row=engine.Evaluate(DailyStock,bars,c,facts,DailyTime(100));Check(row.Candidate&&row.Evaluation!.Conditions.Single(x=>x.Condition=="RSI").Outcome==ConditionOutcome.Disabled,"screening uses shared detector with disabled RSI explicitly passing");
        var start=OversoldScreeningRun.Start([DailyStock,DailyStock],c,new(100,BarUnit.Tick),DailyTime(100));var run=start.Append(row) with{Status="완료"};
        Check(start.Results.Length==0&&run.Targets.Length==1&&run.FinalCandidates.Length==1,"immutable screening run deduplicates targets and appends without mutation");c.MinimumDrawdownPercent=-80;Check(run.Configuration.MinimumDrawdownPercent!=-80,"saved config is a deep snapshot");var read=run.Configuration;read.MinimumDrawdownPercent=-90;Check(run.Configuration.MinimumDrawdownPercent!=-90,"read config cannot mutate stored snapshot");
        Check(run.Funnel().All(s=>s.Total==1)&&run.Funnel().Single(s=>s.Condition=="RSI") is {Passed:1,Disabled:1},"funnel totals reconcile and disabled filter retains cumulative pass count");
        Check(run.Funnel().Single(s=>s.Condition=="ConfirmedPivotHigh").Diagnostic&&run.FinalCandidates.Length==1,"disabled pivot diagnostic is not an AND gate for MA support");
        Throws<InvalidOperationException>(()=>run.Append(row),"duplicate result cannot change funnel counts");
        using(var db=new TradingStore(System.IO.Path.Combine(Dir,"screening-snapshot.db"))){db.State(run.Id,run);var restored=db.State<OversoldScreeningRun>(run.Id)!;Check(restored.BarSpecification==new BarSpecification(100,BarUnit.Tick)&&restored.ConfigJson==run.ConfigJson&&restored.Results[0].Evaluation!.Selected!.Count==1,"snapshot and detailed condition values survive SQLite reload");}
        var prefix=engine.Evaluate(DailyStock,bars,run.Configuration,facts,DailyTime(3));var trimmed=engine.Evaluate(DailyStock,bars.Take(4).ToArray(),run.Configuration,facts,DailyTime(3));Check(prefix.EvaluationJson==trimmed.EvaluationJson,"future suffix cannot affect historical screening snapshot");
        var from=DailyDates.Of(prices[2]);var to=DailyDates.Of(prices[4]);
        var ranged=engine.Evaluate(DailyStock,bars,run.Configuration,facts,DailyTime(100),from:from,to:to);
        var rangedWithoutFuture=engine.Evaluate(DailyStock,bars.Take(5).ToArray(),run.Configuration,facts,DailyTime(100),from:from,to:to);
        Check(ranged.MatchingDates.All(d=>d>=from&&d<=to)&&ranged.DataTo<=bars[4].ClosedAt&&Serialization.Pack(ranged)==Serialization.Pack(rangedWithoutFuture),"range screening warms indicators before start and ignores future bars");
        var outside=engine.Evaluate(DailyStock,bars,run.Configuration,facts,DailyTime(100),from:new DateOnly(2025,2,1),to:new DateOnly(2025,2,2));
        Check(!outside.Candidate&&outside.Status.Contains("선택 기간"),"empty selected period does not reuse earlier signals");
        Throws<InvalidOperationException>(()=>engine.Evaluate(DailyStock,bars,run.Configuration,facts,DailyTime(100),from:to,to:from),"reversed screening interval rejected");
        var missing=engine.Evaluate(DailyStock,bars.Select(b=>b with{TradingValue=null}).ToArray(),run.Configuration,facts,DailyTime(100));Check(!missing.Candidate&&missing.Evaluation!.Conditions.Single(x=>x.Condition=="거래대금").Outcome==ConditionOutcome.MissingData,"missing exact traded notional is blocked with explicit stage");
        var empty=start.Append(ScreeningObservation.Missing(DailyStock,"자료 없음"));Check(empty.FinalCandidates.Length==0&&empty.Funnel().All(s=>s.Total==1),"zero-candidate run remains viewable and count-consistent");
        var trades=DailyRun([DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,75,open:80),DailyBar(4,71,open:75),DailyBar(5,71),DailyBar(6,60),DailyBar(7,58)]);
        Check(trades.Trades.SelectMany(t=>t.Executions).All(e=>e.Snapshot!=null&&e.SignalTime==e.Snapshot.SignalTime&&e.ExecutionTime>e.SignalTime),"all actual simulation fills retain decision-time snapshot separately from next-open fill");
        Check(trades.Trades[0].Executions.Where(e=>e.Buy).Skip(1).All(e=>e.Snapshot!.AdditionalEntry is {PricePass:true,SupportPass:true,FinalDecision:true}),"additional entry snapshot preserves previous fill, threshold and support checks");
        Check(trades.Trades[0].Executions.Last().Snapshot!.PositionRules is {Exit:"SupportBreak",FirstSupportLower:70},"support exit snapshot keeps initial frozen lower boundary");
        var tracker=new TickCoverageTracker();var first=Trade(0,2,10,10);tracker.Observe(first);tracker.Observe(Trade(1,2,10,20));Check(tracker.Current is {Complete:true,FirstSequence:0,LastSequence:1},"capture coverage proves individual-versus-cumulative continuity");tracker.Observe(Trade(2,2,10,50));Check(tracker.Current==null,"gap ends verified capture instead of bridging missing ticks");
    }
}
