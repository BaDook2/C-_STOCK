using Stock.Core.Research;
namespace Stock.Desktop;
public sealed record ChartMarkerGroup(TradeMarker[] Trades,double X,double Price,string Caption)
{
    public string ColorHex=>TradeMarkerPalette.ColorFor(Trades);
}
/// <summary>Realized outcome takes priority over order side or a stop/TP reason.
/// Grouped labels use the complete realized net; an unknown outcome stays neutral.</summary>
public static class TradeMarkerPalette
{
    public const string Profit="#F04452",Loss="#3182F6",Neutral="#6B7684";
    public static string ColorFor(IReadOnlyList<TradeMarker> trades)
    {
        var exits=trades.Where(t=>!t.Buy).ToArray();if(exits.Length==0)return Profit;
        if(exits.All(t=>t.RealizedPnl.HasValue)){var net=exits.Sum(t=>t.RealizedPnl!.Value);return net>0?Profit:net<0?Loss:Neutral;}
        // Reuse the shared execution caption's intent when actual P/L is absent.
        // A trailing exit alone never proves that the position lost money.
        if(exits.All(t=>t.RealizedPnl is >0||t.RealizedPnl==null&&t.SideLabel.Contains("익절",StringComparison.Ordinal)))return Profit;
        if(exits.All(t=>t.RealizedPnl is <0||t.RealizedPnl==null&&t.SideLabel.Contains("손절",StringComparison.Ordinal)))return Loss;
        return Neutral;
    }
}
/// <summary>Pixel-density grouping changes annotations only. Every original execution remains selectable and stored.</summary>
public static class ChartMarkerLayout
{
    public static ChartMarkerGroup[] Build(IEnumerable<TradeMarker> markers,Func<TradeMarker,double> coordinate,double left,double right,double pixels)
    {
        if(right<=left||pixels<=0)return [];
        var values=markers.Where(m=>m.Price!=null).Select(m=>(Marker:m,X:coordinate(m))).Where(m=>m.X>=left&&m.X<=right).OrderBy(m=>m.X).ToArray();var result=new List<ChartMarkerGroup>();var width=(right-left)*116/Math.Max(116,pixels);
        for(int i=0;i<values.Length;)
        {
            var first=i;var start=values[i].X;while(i<values.Length&&values[i].X-start<width)i++;var cluster=values[first..i];var trades=cluster.Select(m=>m.Marker).ToArray();var buys=trades.Count(m=>m.Buy);var sells=trades.Length-buys;
            var caption=trades.Length==1?(trades[0].TradeNumber is {} n?"#"+n+" ":"")+trades[0].SideLabel:
                string.Join(" / ",new[]{buys>0?$"매수 {buys}건":"",sells>0?$"매도 {sells}건":""}.Where(s=>s.Length>0));
            if(trades.Length>1&&sells>0){var exits=trades.Where(t=>!t.Buy).ToArray();if(exits.All(t=>t.RealizedPnl.HasValue)){var profit=exits.Sum(t=>t.RealizedPnl!.Value);caption+=" · "+(profit>0?"익절 ":profit<0?"손절 ":"본전 ")+DisplayNumber.SignedPrice(profit);}else caption+=" · 실현손익 미확인";}
            if(trades.Length>1){var numbers=trades.Where(t=>t.TradeNumber.HasValue).Select(t=>t.TradeNumber!.Value).Distinct().Order().ToArray();if(numbers.Length>0)caption=(numbers.Length==1?$"#{numbers[0]} ":$"#{numbers[0]}~#{numbers[^1]} ")+caption;}
            result.Add(new(trades,cluster.Average(m=>m.X),trades.Average(m=>(double)m.Price!.Value),caption));
        }
        return result.ToArray();
    }
}
