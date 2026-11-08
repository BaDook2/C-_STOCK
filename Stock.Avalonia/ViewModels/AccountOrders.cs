using System.Collections.ObjectModel;
using System.Globalization;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed partial class AccountWorkspace
{
    private bool authorized;
    private long authorizationVersion;
    private string quantity="1",orderLimit="1000000",symbolLimit="2000000",dailyLoss="100000";
    private BrokerOrder? selectedOrder;
    public RemoteTradingSession? Session=>Online?trading.Values.FirstOrDefault(s=>ReferenceEquals(s.Broker,current)):null;
    public Func<StockItem?> OrderStock {get;set;}=()=>null;
    public Func<RemoteTradingSession,Task> BeforeSessionRevoke {get;set;}=_=>Task.CompletedTask;
    public bool Authorized=>authorized&&Online&&Session is {Stopped:false,Unknown:false};
    public bool OrderInputsEditable=>!authorized&&!Busy;
    public string OrderAuthorization=>Authorized?"실제 주문 활성 · 주문마다 다시 확인합니다.":Session is {Unknown:true}?"응답 불명/취소 확인 대기 · HTS 대사 필요":Session is {Stopped:true}?"긴급중지 · 외부 내역 확인 후 해제하세요.":"실제 주문 잠김 · 활성화 확인이 필요합니다.";
    public string Quantity {get=>quantity;set=>Set(ref quantity,value);}
    public string OrderLimit {get=>orderLimit;set=>Set(ref orderLimit,value);}
    public string SymbolLimit {get=>symbolLimit;set=>Set(ref symbolLimit,value);}
    public string DailyLoss {get=>dailyLoss;set=>Set(ref dailyLoss,value);}
    public BrokerOrder? SelectedOrder {get=>selectedOrder;set{if(Set(ref selectedOrder,value))CancelOrder.Refresh();}}
    public ObservableCollection<TradeMarker> Executions {get;}=[];
    public AsyncCommand EnableOrders {get;private set;}=null!;
    public AsyncCommand DisableOrders {get;private set;}=null!;
    public AsyncCommand Buy {get;private set;}=null!;
    public AsyncCommand Sell {get;private set;}=null!;
    public AsyncCommand CancelOrder {get;private set;}=null!;
    public AsyncCommand ReviewOrders {get;private set;}=null!;
    public event Action? FillsChanged;
    public event Action? TradingDisarmed;
    public TradeMarker[] ConfirmedMarkers()
    {
        if(Session is not {} remote)return [];
        var legacy=remote.Broker.Id+":"+TradingStore.Hash(remote.Broker.Account);
        return services.Store.States("actual-fill:").Concat(services.Store.States("trade-marker:")).Select(s=>Serialization.Unpack<TradeMarker>(s.Json)).Where(m=>m.Source=="실제매매"&&(m.AccountScope==remote.Scope||m.AccountScope==legacy)).GroupBy(m=>m.Id[(m.Id.LastIndexOf(':')+1)..]).Select(g=>g.Last()).OrderBy(m=>m.Time).ToArray();
    }
    private static TradingSettings ManualSettings()
    {
        var settings=new TradingSettings();settings.Risk.AccountCapital=10_000_000;settings.Risk.MaxOrderCash=1_000_000;settings.Risk.MaxSymbolCash=2_000_000;settings.Risk.MaxDailyLoss=100_000;settings.Risk.MaxTradeLoss=100_000;settings.Risk.MaxTradeLossPercent=20;settings.Execution.Kind=OrderKind.Market;settings.Execution.Retry=false;settings.Risk.FlattenOnStop=false;return settings;
    }
    private void InitializeOrders()
    {
        EnableOrders=new(Enable,()=>Online&&!Busy&&!authorized);DisableOrders=new(()=>{Disarm();return Task.CompletedTask;});
        Buy=new(()=>Place(true),()=>CanPlace);Sell=new(()=>Place(false),()=>CanPlace);
        CancelOrder=new(CancelSelected,()=>Online&&!Busy&&SelectedOrder is {Remaining:>0,Cancelled:false,Rejected:false});
        ReviewOrders=new(Review,()=>Online&&!Busy&&Session?.AutomationClaimed==false);
    }
    private bool CanPlace=>Authorized&&!Busy&&OrderStock()!=null&&Session?.AutomationClaimed==false;
    public void RefreshOrderCommands()
    {
        foreach(var command in new[]{EnableOrders,DisableOrders,Buy,Sell,CancelOrder,ReviewOrders})command?.Refresh();
        Changed(nameof(Authorized));Changed(nameof(OrderAuthorization));Changed(nameof(OrderInputsEditable));
    }
    public void Disarm(){authorized=false;authorizationVersion++;RefreshOrderCommands();TradingDisarmed?.Invoke();}
    public void Emergency()
    {
        Disarm();CancelPending();foreach(var session in trading.Values)session.BlockNewOrders();RefreshOrderCommands();
        Status="긴급중지 · 모든 계좌 신규 주문 차단 · 보유분 임의청산 없음. 미체결은 계좌 내역에서 확인하세요.";
    }
    private static decimal Money(string text)=>decimal.TryParse(text.Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out var value)&&value>0?value:throw new InvalidOperationException("투자금과 손실 한도는 양수여야 합니다.");
    private async Task Enable()
    {
        var remote=Session;if(remote==null)return;var version=authorizationVersion;
        var order=Money(OrderLimit);var symbol=Money(SymbolLimit);var loss=Money(DailyLoss);if(symbol<order)throw new InvalidOperationException("종목 한도는 주문 한도 이상이어야 합니다.");
        if(!await Confirm($"{remote.Broker.Name} {remote.Broker.MaskedAccount}\n실제 돈으로 주문할 수 있도록 활성화할까요?\n1회 {DisplayNumber.Price(order)}원 / 종목 {DisplayNumber.Price(symbol)}원 / 일 손실 {DisplayNumber.Price(loss)}원\n연결만으로 자동 주문하지 않으며 수동 주문마다 다시 확인합니다."))return;
        if(disposed||Session!=remote||version!=authorizationVersion)return;
        if(remote.Stopped||remote.Unknown)throw new InvalidOperationException("중지/응답 불명 상태입니다. HTS 확인 후 차단 해제를 먼저 진행하세요.");
        remote.SetRiskLimits(order,symbol,loss);authorized=true;RefreshOrderCommands();
    }
    private async Task Place(bool buy)
    {
        var remote=Session;var stock=OrderStock();if(remote==null||stock==null||!Authorized)return;var version=authorizationVersion;
        if(stock.Exchange!=remote.Broker.Exchange||!remote.Broker.SupportsMarketOrders)throw new InvalidOperationException("선택 계좌에서 이 시장의 시장가 주문을 지원하지 않습니다.");
        if(!int.TryParse(Quantity,NumberStyles.None,CultureInfo.InvariantCulture,out var count)||count is <1 or >1_000_000)throw new InvalidOperationException("수량은 1~1,000,000 정수로 입력하세요.");
        if(!LiveBatchSupport.RegularSession(services.Clock()))throw new InvalidOperationException("장 외 시간입니다. 정규 연속매매 시간에 주문하세요.");
        Busy=true;operation?.Cancel();operation=new(services.QueryTimeout);var token=operation.Token;
        try
        {
            var price=await ReferencePrice(remote,stock,token);var limit=Money(OrderLimit);if(price*count>limit)throw new InvalidOperationException("1회 주문 한도를 초과했습니다.");
            if(!await Confirm($"실제 계좌 {remote.Broker.Name} {remote.Broker.MaskedAccount}\n{stock.Name} ({stock.Symbol})\n{(buy?"매수":"매도")} {count:N0}주 · 시장가\n참고가 {DisplayNumber.Price(price)} {stock.Currency} · 예상금액 {DisplayNumber.Price(price*count)}\n시장가 체결가격은 달라질 수 있습니다. 전송할까요?"))return;
            token.ThrowIfCancellationRequested();var fresh=await ReferencePrice(remote,stock,token);
            if(Math.Abs(fresh/price-1)>.02m||fresh*count>limit)throw new InvalidOperationException("확인 중 가격 또는 주문 한도가 달라졌습니다. 현재가를 확인한 후 다시 주문하세요.");
            // Preserve the final unresolved-state check. The session identifies its
            // own not-yet-sent WAL entry; every other pending/unknown intent blocks.
            bool Current()=>!disposed&&authorized&&Online&&remote.TransmissionStateSafe&&Session==remote&&version==authorizationVersion&&!token.IsCancellationRequested;
            if(!Current())throw new InvalidOperationException("계좌 변경/중지로 주문을 차단했습니다.");
            var receipt=await remote.Submit(new(stock.Symbol,count,fresh,buy,stock.Exchange,OrderKind.Market),canTransmit:Current).WaitAsync(token);
            Status="주문 접수 "+receipt.Number+" · 체결 완료가 아닙니다. 계좌 조회로 확인하세요.";
            await remote.Refresh(DailyQuoteMerger.Date(services.Clock(),stock.Exchange)).WaitAsync(token);if(Session==remote){ApplyBalance(remote.Balance!);ApplyOrders(remote.Orders);await RefreshExecutions(remote,token);}
        }
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=remote.Unknown?"주문 응답 불명 · 재전송하지 마세요. HTS 내역 확인 후 차단을 해제하세요.":OperationError.Describe(ex);Notice?.Invoke(Status);}
        finally{Busy=false;RefreshOrderCommands();}
    }
    private async Task<decimal> ReferencePrice(RemoteTradingSession remote,StockItem stock,CancellationToken token)
    {
        if(remote.Broker is ICurrentMarketData source)
        {
            var snapshot=await source.Current(stock,token).WaitAsync(token);
            if(snapshot.Quote.State!="OPEN"||(services.Clock()-snapshot.Quote.TradeTime).Duration()>TimeSpan.FromSeconds(10))throw new InvalidOperationException("현재가 수신 지연/장 외 상태로 주문을 차단했습니다.");
            return snapshot.Quote.Price>0?snapshot.Quote.Price:throw new InvalidOperationException("현재가가 올바르지 않습니다.");
        }
        var started=services.Clock();var price=await remote.Broker.CurrentPrice(stock.Symbol).WaitAsync(token);
        if(price<=0||(services.Clock()-started)>TimeSpan.FromSeconds(10))throw new InvalidOperationException("현재가를 신속하게 확인하지 못해 주문을 차단했습니다.");return price;
    }
    private void ApplyOrders(IReadOnlyList<BrokerOrder> values)
    {
        var id=SelectedOrder?.Number;Orders.Clear();foreach(var order in values)Orders.Add(order);SelectedOrder=Orders.FirstOrDefault(o=>o.Number==id);RefreshOrderCommands();
    }
    private async Task CancelSelected()
    {
        var remote=Session;var order=SelectedOrder;if(remote==null||order==null)return;
        if(!await Confirm($"{remote.Broker.Name} {remote.Broker.MaskedAccount}\n{order.Name} 주문 {order.Number}의 미체결 {order.Remaining:N0}주를 취소 요청할까요?"))return;
        if(disposed||Session!=remote)return;Busy=true;
        try{await remote.Cancel(order.Number,order.Symbol).WaitAsync(services.QueryTimeout);Status="취소 요청 접수 · 취소 완료는 계좌 조회 후 확인합니다.";await remote.Refresh(DailyQuoteMerger.Date(services.Clock(),remote.Broker.Exchange)).WaitAsync(services.QueryTimeout);if(Session==remote)ApplyOrders(remote.Orders);}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=OperationError.Describe(ex);Notice?.Invoke(Status);}finally{Busy=false;RefreshOrderCommands();}
    }
    private async Task Review()
    {
        var remote=Session;if(remote==null)return;if(!await Confirm("HTS/증권사에서 주문·미체결·취소 내역을 직접 확인했나요? 확인되지 않은 주문을 재전송하면 중복 주문할 수 있습니다. 외부 확인을 마쳤다면 로컬 차단을 해제합니다."))return;
        if(Session!=remote||disposed)return;remote.AcknowledgeReview();Disarm();Status="외부 확인 완료 · 실제 주문 활성화는 다시 확인해야 합니다.";
    }
    private async Task RefreshExecutions(RemoteTradingSession remote,CancellationToken token)
    {
        var today=DailyQuoteMerger.Date(services.Clock(),remote.Broker.Exchange);var markers=new List<TradeMarker>();
        if(remote.Broker is IExecutionHistoryBroker history)
        {
            foreach(var fill in await history.GetExecutionsAsync(today).WaitAsync(token))
            {
                if(fill.Quantity<=0||fill.Price<=0||fill.Time>services.Clock().AddSeconds(5))throw new InvalidOperationException("증권사 체결 응답의 수량·가격·시간을 확인하세요.");
                var id="actual-fill:"+TradingStore.Hash(remote.Scope+":"+fill.Id);markers.Add(new(id,new Instrument(fill.Symbol,remote.Broker.Exchange).Key,fill.Time,fill.Buy,fill.Quantity,fill.Price,"실제매매",remote.Scope,Reason:"증권사 확인 체결 · "+fill.OrderNumber));
            }
        }
        else foreach(var order in remote.Orders.Where(o=>o.Filled>0&&o.AverageFillPrice is >0&&o.LastFillTime!=null))
        {
            var id="actual-fill:"+TradingStore.Hash(remote.Scope+":"+order.Date+":"+order.Number);markers.Add(new(id,new Instrument(order.Symbol,remote.Broker.Exchange).Key,order.LastFillTime!.Value,order.Side.Contains("매수"),order.Filled,order.AverageFillPrice,"실제매매",remote.Scope,Reason:"증권사 확인 누적 체결 · "+order.Number));
        }
        if(Session!=remote||disposed)return;
        foreach(var marker in markers)services.Store.State(marker.Id,marker);
        Executions.Clear();foreach(var row in ConfirmedMarkers().OrderByDescending(m=>m.Time))Executions.Add(row);
        FillsChanged?.Invoke();
    }
}
