using System.Globalization;
using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
public enum DailyTouchPolicy { ConservativeHighLow, CloseOnly }
public enum TrailingActivation { AfterEntry, AfterTakeProfit1 }
/// <summary>Single source of parameters and UI help for historical and guarded live evaluation.</summary>
public sealed class OversoldConfig
{
    [Parameter("평균 거래대금 기간","현재 봉 이전의 선택한 봉 수만큼 실제 거래대금을 평균합니다. 일봉 백테스트는 일봉을 사용합니다.","더 긴 과거 기간의 유동성을 비교합니다.","최근 거래대금 변화가 평균에 더 크게 반영됩니다.","봉",1,1000)] public int AverageTradingValueLookbackDays {get;set;}=20;
    [Parameter("최소 거래대금","실제 거래대금 자료가 모두 있는 경우에만 비교합니다. 종가×거래량으로 대체하지 않습니다.","거래대금이 더 큰 종목만 통과합니다.","거래대금이 작은 종목도 포함될 수 있습니다.","원",0,1000000000000000)] public decimal MinimumAverageTradingValue {get;set;}=3000000000m;
    [Parameter("중기 낙폭 기간","현재 확정봉을 포함한 기간의 최고 HIGH와 현재 CLOSE를 비교합니다.","더 오래된 고점을 급락 기준에 포함합니다.","가까운 과거 고점에 집중합니다.","봉",1,1000)] public int CrashLookbackDays {get;set;}=20;
    [Parameter("중기 낙폭 기준","최고 HIGH 대비 이 하락률 이하이면 중기 급락입니다.","0에 가까워지므로 더 작은 하락도 통과합니다.","더 큰 낙폭이 있어야 통과합니다.","%",-99.999,-0.001)] public decimal MinimumDrawdownPercent {get;set;}=-20m;
    [Parameter("단기 낙폭 기간","현재 확정봉을 포함한 단기 최고 HIGH가 기준입니다.","단기 급락의 고점 탐색 범위가 넓어집니다.","더 짧은 구간의 고점과 비교합니다.","봉",1,1000)] public int ShortCrashLookbackDays {get;set;}=3;
    [Parameter("단기 낙폭 기준","기간 전 종가가 아닌 단기 최고 HIGH 대비 하락률입니다.","더 작은 단기 하락도 급락으로 인정합니다.","더 큰 단기 낙폭을 요구합니다.","%",-99.999,-0.001)] public decimal ShortCrashPercent {get;set;}=-15m;
    [Parameter("RSI 사용","켜진 과매도 필터는 모두 함께 충족해야 합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseRsiFilter {get;set;}=true;
    [Parameter("RSI 기간","첫 평균 상승·하락 후 Wilder 방식으로 순차 계산합니다.","RSI가 더 긴 기간의 변화를 반영합니다.","최근 가격 변화에 더 민감하게 반응합니다.","봉",1,1000)] public int RsiPeriod {get;set;}=14;
    [Parameter("최대 RSI","이 값 이하의 RSI만 허용합니다. 준비 자료가 부족하면 통과하지 않습니다.","덜 과매도된 종목도 통과합니다.","더 낮은 RSI를 요구하여 후보가 줄어듭니다.","RSI",0,100)] public decimal MaximumRsi {get;set;}=30m;
    [Parameter("거래량 조건 사용","현재 거래량과 이전 기간 평균을 비교합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseVolumeFilter {get;set;}=true;
    [Parameter("평균 거래량 기간","현재 봉을 제외한 이전 거래량 평균입니다.","거래량 비교에 더 오래된 자료를 포함합니다.","최근 거래량 변화가 평균에 더 크게 반영됩니다.","봉",1,1000)] public int VolumeLookbackDays {get;set;}=20;
    [Parameter("거래량 증가 기준","당일 거래량이 이전 평균 대비 이 배수 이상이어야 합니다.","더 큰 거래량 증가를 요구합니다.","더 작은 거래량 증가도 통과합니다.","배",0.001,10000)] public decimal CrashVolumeRatio {get;set;}=2m;
    [Parameter("이동평균선 사용","준비된 이동평균만 지지 후보에 포함합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseMovingAverageSupport {get;set;}=true;
    [Parameter("이동평균선 기간","쉼표로 구분한 양의 기간입니다. 준비되지 않은 기간은 개별 제외합니다.","더 긴 이동평균에는 더 많은 준비 봉이 필요합니다.","더 짧은 이동평균은 최근 가격 변화에 민감합니다.","봉 목록")] public string MovingAveragePeriods {get;set;}="120,240,480,999";
    [Parameter("과거 고점 사용","중심 HIGH가 좌우 모든 HIGH보다 엄격히 높은 고점만 확정 후 사용합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UsePivotSupport {get;set;}=true;
    [Parameter("고점 이전 비교 기간","중심 고점 이전 비교 봉 수입니다.","더 넓은 이전 구간보다 높은 고점을 요구합니다.","국지적인 고점도 후보가 되기 쉽습니다.","봉",1,1000)] public int PivotLeftBars {get;set;}=20;
    [Parameter("고점 확정 기간","이 수의 봉이 도착하기 전에는 고점을 사용할 수 없습니다.","더 많은 오른쪽 봉을 기다린 후 고점을 확정합니다.","고점의 사용 가능 시점이 빨라집니다.","봉",1,1000)] public int PivotRightBars {get;set;}=20;
    [Parameter("근사 매물대 사용","선택한 봉 범위에 거래량을 균등 분배한 근사 구간이며 실제 volume profile이 아닙니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseApproximateVolumeZone {get;set;}=true;
    [Parameter("매물대 분석 기간","이 개수의 확정 봉만 bin에 배분합니다.","더 오래된 거래량을 구간에 누적합니다.","최근 거래량 분포에 집중합니다.","봉",1,1500)] public int VolumeZoneLookbackDays {get;set;}=240;
    [Parameter("가격 구간 폭","원 단위 최소 폭이며 호가와 최대 bin 수에 맞춰 확대할 수 있습니다.","가격 구간이 넓어져 세부 분포가 합쳐집니다.","가격 구간을 더 잘게 나눕니다. 최대 bin 제한은 유지됩니다.","원",1,10000000)] public decimal VolumeZoneBinSize {get;set;}=100m;
    [Parameter("최대 가격 구간 수","범위가 넓으면 이 수를 넘지 않게 실제 bin 폭을 확대합니다.","더 세밀한 가격 구간을 허용하지만 계산량이 늘 수 있습니다.","bin 폭을 넓혀 메모리와 계산량을 줄입니다.","개",2,4096)] public int VolumeZoneMaxBins {get;set;}=512;
    [Parameter("매물대 거래량 기준","최대 bin 거래량의 이 비율 이상인 bin을 선택합니다.","더 높은 거래량이 쌓인 구간만 선택합니다.","거래량이 상대적으로 적은 구간도 포함됩니다.","%",0.001,100)] public decimal VolumeZoneThresholdPercent {get;set;}=70m;
    [Parameter("매물대 병합 거리","이전 구간 상단 대비 이 거리 이내의 선택 구간을 병합합니다.","더 멀리 떨어진 근사 구간도 병합합니다.","서로 가까운 구간만 병합합니다.","%",0,50)] public decimal VolumeZoneMergeRangePercent {get;set;}=1m;
    [Parameter("지지 군집 범위","인접 대표가격의 차이를 왼쪽 대표가격으로 나눕니다. 연결식이라 전체 폭은 더 넓을 수 있습니다.","더 멀리 떨어진 지지 후보를 연결합니다.","더 가까운 지지 후보만 같은 군집에 묶습니다.","%",0.001,100)] public decimal SupportClusterRangePercent {get;set;}=5m;
    [Parameter("최소 지지 개수","ID 중복을 제거한 후보 개수입니다. 근사 구간의 bin 수가 아닙니다.","더 많은 독립 후보가 겹쳐야 진입합니다.","더 적은 후보의 겹침으로도 진입할 수 있습니다.","개",1,1000)] public int MinimumSupportCount {get;set;}=2;
    [Parameter("지지구간 접근 거리","cluster 하단과 상단을 이 비율만큼 확장한 범위에서 접근을 인정합니다.","지지 구간에서 더 떨어진 가격도 접근으로 인정합니다.","지지 구간에 더 가까이 있어야 합니다.","%",0,100)] public decimal SupportDistancePercent {get;set;}=3m;
    [Parameter("반등 확인 사용","최초 유효 cluster 접근 이후의 저점에서 반등을 확인합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool RequireReboundConfirmation {get;set;}=false;
    [Parameter("반등 기준","접근 이후 저점 대비 확정 종가 상승률입니다.","접근 이후 저점에서 더 큰 반등을 기다립니다.","더 작은 반등에도 진입을 허용합니다.","%",0.001,1000)] public decimal ReboundPercent {get;set;}=3m;
    [Parameter("진입 후보 유지 기간","최초 접근 후 이 시장 거래일을 넘으면 후보 저점을 다시 설정합니다.","같은 반등 후보와 저점을 더 오래 추적합니다.","후보가 빨리 만료되어 접근 저점을 새로 잡습니다.","시장 거래일",1,1000)] public int CandidateLifetimeDays {get;set;}=20;
    [Parameter("지지 군집 변경 기준","후보 구성 변경 또는 중심이 이 거리보다 변하면 접근 후보를 새로 시작합니다.","같은 후보의 중심 변화를 더 넓게 허용합니다.","작은 중심 변화에도 반등 저점을 다시 시작합니다.","%",0,100)] public decimal ClusterChangeTolerancePercent {get;set;}=5m;
    [Parameter("백테스트 초기 현금","선택한 단일 종목의 독립 계좌입니다. 여러 종목의 공유자본 결과가 아닙니다.","모형 계좌의 시작 현금이 늘어납니다.","자금 부족으로 매수 수량이 제한될 수 있습니다.","원",1,1000000000000)] public decimal InitialCapital {get;set;}=10000000m;
    [Parameter("총 투자금","회차별 비율은 이 금액을 기준으로 적용합니다.","회차별로 사용할 주문 예산이 커집니다.","매수 수량과 자산 노출이 줄어듭니다.","원",1,1000000000000)] public decimal PlannedInvestment {get;set;}=1000000m;
    [Parameter("최대 분할매수 횟수","실제로 1주 이상 체결된 매수만 횟수에 포함합니다.","더 많은 분할 진입을 허용합니다. 비율 목록도 맞춰야 합니다.","허용하는 분할 진입 횟수가 줄어듭니다.","회",1,20)] public int MaxEntries {get;set;}=3;
    [Parameter("분할매수 비율","쉼표 구분, 횟수와 길이가 같고 합계 100이어야 합니다.","특정 회차 비중을 높이면 그 회차 주문 예산이 늘어납니다.","특정 회차 비중을 줄이면 그 회차 주문 예산이 줄어듭니다. 합계 100을 유지하세요.","% 목록")] public string EntryRatios {get;set;}="40,30,30";
    [Parameter("추가매수 하락 기준","직전 실제 매수가 대비 현재 확정 종가의 하락률입니다.","0에 가까우면 작은 추가 하락에도 분할 진입합니다.","더 큰 추가 하락을 기다립니다.","%",-99.999,-0.001)] public decimal AdditionalEntryDropPercent {get;set;}=-5m;
    [Parameter("추가매수 시 지지 확인","추가 하락과 유효 cluster 접근을 동시에 요구합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool RequireSupportForAdditionalEntry {get;set;}=true;
    [Parameter("지지구간 이탈 기준","최초 매수 신호 때 고정한 cluster 하단 대비 확정 종가 이탈률입니다.","0에 가까울수록 최초 지지 하단 근처에서 손절합니다.","최초 하단에서 더 큰 이탈을 허용합니다.","%",-99.999,-0.001)] public decimal SupportBreakPercent {get;set;}=-3m;
    [Parameter("비상 손절 사용","평균 실제 매수가 대비 손실을 별도 청산 신호로 사용합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseEmergencyStop {get;set;}=true;
    [Parameter("비상 손절 기준","평균 매수가 대비 이 수준의 손실 감지를 청산 신호로 사용합니다.","0에 가까울수록 더 작은 손실에서 비상 청산합니다.","평균 매수가 대비 더 큰 손실을 허용합니다.","%",-99.999,-0.001)] public decimal EmergencyStopLossPercent {get;set;}=-15m;
    [Parameter("1차 익절 사용","목표 수익 감지 후 다음 거래 가능 시가에서 일부를 매도합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseTakeProfit1 {get;set;}=true;
    [Parameter("1차 익절 기준","평균 실제 매수가 대비 목표 상승률입니다.","1차 익절 전에 더 큰 반등을 기다립니다.","더 작은 반등에서 1차 익절 신호를 냅니다.","%",0.001,10000)] public decimal TakeProfit1Percent {get;set;}=10m;
    [Parameter("1차 익절 비율","첫 익절 체결 시점까지의 최대 실제 포지션 수량을 기준으로 고정합니다.","1차에 더 많은 수량을 청산합니다.","1차 청산 후 더 많은 수량이 남습니다.","%",0.001,100)] public decimal TakeProfit1Ratio {get;set;}=30m;
    [Parameter("2차 익절 사용","같은 날 두 목표가 모두 충족되면 다음 시가에 두 단계를 처리할 수 있습니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseTakeProfit2 {get;set;}=true;
    [Parameter("2차 익절 기준","평균 실제 매수가 대비 두 번째 목표 상승률입니다.","2차 익절 목표가 더 높아집니다.","2차 익절 목표가 낮아집니다. 1차보다 커야 합니다.","%",0.001,10000)] public decimal TakeProfit2Percent {get;set;}=20m;
    [Parameter("2차 익절 비율","고정 기준 수량 비율을 내림하며 남은 보유 수량을 초과하지 않습니다.","2차에 더 많은 수량을 청산합니다.","2차 청산 후 더 많은 수량이 남습니다.","%",0.001,100)] public decimal TakeProfit2Ratio {get;set;}=30m;
    [Parameter("추적 손절 사용","이전 확정 최고가로 먼저 검사하고 당일 고가를 나중에 반영합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool UseTrailingStop {get;set;}=true;
    [Parameter("추적 손절 폭","이전까지의 최고 관측가격에서 이 비율 하락을 감지합니다.","이전 최고가에서 더 큰 되돌림을 허용합니다.","작은 되돌림에도 추적 손절 신호를 냅니다.","%",0.001,99.999)] public decimal TrailingStopPercent {get;set;}=7m;
    [Parameter("추적 손절 시작 기준","최초 체결 또는 1차 익절 체결 이후부터 추적합니다.","AfterEntry는 첫 매수 체결부터 추적합니다.","AfterTakeProfit1은 1차 익절의 실제 체결 뒤 추적합니다.","AfterEntry / AfterTakeProfit1")] public TrailingActivation TrailingActivation {get;set;}=TrailingActivation.AfterTakeProfit1;
    [Parameter("일봉 감지 정책","고저가 감지는 장중 순서를 복원하지 않습니다. 모든 주문은 다음 거래 가능 시가입니다.","ConservativeHighLow는 고저가 도달을 감지하고 손절을 우선합니다.","CloseOnly는 확정 종가만 판단합니다. 둘 다 다음 시가 체결입니다.","ConservativeHighLow / CloseOnly")] public DailyTouchPolicy TouchPolicy {get;set;}=DailyTouchPolicy.ConservativeHighLow;
    [Parameter("가격자료 검증 모드","백테스트에서 실제 일봉 날짜로 거래 세션을 구성하고, 과거 종목상태·위험 자료가 없으면 일반주식·거래가능·위험없음으로만 가정합니다. 실제 주문과 스크리닝에는 적용하지 않습니다.","공개 일봉만으로 엔진 동작과 체결 결과를 점검합니다. 결과는 투자 성과로 해석하지 않습니다.","시점별 상태·위험 자료가 없으면 진입을 보류합니다.","ON / OFF")] public bool UsePriceOnlyBacktest {get;set;}=false;
    [Parameter("최대 보유기간","최초 체결일부터 센 시장 거래일 마감에 청산 신호를 냅니다.","잔여 포지션을 더 오래 보유할 수 있습니다.","기간 만료 청산이 빨라집니다.","시장 거래일",1,10000)] public int MaxHoldingDays {get;set;}=20;
    [Parameter("재진입 대기기간","전량 매도 이후 완전히 지난 시장 거래일 수입니다.","전량 청산 후 더 많은 시장 거래일을 기다립니다.","더 빨리 재진입 신호를 검토할 수 있습니다.","시장 거래일",0,10000)] public int ReentryCooldownDays {get;set;}=5;
    [Parameter("새 지지군집 거리","이전 진입 근거 중심보다 이 비율 이상 낮은 cluster만 허용합니다.","이전 중심보다 더 낮은 새 지지 구간을 요구합니다.","이전 구간과 상대적으로 가까운 새 지지도 허용합니다.","%",0.001,99.999)] public decimal MinimumNewClusterDistancePercent {get;set;}=5m;
    [Parameter("새 지지군집 발견 시 조기 재진입","켜면 새 낮은 cluster가 있을 때 대기기간만 생략합니다. 나머지 조건은 유지합니다.","이 조건 또는 청산 기능을 적용합니다.","이 조건 또는 청산 기능을 적용하지 않습니다.","ON / OFF")] public bool AllowEarlyReentryWithNewCluster {get;set;}=false;
    [Parameter("편도 수수료","각 실제 모형 체결금액에 곱하는 비용 가정입니다.","모든 모형 체결의 비용이 증가합니다.","모형 수수료 비용이 감소합니다.","%",0,10)] public decimal CommissionPercent {get;set;}=.015m;
    [Parameter("매도 거래세 가정","사용할 시기·계좌에 맞춰 직접 입력합니다. 현재 세율을 자동 추정하지 않습니다.","매도 비용이 증가합니다.","매도 비용이 감소합니다.","%",0,10)] public decimal TaxPercent {get;set;}=0m;
    [Parameter("체결 슬리피지 가정","다음 시가를 매수에는 높게, 매도에는 낮게 조정합니다.","매수가는 더 높고 매도가는 더 낮아져 체결이 불리해집니다.","관측 시가에 가까운 체결가격을 사용합니다.","%",0,50)] public decimal SlippagePercent {get;set;}=0m;
    [Parameter("연 무위험수익률","Sharpe 계산에만 사용하는 연 수익률 가정입니다.","초과수익률이 줄어 Sharpe가 낮아질 수 있습니다.","초과수익률이 늘어 Sharpe가 높아질 수 있습니다.","%",-99,100)] public decimal AnnualRiskFreePercent {get;set;}=0m;
    [Parameter("연율화 거래일 수","일별 초과수익률 Sharpe의 연율화 계수입니다.","Sharpe 연율화 계수와 무위험수익률 일 환산이 바뀝니다.","Sharpe 연율화 계수와 무위험수익률 일 환산이 바뀝니다.","일",1,366)] public int AnnualTradingDays {get;set;}=252;
    public int[] MaPeriods()=>MovingAveragePeriods.Split(',',StringSplitOptions.TrimEntries).Select(s=>int.Parse(s,CultureInfo.InvariantCulture)).Distinct().Order().ToArray();
    public decimal[] Ratios()=>EntryRatios.Split(',',StringSplitOptions.TrimEntries).Select(s=>decimal.Parse(s,CultureInfo.InvariantCulture)).ToArray();
    public OversoldConfig Clone()=>Serialization.Unpack<OversoldConfig>(Serialization.Pack(this));
    public void Validate()
    {
        StrategyParameterValidation.Object(this);
        try{if(MaPeriods().Length==0||MaPeriods().Any(n=>n<1||n>10000))throw new Exception();}catch{throw new InvalidOperationException("이동평균 기간은 1~10,000의 쉼표 구분 정수로 입력하세요.");}
        try{var r=Ratios();if(r.Length!=MaxEntries||r.Any(x=>x<=0)||r.Sum()!=100)throw new Exception();}catch{throw new InvalidOperationException("분할 비율은 매수 횟수와 길이가 같고 모두 양수이며 합계가 100이어야 합니다.");}
        if(PlannedInvestment>InitialCapital)throw new InvalidOperationException("예정 투자금은 초기 현금 이하여야 합니다.");
        if(UseTakeProfit1&&UseTakeProfit2&&TakeProfit2Percent<=TakeProfit1Percent)throw new InvalidOperationException("2차 익절 목표는 1차 목표보다 커야 합니다.");
        if((UseTakeProfit1?TakeProfit1Ratio:0)+(UseTakeProfit2?TakeProfit2Ratio:0)>100)throw new InvalidOperationException("활성 익절 수량 비율 합계는 100 이하여야 합니다.");
        if(!UseMovingAverageSupport&&!UsePivotSupport&&!UseApproximateVolumeZone)throw new InvalidOperationException("지지 후보를 하나 이상 사용하세요.");
    }
}

