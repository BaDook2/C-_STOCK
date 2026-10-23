using Stock.Core.Trading;
namespace Stock.Core.Pipeline;
public sealed record PipelineFeatures(Instrument Instrument,DateTimeOffset At,decimal Price,SymbolObservation? Observation)
{
    public decimal? RankVelocity,DayTurnover,RelativeVolume,FiveMinuteRelativeVolume,DayChange,Gap,Drawdown,VwapDistance,LowDistance,ResistanceDistance,Spread,BidDepth,AskDepth,Imbalance,ExecutionStrength,Volatility,VolumeDecay,Support;
    public decimal VolumeRate;public decimal? BidPrice;
}
public static class PipelineFeatureBuilder
{
    public static PipelineFeatures Build(MarketContext c,PipelineAdvancedConfig config,IReadOnlyList<SymbolObservation> observations)
    {
        var o=observations.Where(r=>r.Symbol==c.Instrument.Key&&r.At<=c.Now&&(c.Now-r.At).TotalSeconds<=config.Universe.UniverseRefreshIntervalSeconds*2L).MaxBy(r=>r.At);
        var f=new PipelineFeatures(c.Instrument,c.Now,c.Price,o);var e=c.Last;
        if(o?.PreviousRank is int old&&o.SearchRank is int rank&&o.PreviousRankAt is {} before&&before<o.At)f.RankVelocity=(old-rank)/(decimal)(o.At-before).TotalMinutes;
        f.DayTurnover=o?.DayTurnover;var volume=e?.CumulativeVolume??o?.DayVolume;var avg=o?.Average20DayVolume??e?.AverageDailyVolume;f.RelativeVolume=avg>0?volume/avg:null;
        if(c.Warm(300)&&o?.SameTime5MinAverageVolume>0)f.FiveMinuteRelativeVolume=c.Window(300).Sum(t=>t.TradeSize??0)/o.SameTime5MinAverageVolume;
        if(e?.PreviousClose>0){f.DayChange=(c.Price/e.PreviousClose-1)*100;if(e.Open>0)f.Gap=(e.Open/e.PreviousClose-1)*100;}
        if(o?.RecentHigh>0&&o.RecentHighDays==config.PriceAction.RecentDrawdownDays)f.Drawdown=Math.Max(0,(1-c.Price/o.RecentHigh.Value)*100);
        if(o?.Vwap>0)f.VwapDistance=(c.Price/o.Vwap-1)*100;
        var levels=SupportResistanceService.Levels(c);f.Support=o?.PreviousLow??levels.Where(p=>p<=c.Price).Select(p=>(decimal?)p).Max();
        var resistance=o?.Resistance??levels.Where(p=>p>c.Price).Select(p=>(decimal?)p).Min();
        if(f.Support>0)f.LowDistance=Math.Abs(c.Price/f.Support.Value-1)*100;if(resistance>0)f.ResistanceDistance=(resistance/c.Price-1)*100;
        if(c.QuoteFresh&&c.Quote is {} q){f.BidPrice=q.Bid;f.BidDepth=q.Bids?.Sum(b=>b.Size)??q.BidSize;f.AskDepth=q.Asks?.Sum(b=>b.Size)??q.AskSize;f.Spread=q.Bid>0&&q.Ask>0?(q.Ask-q.Bid)/((q.Ask+q.Bid)/2)*100:null;if(f.BidDepth+f.AskDepth>0)f.Imbalance=(f.BidDepth-f.AskDepth)/(f.BidDepth+f.AskDepth);}
        var minute=c.Window(60);if(c.Warm(60)&&minute.Count>1){var returns=minute.Zip(minute.Skip(1),(a,b)=>Math.Log((double)(b.TradePrice!.Value/a.TradePrice!.Value)));f.Volatility=(decimal)Math.Sqrt(returns.Sum(r=>r*r))*100;if(minute.All(t=>t.BuyerInitiated!=null)){var buy=minute.Where(t=>t.BuyerInitiated==true).Sum(t=>t.TradeSize??0);var sell=minute.Where(t=>t.BuyerInitiated==false).Sum(t=>t.TradeSize??0);f.ExecutionStrength=sell>0?buy/sell*100:buy>0?100000:null;}}
        f.VolumeRate=c.Rate(5);if(c.Warm(10)&&c.Rate(5,5)>0)f.VolumeDecay=f.VolumeRate/c.Rate(5,5);return f;
    }
}
public sealed record PipelineStageResult(string Stage,bool Passed,decimal Score,string Reason,bool Missing=false);
public interface IPipelineFilter { PipelineStageResult Evaluate(PipelineFeatures f,PipelineAdvancedConfig c); }
public static class FilterScore
{
    public static decimal Minimum(decimal value,decimal target)=>target<=0?100:Math.Clamp(value/target*50,0,100);
    public static decimal Maximum(decimal value,decimal target)=>value<=0?100:Math.Clamp(target/value*50,0,100);
    public static decimal Range(decimal value,decimal min,decimal max)=>value<min||value>max?0:max==min?100:50+50*(1-Math.Abs(2*(value-min)/(max-min)-1));
    public static PipelineStageResult Result(string stage,IEnumerable<(string Name,decimal? Value,decimal Min,decimal Max)> checks,decimal threshold)
    {
        var rows=checks.ToArray();var missing=rows.Where(r=>r.Value==null).Select(r=>r.Name).ToArray();if(missing.Length>0)return new(stage,false,0,"자료 없음: "+string.Join(", ",missing),true);
        var failures=rows.Where(r=>r.Value<r.Min||r.Value>r.Max).Select(r=>r.Name).ToArray();var score=rows.Average(r=>r.Max==decimal.MaxValue?Minimum(r.Value!.Value,r.Min):Range(r.Value!.Value,r.Min,r.Max));
        return new(stage,failures.Length==0&&score>=threshold,score,failures.Length>0?string.Join(", ",failures)+" 기준 미충족":score<threshold?"종합 점수 미충족":"통과");
    }
}
public sealed class UniverseFilter:IPipelineFilter
{
    public PipelineStageResult Evaluate(PipelineFeatures f,PipelineAdvancedConfig c)
    {
        var u=c.Universe;var o=f.Observation;
        if(u.Market==PipelineMarket.US&&f.Instrument.Exchange==Exchange.KRX||u.Market==PipelineMarket.Korea&&f.Instrument.Exchange!=Exchange.KRX)return new("Universe",false,0,"시장 필터 제외");
        if(o==null)return new("Universe",false,0,"자료 없음 또는 갱신 주기 초과",true);
        if(o.IsOTC&&!u.IncludeOTC)return new("Universe",false,0,"OTC 제외");
        if(!(u.UseWatchlistSymbols&&o.IsWatchlist||u.UseTopSearchSymbols&&o.SearchRank is >0&&o.SearchRank<=u.TopRankCutoff))return new("Universe",false,0,"관심종목/검색 상위 후보 아님");
        return FilterScore.Result("Universe",[("가격",f.Price,u.MinPrice,u.MaxPrice),("시가총액",o.MarketCap,u.MinMarketCap,u.MaxMarketCap)],0);
    }
}
public sealed class AttentionFilter:IPipelineFilter
{
    public PipelineStageResult Evaluate(PipelineFeatures f,PipelineAdvancedConfig c)
    {
        var a=c.Attention;var checks=new List<(string,decimal?,decimal,decimal)>{("당일 거래대금",f.DayTurnover,a.DayTurnoverMin,decimal.MaxValue),("20일 상대 거래량",f.RelativeVolume,a.DayVolumeTo20DayAverageMin,decimal.MaxValue),("동시간대 거래량",f.FiveMinuteRelativeVolume,a.Intraday5MinVolumeVsSameTimeAverageMin,decimal.MaxValue)};
        if(c.Universe.UseTopSearchSymbols){checks.Add(("검색 상승속도",f.RankVelocity,a.SearchRankVelocityMin,decimal.MaxValue));checks.Add(("현재 순위",f.Observation?.SearchRank,1,a.CurrentSearchRankMax));}
        return FilterScore.Result("Attention Filter",checks,a.AttentionScoreThreshold);
    }
}
public sealed class PriceActionFilter:IPipelineFilter
{
    public PipelineStageResult Evaluate(PipelineFeatures f,PipelineAdvancedConfig c)
    {var p=c.PriceAction;return FilterScore.Result("Price Action",[("당일 등락",f.DayChange,p.DayChangePercentMin,p.DayChangePercentMax),("갭",f.Gap,p.GapPercentMin,p.GapPercentMax),("최근 낙폭",f.Drawdown,p.RecentNDayDrawdownMin,p.RecentNDayDrawdownMax),("VWAP 거리",f.VwapDistance,p.VwapDeviationPercentMin,p.VwapDeviationPercentMax),("전저점 거리",f.LowDistance,0,p.DistanceToPrevLowMax),("저항선 여유",f.ResistanceDistance,p.DistanceToResistanceMin,decimal.MaxValue)],p.PriceActionScoreThreshold);}
}
public sealed class MicrostructureFilter:IPipelineFilter
{
    public PipelineStageResult Evaluate(PipelineFeatures f,PipelineAdvancedConfig c)
    {var m=c.Microstructure;return FilterScore.Result("Microstructure",[("스프레드",f.Spread,0,m.MaxBidAskSpreadPercent),("매수잔량",f.BidDepth,m.MinBidDepth,decimal.MaxValue),("매도잔량",f.AskDepth,m.MinAskDepth,decimal.MaxValue),("호가 불균형",f.Imbalance,m.MinOrderBookImbalance,1),("체결강도",f.ExecutionStrength,m.ExecutionStrengthMin,decimal.MaxValue),("1분 변동성",f.Volatility,m.RealizedVolatility1mMin,m.RealizedVolatility1mMax),("거래량 유지",f.VolumeDecay,m.VolumeDecayRateThreshold,decimal.MaxValue)],m.MicrostructureScoreThreshold);}
}
public sealed record PipelineEvaluation(PipelineFeatures Features,IReadOnlyList<PipelineStageResult> Stages)
{
    public bool Passed=>Stages.All(s=>s.Passed);
    public decimal Score=>Stages.Skip(1).Average(s=>s.Score);
    public string Summary=>string.Join(" → ",Stages.Select(s=>$"{s.Stage}: {s.Reason} ({s.Score:N0})"));
}
public sealed class PipelineEvaluator
{
    private readonly IPipelineFilter[] filters=[new UniverseFilter(),new AttentionFilter(),new PriceActionFilter(),new MicrostructureFilter()];
    public PipelineEvaluation Evaluate(MarketContext context,PipelineAdvancedConfig config,IReadOnlyList<SymbolObservation> observations)
    {var f=PipelineFeatureBuilder.Build(context,config,observations);return new(f,filters.Select(x=>x.Evaluate(f,config)).ToArray());}
}
public sealed record PipelineOrder(bool Buy,int Quantity,decimal Price,OrderKind Kind,string Owner,string Reason,int TimeoutMilliseconds);
public interface IPipelineStrategy { PipelineOrder? Evaluate(PipelineEvaluation evaluation,PipelineAdvancedConfig config,PaperBroker broker); }
public sealed class TickScalpingPipelineStrategy:IPipelineStrategy
{
    private readonly Dictionary<string,(DateTimeOffset Open,decimal Rate,decimal? Support)> entries=[];
    public PipelineOrder? Evaluate(PipelineEvaluation e,PipelineAdvancedConfig c,PaperBroker broker)
    {
        var f=e.Features;var s=c.TickScalping;var key=f.Instrument.Key;var p=broker.Positions.GetValueOrDefault(key);const string name="Pipeline:틱떼기";
        if(p?.Quantity>0)
        {
            if(p.Owner!=name)return null;
            if(!entries.TryGetValue(key,out var state)||state.Open!=p.OpenedAt)entries[key]=state=(p.OpenedAt??f.At,f.VolumeRate,f.Support);
            var pnl=(f.Price/p.AveragePrice-1)*100;var reason=pnl>=s.TakeProfitPercent?"익절":pnl<=s.StopLossPercent?"손절":(f.At-state.Open).TotalSeconds>=s.MaxHoldSeconds?"보유시간":s.LiquidityDropExit&&state.Rate>0&&f.VolumeRate/state.Rate<=s.VolumeDecayExitThreshold?"유동성 감소":s.SupportBreakExit&&state.Support>0&&f.Price<state.Support*(1-s.SupportBreakPercent/100)?"지지 이탈":null;
            return reason==null?null:new(false,p.Quantity,f.Price,OrderKind.Market,name,reason,s.TickOrderTimeoutMilliseconds);
        }
        entries.Remove(key);if(!e.Passed||e.Score<s.EntryScoreThreshold)return null;
        var amount=Math.Min(broker.Equity*c.Risk.MaxPositionPercent/100,broker.Equity*c.Portfolio.MaxPennyPortfolioPercent/100);var qty=(int)Math.Min(int.MaxValue,decimal.Floor(amount/f.Price));
        return new(true,qty,(c.Execution.OrderType==OrderKind.Limit?f.BidPrice??f.Price:f.Price)+(s.LimitEntryOffsetTicks+c.Execution.LimitOrderOffsetTicks)*f.Instrument.Tick(f.Price),c.Execution.OrderType,name,"틱 진입",s.TickOrderTimeoutMilliseconds);
    }
}
public sealed class FarmerPipelineStrategy:IPipelineStrategy
{
    private readonly Dictionary<string,(DateTimeOffset Open,decimal Peak)> peaks=[];
    public PipelineOrder? Evaluate(PipelineEvaluation e,PipelineAdvancedConfig c,PaperBroker broker)
    {
        var f=e.Features;var s=c.Farmer;var key=f.Instrument.Key;var p=broker.Positions.GetValueOrDefault(key);const string name="Pipeline:농부매매";
        PipelineOrder Order(bool buy,int qty,string reason)=>new(buy,qty,f.Price+(buy?1:-1)*c.Execution.LimitOrderOffsetTicks*f.Instrument.Tick(f.Price),buy?c.Execution.OrderType:OrderKind.Market,name,reason,c.Execution.OrderTimeoutMilliseconds);
        var initial=(int)Math.Min(int.MaxValue,decimal.Floor(broker.Equity*s.InitialPositionPercent/100/f.Price));
        if(p?.Quantity>0)
        {
            if(p.Owner!=name)return null;var opened=p.OpenedAt??f.At;
            var peak=peaks.TryGetValue(key,out var old)&&old.Open==opened?Math.Max(old.Peak,f.Price):Math.Max(p.AveragePrice,f.Price);peaks[key]=(opened,peak);
            var profit=(f.Price/p.AveragePrice-1)*100;var hold=e.Stages.Where(s=>s.Stage is "Price Action" or "Microstructure").ToArray();var known=hold.All(s=>!s.Missing);var score=hold.Average(s=>s.Score);
            if(f.Price<=peak*(1-s.TrailingStopPercent/100)||(f.At-opened).TotalDays>=s.FarmerMaxHoldDays||known&&score<=s.ThesisInvalidationScoreThreshold)return Order(false,p.Quantity,"농부 가설 훼손/추적 손절/기간");
            if(s.FullHarvestEnabled&&profit>=s.FullHarvestTriggerPercent)return Order(false,p.Quantity,"농부 전량 수확");
            bool harvested=broker.Orders.Any(o=>o.Symbol==key&&o.Created>=opened&&o.Reason=="농부 일부 수확"&&o.Filled>0);
            if(s.PartialHarvestEnabled&&!harvested&&profit>=s.PartialHarvestTriggerPercent)return Order(false,Math.Max(1,(int)decimal.Floor(p.Quantity*s.PartialHarvestRatio)),"농부 일부 수확");
            bool added=broker.Orders.Any(o=>o.Symbol==key&&o.Created>=opened&&o.Reason=="농부 추가 진입"&&o.Filled>0);
            if(!s.FarmerSignalOnlyMode&&s.AdditionalEntryEnabled&&!added&&e.Passed&&known&&score>=s.TrendContinuationScoreThreshold&&e.Score>=s.AdditionalEntryConditionScore)return Order(true,initial,"농부 추가 진입");
            return null;
        }
        peaks.Remove(key);if(!e.Passed||e.Score<s.TrendContinuationScoreThreshold||s.FarmerSignalOnlyMode)return null;
        return Order(true,initial,"농부 최초 진입");
    }
}
public static class PortfolioPolicy
{
    public static decimal CoreReserve(PipelineAdvancedConfig c,PaperBroker b)=>c.Portfolio.ProfitTransferEnabled&&c.Portfolio.CorePortfolioDestinationEnabled?Math.Max(0,b.Fills.Sum(f=>f.RealizedPnl))*c.Portfolio.ProfitTransferRatio:0;
    public static string? Reject(PipelineOrder order,PipelineFeatures f,PipelineAdvancedConfig c,PaperBroker b)
    {
        if(!order.Buy)return null;
        var held=b.Positions.Values.Where(p=>p.Quantity>0).ToArray();var reserved=b.Orders.Where(o=>o.Active&&o.Buy).ToArray();var reserve=CoreReserve(c,b);
        if(order.Quantity*order.Price*(1+c.Execution.Commission/100)>b.Cash-b.ReservedCash-reserve)return "Core 배정/예약 후 가용 자금 부족";
        var exposure=held.Sum(p=>p.Quantity*p.Last)+reserved.Sum(o=>o.Remaining*o.Price)+order.Quantity*order.Price;
        if(c.Portfolio.NewEntriesBlockedWhenBudgetExceeded&&exposure>b.Equity*c.Portfolio.MaxPennyPortfolioPercent/100)return "페니스탁 예산 초과";
        var keys=held.Select(p=>p.Symbol).Concat(reserved.Select(o=>o.Symbol)).Append(f.Instrument.Key).Distinct().Count();if(keys>c.Portfolio.MaxOpenPennyPositions)return "페니스탁 종목 한도";
        var own=held.FirstOrDefault(p=>p.Symbol==f.Instrument.Key);if((own?.Quantity??0)*f.Price+order.Quantity*order.Price>b.Equity*c.Risk.MaxPositionPercent/100)return "종목 비중 초과";
        var last=b.Fills.LastOrDefault(x=>x.Instrument.Key==f.Instrument.Key&&!x.Buy);var cooldown=Math.Max(c.Risk.ReentryCooldown,c.Strategy==PipelineStrategy.TickScalping?c.TickScalping.TickReentryCooldownSeconds:0);
        if(last!=null&&(f.At-last.Time).TotalSeconds<cooldown)return "재진입 대기";
        var today=f.At.ToOffset(TimeSpan.FromHours(9)).Date;var losses=b.Orders.Where(o=>!o.Buy&&o.State==OrderState.Filled&&o.Created.ToOffset(TimeSpan.FromHours(9)).Date==today).OrderByDescending(o=>o.Created).Select(o=>b.Fills.Where(x=>x.OrderId==o.Id).Sum(x=>x.RealizedPnl)).TakeWhile(p=>p<0).Count();
        return losses>=c.Risk.MaxConsecutiveLosses?"연속 손실 제한":null;
    }
}
public sealed class PipelineRuntime
{
    private readonly PipelineEvaluator evaluator=new();private readonly TickScalpingPipelineStrategy tick=new();private readonly FarmerPipelineStrategy farmer=new();
    public PipelineEvaluation? Last {get;private set;}
    public PipelineOrder? Evaluate(MarketContext context,TradingSettings settings,PaperBroker broker,out string? rejected)
    {var c=settings.Pipeline!;Last=evaluator.Evaluate(context,c,settings.PipelineObservations);var order=(c.Strategy==PipelineStrategy.TickScalping?(IPipelineStrategy)tick:farmer).Evaluate(Last,c,broker);rejected=order==null?null:PortfolioPolicy.Reject(order,Last.Features,c,broker);return rejected==null?order:null;}
}
