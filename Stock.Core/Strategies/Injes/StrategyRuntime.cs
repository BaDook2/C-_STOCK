using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public enum SignalType { Enter, Exit, Hold, CancelEntry }
public enum StrategyState { Idle, Candidate, EntrySignal, BuyPending, Holding, SellPending, Cooldown, Blocked, UnknownOrder, ReconciliationRequired }
public enum EvaluationOutcome { Passed, Failed, MissingData, Disabled, NotEvaluated }
public sealed record StrategyCondition(string Name,EvaluationOutcome Outcome,string Detail);
public sealed record ObservationEvidence(string Kind,DateTimeOffset MarketTime,DateTimeOffset ReceivedAt,DateTimeOffset KnownAt,string Source,long Sequence);
public sealed record StrategySignal(string FamilyId,string StrategyId,string Symbol,string SignalId,SignalType SignalType,
    decimal? ReferencePrice,DateTimeOffset SignalTime,string SignalReason,DateTimeOffset MarketTime,DateTimeOffset ReceivedAt,
    DateTimeOffset KnownAt,string Source,long Sequence,StrategyCondition[] Conditions,IReadOnlyDictionary<string,decimal?> Metrics,
    bool OrderAllowed,string[] BlockReasons,string EpisodeId,decimal? PlannedPrice=null,OrderKind OrderKind=OrderKind.Limit,
    decimal? MaxChasePercent=null,decimal? ProtectivePrice=null)
{
    public bool RiskApproved {get;init;}
    public ObservationEvidence[] UsedObservations {get;init;}=[];
    public InjesSupportCluster? SupportCluster {get;init;}
}
public sealed record StrategyContext(Instrument Instrument,InjesFeatures Features,MarketEvent? Event,Position? Position,
    bool PendingBuy,StrategySignal? EntryEvidence,DateTimeOffset Now);
public sealed record StrategyIntent(SignalType Type,string Reason,string EpisodeId,StrategyCondition[] Conditions,
    IReadOnlyDictionary<string,decimal?> Metrics,decimal? PlannedPrice=null,OrderKind OrderKind=OrderKind.Limit,
    decimal? MaxChasePercent=null,decimal? ProtectivePrice=null)
{
    public InjesSupportCluster? SupportCluster {get;init;}
    public EvaluationOutcome? Attention {get;init;}
    public static StrategyIntent Hold(string reason,params StrategyCondition[] conditions)=>new(SignalType.Hold,reason,"",conditions,new Dictionary<string,decimal?>());
}
public sealed record StrategyEvaluationResult(StrategySignal Signal,StrategyState State,EvaluationOutcome Universe,
    EvaluationOutcome Attention,EvaluationOutcome Strategy,EvaluationOutcome Risk);
public interface IStrategyRuntime
{
    StrategyIntent Evaluate(StrategyContext context);
    void ResetCandidate();
    void OnPositionClosed();
}
internal abstract class EpisodeRuntime:IStrategyRuntime
{
    private int next;protected string Episode="";protected bool Emitted;
    protected void NewEpisode(){Episode=(++next).ToString(System.Globalization.CultureInfo.InvariantCulture);Emitted=false;}
    protected StrategyIntent Wait(string reason,params StrategyCondition[] conditions)=>StrategyIntent.Hold(reason,conditions) with{EpisodeId=Episode};
    public abstract StrategyIntent Evaluate(StrategyContext context);
    public abstract void ResetCandidate();
    public virtual void OnPositionClosed()=>ResetCandidate();
    protected static StrategyCondition Condition(string name,decimal? actual,decimal threshold,bool greater=true)=>new(name,actual==null?EvaluationOutcome.MissingData:(greater?actual>=threshold:actual<=threshold)?EvaluationOutcome.Passed:EvaluationOutcome.Failed,$"{actual?.ToString()??"MissingData"} {(greater?">=":"<=")} {threshold}");
}

internal sealed class MomentumPullbackRuntime(MomentumPullbackParameters p):EpisodeRuntime
{
    private int stage;private decimal high,low,priorBounce;private DateTimeOffset lowAt,registeredAt;private StrategyCondition[] registration=[];
    public override void ResetCandidate(){stage=0;high=low=priorBounce=0;Emitted=false;Episode="";}
    public override StrategyIntent Evaluate(StrategyContext c)
    {
        if(c.Position?.Quantity>0)return Wait("보유 · 공통 보호 청산 평가");
        var f=c.Features;if(c.Event?.Kind!=EventKind.Trade||!f.FreshTrade||!f.FreshQuote||f.Price is not {} price)return Wait("MissingData: 신선한 체결/호가 필요");
        var day=Condition("당일 상승",f.DayGainPercent,p.MinDayGainPercent);var rv=Condition("동일시각 RVOL",f.Rvol,p.MinRVOL);
        if(stage>0&&(f.DayGainPercent is null or <=0||rv.Outcome!=EvaluationOutcome.Passed))
        {var id=Episode;ResetCandidate();return new(SignalType.CancelEntry,"당일 상승 소멸 또는 RVOL 조건 무효화",id,[day,rv],new Dictionary<string,decimal?>());}
        if(stage==0)
        {
            var momentum=Condition("단기 상승",f.Momentum(p.MomentumWindowSeconds),p.MinMomentumPercent);
            if(day.Outcome!=EvaluationOutcome.Passed||momentum.Outcome!=EvaluationOutcome.Passed||rv.Outcome!=EvaluationOutcome.Passed)return Wait("급등 후보 조건 대기",day,momentum,rv);
            NewEpisode();registeredAt=c.Event!.ExchangeTime;registration=[day with{Name="등록 당시 당일 상승"},momentum with{Name="등록 당시 단기 상승"},rv with{Name="등록 당시 RVOL"}];high=low=price;lowAt=c.Event!.ExchangeTime;stage=1;return Wait("급등 후보 등록 · 이후 고점 추적");
        }
        if(Emitted)return Wait("동일 급등/반등 사건 소비 완료");
        high=Math.Max(high,price);var pullback=(high-price)/high*100;
        if(stage==1)
        {if(pullback<p.MinPullbackPercent)return Wait("눌림폭 대기",Condition("눌림",pullback,p.MinPullbackPercent));stage=2;low=price;lowAt=c.Event!.ExchangeTime;priorBounce=0;return Wait("눌림 시작 · 이후 저점 추적");}
        if(price<low){low=price;lowAt=c.Event!.ExchangeTime;priorBounce=0;return Wait("새 저점 · 안정시간 재시작");}
        var bounce=(price/low-1)*100;var crossed=priorBounce<p.MinReboundPercent&&bounce>=p.MinReboundPercent;priorBounce=bounce;
        var stable=Condition("저점 안정 초",(decimal)(c.Event!.ExchangeTime-lowAt).TotalSeconds,p.LowStabilitySeconds);
        var volume=Condition("현재10초/이전30초 재유입",f.Reacceleration,p.ReaccelerationVolumeRatio);
        if(stable.Outcome!=EvaluationOutcome.Passed||!crossed||volume.Outcome!=EvaluationOutcome.Passed)return Wait("저점 안정·새 반등·거래량 재유입 대기",stable,Condition("반등",bounce,p.MinReboundPercent),volume);
        Emitted=true;return new(SignalType.Enter,"급등 → 눌림 → 안정 → 새 반등·재유입",Episode,[..registration,rv,stable,volume],new Dictionary<string,decimal?>{{"registeredAtUnixMs",registeredAt.ToUnixTimeMilliseconds()},{"lowAtUnixMs",lowAt.ToUnixTimeMilliseconds()},{"trackedHigh",high},{"trackedLow",low},{"reboundPercent",bounce},{"entryVolumeRate",f.Window(10).VolumePerSecond(10)}},f.Ask,MaxChasePercent:p.MaxChasePercent);
    }
}

internal sealed class CrashReboundRuntime(CrashReboundParameters p):EpisodeRuntime
{
    private bool candidate,stable;private decimal low,stableHigh,stableLow,previousBounce;private DateTimeOffset lowAt,registeredAt,stableAt;private StrategyCondition[] registration=[];
    public override void ResetCandidate(){candidate=stable=false;low=stableHigh=stableLow=previousBounce=0;Emitted=false;Episode="";}
    public override StrategyIntent Evaluate(StrategyContext c)
    {
        var f=c.Features;
        if(c.Position?.Quantity>0)
        {
            var original=c.EntryEvidence?.Metrics.GetValueOrDefault("crashLow");
            return original>0&&f.Price<original*(1-p.LowInvalidationPercent/100)?new(SignalType.Exit,"CrashLowInvalidated",c.EntryEvidence!.EpisodeId,[],new Dictionary<string,decimal?>(),ProtectivePrice:original*(1-p.LowInvalidationPercent/100)):Wait("보유 · 저점 이탈·공통 청산 평가");
        }
        if(c.Event?.Kind!=EventKind.Trade||!f.FreshTrade||!f.FreshQuote||f.Price is not {} price)return Wait("MissingData: 신선한 체결/호가 필요");
        var risk=f.Reference?.FundamentalRisk??FundamentalRiskState.Unknown;
        if(risk==FundamentalRiskState.RiskDetected||p.RequireFundamentalRiskClearance&&risk!=FundamentalRiskState.Cleared)
        {
            var condition=new StrategyCondition("뉴스·공시",risk==FundamentalRiskState.Unknown?EvaluationOutcome.MissingData:EvaluationOutcome.Failed,risk.ToString());
            if(candidate){var prior=Episode;ResetCandidate();return new(SignalType.CancelEntry,"FundamentalRisk: "+risk,prior,[condition],new Dictionary<string,decimal?>());}
            return Wait("FundamentalRisk: "+risk,condition);
        }
        var reference=p.CrashReferenceMode switch{CrashReferenceMode.PreviousClose=>f.Reference?.PreviousClose,CrashReferenceMode.SessionOpen=>f.Reference?.SessionOpen,_=>f.ObservedHigh};
        decimal? crash=reference>0?(1-price/reference)*100:null;
        var crashCheck=Condition("급락",crash,p.MinCrashPercent);
        decimal? velocity=f.Window(p.CrashLookbackSeconds) is {Complete:true,StartPrice:>0} w?(1-price/w.StartPrice)*100/p.CrashLookbackSeconds:null;
        var velocityCheck=p.MinCrashVelocity==0?new StrategyCondition("하락 속도",EvaluationOutcome.Disabled,"0: OFF"):Condition("하락 속도",velocity,p.MinCrashVelocity);
        var volume=Condition("급락 동길이 거래량 증가",f.VolumeRatio(p.CrashLookbackSeconds),p.CrashVolumeRatio);
        if(!candidate)
        {
            if(crashCheck.Outcome!=EvaluationOutcome.Passed||p.MinCrashVelocity>0&&velocityCheck.Outcome!=EvaluationOutcome.Passed||volume.Outcome!=EvaluationOutcome.Passed)return Wait("급락 후보 대기",crashCheck,velocityCheck,volume);
            candidate=true;NewEpisode();registeredAt=c.Event!.ExchangeTime;registration=[crashCheck with{Name="감지 당시 급락"},velocityCheck with{Name="감지 당시 하락 속도"},volume with{Name="감지 당시 거래량"}];low=stableHigh=stableLow=price;lowAt=c.Event!.ExchangeTime;return Wait("급락 감지 · 이후 저점 안정 추적");
        }
        // Compare the original low BEFORE updating it; otherwise invalidation can never occur.
        if(price<low*(1-p.LowInvalidationPercent/100))
        {var id=Episode;ResetCandidate();return new(SignalType.CancelEntry,"CrashLowInvalidated",id,[],new Dictionary<string,decimal?>());}
        if(price<low){low=stableHigh=stableLow=price;lowAt=c.Event!.ExchangeTime;stable=false;previousBounce=0;return Wait("새 저점 · 안정시간 재시작");}
        if(Emitted)return Wait("동일 급락/반등 사건 소비 완료");
        var bounce=(price/low-1)*100;var crossed=previousBounce<p.MinReboundPercent&&bounce>=p.MinReboundPercent;previousBounce=bounce;
        if(!stable)
        {
            stableHigh=Math.Max(stableHigh,price);stableLow=Math.Min(stableLow,price);
            if((c.Event!.ExchangeTime-lowAt).TotalSeconds>=p.LowStabilitySeconds&&(stableHigh-stableLow)/stableLow*100<=p.LowStabilityTolerancePercent)
            {stable=true;stableAt=c.Event!.ExchangeTime;return Wait("안정 확정 · 다음 이벤트의 새 반등 돌파 대기");}
            return Wait("실제 저점 안정 구간 대기");
        }
        if(!crossed)return Wait("안정 후 새 반등 상향 돌파 대기",Condition("반등",bounce,p.MinReboundPercent));
        Emitted=true;return new(SignalType.Enter,"급락 → 실제 저점 안정 → 새 반등",Episode,registration,new Dictionary<string,decimal?>{{"registeredAtUnixMs",registeredAt.ToUnixTimeMilliseconds()},{"lowAtUnixMs",lowAt.ToUnixTimeMilliseconds()},{"stableAtUnixMs",stableAt.ToUnixTimeMilliseconds()},{"crashLow",low},{"bouncePercent",bounce}},f.Ask,MaxChasePercent:p.MaxChasePercent,ProtectivePrice:low*(1-p.LowInvalidationPercent/100));
    }
}

internal sealed class QuoteScalpingRuntime(QuoteScalpingParameters p):EpisodeRuntime
{
    private bool conditionActive;
    public override void ResetCandidate(){conditionActive=false;Emitted=false;Episode="";}
    public override StrategyIntent Evaluate(StrategyContext c)
    {
        var f=c.Features;var window=f.Window(p.VolumeLookbackSeconds);var rate=window.VolumePerSecond(p.VolumeLookbackSeconds);
        if(c.Position?.Quantity>0)
        {
            var entryRate=c.EntryEvidence?.Metrics.GetValueOrDefault("entryVolumeRate");
            if(rate!=null&&entryRate>0&&rate/entryRate<=p.VolumeDecayRatio)return new(SignalType.Exit,"LiquidityDecay",c.EntryEvidence!.EpisodeId,[],new Dictionary<string,decimal?>());
            if(!f.FreshQuote||f.Ask==null||f.TickSize is not >0)return Wait("MissingData: 매도 호가/단위 대기 · 공통 보호 청산 유지");
            var sell=PriceGrid.Sell(f.Ask.Value-p.SellOffsetTicks*f.TickSize.Value,f.TickSize.Value);
            var cost=(c.Position.AveragePrice+sell)*(c.EntryEvidence!.Metrics.GetValueOrDefault("feeRate",0)!.Value+c.EntryEvidence.Metrics.GetValueOrDefault("slippageRate",0)!.Value)+sell*c.EntryEvidence.Metrics.GetValueOrDefault("taxRate",0)!.Value+c.EntryEvidence.Metrics.GetValueOrDefault("executionCost",0)!.Value;
            if(sell-c.Position.AveragePrice-cost<=0)return Wait("비용 이후 양의 매도 목표 없음 · 보호 청산 유지");
            return new(SignalType.Exit,"QuoteTarget",c.EntryEvidence.EpisodeId,[],new Dictionary<string,decimal?>(),sell,OrderKind.Limit);
        }
        if(!f.FreshTrade||!f.FreshQuote||f.TickSize is not >0||f.Bid is not {} bid||f.Ask is not {} ask)return Wait("MissingData: 정상 체결·호가·유효 TickSize 필요");
        decimal? bidDepth=f.BidSize,askDepth=f.AskSize;
        if(p.OrderBookDepthLevels>1)
        {
            var b=f.LastQuote?.Bids;var a=f.LastQuote?.Asks;
            if(b==null||a==null||b.Count<p.OrderBookDepthLevels||a.Count<p.OrderBookDepthLevels||b.Any(x=>x.Price<=0||x.Size<0)||a.Any(x=>x.Price<=0||x.Size<0))return Wait("MissingData: 설정된 L2 깊이 필요");
            if(b[0].Price!=bid||a[0].Price!=ask||b[0].Size!=f.BidSize||a[0].Size!=f.AskSize||b.Zip(b.Skip(1)).Any(x=>x.First.Price<=x.Second.Price)||a.Zip(a.Skip(1)).Any(x=>x.First.Price>=x.Second.Price))return Wait("MissingData: L2 정렬·최우선 호가/잔량 불일치");
            bidDepth=b.Take(p.OrderBookDepthLevels).Sum(x=>x.Size);askDepth=a.Take(p.OrderBookDepthLevels).Sum(x=>x.Size);
        }
        decimal? imbalance=bidDepth+askDepth>0?(bidDepth-askDepth)/(bidDepth+askDepth):null;
        var checks=new[]{Condition("체결빈도",window.Complete?(decimal)window.Count/p.VolumeLookbackSeconds:null,p.MinTradesPerSecond),Condition("초당 주수",rate,p.MinSharesPerSecond),Condition("호가 가격변경",f.QuoteChangesPerSecond,p.MinQuoteChangesPerSecond),Condition("잔량 불균형",imbalance,p.MinOrderBookImbalance),Condition("Bid 잔량",bidDepth,p.MinBidSize),Condition("Ask 잔량",askDepth,p.MinAskSize),Condition("스프레드 틱",f.SpreadTicks,p.MinSpreadTicks)};
        if(checks.Any(x=>x.Outcome!=EvaluationOutcome.Passed)){conditionActive=false;Emitted=false;return Wait("호가 활성/깊이 조건 대기",checks);}
        if(!conditionActive){conditionActive=true;NewEpisode();}
        if(Emitted)return Wait("동일 호가 조건 사건 소비 완료");
        var buy=PriceGrid.Buy(bid+p.BuyOffsetTicks*f.TickSize.Value,f.TickSize.Value);var sellTarget=PriceGrid.Sell(ask-p.SellOffsetTicks*f.TickSize.Value,f.TickSize.Value);
        Emitted=true;return new(SignalType.Enter,"활성 호가·잔량·스프레드",Episode,checks,new Dictionary<string,decimal?>{{"entryVolumeRate",rate},{"plannedSellPrice",sellTarget},{"passiveOrder",buy<ask?1:0}},buy,OrderKind.Limit);
    }
}
public static class PriceGrid
{
    public static decimal Buy(decimal price,decimal tick)=>tick>0?decimal.Floor(price/tick)*tick:throw new InvalidOperationException("확인된 호가 단위가 필요합니다.");
    public static decimal Sell(decimal price,decimal tick)=>tick>0?decimal.Ceiling(price/tick)*tick:throw new InvalidOperationException("확인된 호가 단위가 필요합니다.");
}
