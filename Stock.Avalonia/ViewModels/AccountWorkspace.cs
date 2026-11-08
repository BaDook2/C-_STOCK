using System.Collections.ObjectModel;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.Desktop;

/// <summary>Verified sessions and OS-protected credentials. Only successful authentication/account reads are saved.</summary>
public sealed partial class AccountWorkspace(AppServices services):Bindable,IDisposable
{
    private readonly Dictionary<Brokerage,IAccountBroker> sessions=[];
    private readonly Dictionary<Brokerage,RemoteTradingSession> trading=[];
    private CancellationTokenSource? operation;
    private bool online,reveal,busy,disposed,remember=true,useAsDefault=true;
    private Brokerage selected=Brokerage.Kiwoom;
    private string key="",secret="",account="",product="01",mac="",status="키는 Windows 암호화 저장소 / Mac Keychain에 보관합니다. 연결만으로 주문하지 않습니다.";
    private IAccountBroker? current;
    private long editorVersion;
    public bool EditorOpen {get;set;}
    public BrokerageInfo[] Brokers {get;}=BrokerageInfo.All.Where(b=>b.Available).ToArray();
    public Brokerage Selected {get=>selected;set{if(Set(ref selected,value)){CancelPending();ClearInputs();Changed(nameof(SelectedInfo));Changed(nameof(NeedsAccount));Changed(nameof(NeedsMac));if(EditorOpen&&LoadSaved!=null)LoadSaved.Execute(null);}}}
    public BrokerageInfo SelectedInfo {get=>Brokers.Single(b=>b.Id==Selected);set{if(value!=null)Selected=value.Id;}}
    public bool NeedsAccount=>Selected==Brokerage.KoreaInvestment;
    public bool NeedsMac=>Selected==Brokerage.Meritz;
    public string AppKey {get=>key;set{if(Set(ref key,value))editorVersion++;}}
    public string Secret {get=>secret;set{if(Set(ref secret,value))editorVersion++;}}
    public string Account {get=>account;set{if(Set(ref account,value))editorVersion++;}}
    public string Product {get=>product;set{if(Set(ref product,value))editorVersion++;}}
    public string Mac {get=>mac;set{if(Set(ref mac,value))editorVersion++;}}
    public bool Remember {get=>remember;set=>Set(ref remember,value);}
    public bool UseAsDefault {get=>useAsDefault;set=>Set(ref useAsDefault,value);}
    public bool Reveal {get=>reveal;set{if(Set(ref reveal,value))Changed(nameof(SecretMask));}}
    public char SecretMask=>Reveal?'\0':'●';
    public bool Busy {get=>busy;private set{Set(ref busy,value);Changed(nameof(Editable));Connect?.Refresh();Refresh?.Refresh();OnlineMode?.Refresh();LoadSaved?.Refresh();ResetSelected?.Refresh();ResetAll?.Refresh();RefreshOrderCommands();}}
    public bool Editable=>!Busy;
    public string Status {get=>status;private set=>Set(ref status,value);}
    public bool Online=>online&&current!=null;
    public IAccountBroker? Active=>Online?current:null;
    public string Mode=>Online?current!.Name+" · "+current.MaskedAccount:"오프라인 · 공개 시세";
    public string Cash {get;private set;}="—";
    public ObservableCollection<Holding> Holdings {get;}=[];
    public ObservableCollection<BrokerOrder> Orders {get;}=[];
    public AsyncCommand Connect {get;private set;}=null!;
    public AsyncCommand Refresh {get;private set;}=null!;
    public AsyncCommand Offline {get;private set;}=null!;
    public AsyncCommand OnlineMode {get;private set;}=null!;
    public AsyncCommand ResetSelected {get;private set;}=null!;
    public AsyncCommand ResetAll {get;private set;}=null!;
    public AsyncCommand LoadSaved {get;private set;}=null!;
    public Func<string,Task<bool>> Confirm {get;set;}=_=>Task.FromResult(false);
    public event Action? ModeChanged;
    public event Action? ConnectionRequested;
    public event Action<string>? Notice;
    public void Initialize()
    {
        Connect=new(ConnectSelected,()=>!Busy);Refresh=new(RefreshAccount,()=>!Busy&&Online);
        Offline=new(()=>{CancelPending();Disarm();online=false;PublishMode();return Task.CompletedTask;});
        OnlineMode=new(EnterOnline,()=>!Busy);LoadSaved=new(LoadEditor,()=>!Busy);
        ResetSelected=new(()=>ResetCredentials(false),()=>!Busy);ResetAll=new(()=>ResetCredentials(true),()=>!Busy);
        try{selected=services.Credentials.DefaultBroker??Brokerage.Kiwoom;}catch(Exception ex) when(ex is not OutOfMemoryException){Status="기본 계좌 설정을 읽지 못했습니다. 키 설정에서 계좌를 선택하세요.";}
        InitializeOrders();
    }
    private async Task ConnectSelected()
    {
        var choice=Selected;BrokerConnection? connection=null;
        if(!sessions.ContainsKey(choice)||!string.IsNullOrWhiteSpace(AppKey))connection=new(){Brokerage=choice,AppKey=AppKey.Trim(),Secret=Secret.Trim(),Account=Account.Trim(),Product=Product.Trim(),MacAddress=Mac.Trim(),UseAsDefault=UseAsDefault};
        await Verify(choice,connection,Remember);
    }
    private async Task Verify(Brokerage choice,BrokerConnection? connection,bool save)
    {
        CancelPending();operation=new(services.QueryTimeout);var token=operation.Token;IAccountBroker? candidate=null;Busy=true;
        try
        {
            if(connection==null&&sessions.TryGetValue(choice,out var cached)){Disarm();current=cached;online=true;PublishMode();Status="검증된 연결을 재사용합니다.";ClearInputs();return;}
            if(connection==null)throw new InvalidOperationException("연결 키를 입력하세요.");connection.Validate();
            Status="인증·계좌 조회 중 · 주문 전송 없음";candidate=services.BrokerFactory(connection,Exchange.KRX);await candidate.ConnectAsync().WaitAsync(token);var balance=await candidate.GetBalanceAsync().WaitAsync(token);token.ThrowIfCancellationRequested();
            var remote=new RemoteTradingSession(candidate,ManualSettings(),services.Store,Path.Combine(services.DataDirectory,"order-guards"));await remote.Refresh(DailyQuoteMerger.Date(services.Clock(),candidate.Exchange)).WaitAsync(token);token.ThrowIfCancellationRequested();
            if(save){Status="인증 완료 · OS 보안 저장 중";await Task.Run(()=>services.Credentials.Save(connection),token);token.ThrowIfCancellationRequested();}
            if(disposed||Selected!=choice)return;if(trading.Remove(choice,out var previousRemote))await previousRemote.Revoke().WaitAsync(token);Disarm();if(sessions.Remove(choice,out var previous))previous.Dispose();sessions.Add(choice,candidate);trading[choice]=remote;current=candidate;candidate=null;online=true;ApplyBalance(remote.Balance!);ApplyOrders(remote.Orders);ClearInputs();Status=save?"인증 완료 · 키 보안 저장 완료 · 주문은 별도 확인 후 실행":"인증 완료 · 이번 실행에서만 연결";PublishMode();
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){Status="연결 요청 취소";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=ex is OperationCanceledException or TimeoutException?OperationError.ResponseTimeout:"인증 또는 키 보안 저장에 실패했습니다. 연결 설정·키 권한·OS 보안 저장소 접근 권한을 확인하세요. 기존 연결은 유지됩니다.";Notice?.Invoke(Status);}
        finally{candidate?.Dispose();Busy=false;}
    }
    private async Task EnterOnline()
    {
        if(current!=null){online=true;PublishMode();return;}
        try
        {
            var id=services.Credentials.DefaultBroker;if(id==null){ConnectionRequested?.Invoke();return;}
            selected=id.Value;Changed(nameof(Selected));Changed(nameof(SelectedInfo));Changed(nameof(NeedsAccount));Changed(nameof(NeedsMac));
            var version=authorizationVersion;Busy=true;var connection=await Task.Run(()=>services.Credentials.Load(id.Value));Busy=false;
            if(disposed||version!=authorizationVersion)return;if(connection==null){ConnectionRequested?.Invoke();return;}await Verify(id.Value,connection,false);
        }
        catch(Exception ex) when(ex is not OutOfMemoryException){Status="저장된 기본 계좌 키를 읽지 못했습니다. OS 보안 저장소 권한을 확인하거나 키를 다시 입력하세요.";Notice?.Invoke(Status);ConnectionRequested?.Invoke();}
        finally{Busy=false;}
    }
    private async Task LoadEditor()
    {
        var choice=Selected;var version=editorVersion;Busy=true;
        try{var saved=await Task.Run(()=>services.Credentials.Load(choice));if(disposed||choice!=Selected||version!=editorVersion)return;ClearInputs();if(saved!=null){AppKey=saved.AppKey;Secret=saved.Secret;Account=saved.Account;Product=saved.Product;Mac=saved.MacAddress;UseAsDefault=services.Credentials.DefaultBroker==choice;Status="저장된 키를 불러왔습니다. 기본은 숨김 표시입니다.";}}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status="저장된 키를 읽지 못했습니다. OS 보안 저장소 권한을 확인하거나 다시 입력하세요.";Notice?.Invoke(Status);}
        finally{Busy=false;}
    }
    private async Task RefreshAccount()
    {
        if(Session is not {} remote)return;CancelPending();operation=new(services.QueryTimeout);var token=operation.Token;Busy=true;
        try{await remote.Refresh(DailyQuoteMerger.Date(services.Clock(),remote.Broker.Exchange)).WaitAsync(token);token.ThrowIfCancellationRequested();if(disposed||Session!=remote)return;ApplyBalance(remote.Balance!);ApplyOrders(remote.Orders);await RefreshExecutions(remote,token);Status="계좌 조회 완료 · 확인된 체결만 표시";}
        catch(OperationCanceledException) when(token.IsCancellationRequested){Status="계좌 조회 취소";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status="계좌 조회 실패 · 이전 조회 값은 최신이 아닐 수 있습니다.";Notice?.Invoke(Status);}
        finally{Busy=false;}
    }
    private void ApplyBalance(Balance balance){Cash=DisplayNumber.Price(balance.Cash)+" "+balance.Currency;Changed(nameof(Cash));Holdings.Clear();foreach(var row in balance.Holdings)Holdings.Add(row);}
    private void PublishMode(){Changed(nameof(Online));Changed(nameof(Mode));Changed(nameof(Session));Refresh.Refresh();RefreshOrderCommands();ModeChanged?.Invoke();}
    public void ClearInputs(){editorVersion++;AppKey="";Secret="";Account="";Mac="";Reveal=false;}
    private async Task ResetCredentials(bool all)
    {
        var id=Selected;var label=all?"이 앱에 저장된 모든 증권사":SelectedInfo.Name;
        if(!await Confirm(label+" API 키와 연결을 초기화할까요? 이 앱의 보안 저장 키와 기본 계좌 선택을 지웁니다. 보유 종목은 청산하지 않습니다."))return;
        Busy=true;CancelPending();
        try
        {
            foreach(var choice in all?sessions.Keys.ToArray():new[]{id})if(sessions.TryGetValue(choice,out var session)){if(current==session){Disarm();online=false;current=null;PublishMode();}if(trading.TryGetValue(choice,out var remote)){await BeforeSessionRevoke(remote);await remote.Revoke().WaitAsync(services.QueryTimeout);trading.Remove(choice);}sessions.Remove(choice);session.Dispose();}
            await Task.Run(()=>{if(all)services.Credentials.DeleteAll();else services.Credentials.Delete(id);});ClearInputs();Status="선택한 보안 저장 키와 연결을 초기화했습니다. 원본 WPF 자료는 변경하지 않았습니다.";
        }
        catch(Exception ex) when(ex is not OutOfMemoryException){Status="연결은 해제했지만 보안 저장 키 삭제를 완료하지 못했습니다. OS 저장소 권한을 확인하세요.";Notice?.Invoke(Status);}
        finally{Busy=false;}
    }
    public void CancelPending()=>operation?.Cancel();
    public void Dispose(){if(disposed)return;disposed=true;CancelPending();Disarm();ClearInputs();foreach(var remote in trading.Values)_=remote.Revoke();foreach(var session in sessions.Values)session.Dispose();sessions.Clear();trading.Clear();current=null;online=false;}
}

public sealed partial class MainViewModel
{
    public AccountWorkspace Accounts {get;private set;}=null!;
    public event Action<AccountWorkspace>? AccountPopupRequested;
    public AsyncCommand OpenAccounts {get;private set;}=null!;
    private void InitializeAccounts()
    {
        Accounts=new(services);Accounts.Initialize();Accounts.OrderStock=()=>SelectedStock;Accounts.Notice+=Notify;Accounts.FillsChanged+=RefreshPaperMarkers;Accounts.ConnectionRequested+=()=>AccountPopupRequested?.Invoke(Accounts);OpenAccounts=new(()=>{AccountPopupRequested?.Invoke(Accounts);return Task.CompletedTask;});
        Accounts.ModeChanged+=()=>{operation?.Cancel();Realtime.Emergency();Research.Stop();Recurring.Stop();Screening.Stop();DayTrading.BlockNewEntries("계좌 모드 변경 · 새 진입 중지");quotePump?.Stop();RefreshPaperMarkers();Changed(nameof(Online));Changed(nameof(ModeText));Changed(nameof(ChartCustomAvailable));Research.ModeChanged();Screening.ModeChanged();if(!Online&&ChartPeriods.Intraday(ChartPeriod))ChartPeriod="일봉";if(selectedStock is {} stock)_=Run(token=>LoadStock(stock,token));};
    }
}
