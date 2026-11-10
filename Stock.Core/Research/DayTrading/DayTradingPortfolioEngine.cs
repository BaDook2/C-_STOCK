using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record DayTradingAudit(DateTimeOffset Time,string Symbol,string Kind,string Reason,string? OrderId=null,decimal? Price=null,int? Quantity=null);
public sealed record DayTradingRowSnapshot(string Symbol,string Strategy,string State,int Quantity,decimal Average,int Entries,decimal Peak,
    bool TakeProfit1,bool TakeProfit2,decimal RealizedProfit,string Error);
public sealed record DayTradingPortfolioSnapshot(decimal Cash,decimal Reserved,decimal Equity,bool EntriesEnabled,bool EmergencyStopped,
    bool NeedsReconciliation,DayTradingRowSnapshot[] Rows,DayTradingAudit[] RecentEvents);

/// <summary>
/// Serial application-level coordinator. A host routes both market events and independent clock pulses here.
/// Positions/cash are read from the execution adapter; only confirmed fills advance strategy ownership.
/// </summary>
public sealed class DayTradingPortfolioEngine
{
    private sealed class Row(IDayTradingSignalRuntime runtime)
    {
        public readonly IDayTradingSignalRuntime Runtime=runtime;
        public int Entries,MaximumQuantity,HarvestBasis,TP1Sold,TP2Sold;
        public decimal Peak,TrailingFloor,LastEntryPrice,Realized,RoundProfit;
        public bool Owned,HarvestStarted;
        public DateTimeOffset? LastExit;
        public string ExitReason="",Status="관측 대기",Error="";
        public int ExitBarOrdinal,EntryDayOrdinal;
        public IntradayEntrySignal? FirstEntry;
        public readonly HashSet<string> EntryOrders=[];
        public readonly Dictionary<string,(int Quantity,decimal Value)> EntryOrderFills=[];
    }
    private readonly object sync=new();
    private readonly IDayTradingExecutionAdapter execution;
    private readonly AccountRiskPolicy limits;
    private readonly Func<bool> liveAuthorized;
    private readonly Dictionary<string,Row> rows=[];
    private readonly Dictionary<string,IntradayEntrySignal> intents=[];
    private readonly HashSet<string> submitted=[];
    private readonly HashSet<string> observedSignals=[];
    private readonly HashSet<string> fillIds=[];
    private readonly Dictionary<string,string> exitSnapshots=[];
    private readonly Dictionary<string,int> symbolEntries=[],strategyEntries=[];
    private readonly Queue<DayTradingAudit> recent=[];
    private readonly Action<DayTradingAudit>? record;
    private int appliedFills,accountEntries,exitSequence,consecutiveStops;
    private bool entryEnabled,stopped,needsReconciliation;
    private DateTimeOffset enabledAt;
    private DateOnly day;
    private decimal openingEquity;
    public event Action<Fill,IntradayEntrySignal?>? FillConfirmed;
    public event Action<IntradayEntrySignal>? EntrySignalObserved;
    public IReadOnlyDictionary<string,string> ExitSnapshots {get{lock(sync)return new Dictionary<string,string>(exitSnapshots);}}

    public DayTradingPortfolioEngine(IDayTradingExecutionAdapter execution,IEnumerable<IDayTradingSignalRuntime> strategies,AccountRiskPolicy limits,Action<DayTradingAudit>? record=null,Func<bool>? liveAuthorized=null)
    {
        this.execution=execution;this.limits=Serialization.Unpack<AccountRiskPolicy>(Serialization.Pack(limits));this.record=record;this.liveAuthorized=liveAuthorized??(()=>false);
        if(limits.MaxOrderCash<=0||limits.MaxConcurrentExposure<=0||limits.MaxDailyLoss<=0)throw new InvalidOperationException("공유계좌 한도를 확인하세요.");
        foreach(var strategy in strategies)
        {
            if(strategy.Instrument.Currency!=execution.Currency||!rows.TryAdd(strategy.Instrument.Key,new(strategy)))throw new InvalidOperationException("동일 종목 중복 실행 또는 공유계좌 통화 혼합입니다.");
        }
        if(rows.Count==0)throw new InvalidOperationException("실행할 종목을 선택하세요.");openingEquity=execution.Equity;
    }
    public void SetEntriesEnabled(bool enabled,DateTimeOffset now)
    {
        lock(sync)
        {
            if(stopped&&enabled)throw new InvalidOperationException("긴급중지 후 새 세션을 준비하세요.");
            if(entryEnabled==enabled)return;entryEnabled=enabled;enabledAt=now;
            Audit(now,"전체",enabled?"진입 활성":"진입 해제","기존 보유 보호 유지 · 이전 신호 소급 없음");
        }
    }
    public void EmergencyStop(DateTimeOffset now)
    {
        lock(sync)
        {
            stopped=true;entryEnabled=false;
            foreach(var order in execution.Orders.Where(o=>o.Active).ToArray())execution.Cancel(order,now);
            Audit(now,"전체","긴급중지","새 전략주문 차단 · 미체결 취소 · 보유분 임의청산 없음");
        }
    }
    public void ConfirmReconciled(bool noUnknownOrders,DateTimeOffset now)
    {
        lock(sync)
        {
            needsReconciliation=!noUnknownOrders||!execution.Synchronized;
            Audit(now,"전체","재동기화",needsReconciliation?"미확인 주문/보유 대사 필요":"실행 어댑터의 대사 완료 확인");
        }
    }
    public void OnMarket(MarketEvent market,DateTimeOffset now)
    {
        lock(sync)
        {
            if(!rows.TryGetValue(market.Key,out var row))return;
            try
            {
                var observed=row.Runtime.OnMarket(market,now);
                if(!observed.Accepted){if(observed.Decision!=FeedDecision.Duplicate){row.Error=observed.Reason;Audit(now,market.Key,"자료 차단",observed.Reason);}return;}
                row.Error="";
                var session=row.Runtime.CurrentSession;
                if(session!=null&&session.Date!=day){day=session.Date;openingEquity=execution.Equity;accountEntries=consecutiveStops=0;symbolEntries.Clear();strategyEntries.Clear();}
                execution.OnMarket(market);ApplyFills(now);
                if(openingEquity-execution.Equity>=limits.MaxDailyLoss)
                    foreach(var pending in execution.Orders.Where(o=>o.Active&&o.Buy).ToArray())execution.Cancel(pending,now);
                ManageRow(row,now,market.Kind==EventKind.Trade,observed.CanEnter);
            }
            catch(Exception ex) when(ex is not OutOfMemoryException){row.Error=ex.Message;row.Status="오류 · 신규진입 차단";Audit(now,market.Key,"오류",ex.GetType().Name+": "+ex.Message);}
        }
    }
    /// <summary>Must run even when no trade arrives; queued exits still require a subsequent executable event.</summary>
    public void OnTime(DateTimeOffset now)
    {
        lock(sync)
        {
            ApplyFills(now);
            foreach(var row in rows.Values)
            {
                try{ManageRow(row,now,false,false);}catch(Exception ex) when(ex is not OutOfMemoryException){row.Error=ex.Message;Audit(now,row.Runtime.Instrument.Key,"시계 오류",ex.GetType().Name+": "+ex.Message);}
            }
        }
    }
    public void ProcessAssumedBarOpening(string symbol,Stock.Core.Research.Oversold.StrategyBar bar,int assumedOpeningShares)
    {
        lock(sync)
        {
            if(execution.IsLive||execution is not DayTradingPaperAdapter paper||!rows.TryGetValue(symbol,out var row)||row.Runtime is not IClosedBarSignalRuntime {ResearchOnly:true} runtime)
                throw new InvalidOperationException("확정봉 근사 입력은 명시적인 가상 연구 세션에서만 사용할 수 있습니다.");
            var session=runtime.ValidateClosedBar(bar);
            runtime.PrepareBarOpening(bar);
            if(session.Date!=day){day=session.Date;openingEquity=execution.Equity;accountEntries=consecutiveStops=0;symbolEntries.Clear();strategyEntries.Clear();}
            paper.ProcessAssumedOpening(runtime.Instrument,bar,assumedOpeningShares);ApplyFills(bar.OpenTime);
        }
    }
    public void OnClosedBar(string symbol,Stock.Core.Research.Oversold.StrategyBar bar,int assumedOpeningShares,bool processOpening=true)
    {
        lock(sync)
        {
            if(execution.IsLive||execution is not DayTradingPaperAdapter paper||!rows.TryGetValue(symbol,out var row)||row.Runtime is not IClosedBarSignalRuntime {ResearchOnly:true} runtime)
                throw new InvalidOperationException("봉 근사 연구는 가상 계좌에서만 사용할 수 있습니다.");
            if(processOpening)ProcessAssumedBarOpening(symbol,bar,assumedOpeningShares);
            var observed=runtime.OnClosedBar(bar);if(!observed.Accepted)return;
            paper.MarkClosed(runtime.Instrument,bar.Price.Close);ManageRow(row,bar.ClosedAt,true,observed.CanEnter);
        }
    }
    public DayTradingPortfolioSnapshot Snapshot()
    {
        lock(sync)return new(execution.Cash,execution.ReservedCash,execution.Equity,entryEnabled,stopped,needsReconciliation,
            rows.Select(pair=>{var row=pair.Value;var p=execution.Positions.GetValueOrDefault(pair.Key);return new DayTradingRowSnapshot(pair.Key,row.Runtime.Profile.InstanceId,row.Status,p?.Quantity??0,p?.AveragePrice??0,row.Entries,row.Peak,TPComplete(row,true),TPComplete(row,false),row.Realized,row.Error);}).ToArray(),recent.ToArray());
    }
    private void ManageRow(Row row,DateTimeOffset now,bool tradeEvaluation,bool freshEntry)
    {
        var runtime=row.Runtime;var profile=runtime.Profile;var key=runtime.Instrument.Key;var p=execution.Positions.GetValueOrDefault(key);
        if(stopped)return;
        if(p?.Quantity>0&&!row.Owned){needsReconciliation=true;row.Error="전략 소유가 확인되지 않은 기존 보유 · 자동 주문 차단";return;}
        var active=execution.Orders.Where(o=>o.Active&&o.Symbol==key).ToArray();bool entryWindowOpen=false;
        if(runtime.CurrentSession is {} session)
        {
            var clock=DayLiquidationPolicy.Evaluate(profile.Session,session,now,p?.Quantity??0,active.Any(o=>o.Buy));
            entryWindowOpen=!clock.BlockNewEntries||runtime.CalendarCloseResearch&&!profile.Session.DayOnly&&tradeEvaluation;
            if(clock.UnexpectedOvernight){needsReconciliation=true;row.Error="UnexpectedOvernightPosition · 미청산/재동기화 필요";row.Status="장 종료 · 미청산";Audit(now,key,"미청산",row.Error);return;}
            if(clock.CancelPendingBuys)foreach(var buy in active.Where(o=>o.Buy))execution.Cancel(buy,now);
            if(clock.LiquidateOwned&&row.Owned&&p?.Quantity>0)
            {RequestExit(row,p.Quantity,"DayLiquidation",now);return;}
        }
        if(row.Owned&&p?.Quantity>0&&runtime.CurrentPrice>0)
        {
            if(tradeEvaluation&&!profile.Session.DayOnly&&runtime.TradingDayOrdinal-row.EntryDayOrdinal+1>=profile.Parameters.MaxHoldingDays)
            {RequestExit(row,p.Quantity,"MaxHoldingDays",now);return;}
            var view=new IntradayPositionView(p.Quantity,p.AveragePrice,p.CostFees,row.Peak,row.MaximumQuantity,row.HarvestBasis,TPComplete(row,true),TPComplete(row,false),row.TrailingFloor,row.TP1Sold,row.TP2Sold,row.TP1Sold>0);
            var risk=IntradayPositionRules.Evaluate(profile.Parameters,profile.Exit,view,runtime.ClosedBarEnvelope?.Low??runtime.CurrentPrice,row.FirstEntry?.ZoneLow,runtime.ClosedAnalysis);
            if(runtime.ClosedBarEnvelope is {} envelope&&risk.Reason==null)
                risk=IntradayPositionRules.Evaluate(profile.Parameters,profile.Exit,view,envelope.High,row.FirstEntry?.ZoneLow,runtime.ClosedAnalysis);
            if(tradeEvaluation)
            {
                if(risk.TrailingStop is {} stop)row.TrailingFloor=Math.Max(row.TrailingFloor,stop);
                row.Peak=Math.Max(row.Peak,runtime.ClosedBarEnvelope?.High??runtime.CurrentPrice);
                if(risk.Reason!=null){RequestExit(row,risk.Quantity,risk.Reason,now,risk);return;}
            }
        }
        var context=new IntradayExecutionContext(row.Owned&&p?.Quantity>0,active.Any(o=>o.Buy),symbolEntries.GetValueOrDefault(key),strategyEntries.GetValueOrDefault(profile.InstanceId),accountEntries,row.LastExit,row.ExitReason,row.FirstEntry,row.ExitBarOrdinal);
        var signal=runtime.Evaluate(context);row.Status=runtime.State;
        bool additional=row.Owned&&p?.Quantity>0;
        if(additional&&tradeEvaluation)signal=runtime.EvaluateAdditional(context,row.Entries,row.LastEntryPrice,row.HarvestStarted);
        if(signal==null||!tradeEvaluation)return;
        if(!observedSignals.Add(signal.Id))return;
        Audit(now,key,"매수 신호",signal.Reason,price:signal.ReferencePrice);
        EntrySignalObserved?.Invoke(signal);
        if(!entryEnabled||signal.Time<=enabledAt){row.Status="매수 신호 · 자동진입 꺼짐/활성화 이전";return;}
        if(!entryWindowOpen){row.Status="현재 시각 신규진입 종료 · 주문 차단";return;}
        if(!freshEntry||needsReconciliation||!execution.Synchronized||execution.IsLive&&(runtime.ResearchOnly||!liveAuthorized())){row.Status="자료/대사/실계좌 활성화 미확인 · 주문 차단";return;}
        if(active.Length>0||!additional&&p?.Quantity>0){row.Status="보유 또는 미체결 · 독립 중복진입 차단";return;}
        if(openingEquity-execution.Equity>=limits.MaxDailyLoss||consecutiveStops>=limits.MaxConsecutiveStops){row.Status="일 손실/연속 손절 한도";return;}
        var pending=execution.Orders.Count(o=>o.Active&&o.Buy&&(execution.Positions.GetValueOrDefault(o.Symbol)?.Quantity??0)==0);
        if(!additional&&accountEntries+pending>=profile.Reentry.MaxEntriesPerAccountPerDay){row.Status="공유계좌 진입 횟수 예약 한도";return;}
        if(profile.Exit.SupportStopEnabled&&signal.ZoneLow is {} lower&&runtime.CurrentPrice<lower*(1+profile.Parameters.SupportBreakPercent/100)){row.Status="주문 직전 지지 손절선 하회";return;}
        if(!additional&&signal.ZoneLow is {} lo&&signal.ZoneHigh is {} hi&&(runtime.CurrentPrice<lo*(1-signal.MaxEntryDistancePercent/100)||runtime.CurrentPrice>hi*(1+signal.MaxEntryDistancePercent/100))){row.Status="주문 직전 지지 추격거리 초과";return;}
        var stopView=new IntradayPositionView(1,runtime.CurrentPrice,0,runtime.CurrentPrice,1,0,false,false);
        var stops=IntradayPositionRules.Evaluate(profile.Parameters,profile.Exit,stopView,runtime.CurrentPrice,signal.ZoneLow);
        var protective=new[]{stops.SupportStop,stops.FixedStop,stops.TrailingStop,additional?(decimal?)row.TrailingFloor:null}.Where(v=>v.HasValue&&v.Value<runtime.CurrentPrice).Select(v=>v!.Value).DefaultIfEmpty(0).Max();
        var exposure=execution.Positions.Values.Sum(pos=>pos.Quantity*pos.Last)+execution.ReservedCash;
        var remainingRisk=Serialization.Unpack<AccountRiskPolicy>(Serialization.Pack(profile.Risk));
        if(additional&&p!=null)remainingRisk.RiskBudgetPerPosition=Math.Max(0,remainingRisk.RiskBudgetPerPosition-p.Quantity*Math.Max(0,p.AveragePrice-protective)-p.CostFees);
        var quantity=IntradayPositionRules.EntrySize(profile.Parameters,remainingRisk,runtime.CurrentPrice,Math.Min(limits.MaxOrderCash,execution.Cash-execution.ReservedCash),limits.MaxConcurrentExposure-exposure,protective,additional?row.Entries:0);
        if(quantity<=0){row.Status="현금/노출/손절거리 위험예산으로 주문수량 0";return;}
        var order=new ManagedOrder{Id=signal.Id,Instrument=runtime.Instrument,Buy=true,Quantity=quantity,Price=runtime.CurrentPrice,Kind=OrderKind.Market,Created=now,Strategy=profile.InstanceId,Reason=signal.Reason};
        Submit(row,order,signal,now);
    }
    private void RequestExit(Row row,int quantity,string reason,DateTimeOffset now,IntradayExitDecision? risk=null)
    {
        var key=row.Runtime.Instrument.Key;var active=execution.Orders.Where(o=>o.Active&&o.Symbol==key).ToArray();
        bool protective=reason.Contains("Stop",StringComparison.Ordinal)||reason=="DayLiquidation";
        if(active.Length>0)
        {
            if(!protective)return;
            foreach(var pending in active)
            {
                if(!pending.Buy&&pending.Reason==reason)return;
                execution.Cancel(pending,now);
            }
            if(execution.Orders.Any(o=>o.Active&&o.Symbol==key)){row.Status="보호 청산 전 기존 주문 취소 확인 대기";return;}
        }
        var owned=execution.Positions.GetValueOrDefault(key)?.Quantity??0;quantity=Math.Min(quantity,owned);if(quantity<=0)return;
        var order=new ManagedOrder{Id=key+"|"+row.Runtime.Profile.InstanceId+"|exit|"+(++exitSequence),Instrument=row.Runtime.Instrument,Buy=false,Quantity=quantity,Price=row.Runtime.CurrentPrice,Kind=OrderKind.Market,Created=now,Strategy=row.Runtime.Profile.InstanceId,Reason=reason};
        Submit(row,order,row.FirstEntry,now);
        if(submitted.Contains(order.Id))
        {
            var position=execution.Positions.GetValueOrDefault(key);
            exitSnapshots[order.Id]=Serialization.Pack(new{Time=now,Reason=reason,Price=row.Runtime.CurrentPrice,Quantity=quantity,Average=position?.AveragePrice,Peak=row.Peak,EntryZoneLow=row.FirstEntry?.ZoneLow,Rules=risk,Policy=row.Runtime.Profile.Exit});
        }
    }
    private void Submit(Row row,ManagedOrder order,IntradayEntrySignal? evidence,DateTimeOffset now)
    {
        if(stopped||needsReconciliation||!execution.Synchronized||execution.IsLive&&(row.Runtime.ResearchOnly||!liveAuthorized()))return; // Last check at the transmission boundary.
        if(!submitted.Add(order.Id))return;
        if(evidence!=null)intents[order.Id]=evidence;
        try{execution.Submit(order);}
        catch{needsReconciliation=true;row.Error="주문 응답 미확인 · 재전송 금지/재동기화 필요";Audit(now,order.Symbol,"주문 미확인",row.Error,order.Id);throw;}
        row.Status=order.Buy?"매수 주문 접수 · 체결 대기":"매도 주문 접수 · 체결 대기";
        Audit(now,order.Symbol,"주문 요청",order.Reason,order.Id,order.Price,order.Quantity);
    }
    private void ApplyFills(DateTimeOffset now)
    {
        while(appliedFills<execution.Fills.Count)
        {
            var index=appliedFills++;var fill=execution.Fills[index];
            if(!fillIds.Add(fill.OrderId+"|"+index))continue;
            if(!rows.TryGetValue(fill.Instrument.Key,out var row)||!submitted.Contains(fill.OrderId)){needsReconciliation=true;throw new InvalidOperationException("소유 주문이 확인되지 않은 체결 · 재동기화 필요");}
            var p=execution.Positions.GetValueOrDefault(fill.Instrument.Key);int quantity=p?.Quantity??0;
            intents.TryGetValue(fill.OrderId,out var evidence);
            if(fill.Buy)
            {
                if(!row.Owned)
                {
                    if(evidence==null)throw new InvalidOperationException("최초 체결의 진입 증거가 없습니다.");
                    row.FirstEntry=evidence;row.Owned=true;row.Peak=fill.Price;row.RoundProfit=0;row.EntryDayOrdinal=row.Runtime.TradingDayOrdinalAt(fill.Time);
                    symbolEntries[fill.Instrument.Key]=symbolEntries.GetValueOrDefault(fill.Instrument.Key)+1;strategyEntries[row.Runtime.Profile.InstanceId]=strategyEntries.GetValueOrDefault(row.Runtime.Profile.InstanceId)+1;accountEntries++;
                }
                var partial=row.EntryOrderFills.GetValueOrDefault(fill.OrderId);partial=(partial.Quantity+fill.Quantity,partial.Value+fill.Price*fill.Quantity);row.EntryOrderFills[fill.OrderId]=partial;
                row.LastEntryPrice=partial.Value/partial.Quantity;row.MaximumQuantity=Math.Max(row.MaximumQuantity,quantity);if(row.EntryOrders.Add(fill.OrderId))row.Entries++;
            }
            else
            {
                row.Realized+=fill.RealizedPnl;row.RoundProfit+=fill.RealizedPnl;
                if(fill.Reason is "TP1" or "TP2")
                {
                    if(!row.HarvestStarted){row.HarvestStarted=true;row.HarvestBasis=row.MaximumQuantity;}
                    if(fill.Reason=="TP1")
                    {if(row.TP1Sold==0&&row.Runtime.Profile.Exit.PeakBasis==TrailingPeakBasis.ResetAtTakeProfit1)row.Peak=fill.Price;row.TP1Sold+=fill.Quantity;}
                    else row.TP2Sold+=fill.Quantity;
                }
                if(quantity==0)
                {
                    row.LastExit=fill.Time;row.ExitReason=row.RoundProfit<0?"RealizedLossStop:"+fill.Reason:fill.Reason;row.ExitBarOrdinal=row.Runtime.AnalysisBarOrdinal;
                    consecutiveStops=row.RoundProfit<0?consecutiveStops+1:0;row.Owned=false;row.Entries=row.MaximumQuantity=row.HarvestBasis=row.TP1Sold=row.TP2Sold=0;row.Peak=row.TrailingFloor=0;row.HarvestStarted=false;row.EntryOrders.Clear();row.EntryOrderFills.Clear();
                }
            }
            row.Status=(fill.Buy?"매수":"매도")+" 체결 확인";Audit(now,fill.Instrument.Key,"체결",fill.Reason,fill.OrderId,fill.Price,fill.Quantity);FillConfirmed?.Invoke(fill,evidence);
        }
    }
    private static bool TPComplete(Row row,bool first)
    {
        var ratio=first?row.Runtime.Profile.Parameters.TakeProfit1Ratio:row.Runtime.Profile.Parameters.TakeProfit2Ratio;var basis=row.HarvestBasis>0?row.HarvestBasis:row.MaximumQuantity;var target=(int)decimal.Floor(basis*ratio/100);
        return target>0&&(first?row.TP1Sold:row.TP2Sold)>=target;
    }
    private void Audit(DateTimeOffset time,string symbol,string kind,string reason,string? order=null,decimal? price=null,int? quantity=null)
    {
        var audit=new DayTradingAudit(time,symbol,kind,reason,order,price,quantity);recent.Enqueue(audit);
        var maximum=rows.Values.Min(r=>r.Runtime.Profile.Quality.MaximumQueuedEvents);while(recent.Count>maximum)recent.Dequeue();record?.Invoke(audit);
    }
}
