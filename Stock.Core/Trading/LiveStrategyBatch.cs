namespace Stock.Core.Trading;

/// <summary>Live-only coordinator. Positions change solely from identified broker executions, never from market ticks.</summary>
public sealed class LiveStrategyBatch
{
    private sealed class Row(LiveBatchItem item,ITradingStrategy strategy)
    {
        public readonly LiveBatchItem Item=item;public readonly MarketContext Context=new(new(item.Stock.Symbol,item.Stock.Exchange),item.Settings);
        public readonly ITradingStrategy Strategy=strategy;public readonly DataQualityGate Quality=new(item.Settings.Data,item.Settings.Execution);
        public bool Enabled=true;public string Status="실제 체결·호가 준비",Signal="",Error="";public int Owned,Losses;public decimal Average,Profit,CycleProfit;public DateTimeOffset? Opened,LastExit;public DateTimeOffset LastEvaluation;
    }
    private readonly RemoteTradingSession session;private readonly TradingStore store;private readonly Func<DateTimeOffset> clock;private readonly Func<TradingSettings,ITradingStrategy> strategyFactory;
    private readonly object sync=new();private readonly SemaphoreSlim work=new(1,1);private readonly Dictionary<string,Row> rows=[];
    private LiveBatchJournal journal=new();private HashSet<string> initialOrders=[];private string account="",status="중지";private volatile bool armed;private DateTimeOffset lastPoll;
    private string savedJournal="",savedProfit="";private Dictionary<string,decimal> priorProfit=[];private string DailyKey=>"live-batch-daily:"+session.Scope+":"+clock().ToOffset(TimeSpan.FromHours(9)).ToString("yyyyMMdd");
    private string JournalKey=>"live-batch:"+session.Scope;
    public LiveStrategyBatch(RemoteTradingSession session,TradingStore store,Func<DateTimeOffset>? clock=null,Func<TradingSettings,ITradingStrategy>? strategyFactory=null)
    {this.session=session;this.store=store;this.clock=clock??(()=>DateTimeOffset.UtcNow);this.strategyFactory=strategyFactory??LiveBatchSupport.Strategy;}
    public LiveBatchStatus Snapshot(){lock(sync)return new(session.Broker.Name+" · "+session.Broker.MaskedAccount,armed,status,rows.Select(p=>new LiveBatchRowStatus(p.Key,p.Value.Item.Stock.Name,p.Value.Item.Preset,p.Value.Enabled&&armed,p.Value.Status,p.Value.Signal,p.Value.Owned,journal.Orders.Where(o=>o.Symbol==p.Key&&o.Pending).Sum(o=>Math.Max(0,o.Quantity-Filled(o))),p.Value.Profit,p.Value.Error)).ToArray());}
    public async Task Start(LiveBatchPlan requested)
    {
        await work.WaitAsync();bool claimed=false;
        try
        {
            if(armed||session.AutomationClaimed)throw new InvalidOperationException("기존 실행을 먼저 중지하세요.");
            if(!LiveBatchSupport.RegularSession(clock()))throw new InvalidOperationException("다중매매는 국내 정규 연속매매 구간(09:00~15:20 한국시간)에 시작할 수 있습니다.");
            if(!session.Broker.IsLive||session.Broker.Id!="kiwoom"||session.Broker.Exchange!=Exchange.KRX||session.Broker is not IExecutionHistoryBroker)throw new InvalidOperationException("실제 체결 대사가 가능한 키움 국내 계좌에서만 실행할 수 있습니다.");
            var plan=Serialization.Unpack<LiveBatchPlan>(Serialization.Pack(requested)) with{Items=requested.Items.Where(x=>x.Selected).Select(x=>Serialization.Unpack<LiveBatchItem>(Serialization.Pack(x))).ToArray()};
            if(plan.Items.Length is <1 or >20||plan.Items.Select(x=>x.Stock.Key).Distinct().Count()!=plan.Items.Length)throw new InvalidOperationException("체크한 종목은 중복 없이 1~20개여야 합니다.");
            if(plan.TotalBudget<=0||plan.TotalBudget>1e12m||plan.DailyLossLimit<=0||plan.DailyLossLimit>plan.TotalBudget)throw new InvalidOperationException("전체 투자 한도와 일 손실 한도를 확인하세요.");
            foreach(var item in plan.Items){item.Settings.Validate();var reason=LiveBatchSupport.Reason(item);if(reason!=null)throw new InvalidOperationException(item.Stock.Name+": "+reason);if(string.IsNullOrWhiteSpace(item.Preset)||item.Budget<=0||item.Budget>plan.TotalBudget||item.Settings.Advanced!.Common.Investment>item.Budget)throw new InvalidOperationException(item.Stock.Name+": 등록 전략/종목 예산/1회 투자금을 확인하세요.");}
            foreach(var item in plan.Items.Where(i=>i.Oversold!=null))
            {
                var facts=item.Oversold!.Facts;var now=clock();var today=DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(9)).DateTime);
                if(!facts.Sessions.Any(s=>s.Date==today&&now>=s.Open&&now<s.Close)||!Stock.Core.Research.Oversold.HistoricalFacts.Tradable(facts.Status(item.Stock.Symbol,today,now))||facts.Risk(item.Stock.Symbol,today,now)!=false||item.Oversold.Warmup.Any(b=>b.ClosedAt>now))throw new InvalidOperationException(item.Stock.Name+": 현재 시장 세션·종목 상태·검증된 과거 봉이 부족합니다.");
            }
            if(plan.Items.Sum(i=>i.Budget)>plan.TotalBudget)throw new InvalidOperationException("선택 종목 예산 합계가 전체 한도를 초과합니다.");
            var previous=store.State<LiveBatchJournal>(JournalKey);if(previous?.Orders.Any(o=>o.Pending)==true)throw new InvalidOperationException("이전 실행의 미체결/응답 불명을 먼저 대사하세요. 자동 재시작하지 않습니다.");
            var ledger=store.State<Dictionary<string,Dictionary<string,decimal>>>(DailyKey)??[];priorProfit=ledger.Values.SelectMany(x=>x).GroupBy(x=>x.Key).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Value));
            journal=new(){Started=clock(),AccountScope=session.Scope,Plan=plan};session.ClaimAutomation(journal.RunId);claimed=true;account=session.Broker.Account;
            await session.Refresh(DateOnly.FromDateTime(clock().ToOffset(TimeSpan.FromHours(9)).DateTime));var balance=session.Balance!;
            if(session.Unknown||session.Stopped||balance.OrderableCash==null)throw new InvalidOperationException("미확인 주문/긴급중지 또는 실제 현금 주문가능금액을 확인할 수 없습니다.");
            if(balance.Currency!="KRW"||plan.Items.Sum(x=>x.Budget)>Math.Min(balance.Cash,balance.OrderableCash.Value))throw new InvalidOperationException("체크 종목의 예산 합계가 현금 주문가능금액을 초과합니다.");
            foreach(var item in plan.Items)if(balance.Holdings.Any(h=>h.Symbol==item.Stock.Symbol&&h.Quantity!=0)||session.Orders.Any(o=>o.Symbol==item.Stock.Symbol&&o.Remaining>0&&!o.Cancelled&&!o.Rejected))throw new InvalidOperationException(item.Stock.Name+": 기존 보유 또는 미체결이 있어 자동매매가 소유권을 가져올 수 없습니다.");
            lock(sync){rows.Clear();foreach(var item in plan.Items)rows.Add(item.Stock.Key,new(item,item.Oversold is {} oversold?new Stock.Core.Research.Oversold.OversoldLiveStrategy(new(item.Stock.Symbol,item.Stock.Exchange),oversold):strategyFactory(item.Settings)));initialOrders=session.Orders.Select(o=>o.Number).ToHashSet();status="실행 중 · 체크한 종목의 새 체결을 기다립니다.";journal.Status=status;lastPoll=clock();}
            Save();armed=true;
        }
        catch{if(claimed)session.ReleaseAutomation(journal.RunId);throw;}finally{work.Release();}
    }
    public void FeedFault(string reason)=>RequestStop(null,"피드 중단: "+reason);
    public void RequestStop(string? key=null,string reason="사용자 중지 · 보유분 자동청산 없음")
    {lock(sync){foreach(var r in rows.Where(p=>key==null||p.Key==key).Select(p=>p.Value)){r.Enabled=false;r.Status=reason;}if(key==null||rows.Values.All(r=>!r.Enabled))armed=false;status=reason;journal.Status=reason;}}
    public void OnMarket(MarketEvent e)
    {
        lock(sync)
        {
            if(!armed||!rows.TryGetValue(e.Key,out var r)||!r.Enabled)return;
            try{var now=clock();if(e.Source!="KIWOOM"||(now-e.ExchangeTime).Duration().TotalSeconds>FreshSeconds(r)||(now-e.ReceivedTime).Duration().TotalSeconds>FreshSeconds(r)){r.Enabled=false;r.Error="지연/출처 불명 시세: 다시 확인 후 시작하세요.";r.Status="중지";return;}if(r.Quality.Accept(e,true,out _)){r.Context.Add(e);if(r.Strategy is Stock.Core.Research.Oversold.OversoldLiveStrategy oversold)oversold.OnMarket(e);}}
            catch(Exception){r.Enabled=false;r.Error="유효하지 않은 실제 시세";r.Status="중지";}
        }
    }
    public async Task Pulse()
    {
        if(!await work.WaitAsync(0))return;
        try
        {
            if(journal.Plan==null)return;
            if(account!=session.Broker.Account||clock().ToOffset(TimeSpan.FromHours(9)).Date!=journal.Started.ToOffset(TimeSpan.FromHours(9)).Date)RequestStop(null,"계좌/거래일 변경: 재시작 확인 필요");
            if((clock()-lastPoll).TotalSeconds>=3||!armed){await Reconcile();lastPoll=clock();}
            foreach(var pair in rows.ToArray())
            {
                var key=pair.Key;var r=pair.Value;
                foreach(var pending in journal.Orders.Where(o=>o.Symbol==key&&o.Pending&&!o.CancelSent&&o.Number!="").ToArray())
                    if(!armed||!r.Enabled||(clock()-pending.Created).TotalMilliseconds>=r.Item.Settings.Advanced!.Execution.OrderTimeoutMilliseconds||r.Item.Settings.Execution.CancelRemainder&&Filled(pending)>0)await Cancel(pending);
                if(!armed||!r.Enabled)continue;
                StrategySignal signal;OrderRequest? request=null;string? why;
                lock(sync)
                {
                    if(r.Context.Last==null){r.Status="첫 실제 체결 대기";continue;}
                    if(r.Context.Quote==null&&(clock()-journal.Started).TotalSeconds<30){r.Status="실제 호가 대기";continue;}
                    if(!Fresh(r)){r.Enabled=false;r.Status="중지";r.Error="체결/호가 지연 또는 정규 연속매매시간 종료";continue;}
                    if(r.Context.Now<=r.LastEvaluation)continue;r.LastEvaluation=r.Context.Now;
                    if(journal.Orders.Any(o=>o.Symbol==key&&o.Pending)){r.Status="미체결/대사 확인 대기";continue;}
                    var c=r.Context;var a=r.Item.Settings.Advanced!;c.Position=r.Owned>0?new(c.Instrument){Quantity=r.Owned,AveragePrice=r.Average,Last=c.Price,Owner=r.Strategy.Name,OpenedAt=r.Opened}:null;
                    if(r.Strategy is Stock.Core.Research.Oversold.OversoldLiveStrategy)signal=r.Strategy.Evaluate(c);
                    else
                    {
                        var risk=new RiskManager(r.Item.Settings);var stop=r.Owned>0?risk.Distance(c,r.Average,r.Item.Settings.Risk.StopMode,r.Item.Settings.Risk.StopValue):0;var take=r.Owned>0?risk.Distance(c,r.Average,r.Item.Settings.Risk.TakeMode,r.Item.Settings.Risk.TakeValue):0;
                        if(r.Owned>0&&(stop>0&&c.Price<=r.Average-stop||take>0&&c.Price>=r.Average+take||r.Opened is {} opened&&(clock()-opened).TotalSeconds>=a.Common.MaxHoldingSeconds))signal=new(r.Strategy.Name,false,c.Price,"손절/익절/최대 보유시간");
                        else{why=new CommonScanner(r.Item.Settings).Reject(c);if(why!=null&&r.Owned==0){r.Signal=why;continue;}signal=r.Strategy.Evaluate(c);}
                    }
                    r.Signal=signal.Reason;r.Status="신호 감시";
                    if(signal.Buy is not bool buy||signal.Price is not decimal price)continue;
                    if(buy&&r.Owned>0&&r.Item.Oversold==null||!buy&&r.Owned<=0)continue;
                    if(buy&&EntryBlocked(r,out why)){r.Status=why!;continue;}
                    if(a.Execution.OrderType==OrderKind.Limit)price+=(buy?1:-1)*a.Execution.LimitOrderOffsetTicks*c.Instrument.Tick(price);
                    price=decimal.Floor(price/c.Instrument.Tick(price))*c.Instrument.Tick(price);var maxMoney=Math.Min(a.Common.Investment,Math.Min(r.Item.Budget,r.Item.Settings.Risk.MaxOrderCash));
                    int qty=buy?(int)Math.Min(r.Item.Settings.Risk.MaxShares-r.Owned,decimal.Floor(maxMoney/(price*(1+CostBuffer(r)/100)))):r.Owned;
                    if(r.Strategy is Stock.Core.Research.Oversold.OversoldLiveStrategy oversold)qty=Math.Min(qty,oversold.OrderQuantity(price));
                    if(qty<=0){r.Status="1주 주문 금액이 예산을 초과합니다.";continue;}request=new(r.Item.Stock.Symbol,qty,price,buy,Exchange.KRX,a.Execution.OrderType);
                }
                await Submit(key,r,request,signal.Reason);
            }
            lock(sync){if(rows.Values.All(r=>!r.Enabled))armed=false;}Save();
        }
        catch(Exception ex){RequestStop(null,"계좌/체결 조회 실패 · 신규 주문 차단");lock(sync)foreach(var r in rows.Values)r.Error=ex is InvalidOperationException?ex.Message:"연결 또는 응답을 확인할 수 없습니다. 증권사 내역을 확인하세요.";try{Save();}catch{/* Keep existing durable guard; never continue after persistence failure. */}}
        finally{work.Release();}
    }
    private bool EntryBlocked(Row r,out string? why)
    {
        why=null;var now=clock();var a=r.Item.Settings.Advanced!;var t=TimeOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(9)).DateTime);var start=TimeOnly.ParseExact(a.Common.TradingStart,"HH:mm");var end=TimeOnly.ParseExact(a.Common.TradingEnd,"HH:mm");
        if(!(start<=end?t>=start&&t<=end:t>=start||t<=end))why="전략 매매 가능시간 밖";
        else if(r.LastExit is {} last&&(now-last).TotalSeconds<a.Risk.ReentryCooldown)why="재진입 대기";
        else if(r.Losses>=a.Risk.MaxConsecutiveLosses)why="연속 손실 한도";
        else if(-(priorProfit.GetValueOrDefault(r.Item.Stock.Key)+RiskProfit(r)+r.Owned*(r.Context.Price-r.Average))>=a.Risk.DailyLossLimit||-(priorProfit.Values.Sum()+rows.Values.Sum(x=>RiskProfit(x)+x.Owned*(x.Context.Price-x.Average)))>=journal.Plan!.DailyLossLimit)why="실현·평가 손실 한도";
        return why!=null;
    }
    private async Task Submit(string key,Row r,OrderRequest request,string reason)
    {
        var intent=new LiveBatchOrder{ClientId="AUTO-"+journal.RunId+"-"+Guid.NewGuid().ToString("N"),Symbol=key,Buy=request.Buy,Quantity=request.Quantity,Price=request.Price,Created=clock(),Reason=reason,OversoldSnapshot=(r.Strategy as Stock.Core.Research.Oversold.OversoldLiveStrategy)?.Snapshot};
        lock(sync)journal.Orders.Add(intent);Save();
        try
        {
            var receipt=await session.Submit(request,new(journal.RunId,intent.ClientId,(balance,orders)=>Preflight(r,intent,request,balance,orders),()=>{lock(sync)return armed&&r.Enabled&&!session.Stopped&&account==session.Broker.Account&&Fresh(r);}));
            lock(sync){intent.Number=receipt.Number;intent.Status="접수 · 체결 확인 대기";r.Status=intent.Status;}Save();
        }
        catch(UnknownOrderException){lock(sync){intent.Status="응답 불명 · 재전송 금지";r.Error=intent.Status;}RequestStop(null,intent.Status);Save();}
        catch(Exception ex)
        {
            var durable=session.LocalOrders.FirstOrDefault(o=>o.Id==intent.ClientId);
            bool uncertain=intent.Number!=""||durable?.Status is "접수" or "전송 중" or "응답 불명";
            lock(sync){if(intent.Number==""&&durable?.Number is {Length:>0} number)intent.Number=number;intent.Pending=uncertain;intent.Status=uncertain?"접수/저장 확인 필요 · 재전송 금지":ex is BrokerRejectedException?"증권사 거부":"전송 전 차단";r.Enabled=false;r.Status=intent.Status;r.Error=ex.Message;}
            if(uncertain)RequestStop(null,intent.Status);Save();
        }
    }
    private string? Preflight(Row r,LiveBatchOrder intent,OrderRequest request,Balance balance,IReadOnlyList<BrokerOrder> orders)
    {
        lock(sync)
        {
            if(!armed||!r.Enabled||session.Stopped||session.Unknown||!Fresh(r)||account!=session.Broker.Account)return "중지/계좌 변경/시세 지연";
            if(Math.Abs(r.Context.Price/request.Price-1)>.02m)return "전송 대기 중 가격이 2% 이상 변경됨";
            if(balance.OrderableCash is not decimal available||balance.Currency!="KRW")return "실제 현금 주문가능금액 없음";
            if(orders.Any(o=>o.Symbol==request.Symbol&&!initialOrders.Contains(o.Number)&&!journal.Orders.Any(p=>p.Number==o.Number||p.Number!=""&&p.Number==o.OriginalNumber)))return "수동/외부 주문과 충돌: 자동매매 중지";
            if(balance.Holdings.Where(h=>h.Symbol==request.Symbol).Sum(h=>h.Quantity)!=r.Owned)return "브로커 보유와 이 전략의 실제 체결 수량 불일치: 대사 필요";
            if(!request.Buy)return request.Quantity<=r.Owned?null:"전략 소유 수량 밖 매도 차단";
            if(EntryBlocked(r,out var reason))return reason;
            var a=r.Item.Settings.Advanced!;var risk=r.Item.Settings.Risk;
            var distance=new RiskManager(r.Item.Settings).Distance(r.Context,request.Price,risk.StopMode,risk.StopValue);
            var fee=request.Price*request.Quantity*r.Item.Settings.Execution.FeePercent/100;
            if(distance<=0||distance*request.Quantity+fee>risk.MaxTradeLoss||distance/request.Price*100>risk.MaxTradeLossPercent)return "종목 전략의 건별 손실 한도";
            if(request.Quantity+r.Owned>risk.MaxShares)return "종목 전략의 보유 수량 한도";
            decimal cost=request.Price*request.Quantity*(1+CostBuffer(r)/100);
            decimal Reserved(LiveBatchOrder o)=>o.Buy&&o.Pending?Math.Max(0,o.Quantity-Filled(o))*o.Price*(1+CostBuffer(rows[o.Symbol])/100):0;
            var localReserved=journal.Orders.Where(o=>o!=intent).Sum(Reserved);
            var externalReserved=orders.Where(o=>o.Side=="매수"&&o.Remaining>0&&!o.Cancelled&&!o.Rejected&&!journal.Orders.Any(p=>p.Number==o.Number)).Sum(o=>o.Remaining*(o.Price>0?o.Price:decimal.MaxValue/10000000));
            if(cost>Math.Min(balance.Cash,available)-localReserved-externalReserved)return "미체결 예약 반영 후 현금 부족";
            var exposure=rows.Values.Sum(x=>x.Owned*Math.Max(x.Average,x.Context.Price));
            if(cost+r.Owned*Math.Max(r.Average,r.Context.Price)>r.Item.Budget||cost+r.Owned*request.Price>Math.Min(r.Item.Settings.Risk.MaxSymbolCash,balance.Total*a.Risk.MaxPositionPercent/100))return "종목 예산/비중 초과";
            if(cost+exposure+localReserved>journal.Plan!.TotalBudget)return "전체 자동매매 예산 초과";
            var open=rows.Values.Count(x=>x.Owned>0||journal.Orders.Any(o=>o!=intent&&o.Symbol==x.Item.Stock.Key&&o.Buy&&o.Pending));if(open>=a.Risk.MaxOpenPositions)return "등록 전략의 동시 보유 한도";
            if(journal.Orders.Count(o=>o!=intent&&o.Status is not("증권사 거부" or "전송 전 차단"))>=r.Item.Settings.Risk.MaxDailyTrades)return "당일 자동 주문 한도";
            return null;
        }
    }
    private async Task Reconcile()
    {
        await session.Refresh(DateOnly.FromDateTime(journal.Started.ToOffset(TimeSpan.FromHours(9)).DateTime));
        var executions=await ((IExecutionHistoryBroker)session.Broker).GetExecutionsAsync(DateOnly.FromDateTime(journal.Started.ToOffset(TimeSpan.FromHours(9)).DateTime));
        lock(sync)
        {
            foreach(var e in executions.OrderBy(x=>x.Time).ThenBy(x=>x.Id,StringComparer.Ordinal))
            {
                var order=journal.Orders.SingleOrDefault(o=>o.Number!=""&&o.Number==e.OrderNumber);if(order==null)continue;
                var existing=journal.Executions.FirstOrDefault(x=>x.Id==e.Id);if(existing!=null){if(existing!=e)throw new InvalidOperationException("체결번호 내용 변경: 확인 필요");continue;}
                var r=rows[order.Symbol];if(e.Symbol!=r.Item.Stock.Symbol||e.Buy!=order.Buy||e.Quantity<=0||e.Price<=0||Filled(order)+e.Quantity>order.Quantity)throw new InvalidOperationException("실제 체결과 원주문 불일치");
                if(r.Strategy is Stock.Core.Research.Oversold.OversoldLiveStrategy oversold)oversold.OnExecution(e,order.Reason,order.OversoldSnapshot);
                if(e.Buy){if(r.Owned==0){r.Opened=e.Time;r.CycleProfit=0;}r.Average=(r.Average*r.Owned+e.Price*e.Quantity)/(r.Owned+e.Quantity);r.Owned+=e.Quantity;}
                else{if(e.Quantity>r.Owned)throw new InvalidOperationException("전략 소유분 밖 체결 감지");var pnl=(e.Price-r.Average)*e.Quantity; r.Profit+=pnl;r.CycleProfit+=pnl;r.Owned-=e.Quantity;if(r.Owned==0){r.LastExit=e.Time;r.Losses=r.CycleProfit<0?r.Losses+1:0;r.Opened=null;}}
                journal.Executions.Add(e);
                var markerId="trade-marker:"+TradingStore.Hash(session.Scope+":"+e.Id);
                store.State(markerId,new Stock.Core.Research.TradeMarker(markerId,order.Symbol,e.Time,e.Buy,e.Quantity,e.Price,"실제매매",session.Broker.Id+":"+TradingStore.Hash(account)));
            }
            foreach(var o in journal.Orders.Where(o=>o.Pending&&o.Number!=""))
            {
                var remote=session.Orders.FirstOrDefault(x=>x.Number==o.Number);if(remote==null){if((clock()-o.Created).TotalSeconds>30){rows[o.Symbol].Enabled=false;rows[o.Symbol].Error="접수 주문이 조회되지 않아 대사 필요";}continue;}
                var filled=Filled(o);if(remote.Filled<filled||remote.Filled>filled){rows[o.Symbol].Status="체결 내역 대사 대기";continue;}
                bool cancelled=remote.Cancelled||session.Orders.Any(x=>x.OriginalNumber==o.Number&&x.Cancelled);
                if(remote.Rejected||cancelled||filled==o.Quantity){o.Pending=false;o.Status=remote.Rejected?"거부":cancelled?"취소 확인":"체결 완료";}else o.Status=filled>0?"부분 체결":"접수 / 미체결";
                rows[o.Symbol].Status=o.Status;
            }
        }
        Save();
    }
    private async Task Cancel(LiveBatchOrder order)
    {
        lock(sync){order.CancelSent=true;order.Status="취소 요청 중 · 완료 아님";}Save();
        try{await session.Cancel(order.Number,rows[order.Symbol].Item.Stock.Symbol);lock(sync)order.Status="취소 접수 · 대사 대기";}
        catch(Exception ex){lock(sync){order.Status="취소 확인 필요";rows[order.Symbol].Error=ex.Message;}RequestStop(null,"취소 확인 필요 · 증권사 내역 확인");}Save();
    }
    public async Task Stop(string? key=null)
    {
        RequestStop(key);await work.WaitAsync();
        try{foreach(var o in journal.Orders.Where(o=>o.Pending&&!o.CancelSent&&o.Number!=""&&(key==null||o.Symbol==key)).ToArray())await Cancel(o);if(journal.Plan!=null)await Reconcile();}
        catch(Exception ex){lock(sync){status="중지됨 · 취소/체결 확인 필요";foreach(var r in rows.Values)r.Error=ex.Message;}}
        finally{if(!armed)session.ReleaseAutomation(journal.RunId);try{Save();}finally{work.Release();}}
    }
    private int Filled(LiveBatchOrder order)=>journal.Executions.Where(e=>e.OrderNumber==order.Number).Sum(e=>e.Quantity);
    /// <summary>Explicit external review only; never adopts old holdings or retries uncertain orders.</summary>
    public async Task ConfirmPreviousClosed()
    {
        if(armed||session.AutomationClaimed)throw new InvalidOperationException("전체 중지를 먼저 완료하세요.");
        var previous=store.State<LiveBatchJournal>(JournalKey);if(previous==null)return;
        await session.Refresh(KoreaClock.Today);var symbols=previous.Plan?.Items.Select(i=>i.Stock.Symbol).ToHashSet()??[];
        if(session.Balance!.Holdings.Any(h=>symbols.Contains(h.Symbol)&&h.Quantity!=0)||session.Orders.Any(o=>symbols.Contains(o.Symbol)&&o.Remaining>0&&!o.Cancelled&&!o.Rejected))throw new InvalidOperationException("이전 실행 종목의 실제 보유/미체결이 남아 있습니다. 증권사에서 처리한 뒤 다시 확인하세요.");
        if(session.Unknown||session.Stopped)throw new InvalidOperationException("계좌·주문의 미확인 주문 외부 확인을 먼저 수행하세요.");
        foreach(var order in previous.Orders.Where(o=>o.Pending)){order.Pending=false;order.Status="사용자 외부 확인 및 보유/미체결 없음 확인";}
        previous.Status="외부 확인 후 종료 · 재시작은 별도 사용자 실행 필요";store.State(JournalKey,previous);
    }
    private double FreshSeconds(Row r)=>Math.Min(10,r.Item.Settings.Execution.StaleSeconds);
    private bool Fresh(Row r){var now=clock();var c=r.Context;return LiveBatchSupport.RegularSession(now)&&c.Last!=null&&c.Quote!=null&&c.Quote.Bid>0&&c.Quote.Ask>=c.Quote.Bid&&(now-c.Last.ExchangeTime).Duration().TotalSeconds<=FreshSeconds(r)&&(now-c.Quote.ExchangeTime).Duration().TotalSeconds<=FreshSeconds(r)&&(now-c.Last.ReceivedTime).Duration().TotalSeconds<=FreshSeconds(r)&&(now-c.Quote.ReceivedTime).Duration().TotalSeconds<=FreshSeconds(r);}
    private static decimal CostBuffer(Row r)=>r.Item.Settings.Execution.FeePercent+(r.Item.Settings.Execution.Kind==OrderKind.Market?Math.Max(2,r.Item.Settings.Execution.SlippagePercent):0);
    private decimal RiskProfit(Row row)=>row.Profit-journal.Executions.Where(e=>e.Symbol==row.Item.Stock.Symbol).Sum(e=>e.Quantity*e.Price*(row.Item.Settings.Execution.FeePercent+(e.Buy?0:row.Item.Settings.Execution.TaxPercent))/100);
    private void Save()
    {
        LiveBatchJournal snapshot;Dictionary<string,decimal> pnl;
        lock(sync){snapshot=Serialization.Unpack<LiveBatchJournal>(Serialization.Pack(journal));pnl=rows.ToDictionary(p=>p.Key,p=>RiskProfit(p.Value));}
        // Run-key upsert keeps same-day loss budget after an explicit stop/restart.
        var journalText=Serialization.Pack(snapshot);var profitText=Serialization.Pack(pnl);if(savedJournal==journalText&&savedProfit==profitText)return;
        if(snapshot.Plan!=null){var key="live-batch-daily:"+session.Scope+":"+snapshot.Started.ToOffset(TimeSpan.FromHours(9)).ToString("yyyyMMdd");var daily=store.State<Dictionary<string,Dictionary<string,decimal>>>(key)??[];daily[snapshot.RunId]=pnl;store.State(key,daily);}
        store.State(JournalKey,snapshot);savedJournal=journalText;savedProfit=profitText;
    }
}
