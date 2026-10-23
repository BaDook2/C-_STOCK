using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Threading;
using Stock.Core;
using Stock.Core.Trading;
namespace Stock.App;
public sealed class ActionCommand(Func<Task> action,Func<bool> enabled) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter)=>enabled();
    public async void Execute(object? parameter){if(CanExecute(parameter))await action();}
    public void Refresh()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
}
public sealed record ScannerRow(string Symbol,string Currency,decimal Price,decimal? Spread,decimal? Volume,decimal Rate,string Support,string Profile,string Status);
public sealed partial class MainViewModel : Observable,IDisposable
{
    private readonly TradingStore store;
    private readonly Func<string,bool> confirm;
    private readonly Func<KiwoomCredentials?> credentialsPrompt;
    private readonly object engineLock=new();
    private readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer timer;
    private readonly List<ActionCommand> commands=[];
    private TradingSettings settings;
    private KiwoomCredentials? credentials;
    private KiwoomBroker? kiwoom;
    private RemoteTradingSession? remote;
    private TradingEngine? engine;
    private CancellationTokenSource? feedCancel,backtestCancel;
    private Task? feedTask;
    private BacktestResult? result;
    private bool busy,live;
    private string status="준비됨 · 샘플 생성 → 백테스트 실행으로 키 없이 테스트할 수 있습니다.",mode="오프라인 데모",runMode="연결 전",symbol="AAPL",price="2.00",quantity="10",preset="기본",metrics="—",dataStatus="수신 전",backtestStatus="합성 샘플 제공. L1 모델은 실제 큐 우선순위를 재현하지 않습니다.";
    private Exchange exchange=Exchange.NASDAQ;
    private long received,stored,excluded;
    private int progress;
    public static string StateDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StockPaperTrading","KiwoomQuant");
    public bool Busy {get=>busy;private set{busy=value;Changed();RefreshCommands();}}
    public bool Live {get=>live;private set{live=value;Changed();RefreshCommands();}}
    public bool CanConfigure=>!Busy&&!Live;
    public string Status {get=>status;private set{status=value;Changed();}}
    public string RunMode {get=>runMode;private set{runMode=value;Changed();}}
    public string Mode {get=>mode;set{if(!CanConfigure)return;mode=value;Changed();}}
    public string[] Modes {get;}=["오프라인 데모","키움 시세 + 로컬 Paper","키움 모의 수동"];
    public Exchange[] Exchanges {get;}=Enum.GetValues<Exchange>();
    public Exchange SelectedExchange {get=>exchange;set{if(!CanConfigure)return;exchange=value;Symbol=value==Exchange.KRX?"005930":"AAPL";Price=value==Exchange.KRX?"70000":"2.00";remote=null;Changed();Changed(nameof(Currency));}}
    public string Currency=>SelectedExchange==Exchange.KRX?"KRW":"USD";
    public string Symbol {get=>symbol;set{symbol=value;Changed();}}
    public string Price {get=>price;set{price=value;Changed();}}
    public string Quantity {get=>quantity;set{quantity=value;Changed();}}
    public OrderKind ManualKind {get;set;}=OrderKind.Limit;
    public OrderKind[] OrderKinds {get;}=Enum.GetValues<OrderKind>();
    public string PresetName {get=>preset;set{preset=value;Changed();}}
    public DateTime? QueryDate {get;set;}=KoreaClock.Today.ToDateTime(TimeOnly.MinValue);
    public string CancelNumber {get;set;}="";
    public string Metrics {get=>metrics;private set{metrics=value;Changed();}}
    public string DataStatus {get=>dataStatus;private set{dataStatus=value;Changed();}}
    public string BacktestStatus {get=>backtestStatus;private set{backtestStatus=value;Changed();}}
    public int Progress {get=>progress;private set{progress=value;Changed();}}
    public string DbPath=>store.Path;
    public string ChartPoints {get;private set;}="";
    public ObservableCollection<string> Presets {get;}=[];
    public ObservableCollection<SettingGroup> SettingGroups {get;}=[];
    public ObservableCollection<FilterEditor> Filters {get;}=[];
    public ObservableCollection<ScannerRow> Scanner {get;}=[];
    public ObservableCollection<ManagedOrder> PaperOrders {get;}=[];
    public ObservableCollection<Position> Positions {get;}=[];
    public ObservableCollection<Fill> Fills {get;}=[];
    public ObservableCollection<BrokerOrder> Orders {get;}=[];
    public ObservableCollection<Holding> Holdings {get;}=[];
    public ObservableCollection<string> Logs {get;}=[];
    public ObservableCollection<string> Datasets {get;}=[];
    public ObservableCollection<string> History {get;}=[];
    public Dictionary<string,ActionCommand> Actions {get;}=[];
    public MainViewModel(Func<string,bool> confirm,Func<KiwoomCredentials?>? credentialsPrompt=null,TradingStore? store=null)
    {
        this.confirm=confirm;this.credentialsPrompt=credentialsPrompt??(()=>null);this.store=store??new TradingStore(Path.Combine(StateDirectory,"stock.db"));settings=this.store.Load("기본");BuildEditors();RefreshSavedLists();
        Add("설정",()=>{credentials=this.credentialsPrompt()??credentials;Status="키는 앱 메모리에만 보관합니다.";return Task.CompletedTask;},()=>CanConfigure);
        Add("연결",Connect,()=>CanConfigure);Add("시작",Start,()=>CanConfigure);Add("중단",Stop,()=>Live&&!Busy);
        Add("긴급중지",async()=>{lock(engineLock)engine?.EmergencyStop();if(remote!=null&&RunMode.StartsWith("키움 모의"))await remote.EmergencyStop();Status="신규 주문 차단 / 취소 확인은 다음 이벤트 또는 증권사 조회에서 확인하세요.";},()=>!Busy&&(engine!=null||remote!=null));
        Add("매수",()=>Place(true),()=>!Busy&&(Live||remote!=null));Add("매도",()=>Place(false),()=>!Busy&&(Live||remote!=null));
        Add("조회",RefreshRemote,()=>!Busy);
        Add("취소",async()=>{if(remote!=null&&RunMode.StartsWith("키움 모의")){var o=Orders.FirstOrDefault(o=>o.Number==CancelNumber)??throw new InvalidOperationException("조회된 주문번호를 입력하세요.");await remote.Cancel(o.Number,o.Symbol);}else lock(engineLock)engine?.Cancel(CancelNumber);Status="취소 요청. 확인 전 재주문 금지.";},()=>!Busy);
        Add("외부확인",()=>{if(remote!=null&&confirm("HTS에서 응답 불명·취소 요청 상태를 확인했습니까?\n신규 주문 차단을 해제합니다. 당일 동일 조건 중복 차단은 유지됩니다.")){remote.AcknowledgeReview();Status="외부 확인 완료";}return Task.CompletedTask;},()=>!Busy&&remote!=null);
        Add("저장",()=>{ApplyEditors();this.store.Save(PresetName,settings);RefreshSavedLists();Status="설정 저장 완료. 다음 실행부터 반영됩니다.";return Task.CompletedTask;},()=>CanConfigure);
        Add("불러오기",()=>{settings=this.store.Load(PresetName);BuildEditors();Status="프리셋 불러오기 완료";return Task.CompletedTask;},()=>CanConfigure);
        Add("기본값",()=>{settings=new();BuildEditors();Status="기본값 복원. 저장 버튼으로 확정하세요.";return Task.CompletedTask;},()=>CanConfigure);
        RegisterDataActions();
        timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};timer.Tick+=(_,_)=>Snapshot();timer.Start();
    }
    private void Add(string name,Func<Task> action,Func<bool> enabled){var c=new ActionCommand(()=>Run(action),enabled);commands.Add(c);Actions[name]=c;}
    private void RefreshCommands(){Changed(nameof(CanConfigure));foreach(var c in commands)c.Refresh();}
    private async Task Run(Func<Task> action){if(Busy)return;Busy=true;try{await action();}catch(UnknownOrderException ex){Status=ex.Message;}catch(BrokerRejectedException ex){Status=ex.Message;}catch(InvalidOperationException ex){Status=ex.Message;}catch(Exception){Status="작업 실패. 연결/파일/저장 상태를 확인하세요. 비밀값/원문 응답은 기록하지 않습니다.";}finally{Busy=false;}}
    public void ApplyEditors(){foreach(var g in SettingGroups)foreach(var f in g.Fields)f.Apply();foreach(var f in Filters)f.Apply();settings.Validate();}
    private void BuildEditors(){SettingGroups.Clear();foreach(var (n,v) in settings.Groups())SettingGroups.Add(new(n,v.GetType().GetProperties().Where(p=>p.GetCustomAttribute<SettingAttribute>()!=null).Select(p=>new SettingField(v,p)).ToArray()));Filters.Clear();foreach(var f in settings.Filters)Filters.Add(new(f));}
    private void RefreshSavedLists(){Replace(Presets,store.Presets());Replace(Datasets,store.Datasets());Replace(History,store.History());}
    private static void Replace<T>(ObservableCollection<T> collection,IEnumerable<T> values){var array=values.ToArray();collection.Clear();foreach(var v in array)collection.Add(v);}
    public async Task ShutdownAsync(){backtestCancel?.Cancel();await Stop();timer.Stop();kiwoom?.Dispose();}
    public void Dispose(){timer.Stop();feedCancel?.Cancel();}
}
