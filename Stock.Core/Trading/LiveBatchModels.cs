using Stock.Core.Research;
namespace Stock.Core.Trading;

public sealed record LiveBatchItem(StockItem Stock,string Preset,TradingSettings Settings,decimal Budget,bool Selected=true)
{
    public Stock.Core.Research.Oversold.OversoldLiveConfiguration? Oversold {get;init;}
}
public sealed record LiveBatchPlan(LiveBatchItem[] Items,decimal TotalBudget,decimal DailyLossLimit);
public sealed record LiveBatchRowStatus(string Key,string Name,string Preset,bool Running,string Status,string Signal,int Owned,int Pending,decimal Profit,string Error);
public sealed record LiveBatchStatus(string Account,bool Running,string Status,IReadOnlyList<LiveBatchRowStatus> Rows);
public sealed class LiveBatchOrder
{
    public string ClientId {get;set;}="";public string Symbol {get;set;}="";public string Number {get;set;}="";public bool Buy {get;set;} public int Quantity {get;set;} public decimal Price {get;set;} public DateTimeOffset Created {get;set;}
    public bool Pending {get;set;}=true;public bool CancelSent {get;set;} public string Status {get;set;}="전송 준비";public string Reason {get;set;}="";
    public Stock.Core.Research.Oversold.OversoldDecisionSnapshot? OversoldSnapshot {get;set;}
}
public sealed class LiveBatchJournal
{
    public string RunId {get;set;}=Guid.NewGuid().ToString("N");public DateTimeOffset Started {get;set;} public string AccountScope {get;set;}="";
    public LiveBatchPlan? Plan {get;set;} public List<LiveBatchOrder> Orders {get;set;}=[];public List<BrokerExecution> Executions {get;set;}=[];public string Status {get;set;}="중지";
}
/// <summary>Capabilities are explicit; missing data never silently changes a registered strategy.</summary>
public static class LiveBatchSupport
{
    public static string? Reason(LiveBatchItem item)
    {
        if(item.Oversold is null)return Reason(item.Stock,item.Settings);
        if(item.Stock.Exchange!=Exchange.KRX)return "낙주 실시간 실행은 국내 일반주식만 지원합니다.";
        if(item.Settings.Advanced==null)return "실계좌 주문 보호 설정이 없습니다.";
        if(item.Settings.Execution.Retry||item.Settings.Risk.FlattenOnStop)return "자동 재주문 또는 중지 시 강제청산은 지원하지 않습니다.";
        item.Oversold.Configuration.Validate();item.Oversold.BarSpecification.Validate();item.Oversold.Facts.Validate();
        if(!item.Oversold.Configuration.UseEmergencyStop)return "실계좌 실행에는 낙주 비상 손절이 필요합니다.";
        if(item.Oversold.Warmup.Length==0||item.Oversold.Warmup.Any(b=>!b.Price.IsComplete||b.TradingValue==null||b.TradingValue<0))return "실제 거래대금이 있는 확정 준비 봉이 필요합니다.";
        if(item.Oversold.Warmup.Length>20000)return "준비 봉이 20,000개를 넘습니다. 더 큰 분/시간 기준을 선택하세요.";
        return null;
    }
    public static string? Reason(StockItem stock,TradingSettings settings)
    {
        if(stock.Exchange!=Exchange.KRX)return "자동매매는 현재 키움 국내주식만 지원합니다.";
        if(settings.Pipeline!=null)return "Pipeline은 실시간 순위·보조자료/포트폴리오 실전 어댑터가 없어 실행할 수 없습니다.";
        if(settings.Advanced==null)return "Standard/Advanced 틱 전략을 활성화해 이름으로 저장하세요. 일봉 전략의 실시간 실행은 미지원입니다.";
        var a=settings.Advanced;
        if(a.StrategyType==StrategyType.Pipeline)return "Pipeline 실전 실행 미지원";
        if(a.StrategyType==StrategyType.Scalping&&a.Scalping.BuyTradeRatio>0||a.StrategyType==StrategyType.Rebound&&a.Rebound.BuyTradeRatio>0)return "현재 피드에 실제 매수 방향 체결 비율이 없습니다.";
        if(a.StrategyType==StrategyType.Rebound&&(a.Rebound.NewsFilter||!a.Rebound.AutoBuy))return a.Rebound.NewsFilter?"뉴스/공시 검증 자료가 없어 반등 자동매매를 실행할 수 없습니다.":"등록 전략의 자동매수 설정이 OFF입니다.";
        if(settings.Filters.Any(f=>f.Enabled&&f.Name is "일평균 거래량" or "일평균 거래대금"))return "실시간 피드에 일평균 거래량/거래대금 자료가 없습니다.";
        if(settings.Execution.Retry)return "자동 재주문 설정은 실계좌 다중매매에서 지원하지 않습니다. 응답 확인 전 재전송하지 않습니다.";
        if(settings.Risk.FlattenOnStop)return "중지 시 강제청산 설정은 다중매매에서 지원하지 않습니다.";
        return null;
    }
    public static ITradingStrategy Strategy(TradingSettings s)=>new AdvancedStrategyGate(s.Advanced!.StrategyType switch{StrategyType.Scalping=>new ScalpingStrategy(),StrategyType.Box=>new BoxStrategy(),_=>new ReboundStrategy()});
    public static bool RegularSession(DateTimeOffset now)
    {var kst=now.ToOffset(TimeSpan.FromHours(9));var time=TimeOnly.FromDateTime(kst.DateTime);return kst.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday)&&time>=new TimeOnly(9,0)&&time<new TimeOnly(15,20);}
}
