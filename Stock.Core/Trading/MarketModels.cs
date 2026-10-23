using System.Globalization;
using System.Text.Json;

namespace Stock.Core.Trading;
public enum Exchange { KRX, NASDAQ, NYSE, AMEX }
public enum EventKind { Trade, Quote }
public enum TradingMode { OfflineDemo, LiveLocalPaper, BrokerPaper, Backtest }
public enum OrderKind { Limit, Market }
public enum OrderState { New, Submitted, PartiallyFilled, Filled, CancelRequested, Cancelled, Rejected, Unknown }
public record Instrument(string Symbol, Exchange Exchange)
{
    public string Currency => Exchange == Exchange.KRX ? "KRW" : "USD";
    public string Key => $"{Exchange}:{Symbol}";
    public string Code => Exchange switch { Exchange.NASDAQ => "ND", Exchange.NYSE => "NY", Exchange.AMEX => "NA", _ => "KRX" };
    public decimal Tick(decimal price) => Exchange != Exchange.KRX ? (price < 1 ? .0001m : .01m) : price switch { <2000 => 1, <5000 => 5, <20000 => 10, <50000 => 50, <200000 => 100, <500000 => 500, _ => 1000 };
    public void Validate()
    {
        var pattern = Exchange == Exchange.KRX ? "^[0-9]{6}$" : "^[A-Z][A-Z0-9.\\-]{0,14}$";
        if (!Enum.IsDefined(Exchange) || !System.Text.RegularExpressions.Regex.IsMatch(Symbol, pattern)) throw new InvalidOperationException("시장/종목코드를 확인하세요.");
    }
    public static Instrument Parse(string key) { var p=key.Trim().Split(':'); if(p.Length!=2 || !Enum.TryParse<Exchange>(p[0],true,out var ex)) throw new InvalidOperationException("종목은 KRX:005930 또는 NASDAQ:AAPL 형식입니다."); var i=new Instrument(p[1].ToUpperInvariant(),ex); i.Validate(); return i; }
}
public record BookLevel(decimal Price, decimal Size);
public record MarketEvent(Instrument Instrument, EventKind Kind, DateTimeOffset ExchangeTime, DateTimeOffset ReceivedTime, string Source, long Sequence)
{
    public decimal? LastPrice { get; init; }
    public decimal? TradePrice { get; init; }
    public decimal? TradeSize { get; init; }
    public decimal? CumulativeVolume { get; init; }
    public decimal? Bid { get; init; }
    public decimal? Ask { get; init; }
    public decimal? BidSize { get; init; }
    public decimal? AskSize { get; init; }
    public decimal? Open { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public decimal? PreviousClose { get; init; }
    public decimal? MarketCap { get; init; }
    public decimal? AverageDailyVolume { get; init; }
    public decimal? AverageDailyTurnover { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? BuyerInitiated {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? NewsRiskCleared {get;init;}
    public IReadOnlyList<BookLevel>? Bids { get; init; }
    public IReadOnlyList<BookLevel>? Asks { get; init; }
    public string TimeQuality { get; init; } = "거래소시간";
    public string Key => Instrument.Key;
    public void Validate()
    {
        Instrument.Validate();
        if (!System.Text.RegularExpressions.Regex.IsMatch(Source,"^[A-Za-z0-9_.-]{1,32}$") || ExchangeTime == default || !Enum.IsDefined(Kind) || TradeSize < 0 || CumulativeVolume < 0 || BidSize < 0 || AskSize < 0 || Bid <= 0 || Ask <= 0 || Bid > Ask || TradePrice <= 0 || LastPrice <= 0)
            throw new InvalidOperationException("시장 데이터 가격·수량·시각이 유효하지 않습니다.");
        if (Kind == EventKind.Trade && (TradePrice == null || TradeSize == null)) throw new InvalidOperationException("체결 이벤트에는 개별 체결가격·수량이 필요합니다.");
    }
}
public record Decision(DateTimeOffset Time,string Symbol,string Strategy,string Signal,string Reason,decimal? Price=null);
public record StrategySignal(string Strategy, bool? Buy, decimal? Price, string Reason);
public record Fill(string OrderId, DateTimeOffset Time, Instrument Instrument,bool Buy,int Quantity,decimal Price,decimal Fee,decimal RealizedPnl,string Strategy,string Reason="");
public sealed class Position(Instrument instrument)
{
    public Instrument Instrument { get; } = instrument;
    public string Symbol => Instrument.Key;
    public string Currency => Instrument.Currency;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CostFees { get; set; }
    public string Owner { get; set; }="";
    public decimal Last { get; set; }
    public DateTimeOffset? OpenedAt {get;set;}
    public decimal Unrealized => Quantity*(Last-AveragePrice)-CostFees;
}
public sealed class ManagedOrder
{
    public int? TimeoutMilliseconds {get;init;}
    public string Id { get; init; } = "";
    public required Instrument Instrument { get; init; }
    public string Symbol => Instrument.Key;
    public string Currency => Instrument.Currency;
    public bool Buy { get; init; }
    public string Side => Buy ? "매수" : "매도";
    public OrderKind Kind { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public int Filled { get; set; }
    public int Remaining => Quantity-Filled;
    public OrderState State { get; set; } = OrderState.New;
    public DateTimeOffset Created { get; init; }
    public string Strategy { get; init; } = "수동";
    public string Reason { get; init; } = "";
    public int Retry { get; init; }
    public bool Active => State is OrderState.New or OrderState.Submitted or OrderState.PartiallyFilled or OrderState.CancelRequested or OrderState.Unknown;
}
public record EquityPoint(DateTimeOffset Time,string Currency,decimal Equity);
public record BacktestResult(string RunId,int Events,int Fills,decimal Fees,decimal NetPnl,decimal MaxDrawdownPercent,decimal WinRate,string Currency,string DataHash,string Status,IReadOnlyList<EquityPoint> Equity,IReadOnlyList<Fill> Trades);
public static class Serialization
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = false, PropertyNameCaseInsensitive = true };
    public static string Pack<T>(T value) => JsonSerializer.Serialize(value,Json);
    public static T Unpack<T>(string json) => JsonSerializer.Deserialize<T>(json,Json) ?? throw new InvalidOperationException("저장 데이터가 비어 있습니다.");
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
