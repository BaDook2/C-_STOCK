using System.Text.Encodings.Web;
using System.Text.Json;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record IntradayFillRow(Fill Fill)
{
    public string Time=>Fill.Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss.fff");
    public string Side=>Fill.Buy?"매수":"매도";
    public string Price=>DisplayNumber.Price(Fill.Price);
    public int Quantity=>Fill.Quantity;
    public string Profit=>Fill.Buy?"—":DisplayNumber.SignedPrice(Fill.RealizedPnl);
    public string Reason=>IntradayPresentation.Reason(Fill.Reason);
}
public static class IntradayPresentation
{
    public static string Reason(string reason)=>reason switch
    {
        "FreshRebound"=>"지지 접근 후 새 반등", "Entry2"=>"2차 분할매수", "Entry3"=>"3차 분할매수",
        "TP1"=>"1차 익절", "TP2"=>"2차 익절", "FixedStop"=>"고정 손절", "SupportStop"=>"지지 이탈 손절",
        "TrailingStop"=>"추적 청산", "BreakEvenStop"=>"본전 보호", "DayLiquidation"=>"당일 청산", "MaxHoldingDays"=>"최대 보유기간", _=>reason
    };
    public static string PrettyJson(string json)
    {try{using var doc=JsonDocument.Parse(json);return JsonSerializer.Serialize(doc.RootElement,new JsonSerializerOptions{WriteIndented=true,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping});}catch(JsonException){return json;}}
}
