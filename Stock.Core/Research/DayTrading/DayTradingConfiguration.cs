using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public enum TimeframeUnit { Trade, Second, Minute, Hour, TradingDay }
public enum SignalEvaluationMode { OnTick, OnActiveBarUpdate, OnBarClosed }
public enum RuleCombination { Any, All }
public enum VolumeProfileSource { Approximate, TradeBased }
public enum ReentryMode { NewLowerZoneOnly, SameZoneWithFreshSignal, NewZoneAnyDirection, Disabled }
public enum ExitMode { FullTakeProfit, PartialTakeProfit, TrailingOnly }
public enum TrailingPeakBasis { FromEntry, ResetAtTakeProfit1 }
public enum WaitUnit { Second, Minute, Bar, TradingDay }
public enum IndicatorKind { Rsi, Volume, MovingAverage, VolumeProfile, Liquidity }

/// <summary>A trade is an event count, never an elapsed second. Trading days follow supplied sessions.</summary>
public sealed record TimeframeSpec(int Size,TimeframeUnit Unit)
{
    public void Validate()
    {
        if(Size<=0||Size>1_000_000||!Enum.IsDefined(Unit)||Unit==TimeframeUnit.TradingDay&&Size!=1)
            throw new InvalidOperationException("분석 기준은 양의 크기와 체결/초/분/시간/거래일이어야 합니다. 일봉은 1거래일입니다.");
    }
    [System.Text.Json.Serialization.JsonIgnore] public TimeSpan? Duration=>Unit switch
    {
        TimeframeUnit.Second=>TimeSpan.FromSeconds(Size),TimeframeUnit.Minute=>TimeSpan.FromMinutes(Size),
        TimeframeUnit.Hour=>TimeSpan.FromHours(Size),_=>null
    };
    public override string ToString()=>$"{Size} {Unit switch{TimeframeUnit.Trade=>"체결",TimeframeUnit.Second=>"초",TimeframeUnit.Minute=>"분",TimeframeUnit.Hour=>"시간",_=>"거래일"}}";
}
public sealed record WaitPeriod(int Value,WaitUnit Unit)
{
    public void Validate()
    {
        if(Value<0||Value>1_000_000||!Enum.IsDefined(Unit))throw new InvalidOperationException("대기기간은 0 이상의 초/분/봉/거래일이어야 합니다.");
    }
}
public sealed class CrashPolicy
{
    public bool DailyEnabled {get;set;}=true;
    public bool MediumEnabled {get;set;}=true;
    public bool ShortEnabled {get;set;}=true;
    public RuleCombination DailyCombination {get;set;}=RuleCombination.Any;
    public bool IntradayEnabled {get;set;}=true;
    public bool SessionHighEnabled {get;set;}=true;
    public decimal SessionDrawdownPercent {get;set;}=-4;
    public bool RollingHighEnabled {get;set;}=true;
    public TimeframeSpec RollingWindow {get;set;}=new(30,TimeframeUnit.Minute);
    public decimal RollingDrawdownPercent {get;set;}=-2;
    public RuleCombination IntradayCombination {get;set;}=RuleCombination.Any;
    public RuleCombination DailyIntradayCombination {get;set;}=RuleCombination.Any;
    public WaitPeriod CandidateLifetime {get;set;}=new(30,WaitUnit.Minute);
}
public sealed class SupportPolicy
{
    public TimeframeSpec MovingAverageTimeframe {get;set;}=new(1,TimeframeUnit.TradingDay);
    public bool IncludeActiveMovingAverageBar {get;set;}
    public VolumeProfileSource ProfileSource {get;set;}=VolumeProfileSource.Approximate;
    public TimeframeSpec ProfileTimeframe {get;set;}=new(1,TimeframeUnit.TradingDay);
    public decimal MaxOverlapDistancePercent {get;set;}=.5m;
    public decimal MaxZoneWidthPercent {get;set;}=2;
    public decimal MaxEntryDistanceFromSupportPercent {get;set;}=1.5m;
    public WaitPeriod ReboundCandidateLifetime {get;set;}=new(15,WaitUnit.Minute);
}
public sealed class ReentryPolicy
{
    public ReentryMode Mode {get;set;}=ReentryMode.SameZoneWithFreshSignal;
    public WaitPeriod MinimumInterval {get;set;}=new(60,WaitUnit.Second);
    public WaitPeriod AfterProfitCooldown {get;set;}=new(60,WaitUnit.Second);
    public WaitPeriod AfterStopCooldown {get;set;}=new(5,WaitUnit.Minute);
    public int MaxEntriesPerSymbolPerDay {get;set;}=10;
    public int MaxEntriesPerStrategyPerDay {get;set;}=50;
    public int MaxEntriesPerAccountPerDay {get;set;}=100;
}
public sealed class ExitPolicy
{
    public bool SupportStopEnabled {get;set;}=true;
    // Fixed stop, trailing and TP thresholds/toggles have one source: the shared 61 parameters.
    public ExitMode Mode {get;set;}=ExitMode.PartialTakeProfit;
    public TrailingPeakBasis PeakBasis {get;set;}=TrailingPeakBasis.FromEntry;
    public bool BreakEvenAfterTakeProfit1 {get;set;}
    public SignalEvaluationMode RiskEvaluation {get;set;}=SignalEvaluationMode.OnTick;
}
public sealed class SessionPolicy
{
    public bool DayOnly {get;set;}=true;
    public TimeOnly NoNewEntryAfter {get;set;}=new(15,0);
    public TimeOnly LiquidationStartAt {get;set;}=new(15,10);
    // These are experimental KRX preset times, not an exchange calendar or an execution guarantee.
    public string TimeZoneId {get;set;}="Korea Standard Time";
}
public sealed class AccountRiskPolicy
{
    public decimal MaxOrderCash {get;set;}=1_000_000;
    public decimal MaxConcurrentExposure {get;set;}=5_000_000;
    public decimal MaxDailyLoss {get;set;}=500_000;
    public decimal RiskBudgetPerPosition {get;set;}=10_000;
    public int MaxConsecutiveStops {get;set;}=3;
}
public sealed class DataQualityPolicy
{
    public int MaximumAgeMilliseconds {get;set;}=2_000;
    public int MaximumBufferedEvents {get;set;}=200_000;
    public int MaximumQueuedEvents {get;set;}=4_096;
    public bool RequireKnownSecurityAndRisk {get;set;}=true;
    public bool RequireContinuousTrades {get;set;}=true;
    public bool RequireVerifiedExchangeTime {get;set;}=true;
}

/// <summary>Versioned additive policies. No legacy config property or meaning is changed.</summary>
public sealed class DayTradingPolicy
{
    public TimeframeSpec SimpleTimeframe {get;set;}=new(1,TimeframeUnit.Minute);
    public bool AdvancedTimeframes {get;set;}
    public Dictionary<IndicatorKind,TimeframeSpec> IndicatorTimeframes {get;set;}=[];
    public SignalEvaluationMode Evaluation {get;set;}=SignalEvaluationMode.OnTick;
    public CrashPolicy Crash {get;set;}=new();
    public SupportPolicy Support {get;set;}=new();
    public ReentryPolicy Reentry {get;set;}=new();
    public ExitPolicy Exit {get;set;}=new();
    public SessionPolicy Session {get;set;}=new();
    public AccountRiskPolicy Risk {get;set;}=new();
    public DataQualityPolicy Quality {get;set;}=new();

    public void Validate(OversoldConfig shared)
    {
        if(shared==null||SimpleTimeframe==null||IndicatorTimeframes==null||Crash==null||Support==null||Reentry==null||Exit==null||Session==null||Risk==null||Quality==null)
            throw new InvalidOperationException("전략 D 설정 그룹은 비어 있을 수 없습니다.");
        shared.Validate();SimpleTimeframe.Validate();
        foreach(var (key,spec) in IndicatorTimeframes){Defined(key);if(spec==null)throw new InvalidOperationException("지표 기준이 비어 있습니다.");spec.Validate();}
        Defined(Evaluation);Defined(Crash.DailyCombination);Defined(Crash.IntradayCombination);Defined(Crash.DailyIntradayCombination);
        Defined(Support.ProfileSource);Defined(Reentry.Mode);Defined(Exit.Mode);Defined(Exit.PeakBasis);Defined(Exit.RiskEvaluation);
        if(Crash.RollingWindow==null||Crash.CandidateLifetime==null||Support.MovingAverageTimeframe==null||Support.ProfileTimeframe==null||Support.ReboundCandidateLifetime==null||Reentry.MinimumInterval==null||Reentry.AfterProfitCooldown==null||Reentry.AfterStopCooldown==null)
            throw new InvalidOperationException("전략 D 기간을 입력하세요.");
        Crash.RollingWindow.Validate();Support.MovingAverageTimeframe.Validate();Support.ProfileTimeframe.Validate();
        foreach(var wait in new[]{Crash.CandidateLifetime,Support.ReboundCandidateLifetime,Reentry.MinimumInterval,Reentry.AfterProfitCooldown,Reentry.AfterStopCooldown})wait.Validate();
        if(Crash.CandidateLifetime.Value==0||Support.ReboundCandidateLifetime.Value==0)throw new InvalidOperationException("후보 유지기간은 양수여야 합니다.");
        if(Crash.RollingWindow.Duration==null)throw new InvalidOperationException("장중 고점 기간은 초/분/시간으로 입력하세요.");
        if(!Crash.DailyEnabled&&!Crash.IntradayEnabled||Crash.DailyEnabled&&!Crash.MediumEnabled&&!Crash.ShortEnabled||Crash.IntradayEnabled&&!Crash.SessionHighEnabled&&!Crash.RollingHighEnabled)
            throw new InvalidOperationException("활성 급락 종류에 검사 규칙을 하나 이상 켜세요.");
        Loss(Crash.SessionDrawdownPercent);Loss(Crash.RollingDrawdownPercent);
        if(!shared.UseMovingAverageSupport||!shared.UseApproximateVolumeZone||!shared.RequireReboundConfirmation)
            throw new InvalidOperationException("전략 D에는 이동평균·매물대·새 반등 확인이 필수입니다.");
        Positive(Support.MaxOverlapDistancePercent,100);Positive(Support.MaxZoneWidthPercent,100);Positive(Support.MaxEntryDistanceFromSupportPercent,100);
        foreach(var count in new[]{Reentry.MaxEntriesPerSymbolPerDay,Reentry.MaxEntriesPerStrategyPerDay,Reentry.MaxEntriesPerAccountPerDay,Risk.MaxConsecutiveStops,Quality.MaximumAgeMilliseconds,Quality.MaximumBufferedEvents,Quality.MaximumQueuedEvents})
            if(count<=0||count>10_000_000)throw new InvalidOperationException("횟수/자료 안전 한도는 양수 범위로 입력하세요.");
        foreach(var cash in new[]{Risk.MaxOrderCash,Risk.MaxConcurrentExposure,Risk.MaxDailyLoss,Risk.RiskBudgetPerPosition})Positive(cash,1_000_000_000_000_000m);
        var profileFrame=AdvancedTimeframes&&IndicatorTimeframes.TryGetValue(IndicatorKind.VolumeProfile,out var profileOverride)?profileOverride:Support.ProfileTimeframe;
        if(profileFrame.Unit==TimeframeUnit.Trade&&(long)profileFrame.Size*shared.VolumeZoneLookbackDays>Quality.MaximumBufferedEvents)
            throw new InvalidOperationException("체결 매물대의 요청 개수가 원본 메모리 한도를 넘습니다.");
        if(Risk.MaxOrderCash>Risk.MaxConcurrentExposure||Risk.RiskBudgetPerPosition>Risk.MaxDailyLoss)throw new InvalidOperationException("주문/포지션 위험 한도는 전체 한도 이하여야 합니다.");
        if(Exit.Mode==ExitMode.TrailingOnly&&(!shared.UseTrailingStop||shared.TrailingActivation==TrailingActivation.AfterTakeProfit1))
            throw new InvalidOperationException("추적 중심 익절은 진입 직후 추적 손절이 필요합니다.");
        if(Exit.Mode==ExitMode.FullTakeProfit&&!shared.UseTakeProfit1)throw new InvalidOperationException("전량 익절은 1차 목표를 켜세요.");
        if(shared.UseTrailingStop&&shared.TrailingActivation==TrailingActivation.AfterTakeProfit1&&(!shared.UseTakeProfit1||Exit.Mode!=ExitMode.PartialTakeProfit))
            throw new InvalidOperationException("1차 익절 뒤 추적 손절에는 실제 부분익절 1차 목표가 필요합니다.");
        if(Exit.BreakEvenAfterTakeProfit1&&(Exit.Mode!=ExitMode.PartialTakeProfit||!shared.UseTakeProfit1))throw new InvalidOperationException("본전 손절은 실제 1차 부분익절 이후에만 사용할 수 있습니다.");
        if(!Exit.SupportStopEnabled&&!shared.UseEmergencyStop&&!shared.UseTrailingStop)throw new InvalidOperationException("전략 D의 보호 손절을 하나 이상 켜세요.");
        if(Session.NoNewEntryAfter>Session.LiquidationStartAt)throw new InvalidOperationException("신규 진입 종료는 청산 시작보다 늦을 수 없습니다.");
        try{TimeZoneInfo.FindSystemTimeZoneById(Session.TimeZoneId);}catch{throw new InvalidOperationException("시장 시간대를 확인하세요.");}
    }
    private static void Defined<T>(T value) where T:struct,Enum{if(!Enum.IsDefined(value))throw new InvalidOperationException("지원하지 않는 전략 정책 값입니다.");}
    private static void Loss(decimal n){if(n>=0||n<=-100)throw new InvalidOperationException("급락 기준은 -100%보다 크고 0%보다 작아야 합니다.");}
    private static void Positive(decimal n,decimal maximum){if(n<=0||n>maximum)throw new InvalidOperationException("전략 금액/비율의 양수 범위를 확인하세요.");}
}
