using System.Collections.ObjectModel;
using Stock.Core.Research;
namespace Stock.App;

public sealed class WatchEntry(StockItem stock) : Observable
{
    public StockItem Stock {get;}=stock;
    private MarketQuote? quote;
    public string Name=>Stock.Name;
    public string Detail=>$"{Stock.Region} · {Stock.Sector}";
    public string Price=>quote==null?"—":$"{quote.PriceText} {(Stock.Currency=="KRW"?"원":"달러")}";
    public string Change=>quote?.ChangeText??"—";
    public string Color=>quote==null||quote.Change==0?"#6B7684":quote.Change>0?"#F04452":"#3182F6";
    public string State {get;private set;}="시세 확인 전";
    public void Update(MarketQuote value){quote=value;State=value.KoreaTime;Changed(nameof(Price));Changed(nameof(Change));Changed(nameof(Color));Changed(nameof(State));}
    public void Failed(){State=quote==null?"조회 실패 · 다시 시도하세요":"갱신 실패 · "+quote.KoreaTime;Changed(nameof(State));}
}
public sealed class WatchGroup : Observable
{
    public string Id {get;}
    private string name;
    private bool screening;
    public bool IncludeInScreening {get=>screening;set{screening=value;Changed();}}
    public string Name {get=>name;set{name=value;Changed();}}
    public ObservableCollection<WatchEntry> Items {get;}=[];
    public ActionCommand Edit {get;}
    public override string ToString()=>Name;
    public WatchGroup(string id,string name,IEnumerable<StockItem> stocks,Action<WatchGroup> edit)
    {Id=id;this.name=name;foreach(var stock in stocks.DistinctBy(s=>s.Key))Items.Add(new(stock));Edit=new(()=>{edit(this);return Task.CompletedTask;},()=>true);}
}
public sealed partial class WorkspaceViewModel
{
    public ObservableCollection<WatchGroup> WatchGroups {get;}=[];
    private WatchGroup? watchGroup,registrationGroup;
    private WatchEntry? watchSelection;
    private bool editingGroup,watchRefreshing;
    private string groupDraft="",watchStatus="종목을 검색한 뒤 그룹을 골라 관심종목으로 등록하세요.";
    public WatchGroup? SelectedWatchGroup {get=>watchGroup;set{watchGroup=value;SelectedWatch=null;EditingGroup=false;Changed();Changed(nameof(WatchItems));Changed(nameof(WatchEmpty));Changed(nameof(WatchHeading));Refresh();if(ActiveTab==6)_=RefreshWatchlist();}}
    public WatchGroup? RegistrationGroup {get=>registrationGroup;set{registrationGroup=value;Changed();Changed(nameof(IsWatchRegistered));Changed(nameof(WatchRegistrationStar));Refresh();}}
    public bool IsWatchRegistered
    {
        get
        {
            var item=selected??dataset?.Stock;
            return item!=null&&RegistrationGroup?.Items.Any(e=>e.Stock.Key==item.Key)==true;
        }
    }
    public string WatchRegistrationStar=>IsWatchRegistered?"★":"☆";
    public ObservableCollection<WatchEntry>? WatchItems=>SelectedWatchGroup?.Items;
    public WatchEntry? SelectedWatch {get=>watchSelection;set{watchSelection=value;Changed();Refresh();}}
    public bool WatchEmpty=>WatchItems?.Count is null or 0;
    public string WatchHeading=>$"{SelectedWatchGroup?.Name??"관심종목"} · {WatchItems?.Count??0}종목";
    public bool EditingGroup {get=>editingGroup;set{editingGroup=value;Changed();}}
    public string GroupDraft {get=>groupDraft;set{groupDraft=value;Changed();}}
    public string WatchStatus {get=>watchStatus;private set{watchStatus=value;Changed();}}
    private void InitializeWatchlist()
    {
        var saved=store.State<WatchGroupData[]>("watch-groups");
        foreach(var g in saved is {Length:>0}?saved:[new("group-1","그룹명1",[]),new("group-2","그룹명2",[])])WatchGroups.Add(MakeGroup(g));
        SelectedWatchGroup=RegistrationGroup=WatchGroups[0];
        foreach(var g in WatchGroups)g.IncludeInScreening=(store.State<string[]>("screen-watch-groups")??[]).Contains(g.Id);
        Add("관심등록",()=>{var item=selected??dataset?.Stock;if(item==null||RegistrationGroup==null)return Task.CompletedTask;if(RegistrationGroup.Items.Any(e=>e.Stock.Key==item.Key)){Status=RegistrationGroup.Name+"에 이미 등록된 종목입니다.";return Task.CompletedTask;}RegistrationGroup.Items.Add(new(item));SaveWatchlist();WatchChanged();Log(item.Name+" · "+RegistrationGroup.Name+"에 관심종목으로 등록했습니다.");return Task.CompletedTask;},()=>!Busy&&RegistrationGroup!=null&&(selected!=null||dataset!=null));
        Add("관심해제",()=>{if(SelectedWatchGroup==null||SelectedWatch==null)return Task.CompletedTask;var item=SelectedWatch;SelectedWatchGroup.Items.Remove(item);SelectedWatch=null;SaveWatchlist();WatchChanged();Status=item.Name+"을 이 그룹에서 해제했습니다.";return Task.CompletedTask;},()=>!Busy&&SelectedWatch!=null);
        Add("관심차트",async()=>{if(SelectedWatch==null)return;Select(SelectedWatch.Stock);await LoadData();ActiveTab=0;},()=>CanConfigure&&SelectedWatch!=null);
        Add("그룹추가",()=>{int n=1;while(WatchGroups.Any(g=>g.Name=="그룹명"+n))n++;var g=MakeGroup(new(Guid.NewGuid().ToString("N"),"그룹명"+n,[]));WatchGroups.Add(g);SelectedWatchGroup=RegistrationGroup=g;SaveWatchlist();return Task.CompletedTask;},()=>!Busy);
        Add("그룹저장",()=>{var name=GroupDraft.Trim();if(SelectedWatchGroup==null)return Task.CompletedTask;if(name.Length is <1 or >20||name.Any(char.IsControl))throw new InvalidOperationException("그룹명은 1~20자의 글자로 입력하세요.");if(WatchGroups.Any(g=>g!=SelectedWatchGroup&&string.Equals(g.Name,name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("이미 사용 중인 그룹명입니다.");SelectedWatchGroup.Name=name;SaveWatchlist();EditingGroup=false;WatchChanged();Status="그룹 이름을 변경했습니다.";return Task.CompletedTask;},()=>!Busy&&EditingGroup);
        Add("그룹편집취소",()=>{EditingGroup=false;return Task.CompletedTask;},()=>!Busy&&EditingGroup);
        Add("관심새로고침",RefreshWatchlist,()=>!Busy&&!watchRefreshing);
    }
    private WatchGroup MakeGroup(WatchGroupData data)=>new(data.Id,data.Name,data.Stocks,g=>{if(Busy)return;SelectedWatchGroup=g;GroupDraft=g.Name;EditingGroup=true;Refresh();});
    private void SaveWatchlist()=>store.State("watch-groups",WatchGroups.Select(g=>new WatchGroupData(g.Id,g.Name,g.Items.Select(e=>e.Stock).ToArray())).ToArray());
    private void WatchChanged(){Changed(nameof(WatchItems));Changed(nameof(WatchEmpty));Changed(nameof(WatchHeading));Changed(nameof(IsWatchRegistered));Changed(nameof(WatchRegistrationStar));UpdateSectors();Refresh();}
    /// <summary>현재 그룹만 조회하고, 조회 도중 그룹을 바꿔도 결과를 원래 종목에 적용합니다.</summary>
    public async Task RefreshWatchlist()
    {
        if(disposed||apiPaused||watchRefreshing||provider is not IMarketOverviewData market||SelectedWatchGroup==null)return;
        var group=SelectedWatchGroup;var items=group.Items.ToArray();watchRefreshing=true;Refresh();WatchStatus="관심종목 시세를 확인하고 있습니다.";int failed=0;
        try{foreach(var item in items){if(disposed||apiPaused||group!=SelectedWatchGroup)break;try{var q=await market.Quote(item.Stock,ApiToken);if(!disposed)item.Update(q);}catch(OperationCanceledException){break;}catch{failed++;if(!disposed)item.Failed();}}
            if(!disposed&&group==SelectedWatchGroup)WatchStatus=$"{group.Name} · {items.Length:N0}종목 · {DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)):HH:mm:ss} 조회"+(failed>0?$" · {failed:N0}개 조회 실패":" · 제공처 지연 가능");
        }finally{watchRefreshing=false;Refresh();if(!disposed&&group!=SelectedWatchGroup&&ActiveTab==6)_=RefreshWatchlist();}
    }
}
