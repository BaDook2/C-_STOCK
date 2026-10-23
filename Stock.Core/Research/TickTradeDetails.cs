using Stock.Core.Trading;
namespace Stock.Core.Research;
public sealed record TickTradeDetail(string Id,DateTimeOffset EntryTime,decimal EntryPrice,DateTimeOffset? ExitTime,decimal? ExitPrice,decimal Quantity,decimal Profit,decimal ReturnPercent,string EntryReason,string ExitReason,string EntryOrder="",string ExitOrder="",bool EntryKnown=true)
{
    public string Summary=>$"거래 {Id}\n주문번호 매수 {EntryOrder} / 매도 {ExitOrder}\n매수 {(EntryKnown?$"{EntryTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss.fffffff} · {EntryPrice:#,##0.########}":"기간 이전 자료 없음")}\n매도 {(ExitTime is {} exit?exit.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss.fffffff"):"미청산")} · {ExitPrice:#,##0.########}\n수량 {Quantity:N0} · 보유 {(EntryKnown&&ExitTime is {} end?(end-EntryTime).ToString():"미확정")}\n손익 {(EntryKnown&&ExitTime!=null?$"{Profit:+#,##0.########;-#,##0.########;0} ({ReturnPercent:+0.00;-0.00;0.00}%)":"미산출")}\n진입: {EntryReason}\n청산: {ExitReason}";
}
public sealed record TickFillMarker(string Id,DateTimeOffset Time,decimal Price,bool Buy,TickTradeDetail Detail);
public static class TickTradeMatcher
{
    /// <summary>FIFO lot matching; partial exits create separate details, unclosed lots remain explicit.</summary>
    public static TickFillMarker[] Match(string run,IReadOnlyList<BarFill> fills)
    {
        var entries=new Queue<(BarFill Fill,int Remaining,int Index)>();var markers=new List<TickFillMarker>();int serial=0;
        foreach(var (f,index) in fills.Select((f,i)=>(f,i)).OrderBy(p=>p.f.Time).ThenBy(p=>p.i))
        {
            if(f.Side=="매수"){entries.Enqueue((f,f.Quantity,index));continue;}int left=f.Quantity;
            while(left>0&&entries.Count>0)
            {
                var entry=entries.Dequeue();int qty=Math.Min(left,entry.Remaining);left-=qty;decimal fee=entry.Fill.Fee*qty/entry.Fill.Quantity+f.Fee*qty/f.Quantity;var profit=(f.Price-entry.Fill.Price)*qty-fee;
                var detail=new TickTradeDetail(run+":"+(++serial),entry.Fill.Time,entry.Fill.Price,f.Time,f.Price,qty,profit,entry.Fill.Price>0?profit/(entry.Fill.Price*qty)*100:0,entry.Fill.Reason,f.Reason,entry.Fill.OrderId,f.OrderId);
                markers.Add(new(detail.Id+":B",entry.Fill.Time,entry.Fill.Price,true,detail));markers.Add(new(detail.Id+":S",f.Time,f.Price,false,detail));
                if(entry.Remaining>qty){var rest=entries.ToArray();entries.Clear();entries.Enqueue((entry.Fill,entry.Remaining-qty,entry.Index));foreach(var r in rest)entries.Enqueue(r);}
            }
            if(left>0){var detail=new TickTradeDetail(run+":"+(++serial),f.Time,f.Price,f.Time,f.Price,left,0,0,"기간 이전 매수 자료 없음",f.Reason,ExitOrder:f.OrderId,EntryKnown:false);markers.Add(new(detail.Id+":S",f.Time,f.Price,false,detail));}
        }
        foreach(var entry in entries){var detail=new TickTradeDetail(run+":"+(++serial),entry.Fill.Time,entry.Fill.Price,null,null,entry.Remaining,0,0,entry.Fill.Reason,"미청산",entry.Fill.OrderId);markers.Add(new(detail.Id+":B",entry.Fill.Time,entry.Fill.Price,true,detail));}
        return markers.ToArray();
    }
}
