using System.Collections.ObjectModel;
using System.Globalization;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed class MultiTradeRow:Observable
{
    public StockItem Stock {get;init;}=null!;private bool selected=true;private string preset="",budget="1,000,000",state="대기",signal="",error="";private int owned,pending;private decimal profit;
    private Action<StockItem,bool>? selectionChanged;
    public bool Selected {get=>selected;set{if(selected==value)return;selected=value;Changed();selectionChanged?.Invoke(Stock,value);}}public string Preset {get=>preset;set{preset=value;Changed();}}public string Budget {get=>budget;set{budget=value;Changed();}}
    public void BindSelection(Action<StockItem,bool> callback)=>selectionChanged=callback;
    public void SetSelected(bool value){if(selected==value)return;selected=value;Changed(nameof(Selected));}
    public string State {get=>state;set{state=value;Changed();}}public string Signal {get=>signal;set{signal=value;Changed();}}public string Error {get=>error;set{error=value;Changed();}}
    public int Owned {get=>owned;set{owned=value;Changed();}}public int Pending {get=>pending;set{pending=value;Changed();}}public string Profit=>profit.ToString("+#,##0;-#,##0;0")+"원 (수수료 전)";
    public void Apply(LiveBatchRowStatus s){State=s.Status;Signal=s.Signal;Owned=s.Owned;Pending=s.Pending;Error=s.Error;profit=s.Profit;Changed(nameof(Profit));}
}
public sealed record MultiTradeDraft(StockItem Stock,string Preset,string Budget,bool Selected);
public sealed record MultiTradeDrafts(MultiTradeDraft[] Rows,string TotalBudget,string DailyLoss){public string[] GroupIds {get;init;}=[];public bool UseRealtime {get;init;}}
public sealed class MultiTradeMember:Observable
{
    private readonly Action<StockItem,bool> changed;private bool selected;public StockItem Stock {get;}public string Name=>Stock.Name;
    public MultiTradeMember(StockItem stock,Action<StockItem,bool> changed){Stock=stock;this.changed=changed;}
    public bool Selected {get=>selected;set{if(selected==value)return;selected=value;Changed();changed(Stock,value);}}
    public void SetSelected(bool value){if(selected==value)return;selected=value;Changed(nameof(Selected));}
}
public sealed class MultiTradeGroup(string id,string name,Action<MultiTradeGroup,bool?> changed):Observable
{
    private bool? selected=false;public string Id {get;}=id;public string Name {get;}=name;public ObservableCollection<MultiTradeMember> Members {get;}=[];
    public bool? Selected {get=>selected;set{if(selected==value)return;selected=value;Changed();changed(this,value);}}
    public void SetSelected(bool? value){if(selected==value)return;selected=value;Changed(nameof(Selected));}
}
public sealed class MultiTradeViewModel:Observable
{
    private readonly IPublicMarketData provider;private readonly TradingStore store;private readonly Func<Task> start;private readonly Func<string?,Task> stop;private readonly Func<Task> review;private readonly Func<string,decimal,TradingSettings> presetLoader;
    private readonly Func<string> account;private readonly Func<bool> supported,mockMode,realtimeSupported;private readonly Func<Task> mockStart,realtimeMockStart;private readonly Func<IEnumerable<StockItem>> screeningSeed;private readonly Func<IEnumerable<string>> strategyNames;private bool busy,running,useMock=true,useRealtime,modeLocked;private string message="종목을 추가하고 등록한 전략을 선택하세요. 재실행 시 자동매매는 시작되지 않습니다.";
    private readonly HashSet<string> selectedKeys=[];private readonly HashSet<string> groupIds=[];
    public ObservableCollection<MultiTradeRow> Rows {get;}=[];public ObservableCollection<MultiTradeGroup> Groups {get;}=[];public ObservableCollection<StockItem> Results {get;}=[];public ObservableCollection<string> Presets {get;}=[];
    public Dictionary<string,ActionCommand> Actions {get;}=[];public string Query {get;set;}="삼성전자";public string Region {get;set;}="국내";public string[] Regions {get;}=["국내","해외"];
    public StockItem? SelectedResult {get;set;}public MultiTradeRow? SelectedRow {get;set;}public string TotalBudget {get;set;}="5,000,000";public string DailyLoss {get;set;}="100,000";
    public event Action<string>? NotificationRequested;
    public string Account=>account();public bool CanEdit=>!busy&&!running;public bool IsLocked=>!CanEdit;public bool CanToggleMock=>CanEdit&&!modeLocked;public bool CanToggleRealtime=>CanEdit&&UseMock&&!modeLocked;public bool UseMock {get=>useMock;set{if(!CanToggleMock||useMock==value)return;useMock=value;if(!value)useRealtime=true;Changed();Changed(nameof(UseRealtime));Reload();}}public bool UseRealtime {get=>useRealtime;set{if(!CanToggleRealtime||useRealtime==value)return;useRealtime=value;Changed();Reload();}}public bool CanUseLiveBudget=>CanEdit;public string RunCaption=>UseMock?(UseRealtime?"실시간 모의매매 시작":"종목별 모의 백테스트"):supported()?"실제 계좌 일괄수행":"실제 실행 지원 전";public string LaunchContextText=>modeLocked?(UseMock?"가상 계좌 주문에서 열었습니다. 실제 주문 없이 실시간 시세로 전략을 실행합니다.":"실제 계좌 주문에서 열었습니다. 선택 계좌로만 전략 주문을 실행합니다."):"모의/실제 실행 방식을 직접 선택합니다.";public string Message {get=>message;private set{message=value;Changed();}}public bool Running=>running;
    public MultiTradeViewModel(IPublicMarketData provider,TradingStore store,Func<Task> start,Func<string?,Task> stop,Func<Task> review,Func<string,decimal,TradingSettings> presetLoader,Func<string> account,Func<bool> supported,Func<bool>? mockMode=null,Func<Task>? mockStart=null,Func<IEnumerable<StockItem>>? screeningSeed=null,Func<Task>? realtimeMockStart=null,Func<bool>? realtimeSupported=null,Func<IEnumerable<string>>? strategyNames=null)
    {
        this.provider=provider;this.store=store;this.start=start;this.stop=stop;this.review=review;this.presetLoader=presetLoader;this.account=account;this.supported=supported;this.mockMode=mockMode??(()=>false);this.mockStart=mockStart??(()=>Task.CompletedTask);this.screeningSeed=screeningSeed??(()=>[]);this.realtimeMockStart=realtimeMockStart??(()=>Task.CompletedTask);this.realtimeSupported=realtimeSupported??supported;this.strategyNames=strategyNames??(()=>["국내 과매도·낙주 반등"]);
        var saved=store.State<MultiTradeDrafts>("multi-trade-draft");if(saved!=null){TotalBudget=saved.TotalBudget;DailyLoss=saved.DailyLoss;useRealtime=saved.UseRealtime;foreach(var id in saved.GroupIds)groupIds.Add(id);foreach(var r in saved.Rows){var row=new MultiTradeRow{Stock=r.Stock,Preset=r.Preset,Budget=r.Budget,Selected=r.Selected};Rows.Add(row);BindRow(row);}}
        Add("검색",async()=>{var found=await provider.Search(Query,Region,default);Results.Clear();foreach(var s in found)Results.Add(s);SelectedResult=Results.FirstOrDefault();Changed(nameof(SelectedResult));},()=>CanEdit);
        Add("추가",()=>{var stock=SelectedResult??throw new InvalidOperationException("검색 결과에서 종목을 선택하세요.");if(Rows.Any(r=>r.Stock.Key==stock.Key))throw new InvalidOperationException("이미 추가한 종목입니다.");var row=new MultiTradeRow{Stock=stock,Preset=Presets.FirstOrDefault()??"국내 과매도·낙주 반등"};Rows.Add(row);BindRow(row);SyncSelected(stock,true);return Task.CompletedTask;},()=>CanEdit);
        Add("삭제",()=>{if(SelectedRow!=null){var stock=SelectedRow.Stock;Rows.Remove(SelectedRow);selectedKeys.Remove(stock.Key);SyncMembers(stock.Key);}return Task.CompletedTask;},()=>CanEdit);
        Add("후보추가",()=>{SeedSymbols(this.screeningSeed());return Task.CompletedTask;},()=>CanEdit);
        Add("저장",()=>{Save();Message="목록만 저장했습니다. 프로그램 재시작 시 자동 주문하지 않습니다.";return Task.CompletedTask;},()=>CanEdit);
        Add("실행",async()=>{Save();if(UseMock){if(UseRealtime)await this.realtimeMockStart();else await this.mockStart();}else await this.start();},()=>CanEdit);
        Add("전체중지",()=>stop(null),()=>!busy);Add("개별중지",()=>SelectedRow==null?Task.CompletedTask:stop(SelectedRow.Stock.Key),()=>!busy);Add("이전확인",review,()=>CanEdit&&supported());
        Reload();
    }
    private void Add(string key,Func<Task> action,Func<bool> can)=>Actions[key]=new ActionCommand(async()=>
    {
        if(busy)return;busy=true;var before=Message;Refresh();
        try{await action();if(!string.Equals(before,Message,StringComparison.Ordinal))NotificationRequested?.Invoke(Message);}
        catch(Exception ex){Message=ex is InvalidOperationException?ex.Message:"처리 실패: 연결과 저장 상태를 확인하세요.";NotificationRequested?.Invoke(Message);}
        finally{busy=false;Refresh();}
    },can);
    private void BindRow(MultiTradeRow row){row.BindSelection(SyncSelected);if(row.Selected)selectedKeys.Add(row.Stock.Key);}
    private void SyncSelected(StockItem stock,bool selected)
    {
        if(!CanEdit){foreach(var row in Rows.Where(r=>r.Stock.Key==stock.Key))row.SetSelected(selectedKeys.Contains(stock.Key));SyncMembers(stock.Key);return;}
        if(selected)selectedKeys.Add(stock.Key);else selectedKeys.Remove(stock.Key);
        foreach(var row in Rows.Where(r=>r.Stock.Key==stock.Key))row.SetSelected(selected);
        SyncMembers(stock.Key);
    }
    private void SyncMembers(string key)
    {
        foreach(var group in Groups)
        {
            foreach(var member in group.Members.Where(m=>m.Stock.Key==key))member.SetSelected(selectedKeys.Contains(key));
            group.SetSelected(group.Members.Count==0?false:group.Members.All(m=>m.Selected)?true:group.Members.Any(m=>m.Selected)?null:false);
        }
    }
    private void SetGroup(MultiTradeGroup group,bool? value)
    {
        if(!CanEdit){group.SetSelected(group.Members.Count==0?false:group.Members.All(m=>m.Selected)?true:group.Members.Any(m=>m.Selected)?null:false);return;}
        if(value==true)groupIds.Add(group.Id);else groupIds.Remove(group.Id);
        foreach(var member in group.Members)SyncSelected(member.Stock,value==true);
    }
    public void ReloadWatchGroups(IEnumerable<WatchGroup> watchGroups)
    {
        if(!CanEdit)return;
        Groups.Clear();
        foreach(var source in watchGroups)
        {
            var group=new MultiTradeGroup(source.Id,source.Name,SetGroup);Groups.Add(group);
            foreach(var entry in source.Items)
            {
                var row=Rows.FirstOrDefault(r=>r.Stock.Key==entry.Stock.Key);
                if(row==null){row=new MultiTradeRow{Stock=entry.Stock,Selected=groupIds.Contains(source.Id),Preset=Presets.FirstOrDefault()??"국내 과매도·낙주 반등"};Rows.Add(row);BindRow(row);}else if(string.IsNullOrWhiteSpace(row.Preset))row.Preset=Presets.FirstOrDefault()??"국내 과매도·낙주 반등";
                var member=new MultiTradeMember(entry.Stock,SyncSelected);member.SetSelected(selectedKeys.Contains(entry.Stock.Key));group.Members.Add(member);
            }
        }
        foreach(var group in Groups)group.SetSelected(group.Members.Count==0?false:group.Members.All(m=>m.Selected)?true:group.Members.Any(m=>m.Selected)?null:false);
    }
    public void SeedSymbols(IEnumerable<StockItem> symbols)
    {
        if(!CanEdit)return;
        foreach(var stock in symbols.DistinctBy(s=>s.Key))
        {
            if(Rows.All(r=>r.Stock.Key!=stock.Key)){var row=new MultiTradeRow{Stock=stock,Preset=Presets.FirstOrDefault()??"국내 과매도·낙주 반등"};Rows.Add(row);BindRow(row);}
            SyncSelected(stock,true);
        }
    }
    public void Reload()
    {
        var names=strategyNames().Where(n=>!string.IsNullOrWhiteSpace(n)).Select(n=>n.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if(names.Length==0)names=["기본"];
        Presets.Clear();foreach(var name in names)Presets.Add(name);
        foreach(var row in Rows.Where(r=>string.IsNullOrWhiteSpace(r.Preset)||!Presets.Contains(r.Preset)))row.Preset=Presets[0];
        Changed(nameof(Account));Changed(nameof(RunCaption));Refresh();
    }
    public void PrepareLaunchMode(bool? mock)
    {
        if(!CanEdit)return;modeLocked=mock.HasValue;
        if(mock.HasValue){useMock=mock.Value;useRealtime=true;}
        Changed(nameof(LaunchContextText));Refresh();
    }
    public void Apply(LiveBatchStatus s){running=s.Running;Message=s.Status;foreach(var r in Rows){var state=s.Rows.FirstOrDefault(x=>x.Key==r.Stock.Key);if(state!=null)r.Apply(state);}Refresh();}
    public StockItem[] SelectedStocks()=>Rows.Where(r=>r.Selected).Select(r=>r.Stock).DistinctBy(s=>s.Key).ToArray();
    public (StockItem Stock,string Preset)[] SelectedStrategies()=>Rows.Where(r=>r.Selected).DistinctBy(r=>r.Stock.Key).Select(r=>(r.Stock,r.Preset)).ToArray();
    public void ApplyMock(string status,IReadOnlyDictionary<string,string> states,bool locked){running=locked;Message=status;foreach(var row in Rows)if(states.TryGetValue(row.Stock.Key,out var state))row.State=state;Refresh();}
    public LiveBatchPlan CreatePlan(decimal equity)
    {
        var items=new List<LiveBatchItem>();foreach(var row in Rows.Where(r=>r.Selected))
        {
            try{var settings=presetLoader(row.Preset,equity);var reason=LiveBatchSupport.Reason(row.Stock,settings);if(reason!=null)throw new InvalidOperationException(reason);var amount=Money(row.Budget,"종목 예산");row.Error="";items.Add(new(row.Stock,row.Preset,settings,amount));}
            catch(InvalidOperationException ex){row.Error=ex.Message;throw new InvalidOperationException(row.Stock.Name+": "+ex.Message);}
        }
        return new(items.ToArray(),Money(TotalBudget,"전체 투자 한도"),Money(DailyLoss,"일 손실 한도"));
    }
    private static decimal Money(string text,string name)=>decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)&&n>0&&n<=1e12m?n:throw new InvalidOperationException(name+"는 양수로 입력하세요.");
    public void Save()=>store.State("multi-trade-draft",new MultiTradeDrafts(Rows.Select(r=>new MultiTradeDraft(r.Stock,r.Preset,r.Budget,r.Selected)).ToArray(),TotalBudget,DailyLoss){GroupIds=groupIds.ToArray(),UseRealtime=UseRealtime});
    public void ResetToMock(){useMock=true;useRealtime=false;modeLocked=false;Reload();Changed(nameof(UseMock));Changed(nameof(UseRealtime));Changed(nameof(LaunchContextText));}
    public void Refresh(){Changed(nameof(CanEdit));Changed(nameof(IsLocked));Changed(nameof(CanToggleMock));Changed(nameof(CanToggleRealtime));Changed(nameof(UseMock));Changed(nameof(UseRealtime));Changed(nameof(CanUseLiveBudget));Changed(nameof(RunCaption));Changed(nameof(Running));Changed(nameof(Account));Changed(nameof(LaunchContextText));foreach(var c in Actions.Values)c.Refresh();}
}
