using System.Collections.ObjectModel;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed record WatchGroupRecord(string Id,string Name,bool Screen,StockItem[] Stocks);
public sealed class WatchGroupViewModel:Bindable
{
    public string Id {get;}
    private string name;
    public string Name {get=>name;set=>Set(ref name,value);}
    private bool include;
    public bool IncludeInScreening {get=>include;set=>Set(ref include,value);}
    public ObservableCollection<StockItem> Stocks {get;}=[];
    public WatchGroupViewModel(WatchGroupRecord record){Id=record.Id;name=record.Name;IncludeInScreening=record.Screen;foreach(var stock in record.Stocks)Stocks.Add(stock);}
    public WatchGroupRecord Snapshot()=>new(Id,Name,IncludeInScreening,Stocks.ToArray());
    public override string ToString()=>Name;
}
public sealed partial class MainViewModel
{
    public ObservableCollection<WatchGroupViewModel> WatchGroups {get;}=[];
    private WatchGroupViewModel? watchGroup;
    private string groupDraft="";
    public WatchGroupViewModel? SelectedWatchGroup {get=>watchGroup;set{if(Set(ref watchGroup,value)){GroupDraft=value?.Name??"";SelectedWatchStock=null;Changed(nameof(WatchItems));Changed(nameof(WatchStar));ToggleWatch?.Refresh();}}}
    public ObservableCollection<StockItem>? WatchItems=>SelectedWatchGroup?.Stocks;
    private StockItem? selectedWatchStock;
    public StockItem? SelectedWatchStock {get=>selectedWatchStock;set{if(Set(ref selectedWatchStock,value)){OpenWatchStock?.Refresh();RemoveWatchStock?.Refresh();}}}
    public string GroupDraft {get=>groupDraft;set=>Set(ref groupDraft,value);}
    public string WatchStar=>SelectedStock!=null&&SelectedWatchGroup?.Stocks.Any(s=>s.Key==SelectedStock.Key)==true?"★ 관심종목":"☆ 관심등록";
    public AsyncCommand ToggleWatch {get;private set;}=null!;
    public AsyncCommand AddWatchGroup {get;private set;}=null!;
    public AsyncCommand RenameWatchGroup {get;private set;}=null!;
    public AsyncCommand OpenWatchStock {get;private set;}=null!;
    public AsyncCommand RemoveWatchStock {get;private set;}=null!;
    private void InitializeWatch()
    {
        foreach(var record in services.Store.State<WatchGroupRecord[]>("watch-groups-avalonia-v1")??[new("default-1","그룹명1",true,[]),new("default-2","그룹명2",false,[])])WatchGroups.Add(CreateWatchGroup(record));SelectedWatchGroup=WatchGroups.FirstOrDefault();
        ToggleWatch=new(()=>
        {
            if(SelectedStock==null||SelectedWatchGroup==null){Notify("등록할 종목과 관심그룹을 선택하세요.");return Task.CompletedTask;}
            var old=SelectedWatchGroup.Stocks.FirstOrDefault(s=>s.Key==SelectedStock.Key);if(old==null)SelectedWatchGroup.Stocks.Add(SelectedStock);else SelectedWatchGroup.Stocks.Remove(old);SaveWatch();return Task.CompletedTask;
        },()=>SelectedStock!=null&&SelectedWatchGroup!=null);
        PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(SelectedStock))ToggleWatch.Refresh();};
        AddWatchGroup=new(()=>{var n=WatchGroups.Count+1;var name="새 그룹"+n;while(WatchGroups.Any(g=>g.Name==name))name="새 그룹"+(++n);var group=CreateWatchGroup(new(Guid.NewGuid().ToString("N"),name,false,[]));WatchGroups.Add(group);SelectedWatchGroup=group;SaveWatch();return Task.CompletedTask;});
        RenameWatchGroup=new(()=>{var text=GroupDraft.Trim();if(SelectedWatchGroup==null||text.Length is <1 or >60||WatchGroups.Any(g=>g!=SelectedWatchGroup&&g.Name==text)){Notify("그룹 이름은 중복 없이1~60자로 입력하세요.");return Task.CompletedTask;}SelectedWatchGroup.Name=text;SaveWatch();return Task.CompletedTask;});
        OpenWatchStock=new(()=>{if(SelectedWatchStock!=null){ActiveTab=0;SelectedStock=SelectedWatchStock;}return Task.CompletedTask;},()=>SelectedWatchStock!=null);
        RemoveWatchStock=new(()=>{if(SelectedWatchGroup!=null&&SelectedWatchStock!=null){SelectedWatchGroup.Stocks.Remove(SelectedWatchStock);SaveWatch();}return Task.CompletedTask;},()=>SelectedWatchGroup!=null&&SelectedWatchStock!=null);
    }
    private WatchGroupViewModel CreateWatchGroup(WatchGroupRecord record)
    {var group=new WatchGroupViewModel(record);group.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(WatchGroupViewModel.IncludeInScreening))SaveWatch();};return group;}
    private void SaveWatch(){services.Store.State("watch-groups-avalonia-v1",WatchGroups.Select(g=>g.Snapshot()).ToArray());Changed(nameof(WatchStar));}
    private StockItem[] WatchScreenTargets()=>WatchGroups.Where(g=>g.IncludeInScreening).SelectMany(g=>g.Stocks).DistinctBy(s=>s.Key).ToArray();
}
