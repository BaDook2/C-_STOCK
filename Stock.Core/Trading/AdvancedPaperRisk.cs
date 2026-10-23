namespace Stock.Core.Trading;

/// <summary>TradingEngine의 모의 주문에서만 호출하는 선택적 고급 제한입니다.</summary>
public static class AdvancedPaperRisk
{
    public static string? Check(AdvancedStrategyConfig a,ManagedOrder o,PaperBroker broker)
    {
        if(!o.Buy)return null;
        var time=TimeOnly.FromDateTime(o.Created.ToOffset(TimeSpan.FromHours(9)).DateTime);var from=TimeOnly.ParseExact(a.Common.TradingStart,"HH:mm");var to=TimeOnly.ParseExact(a.Common.TradingEnd,"HH:mm");
        if(!(from<=to?time>=from&&time<=to:time>=from||time<=to))return "매매 가능 시간 밖 (한국시간)";
        var p=broker.Positions.GetValueOrDefault(o.Symbol);
        if(o.Price*(o.Quantity+(p?.Quantity??0))>broker.Equity*a.Risk.MaxPositionPercent/100)return "종목 최대 자산 비중 초과";
        var lastSell=broker.Fills.LastOrDefault(f=>f.Instrument.Key==o.Symbol&&!f.Buy);
        if(lastSell!=null&&(o.Created-lastSell.Time).TotalSeconds<a.Risk.ReentryCooldown)return "재진입 대기시간";
        var today=o.Created.ToOffset(TimeSpan.FromHours(9)).Date;
        var losses=broker.Orders.Where(x=>!x.Buy&&x.State==OrderState.Filled&&x.Created.ToOffset(TimeSpan.FromHours(9)).Date==today).OrderByDescending(x=>x.Created).Select(x=>broker.Fills.Where(f=>f.OrderId==x.Id).Sum(f=>f.RealizedPnl)).TakeWhile(n=>n<0).Count();
        return losses>=a.Risk.MaxConsecutiveLosses?"당일 연속 손실 제한":null;
    }
}
