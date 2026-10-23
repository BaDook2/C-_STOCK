using Stock.Core.Trading;
namespace Stock.Core.Pipeline;
public enum PipelineMarket { US, Korea, All }
public enum RankingSource { Manual, CSV, Mock, Toss, Kiwoom }
public enum PipelineStrategy { TickScalping, Farmer }
public sealed class PipelineConfiguration
{
    public PipelineStandardConfig Standard {get;set;}=new();
    public PipelineAdvancedConfig Advanced {get;set;}=new();
    public PipelineAdvancedConfig Resolve(StrategyConfigMode mode)=>mode==StrategyConfigMode.Standard?PipelinePresetMapper.Convert(Standard,Advanced):Serialization.Unpack<PipelineAdvancedConfig>(Serialization.Pack(Advanced));
}
public sealed class PipelineAdvancedConfig
{
    public UniverseFilterConfig Universe {get;set;}=new();
    public AttentionFilterConfig Attention {get;set;}=new();
    public PriceActionFilterConfig PriceAction {get;set;}=new();
    public MicrostructureFilterConfig Microstructure {get;set;}=new();
    public TickScalpingStrategyConfig TickScalping {get;set;}=new();
    public FarmerStrategyConfig Farmer {get;set;}=new();
    public PortfolioPolicyConfig Portfolio {get;set;}=new();
    public AdvancedRiskConfig Risk {get;set;}=new();
    public AdvancedExecutionConfig Execution {get;set;}=new();
    public PipelineStrategy Strategy {get;set;}
    public IEnumerable<(string Name,object Config)> Sections()=>[("Universe",Universe),("Attention Filter",Attention),("Price Action",PriceAction),("Microstructure",Microstructure),("Strategy A · 틱떼기",TickScalping),("Strategy B · 농부매매",Farmer),("Portfolio",Portfolio),("Risk",Risk),("Execution",Execution)];
    public void Validate()=>PipelineValidation.Validate(this);
}
public sealed class PipelineStandardConfig
{
    public UniverseStandardConfig Universe {get;set;}=new();
    public AttentionStandardConfig Attention {get;set;}=new();
    public PriceActionStandardConfig PriceAction {get;set;}=new();
    public MicrostructureStandardConfig Microstructure {get;set;}=new();
    public TickStandardConfig TickScalping {get;set;}=new();
    public FarmerStandardConfig Farmer {get;set;}=new();
    public PortfolioStandardConfig Portfolio {get;set;}=new();
    public IEnumerable<(string Name,object Config)> Sections()=>[("Universe",Universe),("Attention Filter",Attention),("Price Action",PriceAction),("Microstructure",Microstructure),("Strategy A · 틱떼기",TickScalping),("Strategy B · 농부매매",Farmer),("Portfolio",Portfolio)];
}
public sealed class UniverseFilterConfig
{
    [Parameter("시장","관찰할 시장입니다. 통화가 다른 자산은 한 계좌에서 합산하지 않습니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","선택",0,1)] public PipelineMarket Market {get;set;}=PipelineMarket.US;
    [Parameter("가격 하한","관찰 대상의 최소 주가입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","계좌 통화",0.0001,1000000000)] public decimal MinPrice {get;set;}=0.01m;
    [Parameter("가격 상한","지정 가격 이하 종목만 후보에 포함합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","계좌 통화",0.0001,1000000000)] public decimal MaxPrice {get;set;}=5;
    [Parameter("시가총액 하한","종목 통화 기준 시가총액의 하한입니다. 0은 하한 해제입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","종목 통화",0,1000000000000000)] public decimal MinMarketCap {get;set;}=0;
    [Parameter("시가총액 상한","시가총액이 이 금액 이하인 후보를 관찰합니다. 자료가 없으면 보류합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","종목 통화",1,1000000000000000)] public decimal MaxMarketCap {get;set;}=300000000;
    [Parameter("OTC 포함","장외시장 자료를 후보에 허용합니다. 현재 주문 어댑터는 OTC를 지원하지 않습니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool IncludeOTC {get;set;}=false;
    [Parameter("순위 데이터 소스","수동·CSV·검증 Mock 또는 지원되는 API를 선택합니다. 미지원 API는 안내 후 보류합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","선택",0,1)] public RankingSource RankingSource {get;set;}=RankingSource.Manual;
    [Parameter("상위 순위 범위","검색 순위 후보로 포함할 최대 순위입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","위",1,100000)] public int TopRankCutoff {get;set;}=50;
    [Parameter("검색 상위 종목 사용","현재 순위가 상위 범위에 속한 종목을 후보에 포함합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool UseTopSearchSymbols {get;set;}=true;
    [Parameter("관심종목 사용","선택한 관심그룹 종목도 후보에 포함합니다. 겹치는 종목은 한 번만 검사합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool UseWatchlistSymbols {get;set;}=true;
    [Parameter("후보 갱신 주기","순위 스냅샷을 이 주기로 다시 읽습니다. 이전 자료가 지나치게 오래되면 보류합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","초",1,86400)] public int UniverseRefreshIntervalSeconds {get;set;}=60;
}
public sealed class AttentionFilterConfig
{
    [Parameter("최소 검색 상승속도","이전 순위에서 현재 순위로 상승한 순위 수를 분 단위로 나눈 값입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","위/분",0,100000)] public decimal SearchRankVelocityMin {get;set;}=0;
    [Parameter("현재 검색 순위 상한","이 순위 이내의 관심도를 요구합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","위",1,100000)] public int CurrentSearchRankMax {get;set;}=50;
    [Parameter("최소 당일 거래대금","당일 누적 거래대금의 하한입니다. 관측 일부 구간을 하루 전체로 추정하지 않습니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","종목 통화",0,1000000000000000)] public decimal DayTurnoverMin {get;set;}=100000;
    [Parameter("일평균 대비 거래량","당일 누적 거래량을 이전 20거래일 평균 거래량으로 나눈 배수입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","배",0,10000)] public decimal DayVolumeTo20DayAverageMin {get;set;}=1;
    [Parameter("동시간대 5분 거래량 배수","최근 5분 거래량과 이전 거래일 같은 시간대 평균의 비율입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","배",0,10000)] public decimal Intraday5MinVolumeVsSameTimeAverageMin {get;set;}=1;
    [Parameter("관심도 점수","순위·거래대금·거래량 조건의 평균 충족 점수 하한입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal AttentionScoreThreshold {get;set;}=50;
}
public sealed class PriceActionFilterConfig
{
    [Parameter("당일 등락률 하한","전일 종가 대비 현재 등락률의 최소값입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,100000)] public decimal DayChangePercentMin {get;set;}=-30;
    [Parameter("당일 등락률 상한","전일 종가 대비 현재 등락률의 최대값입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,100000)] public decimal DayChangePercentMax {get;set;}=100;
    [Parameter("갭 하한","당일 시가와 전일 종가의 차이를 비율로 계산합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,100000)] public decimal GapPercentMin {get;set;}=-30;
    [Parameter("갭 상한","당일 시가의 전일 종가 대비 최대 갭입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,100000)] public decimal GapPercentMax {get;set;}=100;
    [Parameter("최근 낙폭 탐색기간","이전 N거래일 최고 종가 대비 현재 낙폭을 평가합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","거래일",1,252)] public int RecentDrawdownDays {get;set;}=20;
    [Parameter("최근 낙폭 하한","최근 고점 대비 하락 폭을 양수로 입력합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0,100)] public decimal RecentNDayDrawdownMin {get;set;}=0;
    [Parameter("최근 낙폭 상한","과도한 낙폭의 종목을 제외하는 기준입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0,100)] public decimal RecentNDayDrawdownMax {get;set;}=80;
    [Parameter("VWAP 거리 하한","거래량 가중 평균가격 대비 현재가 차이의 최소값입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,100000)] public decimal VwapDeviationPercentMin {get;set;}=-10;
    [Parameter("VWAP 거리 상한","거래량 가중 평균가격 대비 현재가 차이의 최대값입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,100000)] public decimal VwapDeviationPercentMax {get;set;}=10;
    [Parameter("전저점 거리 상한","확정된 이전 저점 대비 현재가 거리의 최대값입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0,100000)] public decimal DistanceToPrevLowMax {get;set;}=10;
    [Parameter("저항선 여유","자동 계산된 저항선까지 최소 상승 여유입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0,100000)] public decimal DistanceToResistanceMin {get;set;}=0.5m;
    [Parameter("가격 구조 점수","등락·갭·낙폭·VWAP·지지저항 조건의 평균 충족 점수입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal PriceActionScoreThreshold {get;set;}=50;
}
public sealed class MicrostructureFilterConfig
{
    [Parameter("최대 스프레드","중간 호가 대비 매수·매도 차이의 최대 비율입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.0001,100)] public decimal MaxBidAskSpreadPercent {get;set;}=1;
    [Parameter("최소 매수잔량","관측된 매수 호가 잔량의 하한입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","주",0,1000000000)] public decimal MinBidDepth {get;set;}=100;
    [Parameter("최소 매도잔량","관측된 매도 호가 잔량의 하한입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","주",0,1000000000)] public decimal MinAskDepth {get;set;}=100;
    [Parameter("최소 호가 불균형","(매수잔량-매도잔량)/(매수잔량+매도잔량) 기준입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","비율",-1,1)] public decimal MinOrderBookImbalance {get;set;}=0;
    [Parameter("최소 체결강도","매수 방향 체결량/매도 방향 체결량 × 100입니다. 방향 미제공 시 보류합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0,100000)] public decimal ExecutionStrengthMin {get;set;}=100;
    [Parameter("1분 변동성 하한","최근 1분 로그 수익률 제곱합의 제곱근입니다. 연율화하지 않습니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0,1000)] public decimal RealizedVolatility1mMin {get;set;}=0;
    [Parameter("1분 변동성 상한","최근 실제 체결 변동성의 허용 상한입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.0001,1000)] public decimal RealizedVolatility1mMax {get;set;}=10;
    [Parameter("거래량 유지 비율","최근 5초 거래량 속도/이전 5초 속도의 최소 비율입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","배",0,1000)] public decimal VolumeDecayRateThreshold {get;set;}=0.5m;
    [Parameter("체결 구조 점수","호가·체결강도·변동성·거래량 유지의 평균 점수입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal MicrostructureScoreThreshold {get;set;}=50;
}
public sealed class TickScalpingStrategyConfig
{
    [Parameter("틱떼기 진입 점수","앞 단계 평균 점수가 이 기준 이상이면 진입 후보입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal EntryScoreThreshold {get;set;}=60;
    [Parameter("지정가 진입 거리","매수 호가에서 위쪽으로 조정할 틱 수입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","틱",0,100)] public int LimitEntryOffsetTicks {get;set;}=0;
    [Parameter("틱떼기 익절률","평균 진입가 대비 목표 수익률입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.001,10000)] public decimal TakeProfitPercent {get;set;}=0.8m;
    [Parameter("틱떼기 손절률","음수로 입력하는 진입가 대비 손실 기준입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",-99,-0.001)] public decimal StopLossPercent {get;set;}=-0.8m;
    [Parameter("틱떼기 최대 보유","최초 체결 후 이 시간이 지나면 다음 이벤트에서 청산합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","초",1,86400)] public int MaxHoldSeconds {get;set;}=300;
    [Parameter("유동성 급감 청산","진입 뒤 거래량 속도가 크게 낮아지면 청산합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool LiquidityDropExit {get;set;}=true;
    [Parameter("청산 거래량 유지율","현재 속도/진입 시 속도가 이 값 이하이면 청산합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","비율",0,1)] public decimal VolumeDecayExitThreshold {get;set;}=0.4m;
    [Parameter("지지선 이탈 청산","진입 시 저장한 지지선을 하향 이탈하면 청산합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool SupportBreakExit {get;set;}=true;
    [Parameter("지지선 이탈 여유","지지선보다 이 비율 이상 떨어지면 청산합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.001,99)] public decimal SupportBreakPercent {get;set;}=0.5m;
    [Parameter("틱 재진입 대기","같은 종목의 마지막 매도 후 기다리는 시간입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","초",1,86400)] public int TickReentryCooldownSeconds {get;set;}=30;
    [Parameter("틱 주문 대기시간","미체결 지정가를 취소 요청할 시간입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","밀리초",1,86400000)] public int TickOrderTimeoutMilliseconds {get;set;}=5000;
}
public sealed class FarmerStrategyConfig
{
    [Parameter("농부 초기 진입 비중","평가자산 중 최초 진입에 사용할 비율입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.01,100)] public decimal InitialPositionPercent {get;set;}=2;
    [Parameter("추가 진입","새로운 조건 충족 시 최초 진입 이후 한 번만 추가 진입을 허용합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool AdditionalEntryEnabled {get;set;}=false;
    [Parameter("추가 진입 점수","전체 진입 점수가 이 기준 이상일 때 추가 진입을 허용합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal AdditionalEntryConditionScore {get;set;}=80;
    [Parameter("추세 유지 점수","가격 구조와 실제 체결의 점수가 이 기준 이상이면 유지합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal TrendContinuationScoreThreshold {get;set;}=60;
    [Parameter("가설 훼손 점수","확인된 가격·체결 점수가 이 기준 이하이면 가설 훼손으로 청산합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","점",0,100)] public decimal ThesisInvalidationScoreThreshold {get;set;}=25;
    [Parameter("추적 손절률","보유 중 관측 최고가 대비 하락 허용폭입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.01,99)] public decimal TrailingStopPercent {get;set;}=15;
    [Parameter("일부 수익 실현","목표 수익에 도달하면 최초 보유량의 일부를 한 번 수익 실현합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool PartialHarvestEnabled {get;set;}=true;
    [Parameter("일부 수확 수익률","부분 청산을 시작할 진입가 대비 수익률입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.01,100000)] public decimal PartialHarvestTriggerPercent {get;set;}=50;
    [Parameter("일부 수확 비율","해당 포지션의 일부를 청산할 비율입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","비율",0.01,0.99)] public decimal PartialHarvestRatio {get;set;}=0.5m;
    [Parameter("전량 수익 실현","큰 목표 수익에 도달하면 남은 포지션을 청산합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool FullHarvestEnabled {get;set;}=true;
    [Parameter("전량 수확 수익률","전량 청산할 진입가 대비 수익률입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.01,100000)] public decimal FullHarvestTriggerPercent {get;set;}=200;
    [Parameter("농부 최대 보유기간","달력일 기준 최대 보유기간입니다. 이벤트가 없으면 다음 관측에서 판단합니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","일",1,3650)] public int FarmerMaxHoldDays {get;set;}=90;
    [Parameter("농부 신호만 표시","켜면 진입 신호만 표시하고 모의 자동 주문을 만들지 않습니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool FarmerSignalOnlyMode {get;set;}=true;
}
public sealed class PortfolioPolicyConfig
{
    [Parameter("페니스탁 예산","총 평가자산 중 파이프라인 포지션과 미체결 매수 예약의 최대 비중입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","%",0.01,100)] public decimal MaxPennyPortfolioPercent {get;set;}=10;
    [Parameter("페니스탁 최대 종목","보유 및 미체결 진입을 합친 종목 수 제한입니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","종목",1,100)] public int MaxOpenPennyPositions {get;set;}=5;
    [Parameter("수익 Core 배정","순실현이익 일부를 모의 Core 자금으로 배정하여 신규 투자에서 제외합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool ProfitTransferEnabled {get;set;}=false;
    [Parameter("Core 배정 비율","누적 순실현이익 중 Core로 배정할 비율입니다. 실제 송금하지 않습니다.","기준값을 높입니다. 최소 조건은 엄격해지고 최대 허용 조건은 완화됩니다.","기준값을 낮춥니다. 최소 조건은 완화되고 최대 허용 조건은 엄격해집니다.","비율",0,1)] public decimal ProfitTransferRatio {get;set;}=0.5m;
    [Parameter("Core 배정 목적지","모의 장부의 Core 배정을 허용합니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool CorePortfolioDestinationEnabled {get;set;}=true;
    [Parameter("예산 초과 진입 차단","신규 주문을 포함한 비중이 페니스탁 예산을 넘으면 진입을 막습니다.","켜면 이 기능을 적용합니다.","끄면 이 기능을 적용하지 않습니다.","ON / OFF",0,1)] public bool NewEntriesBlockedWhenBudgetExceeded {get;set;}=true;
}
public sealed class UniverseStandardConfig
{
    [Parameter("시장","관찰할 시장입니다.","선택 시장에 적용합니다.","선택 시장에 적용합니다.","선택")] public PipelineMarket Market {get;set;}=PipelineMarket.US;
    [Parameter("가격 상한","이 가격 이하 종목을 관찰합니다.","더 많은 후보를 포함합니다.","저가 종목으로 좁힙니다.","계좌 통화",0.0001,1000000000)] public decimal MaxPrice {get;set;}=5;
    [Parameter("시가총액 상한","시가총액 최대값입니다.","큰 회사도 포함합니다.","작은 회사로 좁힙니다.","종목 통화",1,1000000000000000)] public decimal MaxMarketCap {get;set;}=300000000;
    [Parameter("순위 데이터 소스","수동·CSV·검증 자료 또는 지원되는 API입니다.","선택한 소스를 사용합니다.","선택한 소스를 사용합니다.","선택")] public RankingSource RankingSource {get;set;}=RankingSource.Manual;
    [Parameter("검색 상위 종목 사용","상위 순위 후보를 포함합니다.","켜면 포함합니다.","끄면 관심종목만 사용합니다.","ON / OFF")] public bool UseTopSearchSymbols {get;set;}=true;
}
public sealed class AttentionStandardConfig
{
    [Parameter("검색 관심 강도","순위가 빠르게 상승하고 상위에 있는 종목을 선호합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int SearchAttentionStrength {get;set;}=3;
    [Parameter("거래대금 강도","당일 거래대금 하한을 조절합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int TurnoverStrength {get;set;}=3;
    [Parameter("상대 거래량 강도","평소와 동시간대 대비 거래량 배수 하한을 조절합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int RelativeVolumeStrength {get;set;}=3;
}
public sealed class PriceActionStandardConfig
{
    [Parameter("당일 등락 조건","당일 등락률 허용 범위를 조절합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int DayChangeFilter {get;set;}=3;
    [Parameter("갭 조건","시가의 전일 종가 대비 갭 범위를 조절합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int GapFilter {get;set;}=3;
    [Parameter("최근 낙폭 조건","최근 고점 대비 허용 낙폭 범위를 조절합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int RecentDrawdownCondition {get;set;}=3;
    [Parameter("VWAP 거리 조건","거래량 가중 평균가격과의 거리 허용폭입니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int VwapDistanceCondition {get;set;}=3;
    [Parameter("지지저항 위치","전저점 접근과 저항선 여유를 조절합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int SupportResistancePosition {get;set;}=3;
}
public sealed class MicrostructureStandardConfig
{
    [Parameter("스프레드 엄격도","높이면 좁은 스프레드를 요구합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int SpreadTolerance {get;set;}=3;
    [Parameter("유동성 품질","높이면 양쪽 호가 잔량을 더 요구합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int LiquidityQuality {get;set;}=3;
    [Parameter("체결 강도","높이면 매수 방향 체결 우위를 더 요구합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int ExecutionStrength {get;set;}=3;
    [Parameter("변동성 안정도","높이면 낮은 1분 변동성을 요구합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int VolatilityStability {get;set;}=3;
    [Parameter("거래량 감소 민감도","높이면 거래량 감소를 덜 허용합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int VolumeDecaySensitivity {get;set;}=3;
}
public sealed class TickStandardConfig
{
    [Parameter("틱떼기 진입 엄격도","높이면 진입 종합 점수 하한이 높아집니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int EntryStrictness {get;set;}=3;
    [Parameter("틱떼기 익절률","평균 진입가 대비 목표 수익률입니다.","높은 기준을 적용합니다.","낮은 기준을 적용합니다.","%",0.001,10000)] public decimal TakeProfitPercent {get;set;}=0.8m;
    [Parameter("틱떼기 손절률","음수로 입력하는 진입가 대비 손실 기준입니다.","높은 기준을 적용합니다.","낮은 기준을 적용합니다.","%",-99,-0.001)] public decimal StopLossPercent {get;set;}=-0.8m;
    [Parameter("틱떼기 최대 보유","최초 체결 후 이 시간이 지나면 다음 이벤트에서 청산합니다.","높은 기준을 적용합니다.","낮은 기준을 적용합니다.","초",1,86400)] public int MaxHoldSeconds {get;set;}=300;
    [Parameter("유동성 급감 청산","진입 뒤 거래량 속도가 크게 낮아지면 청산합니다.","높은 기준을 적용합니다.","낮은 기준을 적용합니다.","ON / OFF",0,1)] public bool LiquidityDropExit {get;set;}=true;
    [Parameter("지지선 이탈 청산","진입 시 저장한 지지선을 하향 이탈하면 청산합니다.","높은 기준을 적용합니다.","낮은 기준을 적용합니다.","ON / OFF",0,1)] public bool SupportBreakExit {get;set;}=true;
}
public sealed class FarmerStandardConfig
{
    [Parameter("추세 유지 강도","높이면 유지에 필요한 가격·체결 점수가 높아집니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int TrendHoldingStrength {get;set;}=3;
    [Parameter("가설 훼손 민감도","높이면 작은 약화에도 청산 기준에 도달합니다.","더 엄격하게 판단합니다.","더 넓은 후보를 허용합니다.","1 완화 · 3 보통 · 5 엄격",1,5)] public int ThesisInvalidationStrictness {get;set;}=3;
    [Parameter("농부 초기 진입 비중","평가자산 중 최초 진입에 사용할 비율입니다.","비중 또는 기능 적용을 늘립니다.","비중 또는 기능 적용을 줄입니다.","%",0.01,100)] public decimal InitialPositionPercent {get;set;}=2;
    [Parameter("일부 수익 실현","목표 수익에 도달하면 최초 보유량의 일부를 한 번 수익 실현합니다.","비중 또는 기능 적용을 늘립니다.","비중 또는 기능 적용을 줄입니다.","ON / OFF",0,1)] public bool PartialHarvestEnabled {get;set;}=true;
    [Parameter("전량 수익 실현","큰 목표 수익에 도달하면 남은 포지션을 청산합니다.","비중 또는 기능 적용을 늘립니다.","비중 또는 기능 적용을 줄입니다.","ON / OFF",0,1)] public bool FullHarvestEnabled {get;set;}=true;
    [Parameter("농부 자동매수","켜면 모의 자동 진입, 끄면 신호만 표시합니다.","모의 자동 진입합니다.","신호만 표시합니다.","ON / OFF")] public bool AutoBuy {get;set;}
}
public sealed class PortfolioStandardConfig {
    [Parameter("페니스탁 예산","총 평가자산 중 파이프라인 포지션과 미체결 매수 예약의 최대 비중입니다.","배정 또는 적용을 늘립니다.","배정 또는 적용을 줄입니다.","%",0.01,100)] public decimal MaxPennyPortfolioPercent {get;set;}=10;
    [Parameter("수익 Core 배정","순실현이익 일부를 모의 Core 자금으로 배정하여 신규 투자에서 제외합니다.","배정 또는 적용을 늘립니다.","배정 또는 적용을 줄입니다.","ON / OFF",0,1)] public bool ProfitTransferEnabled {get;set;}=false;
    [Parameter("Core 배정 비율","누적 순실현이익 중 Core로 배정할 비율입니다. 실제 송금하지 않습니다.","배정 또는 적용을 늘립니다.","배정 또는 적용을 줄입니다.","비율",0,1)] public decimal ProfitTransferRatio {get;set;}=0.5m;
}
