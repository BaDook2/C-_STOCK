namespace Stock.Core.Research;

/// <summary>확인된 체결만 기록합니다. 주문가격을 체결가격으로 대체하지 않습니다.</summary>
public sealed record TradeMarker(string Id,string StockKey,DateTimeOffset Time,bool Buy,decimal Quantity,decimal? Price,string Source,string AccountScope="",bool DateOnly=false,int? TradeNumber=null,string Reason="",decimal? RealizedPnl=null,decimal? CrashJudgmentLow=null)
{
    public string SideLabel=>TradeOutcome.Label(Buy,Reason,RealizedPnl);
    public string ProfitText=>Buy?"":RealizedPnl is {} pnl?"실현손익 "+(StockKey.StartsWith("KRX:",StringComparison.Ordinal)?DisplayNumber.SignedPrice(pnl)+" 원":pnl.ToString("+0.########;-0.########;0",System.Globalization.CultureInfo.InvariantCulture)+" USD"):"실현손익 미확인";
}

/// <summary>Shared chart captions preserve execution intent; a profitable trailing exit is not labelled a loss.</summary>
public static class TradeOutcome
{
    public static string Label(bool buy,string? reason,decimal? profit=null)
    {
        if(buy)return "매수";
        var text=reason??"";
        var detail=text switch{"TP1"=>"1차 익절","TP2"=>"2차 익절","SupportBreak"=>"지지 이탈","EmergencyStop"=>"비상 손절","TrailingStop"=>"추적 청산","MaxHoldingDays"=>"보유기간","EndOfData"=>"기간 종료",_=>""};
        var outcome=profit is >0?"익절":profit is <0?"손절":profit==0?"본전 청산":
            text.StartsWith("TP",StringComparison.Ordinal)||text.Contains("익절")?"익절":
            text is "SupportBreak" or "EmergencyStop"||text.Contains("손절")?"손절":"";
        if(outcome.Length>0&&detail.Contains(outcome,StringComparison.Ordinal))return "매도 · "+detail;
        return "매도"+(outcome.Length>0?" · "+outcome:"")+(detail.Length>0?" · "+detail:"");
    }
}
