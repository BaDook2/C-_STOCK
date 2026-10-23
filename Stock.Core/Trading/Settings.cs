using System.Reflection;

namespace Stock.Core.Trading;
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string label,double min=0,double max=1000000000000) : Attribute
{ public string Label { get; }=label; public double Min { get; }=min; public double Max { get; }=max; }
public enum SizingMode { FixedCash, EquityPercent, FixedShares, RiskBased }
public enum DistanceMode { Percent, Ticks, Atr }
public enum ScalpExit { Ask, Ticks, Percent }
public class ScalpSettings
{
    [Setting("전략 실행")] public bool Enabled { get; set; }=true;
    [Setting("최소 가격",.0001)] public decimal MinPrice { get; set; }=.01m;
    [Setting("최대 가격",.0001)] public decimal MaxPrice { get; set; }=10;
    [Setting("평균 거래량 창 (초)",2,86400)] public int AverageSeconds { get; set; }=120;
    [Setting("현재 거래량 창 (초)",1,3600)] public int CurrentSeconds { get; set; }=20;
    [Setting("초당 거래량 증가 배수",.01,1000)] public decimal RelativeVolume { get; set; }=2;
    [Setting("최소 초당 체결 수",0,10000)] public decimal TradesPerSecond { get; set; }=1;
    [Setting("최대 스프레드 %",0,100)] public decimal MaxSpread { get; set; }=2;
    [Setting("최소 Bid/Ask 잔량 비율",0,10000)] public decimal BidAskRatio { get; set; }=1;
    [Setting("최소 Bid 잔량")] public decimal MinBidSize { get; set; }=10;
    [Setting("최소 Ask 잔량")] public decimal MinAskSize { get; set; }=10;
    [Setting("L2 잔량 합산 (실제 깊이 필요)")] public bool UseLevel2 { get; set; }
    [Setting("매수 Bid + 틱",0,100)] public int BuyTicks { get; set; }
    [Setting("매도 방식")] public ScalpExit Exit { get; set; }=ScalpExit.Ask;
    [Setting("매도 이익 틱 / %",.0001,10000)] public decimal ExitValue { get; set; }=2;
    [Setting("약화 단기 창 (초)",1,3600)] public int WeakShortSeconds { get; set; }=10;
    [Setting("약화 장기 창 (초)",2,86400)] public int WeakLongSeconds { get; set; }=60;
    [Setting("약화 초당 거래량 비율",0,1)] public decimal WeakVolumeRatio { get; set; }=.3m;
    [Setting("약화 체결빈도 비율",0,1)] public decimal WeakTradesRatio { get; set; }=.3m;
    [Setting("약화 스프레드 증가 배수",1,100)] public decimal WeakSpreadMultiple { get; set; }=2;
    [Setting("약화 충족 수 (3개 중)",1,3)] public int WeakCount { get; set; }=2;
}
public class BoxSettings
{
    [Setting("확정 지지선 근처에서만 진입")] public bool UseSupportLevels {get;set;}
    [Setting("실제 틱 매물대 근처에서만 진입")] public bool UseVolumeProfile {get;set;}
    [Setting("전략 실행")] public bool Enabled { get; set; }=true;
    [Setting("박스 탐색 창 (초)",10,86400)] public int LookbackSeconds { get; set; }=180;
    [Setting("최소 지속 (초)",5,86400)] public int MinimumSeconds { get; set; }=120;
    [Setting("최대 박스 폭 %",.01,100)] public decimal WidthPercent { get; set; }=5;
    [Setting("최소 지지 터치",1,100)] public int SupportTouches { get; set; }=3;
    [Setting("최소 저항 터치",1,100)] public int ResistanceTouches { get; set; }=3;
    [Setting("터치 오차 %",.001,20)] public decimal TolerancePercent { get; set; }=.3m;
    [Setting("독립 터치 최소 간격 (초)",1,3600)] public int TouchGapSeconds { get; set; }=5;
    [Setting("최대 초당 거래량",.001)] public decimal MaxVolumePerSecond { get; set; }=10000;
    [Setting("거래량 감소 필요")] public bool DecliningVolume { get; set; }
    [Setting("매수 하단 영역 %",0,49)] public decimal EntryZonePercent { get; set; }=15;
    [Setting("매도 상단 영역 %",0,49)] public decimal ExitZonePercent { get; set; }=15;
    [Setting("박스 붕괴 여유 %",0,100)] public decimal BreakPercent { get; set; }=1;
}
public class ReboundSettings
{
    [Setting("전략 실행")] public bool Enabled { get; set; }=true;
    [Setting("하락 조건1 사용")] public bool Use1 { get; set; }=true;
    [Setting("측정1 (초)",1,86400)] public int Seconds1 { get; set; }=60;
    [Setting("하락1 크기 %",.01,99)] public decimal Drop1 { get; set; }=10;
    [Setting("하락 조건2 사용")] public bool Use2 { get; set; }=true;
    [Setting("측정2 (초)",1,86400)] public int Seconds2 { get; set; }=300;
    [Setting("하락2 크기 %",.01,99)] public decimal Drop2 { get; set; }=15;
    [Setting("하락 조건3 사용")] public bool Use3 { get; set; }
    [Setting("측정3 (초)",1,86400)] public int Seconds3 { get; set; }=1800;
    [Setting("하락3 크기 %",.01,99)] public decimal Drop3 { get; set; }=25;
    [Setting("관측 당일 고점 조건 사용")] public bool UseHigh { get; set; }
    [Setting("관측 고점 대비 하락 %",.01,99)] public decimal HighDrop { get; set; }=40;
    [Setting("모두 충족 (OFF = 하나 이상)")] public bool RequireAll { get; set; }
    [Setting("후보 저점 대비 반등 %",.01,100)] public decimal BouncePercent { get; set; }=2;
    [Setting("반등 확인 연속 상승 틱",1,100)] public int ConfirmTicks { get; set; }=3;
    [Setting("후보 유효 시간 (초)",1,86400)] public int CandidateSeconds { get; set; }=600;
}
public class AnalysisSettings
{
    [Setting("지지저항 창 (초)",5,86400)] public int LookbackSeconds { get; set; }=180;
    [Setting("확정 Pivot 좌우 틱 수",1,100)] public int PivotRadius { get; set; }=3;
    [Setting("지지저항 오차 %",.001,10)] public decimal TolerancePercent { get; set; }=.3m;
    [Setting("지지저항 최소 터치",1,100)] public int MinimumTouches { get; set; }=2;
    [Setting("매물대 창 (초)",5,86400)] public int ProfileSeconds { get; set; }=180;
    [Setting("매물대 가격구간 틱 수",1,1000)] public int ProfileTicks { get; set; }=5;
    [Setting("표시 매물대 수",1,30)] public int ProfileTop { get; set; }=5;
}
public class RiskSettings
{
    [Setting("계좌 기준 자금 (선택 통화)",1)] public decimal AccountCapital { get; set; }=10000;
    [Setting("1회 최대 투자금",.01)] public decimal MaxOrderCash { get; set; }=1000;
    [Setting("1종목 최대 투자금",.01)] public decimal MaxSymbolCash { get; set; }=2000;
    [Setting("1종목 최대 주수",1,1000000)] public int MaxShares { get; set; }=10000;
    [Setting("동시 보유 종목 한도",1,100)] public int MaxPositions { get; set; }=5;
    [Setting("하루 주문 횟수 한도",1,100000)] public int MaxDailyTrades { get; set; }=100;
    [Setting("하루 최대 손실 금액",.01)] public decimal MaxDailyLoss { get; set; }=300;
    [Setting("하루 최대 손실 %",.01,100)] public decimal MaxDailyLossPercent { get; set; }=3;
    [Setting("건별 손실 한도 금액",.01)] public decimal MaxTradeLoss { get; set; }=100;
    [Setting("건별 손실 한도 %",.01,100)] public decimal MaxTradeLossPercent { get; set; }=5;
    [Setting("사이징 방식")] public SizingMode Sizing { get; set; }=SizingMode.FixedCash;
    [Setting("고정 투자금",.01)] public decimal FixedCash { get; set; }=500;
    [Setting("계좌 투자 비율 %",.01,100)] public decimal EquityPercent { get; set; }=5;
    [Setting("고정 주식 수",1,1000000)] public int FixedShares { get; set; }=10;
    [Setting("위험기반 예산 %",.001,100)] public decimal RiskPercent { get; set; }=.5m;
    [Setting("손절 방식")] public DistanceMode StopMode { get; set; }=DistanceMode.Percent;
    [Setting("손절 % / 틱 / ATR 배수",.001,10000)] public decimal StopValue { get; set; }=2;
    [Setting("익절 방식")] public DistanceMode TakeMode { get; set; }=DistanceMode.Percent;
    [Setting("익절 % / 틱 / ATR 배수",.001,10000)] public decimal TakeValue { get; set; }=3;
    [Setting("ATR 과거 틱 수",2,10000)] public int AtrPeriod { get; set; }=20;
    [Setting("긴급중지시 로컬 강제청산 (별도)")] public bool FlattenOnStop { get; set; }
}
public class ExecutionSettings
{
    public decimal TaxPercent {get;set;}
    public int? TimeoutMilliseconds {get;set;}
    [Setting("주문 방식")] public OrderKind Kind { get; set; }=OrderKind.Limit;
    [Setting("지정가 TTL (초)",1,86400)] public int TtlSeconds { get; set; }=20;
    [Setting("부분체결 허용")] public bool AllowPartial { get; set; }=true;
    [Setting("부분체결 후 잔량 취소")] public bool CancelRemainder { get; set; }
    [Setting("취소 확인 후 로컬 재주문")] public bool Retry { get; set; }
    [Setting("최대 재주문 수",0,10)] public int MaxRetries { get; set; }=1;
    [Setting("재주문 가격 변경 틱",0,100)] public int RetryTicks { get; set; }=1;
    [Setting("수수료 %",0,10)] public decimal FeePercent { get; set; }=.05m;
    [Setting("슬리피지 %",0,10)] public decimal SlippagePercent { get; set; }=.02m;
    [Setting("호가 사용 (OFF=체결량 보수모델)")] public bool UseSpread { get; set; }=true;
    [Setting("체결량/잔량 참여율 %",.01,100)] public decimal ParticipationPercent { get; set; }=10;
    [Setting("최소 체결 지연 (ms)",0,60000)] public int LatencyMs { get; set; }=100;
    [Setting("중복 주문 차단 (초)",1,86400)] public int DuplicateSeconds { get; set; }=10;
    [Setting("호가 유효 시간 (초)",1,600)] public int StaleSeconds { get; set; }=10;
}
public class DataSettings
{
    [Setting("구독 universe (거래소:종목, 쉼표 구분)")] public string Universe { get; set; }="NASDAQ:AAPL,NASDAQ:NVDA,KRX:005930";
    [Setting("최대 구독 종목 (앱 한도 20)",1,20)] public int SubscriptionLimit { get; set; }=20;
    [Setting("수신 버퍼 이벤트 한도",100,100000)] public int BufferCapacity { get; set; }=5000;
    [Setting("재접속 최대 대기 (초)",1,120)] public int ReconnectSeconds { get; set; }=30;
    [Setting("수신 누락 의심 간격 (초)",1,3600)] public int GapSeconds { get; set; }=30;
    [Setting("기억할 종목당 최대 틱",100,200000)] public int MaxHistory { get; set; }=20000;
    [Setting("로컬 데모 이벤트 간격 (ms)",10,10000)] public int DemoIntervalMs { get; set; }=100;
    [Setting("수동 악재 제외 (거래소:종목, 사유는 로그 입력)")] public string ExcludedSymbols { get; set; }="";
}
public class BacktestSettings
{
    [Setting("선택 종목 (거래소:종목)")] public string Instrument { get; set; }="NASDAQ:AAPL";
    [Setting("시작 UTC (ISO 8601)")] public string From { get; set; }="2026-09-25T13:30:00Z";
    [Setting("종료 UTC (ISO 8601)")] public string To { get; set; }="2026-09-25T14:00:00Z";
    [Setting("초기 자금 (선택 종목 통화)",1)] public decimal InitialCash { get; set; }=10000;
    [Setting("소스 (SAMPLE / KIWOOM / CSV 원본)")] public string Source { get; set; }="SAMPLE";
}
public class FilterRule
{
    public string Name { get; set; }="";
    public bool Enabled { get; set; }
    public bool Minimum { get; set; }=true;
    public decimal Value { get; set; }
}
public class TradingSettings
{
    public AdvancedStrategyConfig? Advanced {get;set;}
    public Stock.Core.Pipeline.PipelineAdvancedConfig? Pipeline {get;set;}
    public Stock.Core.Pipeline.SymbolObservation[] PipelineObservations {get;set;}=[];
    public ScalpSettings Scalping { get; set; }=new();
    public BoxSettings Box { get; set; }=new();
    public ReboundSettings Rebound { get; set; }=new();
    public AnalysisSettings Analysis { get; set; }=new();
    public RiskSettings Risk { get; set; }=new();
    public ExecutionSettings Execution { get; set; }=new();
    public DataSettings Data { get; set; }=new();
    public BacktestSettings Backtest { get; set; }=new();
    public List<FilterRule> Filters { get; set; }=new[] {"가격","시가총액","일평균 거래량","일평균 거래대금","당일 거래량","상승률 %","하락률 %","스프레드 %"}.SelectMany(n=>new[]{new FilterRule{Name=n,Minimum=true},new FilterRule{Name=n,Minimum=false,Value=1000000000000}}).ToList();
    public IEnumerable<(string Name,object Value)> Groups() => new (string,object)[]{("전략1 스캘핑",Scalping),("전략2 박스",Box),("전략3 반등",Rebound),("지지저항·매물대",Analysis),("리스크",Risk),("주문·체결 모델",Execution),("데이터·제외",Data),("백테스트",Backtest)};
    public TradingSettings Clone() => Serialization.Unpack<TradingSettings>(Serialization.Pack(this));
    public void Validate()
    {
        Advanced?.Validate();Pipeline?.Validate();
        if(Execution.TaxPercent<0||Execution.TaxPercent>20||Execution.TimeoutMilliseconds is <=0)throw new InvalidOperationException("거래세 또는 주문 대기시간을 확인하세요.");
        foreach(var (name,value) in Groups()) foreach(var p in value.GetType().GetProperties())
        {
            var a=p.GetCustomAttribute<SettingAttribute>(); var v=p.GetValue(value);
            if(a==null || v is string || v is bool) continue;
            if(p.PropertyType.IsEnum) { if(!Enum.IsDefined(p.PropertyType,v!)) throw new InvalidOperationException(name+": 선택값 오류"); continue; }
            var n=Convert.ToDouble(v); if(n<a.Min || n>a.Max) throw new InvalidOperationException($"{name} / {a.Label}: {a.Min}~{a.Max} 범위여야 합니다.");
        }
        if(Scalping.MaxPrice<Scalping.MinPrice || Scalping.AverageSeconds<=Scalping.CurrentSeconds || Scalping.WeakLongSeconds<=Scalping.WeakShortSeconds || Box.MinimumSeconds>Box.LookbackSeconds) throw new InvalidOperationException("전략 가격 범위 또는 장기/단기 기간을 확인하세요.");
        if(Risk.StopMode==DistanceMode.Percent && Risk.StopValue>=100) throw new InvalidOperationException("손절률은 100% 미만이어야 합니다.");
        if(Rebound.Enabled && !(Rebound.Use1||Rebound.Use2||Rebound.Use3||Rebound.UseHigh)) throw new InvalidOperationException("반등 전략의 급락 조건을 하나 이상 활성화하세요.");
        var universe=Data.Universe.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(Instrument.Parse).ToList();
        if(universe.Count==0 || universe.Count>Data.SubscriptionLimit || universe.Distinct().Count()!=universe.Count) throw new InvalidOperationException("구독 종목 수/중복을 확인하세요 (1~설정 한도).");
        foreach(var item in Data.ExcludedSymbols.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)) Instrument.Parse(item);
        Instrument.Parse(Backtest.Instrument);
        if(!DateTimeOffset.TryParse(Backtest.From,out var from)||!DateTimeOffset.TryParse(Backtest.To,out var to)||from>=to) throw new InvalidOperationException("백테스트 시작/종료 날짜를 확인하세요.");
        foreach(var group in Filters.GroupBy(f=>f.Name)) { var min=group.First(f=>f.Minimum);var max=group.First(f=>!f.Minimum); if(min.Value<0||max.Value<0||min.Enabled&&max.Enabled&&min.Value>max.Value) throw new InvalidOperationException(group.Key+" 필터 범위 오류"); }
    }
}
