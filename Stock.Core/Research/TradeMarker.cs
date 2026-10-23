namespace Stock.Core.Research;

/// <summary>확인된 체결만 기록합니다. 주문가격을 체결가격으로 대체하지 않습니다.</summary>
public sealed record TradeMarker(string Id,string StockKey,DateTimeOffset Time,bool Buy,decimal Quantity,decimal? Price,string Source,string AccountScope="",bool DateOnly=false,int? TradeNumber=null);
