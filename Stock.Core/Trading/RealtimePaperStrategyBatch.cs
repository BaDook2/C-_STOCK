using Stock.Core.Research;
using Stock.Core.Research.Oversold;

namespace Stock.Core.Trading;

/// <summary>실제 시세를 기존 낙주 전략에 공급하되 주문은 종목별 가상 계좌에서만 체결한다.</summary>
public sealed partial class RealtimePaperStrategyBatch
{
    public const string MarkerSource="실시간 모의매매";
    public const string PublicMarkerSource="공개시세 모의매매";
    private sealed class Row
    {
        public readonly LiveBatchItem Item;
        public readonly MarketContext Context;
        public readonly OversoldLiveStrategy Strategy;
        public readonly DataQualityGate Quality;
        public readonly PaperBroker Broker;
        public readonly Dictionary<string,(string Reason,OversoldDecisionSnapshot? Snapshot)> Intents=[];
        public bool Enabled=true;
        public DateTimeOffset? SuppressedBuySignal;
        public string Status="실제 체결·호가 준비",Signal="",Error="";
        public DateTimeOffset LastEvaluation;
        public int AppliedFills;
        public CurrentMarketSnapshot? PublicSnapshot;
        public Row(LiveBatchItem item,OversoldLiveCheckpoint? restored=null)
        {
            Item=item;
            Context=new(new(item.Stock.Symbol,item.Stock.Exchange),item.Settings);
            Strategy=restored==null?new(Context.Instrument,item.Oversold!):OversoldLiveStrategy.RestorePublic(Context.Instrument,item.Oversold!,restored);
            Quality=new(item.Settings.Data,item.Settings.Execution);
            Broker=new(item.Stock.Currency,item.Budget,item.Settings.Execution);
        }
    }

    private readonly TradingStore store;
    private readonly Func<DateTimeOffset> clock;
    private readonly object sync=new();
    private readonly Dictionary<string,Row> rows=[];
    private string runId=Guid.NewGuid().ToString("N");
    private readonly Dictionary<string,TradeMarker> stagedMarkers=[];
    private LiveBatchPlan? plan;
    private string status="중지";
    private bool armed;
    private bool entryEnabled=true;
    private int orderSequence;
    private DateOnly riskDate;
    private decimal openingEquity;
    private void ObserveRiskDay(DateTimeOffset time)
    {var day=DailyQuoteMerger.Date(time,Exchange.KRX);if(day!=riskDate){riskDate=day;openingEquity=rows.Values.Sum(r=>r.Broker.Equity);}}

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
            runId=Guid.NewGuid().ToString("N");orderSequence=0;entryEnabled=true;RestoredPlan=null;stagedMarkers.Clear();rows.Clear();foreach(var item in selected)rows.Add(item.Stock.Key,new Row(item));
            riskDate=today;openingEquity=selected.Sum(i=>i.Budget);plan=snapshot;armed=true;status=selected.Any(i=>i.Oversold!.PublicPaper)?"공개시세 모의매매 실행 중 · 완성봉과 새 관측 대기":"실시간 모의매매 실행 중 · 실제 체결을 기다립니다.";Save();
        }
    }

    public void FeedFault(string reason)=>RequestStop(null,"피드 중단: "+reason);
    /// <summary>Blocks new and scale-in buys while keeping protective sell evaluation active.</summary>
    public void SetEntryEnabled(bool enabled)
    {
        lock(sync)
        {
            entryEnabled=enabled;
            foreach(var row in rows.Values)row.SuppressedBuySignal=row.Strategy.ReadySignalTime??row.Strategy.PendingSignalTime;
            status=enabled?"자동매매 활성 · 이후 새 매수 신호만 실행":"자동매매 꺼짐 · 신규/추가매수 차단, 보유 보호 유지";
            Save();
        }
    }
    public void RequestStop(string? key=null,string reason="사용자 중지 · 가상 보유분 자동청산 없음")
    {
        lock(sync)
        {
            foreach(var r in rows.Where(p=>key==null||p.Key==key).Select(p=>p.Value))
            {
                r.Enabled=false;r.Status=reason;
                foreach(var order in r.Broker.Orders.Where(o=>o.Active).ToArray())r.Broker.CancelLocally(order,clock());
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
                ObserveRiskDay(e.ExchangeTime);
                r.Broker.Process(e);ApplyFills(r);
                r.Context.Add(e);r.Context.Position=r.Broker.Positions.GetValueOrDefault(e.Key);r.Strategy.OnMarket(e);
            }
            catch(Exception ex){r.Enabled=false;r.Error=ex is InvalidOperationException?ex.Message:"유효하지 않은 실제 시세";r.Status="중지";}
            if(rows.Values.All(x=>!x.Enabled))armed=false;
            if(stagedMarkers.Count>0)Save();
        }
    }
    public void OnPublicBar(string key,StrategyBar bar)
    {
        lock(sync)if(armed&&rows.TryGetValue(key,out var r)&&r.Enabled&&r.Item.Oversold!.PublicPaper){if(bar.ClosedAt>clock())throw new InvalidOperationException("아직 마감되지 않은 미래 봉으로 신호를 확정할 수 없습니다.");r.Strategy.OnPublicBar(bar);if(r.Item.Oversold.BarSpecification.Unit==BarUnit.Day&&DailyQuoteMerger.Date(bar.ClosedAt,Exchange.KRX)==DailyQuoteMerger.Date(clock(),Exchange.KRX)&&r.Broker.Positions.TryGetValue(key,out var position))position.Last=bar.Price.Close;Save();}
    }
    /// <summary>Observation matching model: interval volume is only a participation cap, not a raw trade.</summary>
    public void OnPublicSnapshot(CurrentMarketSnapshot snapshot)
    {
        lock(sync)
        {
            if(!armed||!rows.TryGetValue(snapshot.Stock.Key,out var r)||!r.Enabled||!r.Item.Oversold!.PublicPaper)return;
            var now=clock();var previous=r.PublicSnapshot;
            if(snapshot.Quote.State!="OPEN"||(now-snapshot.ReceivedAt).Duration()>TimeSpan.FromSeconds(20)||(now-snapshot.Quote.TradeTime).Duration()>TimeSpan.FromSeconds(20))return;
            if(previous!=null&&snapshot.Quote.TradeTime<=previous.Quote.TradeTime)return;
            ObserveRiskDay(snapshot.Quote.TradeTime);
            var delta=previous!=null&&DailyQuoteMerger.Date(previous.Quote.TradeTime,Exchange.KRX)==DailyQuoteMerger.Date(snapshot.Quote.TradeTime,Exchange.KRX)?Math.Max(0,(snapshot.Volume??0)-(previous.Volume??0)):0;
            r.PublicSnapshot=snapshot;
            // This event stays inside the paper matcher. It is never saved as a real tick or
            // passed into TickCoverageTracker, historical raw tick charts, or a broker API.
            var observed=new MarketEvent(r.Context.Instrument,EventKind.Trade,snapshot.Quote.TradeTime,snapshot.ReceivedAt,"NAVER_OBSERVATION",snapshot.Quote.TradeTime.UtcTicks){TradePrice=snapshot.Quote.Price,TradeSize=delta,TimeQuality="공개 서버 관측 · 구간 누적량 기반 모의 체결"};
            var hadActive=r.Broker.Orders.Any(o=>o.Active);r.Broker.Process(observed);ApplyFills(r);r.Context.Add(observed);r.Context.Position=r.Broker.Positions.GetValueOrDefault(snapshot.Stock.Key);r.Strategy.OnPublicObservation(observed.ExchangeTime);
            if(stagedMarkers.Count>0||hadActive)Save();
        }
    }

    public Task Pulse()
    {
        lock(sync)
        {
            if(!armed||plan==null)return Task.CompletedTask;
            if(!LiveBatchSupport.RegularSession(clock())){if(rows.Values.All(r=>r.Item.Oversold is {PublicPaper:true,BarSpecification.Unit:BarUnit.Day})){status="장 외 대기 · 일봉 종가 신호/보유 상태 유지 · 다음 거래일 새 관측부터 실행";Save();}else RequestStop(null,"정규 연속매매시간 종료 · 가상 보유분 자동청산 없음");return Task.CompletedTask;}
            var totalLoss=openingEquity-rows.Values.Sum(r=>r.Broker.Equity);
            foreach(var r in rows.Values)
            {
                if(!r.Enabled)continue;
                if(r.Context.Last==null){r.Status="첫 실제 체결 대기";continue;}
                if(!r.Item.Oversold!.PublicPaper&&r.Context.Quote==null&&(clock()-r.Context.Last.ExchangeTime).TotalSeconds<30){r.Status="실제 호가 대기";continue;}
                if(!Fresh(r)){if(r.Item.Oversold is {PublicPaper:true,BarSpecification.Unit:BarUnit.Day})r.Status="새 거래일의 신선한 관측 대기 · 주문 차단";else{r.Enabled=false;r.Status="중지";r.Error="체결/호가 지연 또는 정규 연속매매시간 종료";}continue;}
                if(r.Context.Now<=r.LastEvaluation)continue;r.LastEvaluation=r.Context.Now;
                if(r.Broker.Orders.Any(o=>o.Active)){r.Status="가상 미체결 대기";continue;}
                r.Context.Position=r.Broker.Positions.GetValueOrDefault(r.Item.Stock.Key);
                var signal=r.Strategy.Evaluate(r.Context);r.Signal=signal.Reason;r.Status="실시간 조건 감시";
                if(signal.Buy is not bool buy||signal.Price is not decimal price)continue;
                if(buy&&(!entryEnabled||rowSignalIsSuppressed(r)))
                {r.Status=entryEnabled?"활성화 이전 매수 신호 · 소급 주문 안 함":"매수 신호 감지 · 자동매매 꺼짐";continue;}
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

    private static bool rowSignalIsSuppressed(Row row)
        =>row.SuppressedBuySignal is {} suppressed&&row.Strategy.ReadySignalTime is {} signal&&signal<=suppressed;

    private void ApplyFills(Row r)
    {
        while(r.AppliedFills<r.Broker.Fills.Count)
        {
            var index=r.AppliedFills++;var fill=r.Broker.Fills[index];if(!r.Intents.TryGetValue(fill.OrderId,out var intent))throw new InvalidOperationException("가상 주문 의도를 찾을 수 없습니다.");
            var execution=new BrokerExecution(runId+":"+r.Item.Stock.Key+":"+index,fill.OrderId,r.Item.Stock.Symbol,fill.Buy,fill.Quantity,fill.Price,fill.Time);
            r.Strategy.OnExecution(execution,intent.Reason,intent.Snapshot);
            var markerId="trade-marker:"+TradingStore.Hash("paper-live:"+execution.Id);
            var marker=new TradeMarker(markerId,r.Item.Stock.Key,fill.Time,fill.Buy,fill.Quantity,fill.Price,r.Item.Oversold!.PublicPaper?PublicMarkerSource:MarkerSource,"paper-live:"+runId,Reason:intent.Reason,RealizedPnl:fill.Buy?null:fill.RealizedPnl);
            stagedMarkers[markerId]=marker;r.Status=(fill.Buy?"모의 매수 ":"모의 매도 ")+fill.Quantity.ToString("N0")+"주 체결";
        }
    }

    private bool Fresh(Row r)
    {
        var now=clock();var c=r.Context;var seconds=Math.Min(10,r.Item.Settings.Execution.StaleSeconds);
        if(r.Item.Oversold!.PublicPaper)return LiveBatchSupport.RegularSession(now)&&r.PublicSnapshot is {} s&&(now-s.ReceivedAt).Duration().TotalSeconds<=20&&(now-s.Quote.TradeTime).Duration().TotalSeconds<=20&&s.Quote.State=="OPEN";
        return LiveBatchSupport.RegularSession(now)&&c.Last!=null&&c.Quote!=null&&c.Quote.Bid>0&&c.Quote.Ask>=c.Quote.Bid&&(now-c.Last.ExchangeTime).Duration().TotalSeconds<=seconds&&(now-c.Quote.ExchangeTime).Duration().TotalSeconds<=seconds&&(now-c.Last.ReceivedTime).Duration().TotalSeconds<=seconds&&(now-c.Quote.ReceivedTime).Duration().TotalSeconds<=seconds;
    }
    private static decimal CostBuffer(Row r)=>r.Item.Settings.Execution.FeePercent+(r.Item.Settings.Execution.Kind==OrderKind.Market?Math.Max(2,r.Item.Settings.Execution.SlippagePercent):0);
    private void Save()
    {
        if(plan==null)return;
        var orders=rows.Values.SelectMany(r=>r.Broker.Orders.Select(o=>new LiveBatchOrder{ClientId=o.Id,Number=o.Id,Symbol=o.Symbol,Buy=o.Buy,Quantity=o.Quantity,Price=o.Price,Created=o.Created,Pending=o.Active,Status=o.State.ToString(),Reason=o.Reason,OversoldSnapshot=r.Intents.GetValueOrDefault(o.Id).Snapshot})).ToList();
        var executions=rows.Values.SelectMany(r=>r.Broker.Fills.Select((f,i)=>new BrokerExecution(runId+":"+r.Item.Stock.Key+":"+i,f.OrderId,r.Item.Stock.Symbol,f.Buy,f.Quantity,f.Price,f.Time))).ToList();
        var journal=new LiveBatchJournal{RunId=runId,Started=clock(),AccountScope="paper-live",Plan=plan,Orders=orders,Executions=executions,Status=status};
        var states=new List<(string Key,object Value)>{("paper-live-batch:"+runId,journal),("paper-live-batch-latest",runId)};
        if(plan.Items.All(i=>i.Oversold?.PublicPaper==true)){var payload=Serialization.Pack(CaptureCheckpoint());states.Add(("paper-live-checkpoint:"+runId,new PaperCheckpointEnvelope(payload,TradingStore.Hash(payload))));}
        states.AddRange(stagedMarkers.Values.Select(m=>(m.Id,(object)m)));store.StatesAtomic(states.ToArray());
        var notifications=stagedMarkers.Values.ToArray();stagedMarkers.Clear();foreach(var marker in notifications)FillRecorded?.Invoke(marker);
    }
}
