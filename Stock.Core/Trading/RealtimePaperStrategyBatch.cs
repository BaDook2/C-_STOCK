using Stock.Core.Research;
using Stock.Core.Research.Oversold;

namespace Stock.Core.Trading;

/// <summary>실제 시세를 기존 낙주 전략에 공급하되 주문은 종목별 가상 계좌에서만 체결한다.</summary>
public sealed class RealtimePaperStrategyBatch
{
    public const string MarkerSource="실시간 모의매매";
    private sealed class Row
    {
        public readonly LiveBatchItem Item;
        public readonly MarketContext Context;
        public readonly OversoldLiveStrategy Strategy;
        public readonly DataQualityGate Quality;
        public readonly PaperBroker Broker;
        public readonly Dictionary<string,(string Reason,OversoldDecisionSnapshot? Snapshot)> Intents=[];
        public bool Enabled=true;
        public string Status="실제 체결·호가 준비",Signal="",Error="";
        public DateTimeOffset LastEvaluation;
        public int AppliedFills;
        public Row(LiveBatchItem item)
        {
            Item=item;
            Context=new(new(item.Stock.Symbol,item.Stock.Exchange),item.Settings);
            Strategy=new(Context.Instrument,item.Oversold!);
            Quality=new(item.Settings.Data,item.Settings.Execution);
            Broker=new(item.Stock.Currency,item.Budget,item.Settings.Execution);
        }
    }

    private readonly TradingStore store;
    private readonly Func<DateTimeOffset> clock;
    private readonly object sync=new();
    private readonly Dictionary<string,Row> rows=[];
    private readonly string runId=Guid.NewGuid().ToString("N");
    private LiveBatchPlan? plan;
    private string status="중지";
    private bool armed;
    private int orderSequence;

    public event Action<TradeMarker>? FillRecorded;
    public RealtimePaperStrategyBatch(TradingStore store,Func<DateTimeOffset>? clock=null)
    {this.store=store;this.clock=clock??(()=>DateTimeOffset.UtcNow);}

    public LiveBatchStatus Snapshot()
    {
        lock(sync)return new("실시간 모의 · 실제 주문 없음",armed,status,rows.Select(pair=>
        {
            var r=pair.Value;var position=r.Broker.Positions.GetValueOrDefault(pair.Key);
            return new LiveBatchRowStatus(pair.Key,r.Item.Stock.Name,r.Item.Preset,armed&&r.Enabled,r.Status,r.Signal,position?.Quantity??0,r.Broker.Orders.Where(o=>o.Active).Sum(o=>o.Remaining),r.Broker.Fills.Where(f=>!f.Buy).Sum(f=>f.RealizedPnl),r.Error);
        }).ToArray());
    }

    public void Start(LiveBatchPlan requested)
    {
        lock(sync)
        {
            if(armed)throw new InvalidOperationException("기존 실행을 먼저 중지하세요.");
            if(!LiveBatchSupport.RegularSession(clock()))throw new InvalidOperationException("실시간 모의매매는 국내 정규 연속매매 구간(09:00~15:20 한국시간)에 시작할 수 있습니다.");
            var selected=requested.Items.Where(i=>i.Selected).Select(i=>Serialization.Unpack<LiveBatchItem>(Serialization.Pack(i))).ToArray();
            var snapshot=new LiveBatchPlan(selected,requested.TotalBudget,requested.DailyLossLimit);
            if(selected.Length is <1 or >20||selected.Select(i=>i.Stock.Key).Distinct().Count()!=selected.Length)throw new InvalidOperationException("체크한 종목은 중복 없이 1~20개여야 합니다.");
            if(snapshot.TotalBudget<=0||snapshot.TotalBudget>1e12m||snapshot.DailyLossLimit<=0||snapshot.DailyLossLimit>snapshot.TotalBudget)throw new InvalidOperationException("전체 투자 한도와 일 손실 한도를 확인하세요.");
            if(selected.Sum(i=>i.Budget)>snapshot.TotalBudget)throw new InvalidOperationException("선택 종목 예산 합계가 전체 한도를 초과합니다.");
            var now=clock();var today=DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(9)).DateTime);
            foreach(var item in selected)
            {
                item.Settings.Validate();var reason=LiveBatchSupport.Reason(item);if(reason!=null)throw new InvalidOperationException(item.Stock.Name+": "+reason);
                if(item.Oversold==null||string.IsNullOrWhiteSpace(item.Preset)||item.Budget<=0||item.Budget>snapshot.TotalBudget||item.Settings.Advanced!.Common.Investment>item.Budget)throw new InvalidOperationException(item.Stock.Name+": 등록 전략/종목 예산/1회 투자금을 확인하세요.");
                var facts=item.Oversold.Facts;
                if(!facts.Sessions.Any(s=>s.Date==today&&now>=s.Open&&now<s.Close)||!HistoricalFacts.Tradable(facts.Status(item.Stock.Symbol,today,now))||facts.Risk(item.Stock.Symbol,today,now)!=false||item.Oversold.Warmup.Any(b=>b.ClosedAt>now))throw new InvalidOperationException(item.Stock.Name+": 현재 시장 세션·종목 상태·검증된 과거 봉이 부족합니다.");
            }
            rows.Clear();foreach(var item in selected)rows.Add(item.Stock.Key,new Row(item));
            plan=snapshot;armed=true;status="실시간 모의매매 실행 중 · 실제 체결을 기다립니다.";Save();
        }
    }

    public void FeedFault(string reason)=>RequestStop(null,"피드 중단: "+reason);
    public void RequestStop(string? key=null,string reason="사용자 중지 · 가상 보유분 자동청산 없음")
    {
        lock(sync)
        {
            foreach(var r in rows.Where(p=>key==null||p.Key==key).Select(p=>p.Value))
            {
                r.Enabled=false;r.Status=reason;
                foreach(var order in r.Broker.Orders.Where(o=>o.Active).ToArray())r.Broker.RequestCancel(order,clock());
            }
            if(key==null||rows.Values.All(r=>!r.Enabled))armed=false;
            status=reason;Save();
        }
    }
    public void Stop(string? key=null)=>RequestStop(key);

    public void OnMarket(MarketEvent e)
    {
        lock(sync)
        {
            if(!armed||!rows.TryGetValue(e.Key,out var r)||!r.Enabled)return;
            try
            {
                var now=clock();var freshness=Math.Min(10,r.Item.Settings.Execution.StaleSeconds);
                if(e.Source!="KIWOOM"||(now-e.ExchangeTime).Duration().TotalSeconds>freshness||(now-e.ReceivedTime).Duration().TotalSeconds>freshness){r.Enabled=false;r.Error="지연/출처 불명 시세: 다시 확인 후 시작하세요.";r.Status="중지";return;}
                if(!r.Quality.Accept(e,true,out _))return;
                r.Broker.Process(e);ApplyFills(r);
                r.Context.Add(e);r.Context.Position=r.Broker.Positions.GetValueOrDefault(e.Key);r.Strategy.OnMarket(e);
            }
            catch(Exception ex){r.Enabled=false;r.Error=ex is InvalidOperationException?ex.Message:"유효하지 않은 실제 시세";r.Status="중지";}
            if(rows.Values.All(x=>!x.Enabled))armed=false;
        }
    }

    public Task Pulse()
    {
        lock(sync)
        {
            if(!armed||plan==null)return Task.CompletedTask;
            if(!LiveBatchSupport.RegularSession(clock())){RequestStop(null,"정규 연속매매시간 종료 · 가상 보유분 자동청산 없음");return Task.CompletedTask;}
            var totalLoss=rows.Values.Sum(r=>r.Item.Budget-r.Broker.Equity);
            foreach(var r in rows.Values)
            {
                if(!r.Enabled)continue;
                if(r.Context.Last==null){r.Status="첫 실제 체결 대기";continue;}
                if(r.Context.Quote==null&&(clock()-r.Context.Last.ExchangeTime).TotalSeconds<30){r.Status="실제 호가 대기";continue;}
                if(!Fresh(r)){r.Enabled=false;r.Status="중지";r.Error="체결/호가 지연 또는 정규 연속매매시간 종료";continue;}
                if(r.Context.Now<=r.LastEvaluation)continue;r.LastEvaluation=r.Context.Now;
                if(r.Broker.Orders.Any(o=>o.Active)){r.Status="가상 미체결 대기";continue;}
                r.Context.Position=r.Broker.Positions.GetValueOrDefault(r.Item.Stock.Key);
                var signal=r.Strategy.Evaluate(r.Context);r.Signal=signal.Reason;r.Status="실시간 조건 감시";
                if(signal.Buy is not bool buy||signal.Price is not decimal price)continue;
                var owned=r.Context.Position?.Quantity??0;if(!buy&&owned<=0)continue;
                if(buy&&totalLoss>=plan.DailyLossLimit){r.Status="전체 일 손실 한도 도달";continue;}
                var advanced=r.Item.Settings.Advanced!;if(advanced.Execution.OrderType==OrderKind.Limit)price+=(buy?1:-1)*advanced.Execution.LimitOrderOffsetTicks*r.Context.Instrument.Tick(price);
                price=decimal.Floor(price/r.Context.Instrument.Tick(price))*r.Context.Instrument.Tick(price);
                var maxMoney=Math.Min(advanced.Common.Investment,Math.Min(r.Item.Budget,r.Item.Settings.Risk.MaxOrderCash));
                int quantity=buy?(int)Math.Min(r.Item.Settings.Risk.MaxShares-owned,decimal.Floor(Math.Min(maxMoney,r.Broker.Cash-r.Broker.ReservedCash)/(price*(1+CostBuffer(r)/100)))):owned;
                quantity=Math.Min(quantity,r.Strategy.OrderQuantity(price));
                if(quantity<=0){r.Status="1주 주문 금액이 예산을 초과하거나 매도 수량이 없습니다.";continue;}
                var id="PAPER-LIVE-"+runId+"-"+(++orderSequence).ToString("D6");
                var order=new ManagedOrder{Id=id,Instrument=r.Context.Instrument,Buy=buy,Quantity=quantity,Price=price,Kind=advanced.Execution.OrderType,Created=r.Context.Now,Strategy=r.Strategy.Name,Reason=signal.Reason,TimeoutMilliseconds=advanced.Execution.OrderTimeoutMilliseconds};
                r.Intents[id]=(signal.Reason,r.Strategy.Snapshot);r.Broker.Submit(order);r.Status="가상 주문 접수 · 다음 실제 이벤트부터 체결";
            }
            if(rows.Values.All(r=>!r.Enabled))armed=false;Save();
        }
        return Task.CompletedTask;
    }

    private void ApplyFills(Row r)
    {
        while(r.AppliedFills<r.Broker.Fills.Count)
        {
            var index=r.AppliedFills++;var fill=r.Broker.Fills[index];if(!r.Intents.TryGetValue(fill.OrderId,out var intent))throw new InvalidOperationException("가상 주문 의도를 찾을 수 없습니다.");
            var execution=new BrokerExecution(runId+":"+r.Item.Stock.Key+":"+index,fill.OrderId,r.Item.Stock.Symbol,fill.Buy,fill.Quantity,fill.Price,fill.Time);
            r.Strategy.OnExecution(execution,intent.Reason,intent.Snapshot);
            var markerId="trade-marker:"+TradingStore.Hash("paper-live:"+execution.Id);
            var marker=new TradeMarker(markerId,r.Item.Stock.Key,fill.Time,fill.Buy,fill.Quantity,fill.Price,MarkerSource,"paper-live:"+runId);
            store.State(markerId,marker);FillRecorded?.Invoke(marker);r.Status=(fill.Buy?"모의 매수 ":"모의 매도 ")+fill.Quantity.ToString("N0")+"주 체결";
        }
    }

    private bool Fresh(Row r)
    {
        var now=clock();var c=r.Context;var seconds=Math.Min(10,r.Item.Settings.Execution.StaleSeconds);
        return LiveBatchSupport.RegularSession(now)&&c.Last!=null&&c.Quote!=null&&c.Quote.Bid>0&&c.Quote.Ask>=c.Quote.Bid&&(now-c.Last.ExchangeTime).Duration().TotalSeconds<=seconds&&(now-c.Quote.ExchangeTime).Duration().TotalSeconds<=seconds&&(now-c.Last.ReceivedTime).Duration().TotalSeconds<=seconds&&(now-c.Quote.ReceivedTime).Duration().TotalSeconds<=seconds;
    }
    private static decimal CostBuffer(Row r)=>r.Item.Settings.Execution.FeePercent+(r.Item.Settings.Execution.Kind==OrderKind.Market?Math.Max(2,r.Item.Settings.Execution.SlippagePercent):0);
    private void Save()
    {
        if(plan==null)return;
        var orders=rows.Values.SelectMany(r=>r.Broker.Orders.Select(o=>new LiveBatchOrder{ClientId=o.Id,Number=o.Id,Symbol=o.Symbol,Buy=o.Buy,Quantity=o.Quantity,Price=o.Price,Created=o.Created,Pending=o.Active,Status=o.State.ToString(),Reason=o.Reason,OversoldSnapshot=r.Intents.GetValueOrDefault(o.Id).Snapshot})).ToList();
        var executions=rows.Values.SelectMany(r=>r.Broker.Fills.Select((f,i)=>new BrokerExecution(runId+":"+r.Item.Stock.Key+":"+i,f.OrderId,r.Item.Stock.Symbol,f.Buy,f.Quantity,f.Price,f.Time))).ToList();
        store.State("paper-live-batch:"+runId,new LiveBatchJournal{RunId=runId,Started=clock(),AccountScope="paper-live",Plan=plan,Orders=orders,Executions=executions,Status=status});
        store.State("paper-live-batch-latest",runId);
    }
}
