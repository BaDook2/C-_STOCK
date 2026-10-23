using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
public enum HistoricalBoard { Unknown,Kospi,Kosdaq,Other }
public enum HistoricalSecurityKind { Unknown,Common,Etf,Etn,Spac,Other }
public sealed record DailySecurityStatus(string Symbol,DateOnly Date,DateTimeOffset KnownAt,HistoricalBoard Board,HistoricalSecurityKind Kind,bool? Suspended,bool? Administrative);
public sealed record DailyTradingValue(string Symbol,DateOnly Date,DateTimeOffset KnownAt,decimal Value);
public sealed record FundamentalRiskRecord(string Symbol,DateOnly Date,[property:System.Text.Json.Serialization.JsonRequired] bool IsRisk,DateTimeOffset KnownAt);
public sealed record DailyMarketSession(DateOnly Date,DateTimeOffset Open,DateTimeOffset Close);
public interface IHistoricalUniverseProvider
{
    DailySecurityStatus? Status(string symbol,DateOnly date,DateTimeOffset asOf);
    decimal? TradingValue(string symbol,DateOnly date,DateTimeOffset asOf);
}
public interface IFundamentalRiskProvider { bool? Risk(string symbol,DateOnly date,DateTimeOffset asOf); }
public interface IDailySessionProvider { IReadOnlyList<DailyMarketSession> Sessions {get;} }
/// <summary>Explicit point-in-time input, stored with existing SQLite state. No current-status backfill.</summary>
public sealed class HistoricalFacts:IHistoricalUniverseProvider,IFundamentalRiskProvider,IDailySessionProvider
{
    public string Source {get;set;}="";
    public DailySecurityStatus[] StatusRecords {get;set;}=[];
    public DailyTradingValue[] TradingValues {get;set;}=[];
    public FundamentalRiskRecord[] RiskRecords {get;set;}=[];
    public DailyMarketSession[] MarketSessions {get;set;}=[];
    [System.Text.Json.Serialization.JsonIgnore] public IReadOnlyList<DailyMarketSession> Sessions=>MarketSessions;
    private Dictionary<(string,DateOnly),DailySecurityStatus[]>? statuses;
    private Dictionary<(string,DateOnly),DailyTradingValue[]>? values;
    private Dictionary<(string,DateOnly),FundamentalRiskRecord[]>? risks;
    public HistoricalFacts Snapshot(){var copy=Serialization.Unpack<HistoricalFacts>(Serialization.Pack(this));copy.Validate();return copy;}
    public void Validate()
    {
        if(StatusRecords==null||TradingValues==null||RiskRecords==null||MarketSessions==null||StatusRecords.Any(r=>r==null)||TradingValues.Any(r=>r==null)||RiskRecords.Any(r=>r==null)||MarketSessions.Any(r=>r==null))throw new InvalidOperationException("시점별 입력 목록은 null일 수 없습니다.");
        if(MarketSessions.Length>0&&string.IsNullOrWhiteSpace(Source))throw new InvalidOperationException("시장 거래일·시점별 입력의 출처를 입력하세요.");
        if(MarketSessions.Select(s=>s.Date).Distinct().Count()!=MarketSessions.Length||MarketSessions.Any(s=>s.Open>=s.Close||DateOnly.FromDateTime(s.Open.ToOffset(TimeSpan.FromHours(9)).DateTime)!=s.Date||DateOnly.FromDateTime(s.Close.ToOffset(TimeSpan.FromHours(9)).DateTime)!=s.Date))throw new InvalidOperationException("시장 거래일은 중복 없이 같은 한국 날짜의 시가·종가 시각 순서여야 합니다.");
        void Identity(string? symbol,DateTimeOffset known){if(string.IsNullOrWhiteSpace(symbol)||known==default)throw new InvalidOperationException("시점별 자료에는 Symbol과 KnownAt이 필요합니다.");new Instrument(symbol,Exchange.KRX).Validate();}
        foreach(var r in StatusRecords){Identity(r.Symbol,r.KnownAt);if(!Enum.IsDefined(r.Board)||!Enum.IsDefined(r.Kind))throw new InvalidOperationException("과거 시장/종목 분류 값을 확인하세요.");}
        foreach(var r in TradingValues){Identity(r.Symbol,r.KnownAt);if(r.Value<0)throw new InvalidOperationException("실제 거래대금은 0 이상이어야 합니다.");}
        foreach(var r in RiskRecords)Identity(r.Symbol,r.KnownAt);
        if(StatusRecords.GroupBy(r=>(r.Symbol,r.Date,r.KnownAt)).Any(g=>g.Count()>1)||TradingValues.GroupBy(r=>(r.Symbol,r.Date,r.KnownAt)).Any(g=>g.Count()>1)||RiskRecords.GroupBy(r=>(r.Symbol,r.Date,r.KnownAt)).Any(g=>g.Count()>1))throw new InvalidOperationException("동일 종목·날짜·KnownAt의 중복 입력을 제거하세요.");
        statuses=StatusRecords.GroupBy(r=>(r.Symbol,r.Date)).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.KnownAt).ToArray());
        values=TradingValues.GroupBy(r=>(r.Symbol,r.Date)).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.KnownAt).ToArray());
        risks=RiskRecords.GroupBy(r=>(r.Symbol,r.Date)).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r.KnownAt).ToArray());
    }
    public DailySecurityStatus? Status(string symbol,DateOnly date,DateTimeOffset asOf){if(statuses==null)Validate();return statuses!.GetValueOrDefault((symbol,date))?.LastOrDefault(r=>r.KnownAt<=asOf);}
    public decimal? TradingValue(string symbol,DateOnly date,DateTimeOffset asOf){if(values==null)Validate();return values!.GetValueOrDefault((symbol,date))?.LastOrDefault(r=>r.KnownAt<=asOf)?.Value;}
    public bool? Risk(string symbol,DateOnly date,DateTimeOffset asOf){if(risks==null)Validate();return risks!.GetValueOrDefault((symbol,date))?.LastOrDefault(r=>r.KnownAt<=asOf)?.IsRisk;}
    public static bool Tradable(DailySecurityStatus? s)=>s is {Board:HistoricalBoard.Kospi or HistoricalBoard.Kosdaq,Kind:HistoricalSecurityKind.Common,Suspended:false,Administrative:false};
}
