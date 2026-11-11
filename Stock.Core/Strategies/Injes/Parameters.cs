using System.Globalization;
using System.Reflection;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public enum StrategyMode { Standard, Advanced }
public enum CrashReferenceMode { PreviousClose, SessionOpen, ObservedSessionHigh }
public enum StrictnessDirection { HigherIsStricter, LowerIsStricter, Duration, Choice }

[AttributeUsage(AttributeTargets.Property)]
public sealed class InjeParameterAttribute(string label,string description,string unit="",double min=0,double max=1e12,
    StrictnessDirection strictness=StrictnessDirection.HigherIsStricter):Attribute
{
    public string Label=>label; public string Description=>description; public string Unit=>unit;
    public decimal Min=>(decimal)min; public decimal Max=>(decimal)max; public StrictnessDirection Strictness=>strictness;
}
public sealed record ParameterDefinition(string Key,string Label,string Description,string Unit,string ValueType,
    object? DefaultValue,decimal? MinValue,decimal? MaxValue,StrictnessDirection StrictnessDirection,
    IReadOnlyDictionary<int,decimal>? StandardMapping,string ValidationRule);

/// <summary>Exactly fourteen strategy inputs. Null optional limits mean disabled, never a fabricated zero.</summary>
public sealed class InjesCommonParameters
{
    [InjeParameter("최소 주가","USD 가격 하한입니다.","USD",.0001,100000,StrictnessDirection.HigherIsStricter)] public decimal MinPrice {get;set;}=1;
    [InjeParameter("최대 주가","USD 가격 상한입니다.","USD",.0001,100000,StrictnessDirection.LowerIsStricter)] public decimal MaxPrice {get;set;}=5;
    [InjeParameter("최대 시가총액","비어 있으면 필터 OFF. 설정하면 해당 시점 확인 자료가 필요합니다.","USD",1,strictness:StrictnessDirection.LowerIsStricter)] public decimal? MaxMarketCap {get;set;}
    [InjeParameter("최소 일 거래대금","해당 세션에서 실제 관측한 누적 체결금액입니다. 부족한 일봉을 대체하지 않습니다.","USD",0)] public decimal MinDailyTradingValue {get;set;}=100000;
    [InjeParameter("최대 Float","비어 있으면 필터 OFF. 설정하면 유통주식수 확인 자료가 필요합니다.","주",1,strictness:StrictnessDirection.LowerIsStricter)] public decimal? MaxFloat {get;set;}
    [InjeParameter("1회 투자금","수수료를 포함한 신규 진입 예산 상한입니다.","USD",.01,strictness:StrictnessDirection.LowerIsStricter)] public decimal OrderBudget {get;set;}=500;
    [InjeParameter("목표 익절률","실제 평균 매수가 대비 수익 목표입니다.","%",.0001,10000)] public decimal TakeProfitPercent {get;set;}=3;
    [InjeParameter("손절률","양수로 저장하며 평균가 × (1 - 값 / 100)입니다.","%",.0001,99.99,strictness:StrictnessDirection.LowerIsStricter)] public decimal StopLossPercent {get;set;}=1.5m;
    [InjeParameter("최대 보유시간","최초 확인된 실제 매수 체결 이후부터 측정합니다.","초",1,86400,strictness:StrictnessDirection.LowerIsStricter)] public int MaxHoldingSeconds {get;set;}=60;
    [InjeParameter("재진입 대기","전량 매도 체결 확인 후 대기합니다. 같은 사건 재사용 금지.","초",0,86400)] public int ReentryCooldownSeconds {get;set;}=60;
    [InjeParameter("하루 손실 한도","미국 세션 날짜 기준 계좌 손실 한도입니다.","USD",.01,strictness:StrictnessDirection.LowerIsStricter)] public decimal MaxDailyLossCash {get;set;}=100;
    [InjeParameter("동시 보유 한도","미체결 최초 매수의 예약 자리도 포함합니다.","종목",1,100,strictness:StrictnessDirection.LowerIsStricter)] public int MaxConcurrentPositions {get;set;}=3;
    [InjeParameter("최대 진입 스프레드","신선한 정상 Bid/Ask가 필요합니다.","%",.0001,100)] public decimal MaxSpreadPercent {get;set;}=2;
    [InjeParameter("주문 대기 제한","만료 시 취소 요청하며 취소 확인 전 재주문하지 않습니다.","ms",1,3600000,strictness:StrictnessDirection.LowerIsStricter)] public int OrderTimeoutMilliseconds {get;set;}=3000;
}
public sealed class MomentumPullbackParameters
{
    [InjeParameter("최소 당일 상승률","전일 종가 대비 후보 등록 조건입니다.","%",.001,10000)] public decimal MinDayGainPercent {get;set;}=10;
    [InjeParameter("상승률 탐색기간","해당 시간 전체의 연속 관측이 필요합니다.","초",1,3600)] public int MomentumWindowSeconds {get;set;}=120;
    [InjeParameter("구간 최소 상승률","탐색기간 시작가격 대비 상승률입니다.","%",.001,10000)] public decimal MinMomentumPercent {get;set;}=3;
    [InjeParameter("최소 RVOL","과거 동일 세션·동일 경과시간 누적 거래량과 비교합니다.","배",.001,10000)] public decimal MinRVOL {get;set;}=5;
    [InjeParameter("RVOL 비교일","과거 세션의 동일 경과시간 자료가 모두 필요합니다.","세션",1,252)] public int RVOLLookbackSessions {get;set;}=20;
    [InjeParameter("거래량 재증가","현재10초 속도 / 직전30초 속도입니다.","배",.001,1000)] public decimal ReaccelerationVolumeRatio {get;set;}=1.5m;
    [InjeParameter("최소 눌림폭","후보 등록 이후 관측 고점 대비 하락폭입니다.","%",.001,99)] public decimal MinPullbackPercent {get;set;}=3;
    [InjeParameter("저점 안정시간","저점 갱신·자료 공백 시 재시작합니다.","초",1,3600)] public int LowStabilitySeconds {get;set;}=60;
    [InjeParameter("최소 반등률","저점 대비 기준의 새로운 상향 돌파입니다.","%",.001,1000)] public decimal MinReboundPercent {get;set;}=.3m;
    [InjeParameter("최대 추격률","실제 주문가격은 신호가격 대비 이 범위 이하여야 합니다.","%",0,100,strictness:StrictnessDirection.LowerIsStricter)] public decimal MaxChasePercent {get;set;}=.5m;
}
public sealed class QuoteScalpingParameters
{
    [InjeParameter("최소 체결빈도","관측 창 전체 초당 개별 체결 건수입니다.","건/초",.001,100000)] public decimal MinTradesPerSecond {get;set;}=2;
    [InjeParameter("최소 초당 주수","관측 창 전체 체결 주수 / 초입니다.","주/초",.001)] public decimal MinSharesPerSecond {get;set;}=100;
    [InjeParameter("체결량 비교기간","체결·호가 변경 빈도의 관측기간입니다.","초",1,3600)] public int VolumeLookbackSeconds {get;set;}=30;
    [InjeParameter("최소 호가 변경빈도","최우선 가격 변경만 셉니다. 수량 변경은 제외합니다.","건/초",0,100000)] public decimal MinQuoteChangesPerSecond {get;set;}=1;
    [InjeParameter("최소 잔량 불균형","(BidDepth-AskDepth)/(BidDepth+AskDepth). 합이0이면 계산 불가.","비율",-1,1)] public decimal MinOrderBookImbalance {get;set;}=.2m;
    [InjeParameter("최소 매수잔량","선택한 깊이의 실제 Bid 잔량 합입니다.","주",0)] public decimal MinBidSize {get;set;}=100;
    [InjeParameter("최소 매도잔량","선택한 깊이의 실제 Ask 잔량 합입니다.","주",0)] public decimal MinAskSize {get;set;}=100;
    [InjeParameter("호가 분석 단계","2 이상이면 실제 L2 양쪽 자료가 필요합니다.","단계",1,50)] public int OrderBookDepthLevels {get;set;}=1;
    [InjeParameter("최소 스프레드","비용 차감 후 양의 기대 차익도 필요합니다.","틱",1,1000)] public int MinSpreadTicks {get;set;}=2;
    [InjeParameter("매수 오프셋","BestBid + 값 × 확인된 TickSize입니다.","틱",0,100)] public int BuyOffsetTicks {get;set;}
    [InjeParameter("매도 오프셋","BestAsk - 값 × 확인된 TickSize입니다.","틱",0,100)] public int SellOffsetTicks {get;set;}
    [InjeParameter("유동성 감소 청산","현재 속도 / 진입 당시 속도가 이 값 이하이면 보호 청산합니다.","비율",0,1)] public decimal VolumeDecayRatio {get;set;}=.3m;
}
public sealed class CrashReboundParameters
{
    [InjeParameter("급락 기준가격","전일 종가·세션 시가·관측 세션 고점 중 하나입니다.")] public CrashReferenceMode CrashReferenceMode {get;set;}
    [InjeParameter("최소 급락률","(1 - 현재가 / 기준가) ×100입니다.","%",.001,99.99)] public decimal MinCrashPercent {get;set;}=50;
    [InjeParameter("급락 탐색기간","급락 속도 및 현재/이전 동길이 거래량 비교기간입니다.","초",1,3600)] public int CrashLookbackSeconds {get;set;}=300;
    [InjeParameter("최소 하락 속도","0은 속도 조건만 OFF. 켜면 탐색기간 전체가 필요합니다.","%/초",0,100)] public decimal MinCrashVelocity {get;set;}
    [InjeParameter("급락 거래량 배수","현재 구간 / 바로 이전 동길이 구간의 거래량 속도입니다.","배",.001,10000)] public decimal CrashVolumeRatio {get;set;}=2;
    [InjeParameter("저점 안정시간","새 저점 또는 자료 공백 시 안정 시계를 다시 시작합니다.","초",1,3600)] public int LowStabilitySeconds {get;set;}=60;
    [InjeParameter("안정 허용 변동폭","저점 이후 실제 관측 안정 구간 폭입니다. 안정 확정 뒤 반등을 기다립니다.","%",0,100,strictness:StrictnessDirection.LowerIsStricter)] public decimal LowStabilityTolerancePercent {get;set;}=.2m;
    [InjeParameter("최소 반등률","안정 확정 후 새 이벤트에서의 상향 돌파입니다.","%",.001,1000)] public decimal MinReboundPercent {get;set;}=.5m;
    [InjeParameter("최대 추격률","실제 주문가격의 신호가격 대비 최대 상승폭입니다.","%",0,100,strictness:StrictnessDirection.LowerIsStricter)] public decimal MaxChasePercent {get;set;}=1;
    [InjeParameter("저점 무효화 폭","기존 저점에 대해 먼저 검사한 뒤 새 저점을 반영합니다.","%",.001,99,strictness:StrictnessDirection.LowerIsStricter)] public decimal LowInvalidationPercent {get;set;}=.5m;
    [InjeParameter("위험 확인 필수","ON이면 당시 Cleared만 신규 진입 허용. 뉴스 부재는 Unknown입니다.")] public bool RequireFundamentalRiskClearance {get;set;}=true;
}
public sealed class SupportZoneParameters
{
    [InjeParameter("지지선 종류","쉼표로 MA,Pivot,VolumeZone을 지정합니다. 각 근거 ID는 독립입니다.")] public string SupportSources {get;set;}="MA,Pivot,VolumeZone";
    [InjeParameter("이동평균 기간","완성된 일봉 종가. 기간마다 충분한 봉이 있어야 합니다.","일봉")] public string MovingAveragePeriods {get;set;}="120,240,480,999";
    [InjeParameter("고점 왼쪽 비교봉","모든 왼쪽 High보다 엄격히 큰 고점입니다.","일봉",1,1000)] public int PivotLeftBars {get;set;}=20;
    [InjeParameter("고점 오른쪽 확정봉","오른쪽 봉이 모두 완성되고 사용 가능해진 뒤만 확정합니다.","일봉",1,1000)] public int PivotRightBars {get;set;}=20;
    [InjeParameter("매물대 분석기간","일봉 OHLCV 비례 배분 근사 자료입니다.","일봉",1,2000)] public int VolumeProfileLookbackBars {get;set;}=240;
    [InjeParameter("최소 가격 Bin 폭","USD 단위이며 제공자가 확인한 호가 단위 이상으로 맞춥니다.","USD",.0001,100)] public decimal VolumeBinSize {get;set;}=.01m;
    [InjeParameter("지지선 병합 거리","인접 거리와 전체 군집 폭을 함께 제한합니다.","%",0,100,strictness:StrictnessDirection.LowerIsStricter)] public decimal SupportClusterRangePercent {get;set;}=5;
    [InjeParameter("최소 독립 지지 수","매물대 내부 Bin을 여러 독립 근거로 세지 않습니다.","개",1,100)] public int MinimumSupportCount {get;set;}=2;
    [InjeParameter("지지 접근 거리","군집 하단/상단에서 허용하는 거리입니다.","%",0,100,strictness:StrictnessDirection.LowerIsStricter)] public decimal SupportDistancePercent {get;set;}=3;
    [InjeParameter("반등 확인 사용","OFF이면 최초 접근, ON이면 접근 이후 저점 대비 반등을 기다립니다.")] public bool RequireReboundConfirmation {get;set;}
    [InjeParameter("반등 확인률","접근 이후 관측 저점의 새로운 반등 사건입니다.","%",.001,1000)] public decimal ReboundPercent {get;set;}=3;
    [InjeParameter("평균 거래대금 상한","완성 일봉 종가×거래량 근사를 사용하지 않습니다. 당시 확인된 평균 거래대금이 필요합니다.","USD",.01,strictness:StrictnessDirection.LowerIsStricter)] public decimal MaxAverageDailyTradingValue {get;set;}=2000000;
    [InjeParameter("지지 이탈 손절폭","진입 근거의 군집 하단을 고정하여 틱에서 즉시 판단합니다.","%",.001,99,strictness:StrictnessDirection.LowerIsStricter)] public decimal SupportBreakPercent {get;set;}=3;
}
public sealed class InjesExecutionPolicy
{
    [InjeParameter("편도 수수료 가정","실험용 비용이며 실제 브로커 요율을 자동 추정하지 않습니다.","%",0,10)] public decimal FeePercent {get;set;}=.05m;
    [InjeParameter("매도세금 가정","계좌별 적용 비용을 직접 지정합니다.","%",0,10)] public decimal TaxPercent {get;set;}
    [InjeParameter("슬리피지 가정","시장성 체결에 불리한 방향으로 적용합니다.","%",0,10)] public decimal SlippagePercent {get;set;}=.02m;
    [InjeParameter("보수적 체결비용","호가 틱떼기 기대 차익에서 추가 차감하는 왕복 주당 비용입니다.","USD/주",0,1000)] public decimal ConservativeExecutionCost {get;set;}=.002m;
    [InjeParameter("주문 지연","신호에 사용한 동일 이벤트는 지연값과 관계없이 체결 금지입니다.","ms",0,60000)] public int SubmissionLatencyMilliseconds {get;set;}=100;
    [InjeParameter("취소 확인 지연","가상 체결모형에서 취소 확인을 기다리는 최소 시간입니다.","ms",0,60000)] public int CancellationLatencyMilliseconds {get;set;}=100;
    [InjeParameter("체결 참여율","후속 실제 체결량/호가잔량의 이 비율까지만 모형 체결합니다.","%",.001,100,strictness:StrictnessDirection.LowerIsStricter)] public decimal ParticipationPercent {get;set;}=10;
    [InjeParameter("계좌 노출 상한","보유 원금과 미체결 매수 예약액의 합을 제한합니다.","USD",.01,strictness:StrictnessDirection.LowerIsStricter)] public decimal MaximumExposureCash {get;set;}=1500;
    [InjeParameter("계좌 위험 예산","비어 있으면 추가 상한 OFF. 켜면 보유·매수 미체결의 손절거리와 왕복 비용을 차감한 잔여 위험 예산으로 수량을 제한합니다.","USD",.01,strictness:StrictnessDirection.LowerIsStricter)] public decimal? RiskBudgetCash {get;set;}
    // System guards; not additional strategy alpha parameters.
    public int MaximumDataAgeMilliseconds {get;set;}=3000;
    public int MaximumTradeGapMilliseconds {get;set;}=3000;
    public int MaximumBufferedEvents {get;set;}=200000;
    public int MaximumVolumeBins {get;set;}=20000;
    public bool AllowPreMarket {get;set;}
    public bool AllowAfterHours {get;set;}
    public bool LiveOrdersEnabled {get;set;} // Permanently rejected by validation in this implementation.
}
public static class InjesParameterValidation
{
    public static void Validate(object target)
    {
        foreach(var property in Properties(target))
        {
            var a=property.GetCustomAttribute<InjeParameterAttribute>()!;var value=property.GetValue(target);
            if(value==null){if(Nullable.GetUnderlyingType(property.PropertyType)!=null)continue;throw new InvalidOperationException(a.Label+"이 비어 있습니다.");}
            if(value is decimal or int)
            {var n=Convert.ToDecimal(value,CultureInfo.InvariantCulture);if(n<a.Min||n>a.Max)throw new InvalidOperationException($"{a.Label}: {a.Min}~{a.Max} {a.Unit}");}
            if(property.PropertyType.IsEnum&&!Enum.IsDefined(property.PropertyType,value))throw new InvalidOperationException(a.Label+": 잘못된 선택값입니다.");
        }
        if(target is InjesCommonParameters c&&c.MinPrice>c.MaxPrice)throw new InvalidOperationException("최소 주가는 최대 주가 이하여야 합니다.");
        if(target is InjesExecutionPolicy e&&(e.LiveOrdersEnabled||e.MaximumDataAgeMilliseconds is <1 or >60000||e.MaximumTradeGapMilliseconds is <1 or >60000||e.MaximumBufferedEvents is <100 or >2000000||e.MaximumVolumeBins is <10 or >100000))throw new InvalidOperationException("실거래 잠금·시세 신선도·버퍼 보호 규칙을 확인하세요.");
        if(target is SupportZoneParameters s)
        {
            if(s.SupportSources.Split(',',StringSplitOptions.TrimEntries).Any(x=>x is not("MA" or "Pivot" or "VolumeZone"))||s.SupportSources.Split(',',StringSplitOptions.TrimEntries).Distinct().Count()!=s.SupportSources.Split(',').Length)throw new InvalidOperationException("SupportSources는 MA,Pivot,VolumeZone의 중복 없는 목록이어야 합니다.");
            try{var periods=s.MovingAveragePeriods.Split(',',StringSplitOptions.TrimEntries).Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();if(periods.Length==0||periods.Distinct().Count()!=periods.Length||periods.Any(x=>x is <1 or >10000))throw new FormatException();}catch(Exception x)when(x is FormatException or OverflowException){throw new InvalidOperationException("이동평균 기간은 중복 없는1~10000 정수 목록입니다.");}
        }
    }
    public static PropertyInfo[] Properties(object target)=>target.GetType().GetProperties().Where(p=>p.GetCustomAttribute<InjeParameterAttribute>()!=null).ToArray();
}
