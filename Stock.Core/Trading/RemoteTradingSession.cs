namespace Stock.Core.Trading;
// Manual and explicitly authorized live strategy orders share account serialization and durable guards.
public sealed record AutomaticOrderContext(string Owner,string ClientId,Func<Balance,IReadOnlyList<BrokerOrder>,string?> Check,Func<bool> CanTransmit);
public sealed class RemoteTradingSession
{
    private readonly TradingStore store;
    private readonly TradingSettings settings;
    private readonly OrderGuard guard;
    private readonly string scope,run;
    private readonly SemaphoreSlim gate=new(1,1);
    public IAccountBroker Broker { get; }
    public Balance? Balance { get; private set; }
    public IReadOnlyList<BrokerOrder> Orders { get; private set; }=[];
    public IReadOnlyList<LocalOrder> LocalOrders=>guard.Entries;
    public string Scope=>scope; private string? automaticOwner; public bool AutomationClaimed=>automaticOwner!=null; public void ClaimAutomation(string owner){if(Interlocked.CompareExchange(ref automaticOwner,owner,null)!=null)throw new InvalidOperationException("이 계좌에서 다른 자동매매가 실행 중입니다.");} public void ReleaseAutomation(string owner)=>Interlocked.CompareExchange(ref automaticOwner,null,owner); public bool Stopped { get; private set; }
    public bool Unknown=>guard.HasUnresolved||store.State<Dictionary<string,string>>(scope+"-cancel")?.Any(p=>p.Value is "전송 중" or "응답 불명" or "취소 요청")==true;
    private volatile bool revoked;
    /// <summary>Irreversibly stops this in-memory session, waits for accepted work, and retains reconciliation records.</summary>
    public async Task Revoke()
    {revoked=true;await gate.WaitAsync();gate.Release();}
    public RemoteTradingSession(IAccountBroker broker,TradingSettings settings,TradingStore store,string directory)
    {
        if(broker.Account=="")throw new InvalidOperationException("계좌 연결 필요");Broker=broker;this.settings=settings.Clone();this.store=store;scope=broker.Id+(broker.IsLive?"-live-":"-")+TradingStore.Hash(broker.Account)[..24];guard=new OrderGuard(System.IO.Path.Combine(directory,scope+".json"));Stopped=store.State<bool>(scope+"-stopped");run=store.BeginRun(broker.Name+(broker.IsLive?" 실제 계좌 수동":" 모의 수동"),settings);
    }
    public async Task Refresh(DateOnly date){await gate.WaitAsync();try{await RefreshCore(date);}finally{gate.Release();}}
    private async Task RefreshCore(DateOnly date)
    {Balance=await Broker.GetBalanceAsync();Orders=await Broker.GetOrdersAsync(date);var cancellations=store.State<Dictionary<string,string>>(scope+"-cancel")??[];foreach(var key in cancellations.Keys.ToArray())if(Orders.Any(o=>(o.Number==key||o.OriginalNumber==key)&&(o.Cancelled||o.Filled>=o.Quantity&&o.Quantity>0)))cancellations[key]="확인 완료";store.State(scope+"-cancel",cancellations);}
    public async Task<OrderReceipt> Submit(OrderRequest request,AutomaticOrderContext? automatic=null)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("주문 처리 중");try
        {
            request.Validate();if(revoked)throw new InvalidOperationException("종료된 계좌 연결입니다.");if(automaticOwner!=null&&automatic?.Owner!=automaticOwner||automatic!=null&&automatic.Owner!=automaticOwner)throw new InvalidOperationException("자동매매가 계좌를 사용 중입니다. 일괄 중지 후 수동 주문하세요.");if(Stopped||Unknown)throw new InvalidOperationException("긴급중지/응답 불명/취소 확인 대기: 신규 주문 차단");
            await RefreshCore(KoreaClock.Today);var b=Balance!;if(Stopped||Unknown||automatic?.CanTransmit()==false)throw new InvalidOperationException("중지/미확인/시세 만료 상태입니다.");var automaticError=automatic?.Check(b,Orders);if(automaticError!=null)throw new InvalidOperationException(automaticError);var p=new PaperBroker(b.Currency,b.Cash,settings.Execution);
            foreach(var h in b.Holdings)p.Positions[new Instrument(h.Symbol,Broker.Exchange).Key]=new(new Instrument(h.Symbol,Broker.Exchange)){Quantity=(int)h.Quantity,AveragePrice=h.AveragePrice,Last=h.CurrentPrice,Owner="수동"};
            foreach(var o in Orders.Where(o=>o.Remaining>0&&!o.Cancelled&&!o.Rejected))p.Orders.Add(new(){Id=o.Number,Instrument=new(o.Symbol,Broker.Exchange),Buy=o.Side=="매수",Quantity=(int)o.Quantity,Filled=(int)o.Filled,Price=o.Price>0?o.Price:request.Price,State=OrderState.Submitted,Created=DateTimeOffset.UtcNow});
            var instrument=new Instrument(request.Symbol,request.Market);var context=new MarketContext(instrument,settings);context.Add(new(instrument,EventKind.Trade,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"MANUAL_REFERENCE",0){TradePrice=request.Price,TradeSize=0});
            var baselineKey=scope+"-baseline-"+b.Currency+"-"+KoreaClock.Today;var baseline=store.State<decimal?>(baselineKey);if(baseline==null){baseline=p.Equity;store.State(baselineKey,baseline.Value);}
            var count=guard.Entries.Count(e=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(e.Time,"Korea Standard Time").DateTime)==KoreaClock.Today);
            var candidate=new ManagedOrder{Instrument=instrument,Buy=request.Buy,Quantity=request.Quantity,Price=request.Price,Kind=request.Kind,Created=DateTimeOffset.UtcNow};
            var reason=new RiskManager(settings).Check(candidate,p,context,baseline.Value,count);if(reason!=null){store.Audit(run,"리스크 거절",new{instrument.Key,reason},DateTimeOffset.UtcNow);throw new InvalidOperationException(reason);}
            store.Audit(run,automatic==null?"수동 주문 의도":"전략 주문 의도",candidate,DateTimeOffset.UtcNow);
            try{var receipt=await guard.SubmitAsync(Broker,request,automatic?.ClientId,()=>!revoked&&!Stopped&&automatic?.CanTransmit()!=false);store.Audit(run,Broker.IsLive?"실계좌 접수":"모의 접수",new{receipt.Number,request.Symbol,request.Market},DateTimeOffset.UtcNow);return receipt;}
            catch{try{store.Audit(run,"주문 확인 필요",guard.Entries.ToArray(),DateTimeOffset.UtcNow);}catch{}throw;}
        }finally{gate.Release();}
    }
    public async Task Cancel(string number,string symbol)
    {
        if(!await gate.WaitAsync(0))throw new InvalidOperationException("주문 처리 중");try
        {
            var cancels=store.State<Dictionary<string,string>>(scope+"-cancel")??[];if(cancels.ContainsKey(number))throw new InvalidOperationException("이미 취소 요청한 주문입니다. 확인 전 재요청하지 않습니다.");cancels[number]="전송 중";store.State(scope+"-cancel",cancels);
            try{var receipt=await Broker.CancelAsync(number,symbol);cancels[number]="취소 요청";store.State(scope+"-cancel",cancels);store.Audit(run,"취소 접수 (완료 아님)",new{OriginalNumber=number,ReceiptNumber=receipt.Number,symbol},DateTimeOffset.UtcNow);}
            catch(BrokerRejectedException){cancels[number]="거부";store.State(scope+"-cancel",cancels);throw;}
            catch{cancels[number]="응답 불명";store.State(scope+"-cancel",cancels);throw new UnknownOrderException();}
        }finally{gate.Release();}
    }
    public async Task EmergencyStop()
    {
        Stopped=true;store.State(scope+"-stopped",true);await Refresh(KoreaClock.Today);
        var failures=0;foreach(var o in Orders.Where(o=>o.Remaining>0&&!o.Cancelled&&!o.Rejected)){try{await Cancel(o.Number,o.Symbol);}catch{failures++;}}
        if(failures>0)throw new InvalidOperationException($"신규 주문 차단됨. {failures}개 취소 확인 필요. HTS와 내역을 확인하세요.");
    }
    public void AcknowledgeReview()
    {if(AutomationClaimed)throw new InvalidOperationException("다중매매 전체 중지를 먼저 완료하세요.");guard.AcknowledgeExternalReview();var cancels=store.State<Dictionary<string,string>>(scope+"-cancel")??[];foreach(var k in cancels.Keys.ToArray())cancels[k]="외부 확인 완료";store.State(scope+"-cancel",cancels);store.State(scope+"-stopped",false);Stopped=false;store.Audit(run,"외부 확인 및 수동 차단 해제",new{Confirmed=true},DateTimeOffset.UtcNow);}
}
