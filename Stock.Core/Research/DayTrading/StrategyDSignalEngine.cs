using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public enum StrategyDStage { Idle, CrashObserved, WatchingRebound, EntrySignal, OrderPending, Holding, Cooldown, Blocked }
public sealed record StrategyDEntryContext(bool HasPosition=false,bool PendingBuy=false,int SymbolEntries=0,int StrategyEntries=0,int AccountEntries=0,
    DateTimeOffset? LastExit=null,string LastExitReason="",StrategyDSupport? PriorSupport=null,int ExitBarOrdinal=0);
public sealed record CrashEvent(DateTimeOffset At,int BarOrdinal,CrashSnapshot Snapshot);
public sealed record StrategyDSignal(string Id,string Symbol,string Instance,long Episode,long Sequence,DateTimeOffset Time,decimal Price,
    bool Buy,string Reason,StrategyDSupport Support,decimal ReboundLow,CrashEvent Crash,StrategyDObservation Decision);
public sealed record StrategyDSignalState(StrategyDStage Stage,string Reason,long Episode,DateTimeOffset? ApproachAt,decimal? Lowest,
    decimal? ReboundPercent,StrategyDSupport? Support,StrategyDSignal? Signal);

/// <summary>Pure causal signal state. This class never submits orders or assumes an order was filled.</summary>
public sealed class StrategyDSignalEngine
{
    private readonly Instrument instrument;
    private readonly string instance;
    private readonly OversoldConfig shared;
    private readonly DayTradingPolicy policy;
    private readonly DailyMarketSession[] sessions;
    private CrashEvent? trigger;
    private StrategyDSupport? support;
    private DateTimeOffset? approached,lastExitSeen;
    private int approachBar;
    private decimal? low;
    private decimal priorRebound;
    private long episode,signalSequence;
    private bool emitted;
    private (DateTimeOffset,long)? lastObservation;
    public StrategyDSignalState State {get;private set;}=new(StrategyDStage.Idle,"급락 관측 대기",0,null,null,null,null,null);

    public StrategyDSignalEngine(Instrument instrument,StrategyDefinition requested,IReadOnlyList<DailyMarketSession> sessions,string instance)
    {
        var definition=requested.Snapshot();if(!definition.IsStrategyD||string.IsNullOrWhiteSpace(instance))throw new InvalidOperationException("전략 D와 실행 식별자가 필요합니다.");
        this.instrument=instrument;this.instance=instance;shared=definition.Configuration;policy=definition.DayTrading!;this.sessions=sessions.OrderBy(s=>s.Open).ToArray();
    }
    public void Invalidate(string reason)
    {ClearCandidate();trigger=null;State=new(StrategyDStage.Blocked,reason,episode,null,null,null,null,null);}
    public StrategyDSignalState Observe(StrategyDObservation observation,StrategyDEntryContext position,bool allowSignal=true)
    {
        var o=observation;
        if(lastObservation is {} seen&&(o.Time<seen.Item1||o.Time==seen.Item1&&o.Sequence<=seen.Item2))
            return State=State with{Signal=null};
        lastObservation=(o.Time,o.Sequence);
        StrategyDSignalState Result(StrategyDStage stage,string reason,StrategyDSignal? signal=null)
            =>State=new(stage,reason,episode,approached,low,low>0?(o.Price/low-1)*100:null,support,signal);
        if(o.Feed.Discontinuity){Invalidate("체결 누락 · 기존 반등 후보 무효화");return State;}
        if(position.LastExit!=lastExitSeen){lastExitSeen=position.LastExit;ClearCandidate();}
        if(position.HasPosition)return Result(StrategyDStage.Holding,"체결 확인된 포지션 보호 중");
        if(position.PendingBuy)return Result(StrategyDStage.OrderPending,"기존 매수 주문의 체결/취소 확인 대기");
        if(o.SecurityTradable==false||o.FundamentalRisk==true){ClearCandidate();trigger=null;return Result(StrategyDStage.Blocked,"종목 상태/위험으로 신규 매수 금지");}
        if(policy.Quality.RequireKnownSecurityAndRisk&&(o.SecurityTradable!=true||o.FundamentalRisk!=false))
        {ClearCandidate();return Result(StrategyDStage.Blocked,"당시 일반주식 상태 또는 위험 자료 미확인");}
        var local=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(o.Time,policy.Session.TimeZoneId);
        if(TimeOnly.FromDateTime(local.DateTime)>=policy.Session.NoNewEntryAfter){ClearCandidate();return Result(StrategyDStage.Blocked,"신규 진입 종료 시각 이후");}
        if(!CanReenter(position,o,out var reentryReason)){ClearCandidate();return Result(StrategyDStage.Cooldown,reentryReason);}
        if(o.Crash.Passed==true&&(trigger==null||WaitElapsed(policy.Crash.CandidateLifetime,trigger.At,trigger.BarOrdinal,o)))
            trigger=new(o.Time,o.AnalysisBarOrdinal,o.Crash);
        if(trigger==null)return Result(StrategyDStage.Idle,o.Crash.Passed==null?"급락 분석 자료 부족":"급락 조건 미충족");
        if(WaitElapsed(policy.Crash.CandidateLifetime,trigger.At,trigger.BarOrdinal,o)){ClearCandidate();trigger=null;return Result(StrategyDStage.Idle,"급락 후보 유효시간 만료");}
        if(shared.UseRsiFilter&&!(o.Rsi<=shared.MaximumRsi))return Result(StrategyDStage.CrashObserved,"RSI 자료 부족/조건 미충족");
        if(shared.UseVolumeFilter&&!(o.VolumeRatio>=shared.CrashVolumeRatio))return Result(StrategyDStage.CrashObserved,"거래량 자료 부족/조건 미충족");
        if(o.AverageTradingValue==null||o.AverageTradingValue<shared.MinimumAverageTradingValue)return Result(StrategyDStage.CrashObserved,"평균 거래대금 자료 부족/조건 미충족");
        var chosen=o.Supports.OrderBy(s=>support!=null&&s.SamePhysicalZone(support,shared.ClusterChangeTolerancePercent)?0:1).ThenBy(s=>Math.Abs(s.Center/o.Price-1)).FirstOrDefault();
        if(chosen==null){ClearCandidate();return Result(StrategyDStage.CrashObserved,"실제 MA와 유효 매물대 중첩 없음");}
        if(position.PriorSupport is {} previous)
        {
            if(policy.Reentry.Mode==ReentryMode.NewLowerZoneOnly&&chosen.Center>previous.Center*(1-shared.MinimumNewClusterDistancePercent/100))return Result(StrategyDStage.Cooldown,"이전보다 낮은 새 지지 조건 미충족");
            if(policy.Reentry.Mode==ReentryMode.NewZoneAnyDirection&&chosen.SamePhysicalZone(previous,shared.ClusterChangeTolerancePercent))return Result(StrategyDStage.Cooldown,"기존과 다른 새 지지 구간 대기");
        }
        bool newCandidate=support==null||!chosen.SamePhysicalZone(support,shared.ClusterChangeTolerancePercent)||approached is {} at&&WaitElapsed(policy.Support.ReboundCandidateLifetime,at,approachBar,o)||emitted&&low>0&&o.Price<low*(1+shared.ReboundPercent/100);
        if(newCandidate)
        {
            ClearCandidate();
            if(!chosen.Approached(o.Price,shared.SupportDistancePercent))return Result(StrategyDStage.CrashObserved,"지지 접근 대기");
            support=chosen;approached=o.Time;approachBar=o.AnalysisBarOrdinal;low=o.Price;episode++;
        }
        else support=chosen;
        low=Math.Min(low!.Value,o.Price);var rebound=(o.Price/low.Value-1)*100;
        var crossed=allowSignal&&priorRebound<shared.ReboundPercent&&rebound>=shared.ReboundPercent;if(allowSignal)priorRebound=rebound;
        if(!o.Feed.CanEnter)return Result(StrategyDStage.Blocked,o.DataReason);
        if(!support!.Approached(o.Price,policy.Support.MaxEntryDistanceFromSupportPercent))return Result(StrategyDStage.WatchingRebound,"지지구간과 너무 멀어 추격 진입 차단");
        if(emitted)return Result(StrategyDStage.EntrySignal,"이미 발생한 반등 신호 · 새 episode/체결 상태 대기");
        if(!crossed)return Result(StrategyDStage.WatchingRebound,"지지 접근 후 새로운 반등 상향 통과 대기");
        emitted=true;var sequence=++signalSequence;var id=$"{instrument.Key}|{instance}|{episode}|{sequence}";
        var signal=new StrategyDSignal(id,instrument.Key,instance,episode,sequence,o.Time,o.Price,true,"FreshRebound",support,low.Value,trigger,o);
        return Result(StrategyDStage.EntrySignal,"전략 D의 새 반등 신호",signal);
    }
    internal void TrackBoundaryPrice(StrategyDObservation observation)
    {
        if(low==null||support==null)return;
        low=Math.Min(low.Value,observation.Price);
        State=State with{Lowest=low,ReboundPercent=(observation.Price/low-1)*100};
    }
    private bool CanReenter(StrategyDEntryContext position,StrategyDObservation o,out string reason)
    {
        reason="";
        if(position.SymbolEntries>=policy.Reentry.MaxEntriesPerSymbolPerDay||position.StrategyEntries>=policy.Reentry.MaxEntriesPerStrategyPerDay||position.AccountEntries>=policy.Reentry.MaxEntriesPerAccountPerDay){reason="일일 신규 진입 횟수 한도";return false;}
        if(position.LastExit is not {} exit)return true;
        if(policy.Reentry.Mode==ReentryMode.Disabled){reason="재진입 사용 안함";return false;}
        var afterStop=position.LastExitReason.Contains("Stop",StringComparison.Ordinal)||position.LastExitReason=="SupportBreak";
        foreach(var wait in new[]{policy.Reentry.MinimumInterval,afterStop?policy.Reentry.AfterStopCooldown:policy.Reentry.AfterProfitCooldown})
            if(!WaitElapsed(wait,exit,position.ExitBarOrdinal,o)){reason=afterStop?"손절 후 재진입 대기":"재진입 최소 간격 대기";return false;}
        return true;
    }
    private bool WaitElapsed(WaitPeriod wait,DateTimeOffset start,int startBar,StrategyDObservation now)=>wait.Unit switch
    {
        WaitUnit.Second=>now.Time-start>=TimeSpan.FromSeconds(wait.Value),WaitUnit.Minute=>now.Time-start>=TimeSpan.FromMinutes(wait.Value),
        WaitUnit.Bar=>now.AnalysisBarOrdinal-startBar>=wait.Value,
        _=>sessions.Count(s=>s.Open>start&&s.Open<=now.Time)>=wait.Value
    };
    private void ClearCandidate(){support=null;approached=null;low=null;priorRebound=0;emitted=false;approachBar=0;}
}
