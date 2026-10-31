namespace Stock.Core.Research.Oversold;
public enum OversoldState { Idle,CrashTriggered,SupportApproached,WaitingForRebound,Entry1,Entry2,Entry3,Holding,PartialTP,Exited,Cooldown }
public sealed record OversoldTransition(DateTimeOffset Time,OversoldState State,string Reason);
public sealed record OversoldSignal(DateTimeOffset SignalTime,string Action,string Reason,DailyIndicators Indicators,SupportCluster? Cluster,OversoldState State,OversoldDecisionSnapshot? Snapshot=null);
public sealed record OversoldExecution(DateTimeOffset SignalTime,DateTimeOffset ExecutionTime,bool Buy,int Quantity,decimal Price,decimal Fee,string Reason,decimal AveragePriceAfter,decimal RealizedPnl=0,OversoldDecisionSnapshot? Snapshot=null,string ExecutionMode="Backtest");
public sealed record OversoldDecision(DateOnly Date,OversoldState State,string Reason,DailyIndicators Indicators,SupportCluster? Cluster,decimal? ReboundLow);
public sealed class OversoldTrade
{
    public string Symbol {get;set;}="";public DateOnly TriggerDate {get;set;} public string TriggerType {get;set;}="";
    public decimal MediumDrawdown {get;set;}public decimal ShortDrawdown {get;set;}public DateTimeOffset EntrySignalDate {get;set;}public DateTimeOffset FirstExecutionDate {get;set;}public DateTimeOffset? LastExecutionDate {get;set;}
    public decimal? EntryRsi {get;set;}public decimal? VolumeRatio {get;set;}public SupportCluster Cluster {get;set;}=null!;public OversoldConfig Configuration {get;set;}=null!;
    public List<OversoldExecution> Executions {get;set;}=[];public decimal AveragePrice {get;set;}public decimal AcquisitionCost {get;set;}public decimal RealizedPnl {get;set;}public decimal? FinalReturnPercent {get;set;}public int HoldingDays {get;set;}public string ExitReason {get;set;}="미청산";public bool Reentry {get;set;}public bool Closed=>LastExecutionDate!=null;
}
public sealed record PerformanceSummary(int Trades,decimal? WinRate,decimal? AverageWinPercent,decimal? AverageLossPercent,decimal? AverageHoldingDays,decimal? ProfitFactor,bool InfiniteProfitFactor,decimal? Expectancy,decimal? ExpectancyPercent,decimal? Cagr,decimal MaxDrawdown,decimal? Sharpe,decimal FinalEquity,int OpenTrades);
/// <summary>Completed round trips and cost-inclusive account equity; not a shared-capital multi-symbol portfolio.</summary>
public static class PerformanceCalculator
{
    public static PerformanceSummary Calculate(decimal initial,IReadOnlyList<BarEquity> curve,IReadOnlyList<OversoldTrade> trades,decimal riskFree,int annualDays,DateTimeOffset? startedAt=null)
    {
        var closed=trades.Where(t=>t.Closed).ToArray();var wins=closed.Where(t=>t.RealizedPnl>0).ToArray();var losses=closed.Where(t=>t.RealizedPnl<0).ToArray();var win=wins.Sum(t=>t.RealizedPnl);var loss=-losses.Sum(t=>t.RealizedPnl);decimal peak=initial,dd=0,prior=initial;var returns=new List<double>();
        foreach(var p in curve){peak=Math.Max(peak,p.Value);if(peak>0)dd=Math.Max(dd,(peak-p.Value)/peak*100);if(prior>0)returns.Add((double)(p.Value/prior-1));prior=p.Value;}
        decimal final=curve.Count>0?curve[^1].Value:initial;decimal? cagr=null,sharpe=null;
        if(curve.Count>1&&initial>0&&final>0){var days=(curve[^1].Time-(startedAt??curve[0].Time)).TotalDays;if(days>0)cagr=Finite((Math.Pow((double)(final/initial),365.25/days)-1)*100);}
        if(returns.Count>1){var dailyRf=Math.Pow(1+(double)riskFree/100,1d/annualDays)-1;var excess=returns.Select(r=>r-dailyRf).ToArray();var mean=excess.Average();var variance=excess.Sum(r=>(r-mean)*(r-mean))/(excess.Length-1);if(variance>0)sharpe=Finite(mean/Math.Sqrt(variance)*Math.Sqrt(annualDays));}
        return new(closed.Length,closed.Length>0?100m*wins.Length/closed.Length:null,wins.Length>0?wins.Average(t=>t.FinalReturnPercent):null,losses.Length>0?losses.Average(t=>t.FinalReturnPercent):null,closed.Length>0?closed.Average(t=>(decimal)t.HoldingDays):null,loss>0?win/loss:null,loss==0&&win>0,closed.Length>0?closed.Average(t=>t.RealizedPnl):null,closed.Length>0?closed.Average(t=>t.FinalReturnPercent):null,cagr,dd,sharpe,final,trades.Count(t=>!t.Closed));
    }
    private static decimal? Finite(double value)=>double.IsFinite(value)&&Math.Abs(value)<(double)decimal.MaxValue?(decimal)value:null;
}
public sealed class OversoldResult
{
    public string Id {get;set;}="oversold-run:"+Guid.NewGuid().ToString("N");public DateTimeOffset At {get;set;}=DateTimeOffset.UtcNow;public StockItem Stock {get;set;}=null!;public DateOnly From {get;set;}public DateOnly To {get;set;}public BarSpecification? BarSpecification {get;set;}public OversoldConfig Configuration {get;set;}=null!;public string DataHash {get;set;}="";public string FactsHash {get;set;}="";public string Source {get;set;}="";
    public List<OversoldTransition> Transitions {get;set;}=[];public List<OversoldTrade> Trades {get;set;}=[];public List<OversoldSignal> Signals {get;set;}=[];public List<OversoldDecision> Decisions {get;set;}=[];public List<BarEquity> Equity {get;set;}=[];public List<string> Notices {get;set;}=[];public PerformanceSummary Performance {get;set;}=null!;public decimal Cash {get;set;}public int Quantity {get;set;}public bool PendingOrderAtEnd {get;set;}
    public string Title=>$"{At.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm} · {Stock.Name} · 국내 과매도·낙주 · {BarSpecification?.ToString()??"일봉"}";
    public SavedBacktest ToHistory()=>new(){Id="chart-run:"+Id,At=At,Stock=Stock,Period=$"국내 과매도·낙주 · {BarSpecification?.ToString()??"일봉"} next-open",DataHash=DataHash,Initial=Configuration.InitialCapital,Cash=Cash,Quantity=Quantity,Profit=Performance.FinalEquity-Configuration.InitialCapital,MaxDrawdown=Performance.MaxDrawdown,Fills=Trades.SelectMany(t=>t.Executions).Select(e=>new BarFill(e.ExecutionTime,e.Buy?"매수":"매도",e.Quantity,e.Price,e.Fee,e.RealizedPnl,e.Reason)).ToArray(),Equity=Equity.ToArray(),OversoldResultId=Id};
}
