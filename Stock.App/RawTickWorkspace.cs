using Stock.Core.Research;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    public RawTickChartModel? RawTicks {get;private set;}
    private void OpenRawTickChart()
    {
        if(selected==null){Status="종목을 먼저 선택하세요.";return;}
        RawTicks?.Dispose();var saved=SelectedRun?.Stock.Key==selected.Key?SelectedRun:null;
        var fills=saved!=null&&saved.Period.StartsWith("실제 체결")?TickTradeMatcher.Match(saved.Id,saved.Fills):[];
        var observed=TradeMarkers.Where(t=>!t.DateOnly&&t.Price!=null).Select(t=>new TickFillMarker(t.Id,t.Time,t.Price!.Value,t.Buy,new TickTradeDetail(t.Id,t.Time,t.Price!.Value,t.Buy?null:t.Time,t.Buy?null:t.Price,t.Quantity,0,0,t.Source,"대응 체결 자료는 테스트 기록에서 선택하세요.",EntryKnown:t.Buy))).ToArray();
        RawTicks=new(store.Path,selected.Key,fills.Concat(observed).ToArray()){FollowLatest=IsOnline};Changed(nameof(RawTicks));ActiveTab=7;
    }
}
