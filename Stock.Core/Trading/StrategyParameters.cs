namespace Stock.Core.Trading;

public enum StrategyType { Scalping, Box, Rebound, Pipeline }
public enum StrategyConfigMode { Standard, Advanced }
public enum StrategyConfigState { Default, Preset, Custom }

public sealed class CommonStrategyConfig
{
    [Parameter("1회 투자금","한 번의 매수 신호에서 사용할 최대 주문 금액입니다.","투자금과 손익 변동이 커집니다.","한 거래의 자산 비중이 작아집니다.","계좌 통화",1,1e12)] public decimal Investment {get;set;}=500;
    [Parameter("최대 동시 보유 종목","동시에 보유할 수 있는 최대 종목 수입니다.","더 많은 종목을 보유할 수 있습니다.","신규 진입이 더 빨리 제한됩니다.","종목",1,100)] public int MaxPositions {get;set;}=5;
    [Parameter("진입 강도","얼마나 강한 매수 신호가 발생해야 진입할지 설정합니다.","더 강한 신호를 요구합니다.","약한 신호에서도 진입 후보가 됩니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int EntryStrength {get;set;}=3;
    [Parameter("청산 강도","매매 상황이 약해질 때 얼마나 민감하게 빠져나올지 설정합니다.","신호 악화에 더 민감하게 반응합니다.","신호 악화를 더 오래 허용합니다.","1 느긋 · 3 보통 · 5 민감",1,5)] public int ExitStrength {get;set;}=3;
    [Parameter("손절률","진입 가격 대비 허용할 최대 손실률의 크기입니다. 양수로 입력합니다.","허용 손실이 커집니다.","작은 손실에도 청산합니다.","% 손실",0.001,99)] public decimal StopLossPercent {get;set;}=2;
    [Parameter("익절률","목표 수익률 도달 시 청산할 기준입니다.","더 큰 수익을 기다립니다.","더 이른 수익 실현을 시도합니다.","% 수익",0.001,1000)] public decimal TakeProfitPercent {get;set;}=3;
    [Parameter("최대 보유시간","진입 후 이 시간이 지나면 다음 관측 이벤트에서 청산을 판단합니다.","더 오래 보유할 수 있습니다.","시간에 따른 청산이 빨라집니다.","초",1,86400)] public int MaxHoldingSeconds {get;set;}=300;
    [Parameter("재진입 대기시간","같은 종목을 청산한 뒤 다시 매수하기까지 기다리는 시간입니다.","반복 거래가 줄어듭니다.","빠른 재진입을 허용합니다.","초",1,86400)] public int ReentryCooldown {get;set;}=30;
    [Parameter("주문 방식","시장가는 체결 가능성을, 지정가는 주문 가격 제한을 우선합니다.","시장가는 슬리피지 영향을 받습니다.","지정가는 미체결될 수 있습니다.","시장가 / 지정가")] public OrderKind OrderType {get;set;}=OrderKind.Limit;
    [Parameter("매매 시작시간","한국시간 기준 신규 진입 시작 시각입니다. 청산은 시간 밖에서도 판단합니다.","신규 진입 시작이 늦어집니다.","신규 진입을 일찍 허용합니다.","HH:mm 한국시간")] public string TradingStart {get;set;}="00:00";
    [Parameter("매매 종료시간","한국시간 기준 신규 진입 종료 시각입니다. 시작보다 이르면 자정을 넘는 시간대입니다.","신규 진입 시간을 늘립니다.","신규 진입을 일찍 마칩니다.","HH:mm 한국시간")] public string TradingEnd {get;set;}="23:59";
}
public sealed class ScalpStandardConfig
{
    [Parameter("거래량 강도","평소보다 거래량이 얼마나 강하게 증가해야 할지 설정합니다.","더 큰 거래량 증가가 필요합니다.","작은 증가도 허용합니다.","1 완화 · 5 엄격",1,5)] public int VolumeStrength {get;set;}=3;
    [Parameter("상승속도 강도","짧은 시간 동안 필요한 가격 상승속도를 설정합니다.","더 빠른 상승이 필요합니다.","느린 상승도 허용합니다.","1 완화 · 5 엄격",1,5)] public int MomentumStrength {get;set;}=3;
    [Parameter("호가 활성도","호가 잔량과 초당 체결이 활발한 종목을 선택합니다.","더 많은 체결과 매수 잔량을 요구합니다.","활동이 적은 종목도 후보가 됩니다.","1 완화 · 5 엄격",1,5)] public int QuoteActivity {get;set;}=3;
    [Parameter("거래량 감소 시 청산","진입 후 거래량 비율이 청산 기준 아래로 떨어지면 청산합니다.","켜면 거래량 약화 청산을 사용합니다.","끄면 다른 청산 조건만 사용합니다.","ON / OFF")] public bool ExitOnVolumeDecay {get;set;}=true;
}
public sealed class BoxStandardConfig
{
    [Parameter("박스권 탐색기간","최근 이 기간의 고점·저점과 변동성을 평가합니다.","더 큰 가격 구조를 봅니다.","최근 움직임에 민감해집니다.","분",1,1440)] public int LookbackMinutes {get;set;}=10;
    [Parameter("박스권 엄격도","안정적인 횡보와 반복된 지지·저항을 요구하는 정도입니다.","좁은 박스와 많은 접촉을 요구합니다.","넓은 횡보도 허용합니다.","1 완화 · 5 엄격",1,5)] public int Strictness {get;set;}=3;
    [Parameter("지지선 접근 강도","박스 하단에 얼마나 가까워야 매수 후보가 되는지 설정합니다.","더 가까운 가격을 요구합니다.","지지선에서 먼 가격도 후보가 됩니다.","1 완화 · 5 엄격",1,5)] public int SupportStrength {get;set;}=3;
    [Parameter("거래량 유입 강도","지지선에서 거래량이 다시 증가해야 하는 정도입니다.","강한 유입이 필요합니다.","약한 유입도 허용합니다.","1 완화 · 5 엄격",1,5)] public int VolumeStrength {get;set;}=3;
    [Parameter("박스 상단 자동청산","박스 상단 영역에서 청산 신호를 발생시킵니다.","켜면 상단 청산을 사용합니다.","끄면 다른 청산 조건을 사용합니다.","ON / OFF")] public bool ExitAtResistance {get;set;}=true;
    [Parameter("박스 하단 이탈 손절","기존 지지 영역을 이탈하면 청산 신호를 발생시킵니다.","켜면 지지 이탈 시 청산합니다.","끄면 공통 손절만 적용합니다.","ON / OFF")] public bool StopOnBreakdown {get;set;}=true;
}
public sealed class ReboundStandardConfig
{
    [Parameter("급락 탐색기간","급격한 하락을 판단할 최근 시간 범위입니다.","긴 하락을 포함합니다.","최근 급락에 집중합니다.","분",1,1440)] public int LookbackMinutes {get;set;}=3;
    [Parameter("급락 강도","급락으로 판단하기 위한 하락 폭과 속도입니다.","크고 빠른 하락이 필요합니다.","작은 하락도 급락 후보가 됩니다.","1 완화 · 5 엄격",1,5)] public int CrashStrength {get;set;}=3;
    [Parameter("반등 확인 강도","저점 이후 반등이 유지되는지 확인하는 정도입니다.","크고 오래 유지된 반등을 요구합니다.","저점 직후 빠른 진입을 허용합니다.","1 완화 · 5 엄격",1,5)] public int ReboundStrength {get;set;}=3;
    [Parameter("거래량 확인","급락 과정의 거래량 증가를 진입 조건으로 사용합니다.","켜면 거래량 조건도 충족해야 합니다.","끄면 가격 조건으로 평가합니다.","ON / OFF")] public bool ConfirmVolume {get;set;}=true;
    [Parameter("뉴스/공시 필터","해당 시점의 확인된 뉴스 위험 데이터가 있어야 진입합니다. 없으면 판단을 보류합니다.","켜면 확인되지 않은 종목을 차단합니다.","끄면 뉴스 위험을 평가하지 않습니다.","ON / OFF")] public bool NewsFilter {get;set;}=true;
    [Parameter("자동매수","조건 충족 시 모의 엔진이 주문할지 신호만 표시할지 결정합니다. 실계좌 자동 주문은 하지 않습니다.","켜면 모의 자동 주문을 허용합니다.","끄면 신호만 기록합니다.","ON / OFF")] public bool AutoBuy {get;set;}=false;
}
public sealed class StandardStrategyConfig
{
    public CommonStrategyConfig Common {get;set;}=new();
    public ScalpStandardConfig Scalping {get;set;}=new();
    public BoxStandardConfig Box {get;set;}=new();
    public ReboundStandardConfig Rebound {get;set;}=new();
}
public sealed class ScalpAdvancedConfig
{
    [Parameter("거래량 비교기간","거래량 비교에 사용할 과거 데이터 기간입니다.","긴 평균을 사용합니다.","최근 거래량 변화에 민감해집니다.","초",2,86400)] public int VolumeLookbackSeconds {get;set;}=120;
    [Parameter("최소 거래량 배수","최근 평균 대비 필요한 거래량 증가배수입니다.","더 강한 증가가 필요합니다.","더 많은 후보가 나옵니다.","배",0.01,1000)] public decimal VolumeRatioThreshold {get;set;}=2;
    [Parameter("거래량 백분위","초당 거래량 분포에서 현재 거래량의 최소 백분위입니다. 0은 조건 해제입니다.","더 드문 거래량 급증을 요구합니다.","보통 거래량도 허용합니다.","백분위",0,100)] public decimal VolumePercentileThreshold {get;set;}=75;
    [Parameter("거래량 Z 점수","현재 거래량이 과거 평균보다 몇 표준편차 높은지 평가합니다. 0은 조건 해제입니다.","더 이례적인 증가를 요구합니다.","작은 이탈도 허용합니다.","표준편차",0,100)] public decimal VolumeZScoreThreshold {get;set;}=1;
    [Parameter("상승속도 계산기간","가격 상승률을 계산할 시간 구간입니다.","긴 움직임을 봅니다.","짧은 급등에 민감합니다.","초",1,3600)] public int MomentumWindowSeconds {get;set;}=20;
    [Parameter("최소 상승률","해당 구간에 필요한 최소 가격 상승률입니다.","큰 상승을 요구합니다.","작은 상승도 허용합니다.","%",0,99)] public decimal MinimumPriceIncreasePercent {get;set;}=.3m;
    [Parameter("초당 최소 체결","현재 구간에서 필요한 초당 체결 건수입니다.","활발한 종목만 진입합니다.","적은 체결도 허용합니다.","건/초",0,10000)] public decimal MinimumTradesPerSecond {get;set;}=1;
    [Parameter("매수 체결 비율","제공처가 명시한 매수 방향 체결 비율입니다. 0은 미사용이며 값이 있으면 방향 데이터가 필수입니다.","매수 방향이 강해야 합니다.","작은 매수 우세도 허용합니다.","비율 0~1",0,1)] public decimal BuyTradeRatio {get;set;}=0;
    [Parameter("호가 불균형","(매수잔량-매도잔량)/(양쪽 잔량 합)의 최소값입니다.","매수 잔량 우세가 필요합니다.","매도 잔량 우세도 허용합니다.","-1~1",-1,1)] public decimal OrderBookImbalance {get;set;}=0;
    [Parameter("최대 스프레드","허용할 최대 매수·매도 호가 차이입니다.","넓은 호가 차이도 허용합니다.","촘촘한 호가만 허용합니다.","%",0,100)] public decimal MaximumSpreadPercent {get;set;}=2;
    [Parameter("진입 점수","거래량·상승률·체결빈도·호가 우세의 평균 충족 점수입니다.","강한 종합 조건을 요구합니다.","완화된 종합 조건을 허용합니다.","점 0~100",0,100)] public decimal EntryScoreThreshold {get;set;}=60;
    [Parameter("청산 점수","보유 중 종합 점수가 이 기준 이하로 약해지면 청산합니다.","조금만 약해져도 청산합니다.","더 약해질 때까지 보유합니다.","점 0~100",0,100)] public decimal ExitScoreThreshold {get;set;}=30;
    [Parameter("거래량 감소 기준","현재/진입 거래량 속도가 이 비율 이하이면 청산합니다.","작은 감소에도 청산합니다.","큰 감소를 허용합니다.","비율",0,1)] public decimal VolumeDecayThreshold {get;set;}=.3m;
    [Parameter("거래량 감소 시 청산","거래량 감소 기준을 청산에 적용합니다.","켜면 적용합니다.","끄면 적용하지 않습니다.","ON / OFF")] public bool ExitOnVolumeDecay {get;set;}=true;
}
public sealed class BoxAdvancedConfig
{
    [Parameter("박스 계산기간","박스권 계산에 사용할 기간입니다.","긴 구조를 봅니다.","짧은 구조를 봅니다.","분",1,1440)] public int RangeLookbackMinutes {get;set;}=10;
    [Parameter("최대 가격폭","박스권으로 인정할 고점·저점의 최대 차이입니다.","넓은 박스도 허용합니다.","좁은 박스만 허용합니다.","%",0.01,100)] public decimal MaximumRangePercent {get;set;}=5;
    [Parameter("ATR 비율","평균 틱 가격변화/박스폭의 최대 비율입니다.","거친 움직임도 허용합니다.","안정적인 횡보만 허용합니다.","비율",0.001,10)] public decimal ATRRatioThreshold {get;set;}=.3m;
    [Parameter("최소 지지 접촉","지지선에 독립적으로 접촉해야 하는 횟수입니다.","검증된 지지가 필요합니다.","적은 접촉도 허용합니다.","회",1,100)] public int MinimumSupportTouches {get;set;}=3;
    [Parameter("최소 저항 접촉","저항선에 독립적으로 접촉해야 하는 횟수입니다.","반복 저항을 요구합니다.","적은 접촉도 허용합니다.","회",1,100)] public int MinimumResistanceTouches {get;set;}=3;
    [Parameter("지지선 허용 거리","지지선 가격 대비 허용할 최대 거리입니다.","멀리서도 접근으로 봅니다.","지지선 가까이에서만 진입합니다.","%",0.001,20)] public decimal SupportDistancePercent {get;set;}=.3m;
    [Parameter("저항선 접근 거리","상단 청산을 판단할 저항선 가격 대비 거리입니다.","상단에 일찍 접근한 것으로 봅니다.","저항선 바로 근처까지 기다립니다.","%",0.001,20)] public decimal ResistanceDistancePercent {get;set;}=.3m;
    [Parameter("저거래량 백분위","박스 안의 최근 거래량이 과거 분포에서 이 백분위 이하인지 평가합니다.","보통 거래량도 횡보로 인정합니다.","더 낮은 거래량을 요구합니다.","백분위",0,100)] public decimal LowVolumePercentile {get;set;}=50;
    [Parameter("거래량 재유입 배수","하단에서 최근 거래량/이전 평균 거래량의 최소 배수입니다.","강한 유입을 요구합니다.","작은 유입도 허용합니다.","배",0.01,1000)] public decimal VolumeReentryRatio {get;set;}=2;
    [Parameter("지지선 점수","지지선 접촉·거리로 계산한 최소 점수입니다.","명확한 지지를 요구합니다.","완화된 지지도 허용합니다.","점",0,100)] public decimal SupportScoreThreshold {get;set;}=60;
    [Parameter("박스권 점수","폭·변동성·접촉·저거래량 충족 비율의 최소 점수입니다.","더 많은 박스 조건을 요구합니다.","일부 조건이 약해도 허용합니다.","점",0,100)] public decimal RangeScoreThreshold {get;set;}=60;
    [Parameter("하단 이탈률","지지선 아래로 허용할 하락 폭입니다.","큰 이탈까지 기다립니다.","작은 이탈에도 청산합니다.","%",0.001,99)] public decimal BreakdownPercent {get;set;}=1;
    [Parameter("상단 자동청산","저항선 접근 시 청산합니다.","켜면 적용합니다.","끄면 적용하지 않습니다.","ON / OFF")] public bool ExitAtResistance {get;set;}=true;
    [Parameter("하단 이탈 손절","지지선 이탈 시 청산합니다.","켜면 적용합니다.","끄면 적용하지 않습니다.","ON / OFF")] public bool StopOnBreakdown {get;set;}=true;
}
public sealed class ReboundAdvancedConfig
{
    [Parameter("반등 약화 청산폭","진입 후 관측한 최고가에서 이 비율만큼 내려오면 청산합니다. 0은 해제입니다.","더 큰 되돌림을 허용합니다.","작은 반등 약화에도 청산합니다.","%",0,99)] public decimal ExitRetracementPercent {get;set;}
    [Parameter("급락 계산기간","급락률을 계산할 기간입니다.","긴 하락을 포함합니다.","짧은 하락만 평가합니다.","초",1,86400)] public int CrashLookbackSeconds {get;set;}=180;
    [Parameter("최소 급락률","급락으로 인정할 최소 하락률 크기입니다.","큰 하락이 필요합니다.","작은 하락도 허용합니다.","%",0.01,99)] public decimal MinimumCrashPercent {get;set;}=10;
    [Parameter("최대 급락률","이를 넘는 지나친 하락은 진입 대상에서 제외합니다.","더 큰 폭락도 허용합니다.","심한 폭락을 빨리 제외합니다.","%",0.01,99)] public decimal MaximumCrashPercent {get;set;}=40;
    [Parameter("급락 속도","구간 하락률/기간으로 계산한 최소 하락속도입니다.","더 빠른 급락을 요구합니다.","느린 하락도 허용합니다.","%/초",0,100)] public decimal CrashVelocity {get;set;}=.03m;
    [Parameter("급락 거래량 배수","급락 구간의 이전 평균 대비 거래량 증가배수입니다.","강한 거래량 증가를 요구합니다.","적은 거래량도 허용합니다.","배",0.01,1000)] public decimal CrashVolumeRatio {get;set;}=2;
    [Parameter("최소 반등률","관측 저점 이후 필요한 반등률입니다.","더 큰 반등을 확인합니다.","작은 반등에서 후보가 됩니다.","%",0.01,100)] public decimal MinimumReboundPercent {get;set;}=2;
    [Parameter("반등 확인시간","반등 기준 이상으로 유지되어야 하는 시간입니다.","오래 유지된 반등을 요구합니다.","짧은 반등도 허용합니다.","초",1,3600)] public int ReboundConfirmationSeconds {get;set;}=3;
    [Parameter("반등 매수 비율","매수 방향 체결의 최소 비율입니다. 0은 해제, 방향 데이터가 없으면 진입을 보류합니다.","매수 우세가 강해야 합니다.","작은 매수 우세도 허용합니다.","비율",0,1)] public decimal BuyTradeRatio {get;set;}=0;
    [Parameter("매수호가 회복 비율","급락 후보 등록 시 매수 잔량 대비 현재 잔량의 최소 비율입니다. 0은 해제입니다.","매수 잔량이 더 회복되어야 합니다.","작은 회복도 허용합니다.","배",0,100)] public decimal BidRecoveryRatio {get;set;}=0;
    [Parameter("급락 점수","하락률·속도·거래량 충족도의 최소 종합 점수입니다.","뚜렷한 급락을 요구합니다.","완화된 급락을 허용합니다.","점",0,100)] public decimal CrashScoreThreshold {get;set;}=60;
    [Parameter("반등 점수","반등률·확인시간·매수 흐름의 최소 종합 점수입니다.","명확한 반등만 허용합니다.","약한 반등도 허용합니다.","점",0,100)] public decimal ReboundScoreThreshold {get;set;}=60;
    [Parameter("거래량 확인","급락 거래량 조건을 적용합니다.","켜면 거래량을 확인합니다.","끄면 생략합니다.","ON / OFF")] public bool ConfirmVolume {get;set;}=true;
    [Parameter("뉴스/공시 필터","실제 해당 시점의 뉴스 검증 자료가 없으면 신규 진입을 보류합니다.","켜면 미확인 위험을 차단합니다.","끄면 뉴스 위험을 평가하지 않습니다.","ON / OFF")] public bool NewsFilter {get;set;}=true;
    [Parameter("자동매수","켜면 모의 주문, 끄면 신호만 표시합니다. 실제 계좌 자동 주문은 하지 않습니다.","켜면 모의 주문을 허용합니다.","끄면 신호만 기록합니다.","ON / OFF")] public bool AutoBuy {get;set;}=false;
}
public sealed class AdvancedRiskConfig
{
    [Parameter("종목당 최대 비중","한 종목에 투자할 수 있는 평가자산의 최대 비율입니다.","집중 투자를 허용합니다.","종목 비중을 줄입니다.","%",0.01,100)] public decimal MaxPositionPercent {get;set;}=20;
    [Parameter("일 손실 한도","하루 손실이 이 금액에 도달하면 신규 진입을 중단합니다.","큰 일 손실을 허용합니다.","더 일찍 신규 거래를 중단합니다.","계좌 통화",0.01,1e12)] public decimal DailyLossLimit {get;set;}=300;
    [Parameter("연속 손실 제한","연속 손실 청산 횟수에 도달하면 당일 신규 진입을 제한합니다.","더 많은 연속 손실을 허용합니다.","빨리 거래를 제한합니다.","회",1,100)] public int MaxConsecutiveLosses {get;set;}=3;
    [Parameter("동시 포지션 한도","보유와 미체결 매수 예약을 합한 종목 수의 한도입니다.","더 많은 종목을 허용합니다.","보유 종목을 줄입니다.","종목",1,100)] public int MaxOpenPositions {get;set;}=5;
    [Parameter("재진입 대기","청산 후 동일 종목 진입까지 기다립니다.","반복 거래가 줄어듭니다.","더 빠르게 재진입합니다.","초",1,86400)] public int ReentryCooldown {get;set;}=30;
}
public sealed class AdvancedExecutionConfig
{
    [Parameter("주문 방식","시장가 또는 지정가로 모의 주문을 제출합니다.","시장가는 체결가격 변동이 있습니다.","지정가는 미체결될 수 있습니다.","시장가 / 지정가")] public OrderKind OrderType {get;set;}=OrderKind.Limit;
    [Parameter("슬리피지","예상가격에 불리한 방향으로 적용할 체결가격 차이입니다.","백테스트 비용이 커집니다.","가격 차이 가정을 줄입니다.","%",0,20)] public decimal SlippagePercent {get;set;}=.02m;
    [Parameter("지정가 호가 거리","전략 가격에서 매수는 위, 매도는 아래로 조정할 틱 수입니다.","체결 가능성과 가격 양보가 커집니다.","원래 가격에 가까워집니다.","틱",0,100)] public int LimitOrderOffsetTicks {get;set;}=0;
    [Parameter("주문 대기시간","미체결 주문은 이 시간이 지난 다음 이벤트에서 취소를 요청합니다.","체결을 더 오래 기다립니다.","빨리 취소를 요청합니다.","밀리초",1,86400000)] public int OrderTimeoutMilliseconds {get;set;}=20000;
    [Parameter("거래 수수료","매수·매도 체결금액에 각각 적용할 수수료입니다.","거래비용이 커집니다.","거래비용이 작아집니다.","%",0,20)] public decimal Commission {get;set;}=.05m;
    [Parameter("거래세","매도 체결금액에 적용할 세율입니다. 시장별 실제 세율은 직접 설정하세요.","매도 비용이 커집니다.","매도 비용이 작아집니다.","%",0,20)] public decimal Tax {get;set;}=0;
}
public sealed class AdvancedStrategyConfig
{
    public StrategyType StrategyType {get;set;}
    public CommonStrategyConfig Common {get;set;}=new();
    public ScalpAdvancedConfig Scalping {get;set;}=new();
    public BoxAdvancedConfig Box {get;set;}=new();
    public ReboundAdvancedConfig Rebound {get;set;}=new();
    public AdvancedRiskConfig Risk {get;set;}=new();
    public AdvancedExecutionConfig Execution {get;set;}=new();
    public void Validate()=>StrategyParameterValidation.Validate(this);
}
public sealed class StrategyConfiguration
{
    public Stock.Core.Pipeline.PipelineConfiguration Pipeline {get;set;}=new();
    public bool Enabled {get;set;}
    public StrategyType StrategyType {get;set;}
    public StrategyConfigMode Mode {get;set;}
    public StrategyConfigState State {get;set;}
    public StandardStrategyConfig Standard {get;set;}=new();
    public AdvancedStrategyConfig Advanced {get;set;}=new();
    public AdvancedStrategyConfig Resolve()
    {
        var result=Mode==StrategyConfigMode.Standard?StrategyPresetMapper.ConvertToAdvanced(StrategyType,Standard,Advanced):Serialization.Unpack<AdvancedStrategyConfig>(Serialization.Pack(Advanced));
        result.StrategyType=StrategyType;result.Validate();return result;
    }
}
