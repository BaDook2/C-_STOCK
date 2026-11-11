using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

/// <summary>Shared existing ledger/broker, with explicit conservative passive-limit and delayed-cancel policies.</summary>
public sealed class InjesPaperExecution:IStrategyExecutionAdapter
{
    private readonly PaperBroker broker;private readonly InjesExecutionPolicy policy;
    public PaperBroker Broker=>broker;
    public bool IsLive=>false;public bool Synchronized=>true;public string Currency=>broker.Currency;
    public decimal Cash=>broker.Cash;public decimal Equity=>broker.Equity;public decimal ReservedCash=>broker.ReservedCash;
    public IReadOnlyDictionary<string,Position> Positions=>broker.Positions;public IReadOnlyList<ManagedOrder> Orders=>broker.Orders;public IReadOnlyList<Fill> Fills=>broker.Fills;
    public InjesPaperExecution(decimal cash,InjesExecutionPolicy policy,Action<string,object,DateTimeOffset>? record=null)
    {
        InjesParameterValidation.Validate(policy);if(cash<=0)throw new InvalidOperationException("양의 USD 계좌 현금이 필요합니다.");this.policy=policy;
        broker=new("USD",cash,new(){UseSpread=true,ConservativeLimitMatching=true,CancellationLatencyMilliseconds=policy.CancellationLatencyMilliseconds,FeePercent=policy.FeePercent,TaxPercent=policy.TaxPercent,SlippagePercent=policy.SlippagePercent,LatencyMs=policy.SubmissionLatencyMilliseconds,ParticipationPercent=policy.ParticipationPercent,AllowPartial=true}){Record=record};
    }
    public void OnMarket(MarketEvent market)=>broker.Process(market);
    public void Submit(ManagedOrder order){if(Orders.Any(o=>o.Id==order.Id))throw new InvalidOperationException("중복 주문 ID");broker.Submit(order);order.State=OrderState.Accepted;broker.Record?.Invoke("주문 접수 확인",order,order.Created);if(order.PassiveLimit)broker.Record?.Invoke("QueuePositionUnknown",new{order.Id,Model="후속 반대 방향 엄격 통과 체결량 제한; 실제 대기열 재현 아님"},order.Created);}
    public void Cancel(ManagedOrder order,DateTimeOffset now)=>broker.RequestCancel(order,now);
    public void OnTime(DateTimeOffset now)
    {
        // A deterministic SIMULATOR acknowledgement; never used for a live adapter or fabricated as a market event.
        foreach(var order in broker.Orders.Where(o=>o.Active&&o.State!=OrderState.Unknown))
        {
            if(order.State==OrderState.CancelRequested)
            {if((now-order.CancellationRequestedAt!.Value).TotalMilliseconds>=policy.CancellationLatencyMilliseconds){order.State=OrderState.Cancelled;broker.Record?.Invoke("가상 취소 확인",order,now);}continue;}
            if((now-order.Created).TotalMilliseconds>=order.TimeoutMilliseconds)broker.RequestCancel(order,now);
        }
    }
}

public static class InjesPositionSizing
{
    public static int Quantity(InjesCommonParameters c,InjesExecutionPolicy p,decimal price,decimal availableCash,decimal remainingExposure,decimal? protectiveStop,decimal? remainingRisk=null)
    {
        if(price<=0)return 0;var cash=Math.Max(0,Math.Min(c.OrderBudget,Math.Min(availableCash,remainingExposure)));
        var quantity=decimal.Floor(cash/(price*(1+p.FeePercent/100)));
        if(p.RiskBudgetCash is {} budget)
        {
            if(protectiveStop is not >0||protectiveStop>=price)return 0;
            var costs=price*(2*p.FeePercent+p.TaxPercent+2*p.SlippagePercent)/100+p.ConservativeExecutionCost;
            var perShare=Math.Max(0,price-protectiveStop.Value)+costs;if(perShare<=0)return 0;
            quantity=Math.Min(quantity,decimal.Floor(Math.Max(0,remainingRisk??budget)/perShare));
        }
        return (int)Math.Min(int.MaxValue,Math.Max(0,quantity));
    }
}

public sealed record InjesRowCheckpoint(string Symbol,StrategySignal? Entry,StrategySignal? PendingEvidence,DateTimeOffset? LastExit,bool EntriesBlocked=false);
public sealed record InjesSessionCheckpoint(string ConfigurationHash,InjesRowCheckpoint[] Rows,Dictionary<string,StrategySignal> OrderEvidence,
    string[] SubmittedOrders,StrategySignal[] Signals,int OrderSequence,DateOnly MarketDate,decimal OpeningEquity,bool DailyLossLatched,bool EmergencyStopped);

/// <summary>One immutable algorithm selection for all symbols. Execution owns confirmed positions; signals never create holdings.</summary>
public sealed class InjesSession
{
    private sealed class Row(Instrument instrument,InjesFeatureEngine features,IStrategyRuntime runtime)
    {
        public Instrument Instrument=instrument;public InjesFeatureEngine Features=features;public IStrategyRuntime Runtime=runtime;
        public StrategyState State;public StrategySignal? Entry,PendingEvidence;public StrategyEvaluationResult? Latest;public bool EntriesBlocked;
        public DateTimeOffset? LastExit;public string Error="";public InjesFeatures? LastFeatures;public DateTimeOffset LastClock;
    }
    private readonly object gate=new();private readonly InjesStrategyDefinition definition;private readonly IStrategyFactory factory;
    private readonly object effective;
    private readonly IStrategyExecutionAdapter execution;private readonly Dictionary<string,Row> rows=[];
    private readonly HashSet<string> submitted=[],episodes=[];private readonly Dictionary<string,StrategySignal> evidence=[];
    private readonly Action<StrategyEvaluationResult>? sink;private int fillsRead,orderSequence;private bool enabled,stopped,needsReconciliation;private DateTimeOffset enabledAt;
    private DateOnly day;private decimal openingEquity;private bool dailyLossLatched;
    private readonly List<StrategySignal> signals=[];private readonly List<EquityPoint> equity=[];private readonly Dictionary<string,int> rejected=[];
    public InjesStrategyDefinition Definition=>definition.Snapshot();
    public IStrategyExecutionAdapter Execution=>execution;
    public StrategySignal[] Signals {get{lock(gate)return signals.ToArray();}}
    public EquityPoint[] Equity {get{lock(gate)return equity.ToArray();}}
    public IReadOnlyDictionary<string,int> Rejections {get{lock(gate)return new Dictionary<string,int>(rejected);}}
    public bool EntriesEnabled=>enabled&&!stopped;public bool EmergencyStopped=>stopped;public bool ReconciliationRequired=>needsReconciliation;
    public StrategyEvaluationResult? Latest(string symbol){lock(gate)return rows.GetValueOrDefault(symbol)?.Latest;}
    public bool RequiresClockPulse(DateTimeOffset now)
    {lock(gate)return execution.Orders.Any(o=>o.Active&&o.State!=OrderState.Unknown)||execution.Positions.Values.Any(p=>p.Quantity>0&&p.OpenedAt is {} opened&&now<=opened.AddSeconds(definition.CommonParameters.MaxHoldingSeconds));}
    public InjesSession(InjesStrategyDefinition definition,IEnumerable<Instrument> instruments,IStrategyExecutionAdapter execution,Action<StrategyEvaluationResult>? sink=null,InjesSessionCheckpoint? checkpoint=null)
    {
        this.definition=definition.Snapshot();factory=StrategyRegistry.Injes.Get(definition.StrategyId);this.execution=execution;this.sink=sink;
        if(execution.Currency!="USD"||execution.IsLive)throw new InvalidOperationException("Inje 실계좌 실행은 잠겨 있습니다. USD 가상/리플레이 어댑터를 사용하세요.");
        if(checkpoint==null&&(execution.Orders.Any(o=>o.Active)||execution.Positions.Values.Any(p=>p.Quantity>0)))throw new InvalidOperationException("미체결/보유가 남은 계좌에서 전략을 교체할 수 없습니다.");
        effective=factory.Resolve(this.definition);
        foreach(var i in instruments){if(i.Currency!="USD"||!rows.TryAdd(i.Key,new(i,new(i,this.definition.ExecutionPolicy,factory.RequiredWindows(effective)),factory.Create(this.definition))))throw new InvalidOperationException("미국 종목만 중복 없이 지정하세요.");}
        if(rows.Count==0)throw new InvalidOperationException("감시 종목이 없습니다.");openingEquity=execution.Equity;
        if(checkpoint!=null)Restore(checkpoint);
    }
    public InjesSessionCheckpoint Capture()
    {
        lock(gate)return Serialization.Unpack<InjesSessionCheckpoint>(Serialization.Pack(new InjesSessionCheckpoint(TradingStore.Hash(InjesStrategyJson.Export(definition)),rows.Values.Select(r=>new InjesRowCheckpoint(r.Instrument.Key,r.Entry,r.PendingEvidence,r.LastExit,r.EntriesBlocked)).ToArray(),new(evidence),submitted.ToArray(),signals.ToArray(),orderSequence,day,openingEquity,dailyLossLatched,stopped)));
    }
    private void Restore(InjesSessionCheckpoint checkpoint)
    {
        if(checkpoint.ConfigurationHash!=TradingStore.Hash(InjesStrategyJson.Export(definition))||checkpoint.Rows.Length!=rows.Count||checkpoint.Rows.Select(r=>r.Symbol).Distinct().Count()!=rows.Count||checkpoint.Rows.Any(r=>!rows.ContainsKey(r.Symbol))||checkpoint.OrderSequence<execution.Orders.Count||execution.Orders.Any(o=>!checkpoint.SubmittedOrders.Contains(o.Id)||!checkpoint.OrderEvidence.ContainsKey(o.Id)))throw new InvalidOperationException("Inje 보관 계좌의 설정/소유 주문 대사가 일치하지 않습니다.");
        foreach(var saved in checkpoint.Rows)
        {
            var row=rows[saved.Symbol];row.Entry=saved.Entry;row.PendingEvidence=saved.PendingEvidence;row.LastExit=saved.LastExit;row.EntriesBlocked=saved.EntriesBlocked;
            if(execution.Positions.GetValueOrDefault(saved.Symbol) is {Quantity:>0} position&&(row.Entry==null||position.OpenedAt==null))throw new InvalidOperationException("보유분의 진입 당시 근거/최초 체결 시각이 없습니다.");
            row.State=execution.Positions.GetValueOrDefault(saved.Symbol)?.Quantity>0?StrategyState.Holding:StrategyState.Idle;
            row.Runtime.ResetCandidate(); // Reconnection never inherits an unfinished old candidate or rolling window.
        }
        foreach(var pair in checkpoint.OrderEvidence)evidence[pair.Key]=pair.Value;foreach(var id in checkpoint.SubmittedOrders)submitted.Add(id);
        signals.AddRange(checkpoint.Signals);orderSequence=checkpoint.OrderSequence;day=checkpoint.MarketDate;openingEquity=checkpoint.OpeningEquity;dailyLossLatched=checkpoint.DailyLossLatched;stopped=checkpoint.EmergencyStopped;
        needsReconciliation=execution.Orders.Any(o=>o.State==OrderState.Unknown)||!execution.Synchronized;fillsRead=execution.Fills.Count;enabled=false;
    }
    /// <summary>Explicit protection-resume action; entry authorization remains OFF and fresh candidates must start again.</summary>
    public void ResumeProtection(DateTimeOffset now)
    {
        lock(gate){if(needsReconciliation||!execution.Synchronized)throw new InvalidOperationException("미확인 주문의 대사 전 재개할 수 없습니다.");stopped=false;enabled=false;enabledAt=now;foreach(var row in rows.Values)row.Runtime.ResetCandidate();foreach(var o in execution.Orders.Where(o=>o.Active&&o.Buy).ToArray())execution.Cancel(o,now);}
    }
    public void SetEntriesEnabled(bool value,DateTimeOffset now)
    {lock(gate){if(value&&(stopped||needsReconciliation))throw new InvalidOperationException("중지·미확인 주문 상태에서는 새 세션/대사가 필요합니다.");enabled=value;enabledAt=now;}}
    public void StopSymbolEntries(string symbol,DateTimeOffset now)
    {lock(gate){if(!rows.TryGetValue(symbol,out var row))throw new InvalidOperationException("감시 종목이 없습니다.");row.EntriesBlocked=true;row.Runtime.ResetCandidate();foreach(var o in execution.Orders.Where(o=>o.Active&&o.Buy&&o.Symbol==symbol))execution.Cancel(o,now);}}
    public void EmergencyStop(DateTimeOffset now)
    {lock(gate){stopped=true;enabled=false;foreach(var o in execution.Orders.Where(o=>o.Active).ToArray())execution.Cancel(o,now);}}
    public void OnMarket(InjesObservation input,DateTimeOffset now)
    {
        lock(gate)
        {
            if(!rows.TryGetValue(input.Market.Key,out var row))throw new InvalidOperationException("등록하지 않은 종목입니다.");
            if(now<row.LastClock)throw new InvalidOperationException("수신/실행 시계는 역행할 수 없습니다.");row.LastClock=now;
            var admission=row.Features.Add(input,now);
            if(!admission.Accepted){if(!admission.Duplicate){row.Error=admission.Reason;Reject(admission.Reason);Publish(row,StrategyIntent.Hold(admission.Reason),input.Market,now,false);}return;}
            row.Error="";if(admission.Discontinuity){row.Runtime.ResetCandidate();foreach(var pending in execution.Orders.Where(o=>o.Active&&o.Buy&&o.Symbol==row.Instrument.Key))execution.Cancel(pending,now);}
            var f=Features(row,now);if(f.Session is {} session&&session.MarketDate!=day){day=session.MarketDate;openingEquity=execution.Equity;dailyLossLatched=false;}
            // The simulator cannot confirm a fill from a halted/unknown session or a bad/stale quote.
            // Cancellation acknowledgement still advances independently through OnTime.
            if(f.Session is {TradingAllowed:true,CorporateActionsVerified:true} confirmed&&now>=confirmed.Open&&now<confirmed.Close&&input.Market.ExchangeTime>=confirmed.Open&&input.Market.ExchangeTime<confirmed.Close&&f.Reference?.Halted==false&&input.Market.TimeQuality=="거래소시간"&&f.FreshQuote&&
                execution.Orders.Where(o=>o.Active&&o.Symbol==row.Instrument.Key).All(o=>confirmed.AllowedOrderKinds.Contains(o.Kind)))execution.OnMarket(input.Market);
            else foreach(var pending in execution.Orders.Where(o=>o.Active&&o.Buy&&o.Symbol==row.Instrument.Key))execution.Cancel(pending,now);
            ApplyFills();CheckDailyLoss(now);
            Evaluate(row,input.Market,now);Mark(now);
        }
    }
    public void OnTime(DateTimeOffset now)
    {
        lock(gate)
        {
            if(execution is InjesPaperExecution paper)paper.OnTime(now);ApplyFills();CheckDailyLoss(now);
            foreach(var row in rows.Values){if(now<row.LastClock)continue;row.LastClock=now;Evaluate(row,null,now);}Mark(now);
        }
    }
    private InjesFeatures Features(Row row,DateTimeOffset now)
    {
        var p=effective;
        return row.LastFeatures=row.Features.Snapshot(now,p is MomentumPullbackParameters m?m.RVOLLookbackSessions:20,p is QuoteScalpingParameters q?q.VolumeLookbackSeconds:30);
    }
    private void Evaluate(Row row,MarketEvent? market,DateTimeOffset now)
    {
        var key=row.Instrument.Key;var f=Features(row,now);var position=execution.Positions.GetValueOrDefault(key);var c=definition.CommonParameters;
        var active=execution.Orders.Where(o=>o.Active&&o.Symbol==key).ToArray();
        if(active.Any(o=>o.State==OrderState.Unknown)||!execution.Synchronized){needsReconciliation=true;row.State=StrategyState.ReconciliationRequired;Publish(row,StrategyIntent.Hold("UnknownOrder/ReconciliationRequired"),market,now,false);return;}
        if(stopped){Publish(row,StrategyIntent.Hold("EmergencyStopped · 주문 취소/계좌 확인"),market,now,false);return;}
        if(position?.Quantity>0)
        {
            if(row.Entry==null){needsReconciliation=true;row.State=StrategyState.ReconciliationRequired;return;}
            var stop=position.AveragePrice*(1-c.StopLossPercent/100);var take=position.AveragePrice*(1+c.TakeProfitPercent/100);
            StrategyIntent? exit=null;
            if(f.FreshTrade&&f.Price<=stop)exit=new(SignalType.Exit,"StopLoss",row.Entry.EpisodeId,[],new Dictionary<string,decimal?>(),ProtectivePrice:stop);
            // Max holding uses the first confirmed fill, and is evaluated by an independent clock as well.
            if(exit==null&&position.OpenedAt is {} opened&&(now-opened).TotalSeconds>=c.MaxHoldingSeconds)exit=new(SignalType.Exit,"MaxHoldingSeconds",row.Entry.EpisodeId,[],new Dictionary<string,decimal?>());
            var strategy=row.Runtime.Evaluate(new(row.Instrument,f,market,position,active.Any(o=>o.Buy),row.Entry,now));
            // Invalidations outrank time, liquidity and profit; target quoting is deliberately last.
            if(strategy.Type==SignalType.Exit&&strategy.Reason is "CrashLowInvalidated" or "SupportBreak")exit=strategy;
            if(exit==null&&strategy.Type==SignalType.Exit&&strategy.Reason!="QuoteTarget")exit=strategy;
            if(exit==null&&f.FreshTrade&&f.Price>=take)exit=new(SignalType.Exit,"TakeProfit",row.Entry.EpisodeId,[],new Dictionary<string,decimal?>());
            if(exit==null&&strategy.Type==SignalType.Exit)exit=strategy;
            if(exit!=null){Exit(row,exit,f,active,market,now);return;}
            row.State=active.Any(o=>!o.Buy)?StrategyState.SellPending:StrategyState.Holding;Publish(row,StrategyIntent.Hold("보유 보호 평가 · 진입 필터와 무관"),market,now,false);return;
        }
        if(active.Length>0)
        {
            row.State=StrategyState.BuyPending;
            var unsafeEntry=CommonConditions(f,now).Any(c=>c.Name!="Risk.ConcurrentPositions"&&c.Outcome is EvaluationOutcome.Failed or EvaluationOutcome.MissingData);
            var pending=row.Runtime.Evaluate(new(row.Instrument,f,market,null,active.Any(o=>o.Buy),row.PendingEvidence,now));
            if(unsafeEntry||pending.Type==SignalType.CancelEntry){foreach(var o in active.Where(o=>o.Buy))execution.Cancel(o,now);row.Runtime.ResetCandidate();}
            Publish(row,StrategyIntent.Hold("미체결/취소 확인 대기"),market,now,false);return;
        }
        if(row.LastExit is {} last&&(now-last).TotalSeconds<c.ReentryCooldownSeconds){row.State=StrategyState.Cooldown;Publish(row,StrategyIntent.Hold("재진입 대기"),market,now,false);return;}
        if(row.EntriesBlocked){row.State=StrategyState.Blocked;Publish(row,StrategyIntent.Hold("종목 신규 진입 중지 · 보유 보호 유지"),market,now,false);return;}
        if(market==null)return; // A clock pulse cannot invent an entry event.
        var intent=row.Runtime.Evaluate(new(row.Instrument,f,market,null,false,null,now));
        if(intent.Type!=SignalType.Enter){row.State=intent.EpisodeId!=""?StrategyState.Candidate:StrategyState.Idle;Publish(row,intent,market,now,false);return;}
        var episode=key+"|"+definition.StrategyId+"|"+intent.EpisodeId;
        if(!episodes.Add(episode))return;
        var checks=EntryBlocks(row,f,now).ToList();
        var price=intent.PlannedPrice;var tick=f.TickSize;
        if(price is >0&&tick is >0)price=PriceGrid.Buy(price.Value,tick.Value);
        if(price is >0&&intent.ProtectivePrice>=price)checks.Add("Risk.ProtectiveStopAlreadyBroken");
        if(price is not >0||tick is not >0)checks.Add("MissingData: 주문가격/호가 단위");
        if(intent.MaxChasePercent is {} chase&&price>f.Price*(1+chase/100))checks.Add("MaxChasePercent");
        if(f.Session?.AllowedOrderKinds.Contains(intent.OrderKind)!=true)checks.Add("Risk.OrderKindUnsupportedOrUnknown");
        decimal? expectedNet=null;
        if(intent.Metrics.TryGetValue("plannedSellPrice",out var plannedSell)&&plannedSell is {} sell&&price is {} buy)
        {
            var p=definition.ExecutionPolicy;var cost=(buy+sell)*p.FeePercent/100+sell*p.TaxPercent/100+(buy+sell)*p.SlippagePercent/100+p.ConservativeExecutionCost;
            expectedNet=sell-buy-cost;if(expectedNet<=0)checks.Add("ExpectedNet <= 0");
        }
        var fixedStop=price is >0?price.Value*(1-c.StopLossPercent/100):0;var protective=Math.Max(fixedStop,intent.ProtectivePrice??0);
        var exposure=execution.Positions.Values.Sum(p=>p.Quantity*p.AveragePrice)+execution.ReservedCash;
        var remainingRisk=RemainingRiskBudget();
        var quantity=price is >0?InjesPositionSizing.Quantity(c,definition.ExecutionPolicy,price.Value,execution.Cash-execution.ReservedCash,definition.ExecutionPolicy.MaximumExposureCash-exposure,protective,remainingRisk):0;
        if(quantity<=0)checks.Add("Risk.QuantityZero");
        var enriched=new Dictionary<string,decimal?>(intent.Metrics){{"feeRate",definition.ExecutionPolicy.FeePercent/100},{"taxRate",definition.ExecutionPolicy.TaxPercent/100},{"slippageRate",definition.ExecutionPolicy.SlippagePercent/100},{"executionCost",definition.ExecutionPolicy.ConservativeExecutionCost},{"expectedNetPerShare",expectedNet},{"remainingRiskBudget",remainingRisk},{"orderQuantity",quantity},{"protectiveStop",protective}};
        intent=intent with{Metrics=enriched,PlannedPrice=price};
        row.State=checks.Count>0?StrategyState.Blocked:StrategyState.EntrySignal;
        var signal=Publish(row,intent,market,now,checks.Count==0&&enabled&&now>enabledAt,checks.ToArray(),checks.Count==0);
        if(checks.Count>0){foreach(var reason in checks)Reject(reason);return;}
        if(!enabled||now<=enabledAt){Reject("EntriesDisabled");return;}

        var order=new ManagedOrder{Id=key+"|inje|"+definition.StrategyId+"|"+(++orderSequence),Instrument=row.Instrument,Buy=true,Price=price!.Value,PriceTick=tick,Quantity=quantity,Kind=intent.OrderKind,PassiveLimit=intent.OrderKind==OrderKind.Limit&&price<f.Ask,Created=now,Strategy=definition.StrategyId,Reason=intent.Reason,TimeoutMilliseconds=c.OrderTimeoutMilliseconds};
        evidence[order.Id]=signal;row.PendingEvidence=signal;Submit(row,order);row.State=StrategyState.BuyPending;if(row.Latest!=null)row.Latest=row.Latest with{State=row.State};
    }
    /// <summary>Account risk is reserved by both confirmed holdings and unfilled buys until cancellation is acknowledged.</summary>
    private decimal? RemainingRiskBudget()
    {
        var p=definition.ExecutionPolicy;if(p.RiskBudgetCash is not {} budget)return null;
        decimal Used(decimal price,int quantity,decimal stop)=>quantity*(Math.Max(0,price-stop)+price*(2*p.FeePercent+p.TaxPercent+2*p.SlippagePercent)/100+p.ConservativeExecutionCost);
        decimal used=0;
        foreach(var position in execution.Positions.Values.Where(x=>x.Quantity>0))
        {
            if(!rows.TryGetValue(position.Instrument.Key,out var row)||row.Entry==null)return 0;
            var fixedStop=position.AveragePrice*(1-definition.CommonParameters.StopLossPercent/100);
            used+=Used(position.AveragePrice,position.Quantity,Math.Max(fixedStop,row.Entry.ProtectivePrice??0));
        }
        foreach(var order in execution.Orders.Where(x=>x.Active&&x.Buy))
        {if(!evidence.TryGetValue(order.Id,out var signal)||signal.Metrics.GetValueOrDefault("protectiveStop") is not >0)return 0;used+=Used(order.Price,order.Remaining,signal.Metrics["protectiveStop"]!.Value);}
        return Math.Max(0,budget-used);
    }
    private IEnumerable<string> EntryBlocks(Row row,InjesFeatures f,DateTimeOffset now)
    {
        foreach(var condition in CommonConditions(f,now))if(condition.Outcome is EvaluationOutcome.Failed or EvaluationOutcome.MissingData)yield return condition.Name+(condition.Outcome==EvaluationOutcome.MissingData?": MissingData":"");
    }
    private StrategyCondition[] CommonConditions(InjesFeatures f,DateTimeOffset now)
    {
        var c=definition.CommonParameters;var p=definition.ExecutionPolicy;var result=new List<StrategyCondition>();
        void Check(string key,bool? passed,string detail)=>result.Add(new(key,passed==null?EvaluationOutcome.MissingData:passed.Value?EvaluationOutcome.Passed:EvaluationOutcome.Failed,detail));
        void Disabled(string key,string detail)=>result.Add(new(key,EvaluationOutcome.Disabled,detail));
        Check("Universe.Price",f.Price==null?null:f.Price>=c.MinPrice&&f.Price<=c.MaxPrice,$"{f.Price} USD / {c.MinPrice}~{c.MaxPrice}");
        if(c.MaxMarketCap is {} cap)Check("Universe.MarketCap",f.Reference?.MarketCap is {} capValue?capValue<=cap:null,$"{f.Reference?.MarketCap} <= {cap} USD");else Disabled("Universe.MarketCap","필터 OFF; 값을 안전/0으로 대체하지 않음");
        if(c.MaxFloat is {} limit)Check("Universe.Float",f.Reference?.Float is {} floatValue?floatValue<=limit:null,$"{f.Reference?.Float} <= {limit}주");else Disabled("Universe.Float","필터 OFF; 값 없음 허용");
        Check("Universe.DailyTradingValue",f.Reference?.DailyTradingValue is {} tradingValue?tradingValue>=c.MinDailyTradingValue:null,$"{f.Reference?.DailyTradingValue} >= {c.MinDailyTradingValue} USD");
        Check("Risk.TradeFresh",f.FreshTrade?true:null,"신선한 정상 체결 및 시각");Check("Risk.QuoteFresh",f.FreshQuote?true:null,"신선한 정상 양방향 호가·잔량");
        Check("Risk.ExecutableDepth",f.BidSize==null||f.AskSize==null?null:f.BidSize>0&&f.AskSize>0,"매수·보호 매도 가능한 최우선 잔량이 모두 양수");
        Check("Risk.TickSize",f.TickSize is >0?true:null,"제공자가 확인한 종목·거래소·세션 호가 단위");
        Check("Risk.Spread",f.SpreadPercent is {} spread?spread<=c.MaxSpreadPercent:null,$"{f.SpreadPercent}% <= {c.MaxSpreadPercent}%");
        Check("Risk.Halt",f.Reference?.Halted is {} halted?!halted:null,"거래정지 확인; Unknown은 진입 불가");
        if(f.Reference?.FundamentalRisk==FundamentalRiskState.RiskDetected)Check("Risk.FundamentalRisk",false,"당시 위험 발견");else if(f.Reference?.FundamentalRisk==FundamentalRiskState.Cleared)Check("Risk.FundamentalRisk",true,"당시 확인된 검사 범위에서 Cleared");else Disabled("Risk.FundamentalRisk","Unknown; 전략별 확인 필수 조건은 별도로 적용");
        var session=f.Session;
        Check("Risk.Session",session==null?null:session.TradingAllowed&&now>=session.Open&&now<session.Close,"제공자가 확인한 현재 미국 세션·주문 허용 상태");
        Check("Risk.CorporateAction",session?.CorporateActionsVerified==true?true:null,"주식병합·분할·중단 등 해석 확인");
        Check("Risk.EnabledPhase",session==null?null:session.Phase==UsMarketPhase.Regular||session.Phase==UsMarketPhase.PreMarket&&p.AllowPreMarket||session.Phase==UsMarketPhase.AfterHours&&p.AllowAfterHours,"확인된 세션 중 사용자가 허용한 범위");
        Check("Risk.DailyLoss",!dailyLossLatched,$"미국 세션 날짜 손실 한도 {c.MaxDailyLossCash} USD");
        Check("Risk.Reconciliation",!needsReconciliation&&execution.Synchronized,"미확인 주문/계좌 불일치 없는 상태");
        var held=execution.Positions.Values.Count(x=>x.Quantity>0)+execution.Orders.Count(o=>o.Active&&o.Buy&&execution.Positions.GetValueOrDefault(o.Symbol)?.Quantity is not >0);
        Check("Risk.ConcurrentPositions",held<c.MaxConcurrentPositions,$"예약 포함 {held} < {c.MaxConcurrentPositions}");
        Check("Risk.QuoteTimeQuality",f.LastQuote?.TimeQuality=="거래소시간"?true:null,"호가 거래소 시각 품질");Check("Risk.TradeTimeQuality",f.LastTrade?.TimeQuality=="거래소시간"?true:null,"체결 거래소 시각 품질");
        return result.ToArray();
    }
    private void Exit(Row row,StrategyIntent intent,InjesFeatures f,ManagedOrder[] active,MarketEvent? market,DateTimeOffset now)
    {
        bool protective=intent.Reason!="QuoteTarget";
        foreach(var o in active)
        {
            if(!o.Buy&&o.Reason==intent.Reason){row.State=StrategyState.SellPending;return;}
            if(protective||o.Buy)execution.Cancel(o,now);
        }
        if(active.Length>0){row.State=StrategyState.SellPending;Publish(row,StrategyIntent.Hold("보호 청산 전 취소 확인 대기"),market,now,false);return;}
        if(!f.FreshQuote||f.Bid is not >0||f.TickSize is not >0||f.Session is not {TradingAllowed:true} s||now>=s.Close||f.Reference?.Halted!=false){Publish(row,StrategyIntent.Hold("청산 의도 유지 · 실행 가능한 호가/거래상태 대기"),market,now,false);return;}
        var quantity=execution.Positions.GetValueOrDefault(row.Instrument.Key)?.Quantity??0;if(quantity<=0)return;
        var price=protective?f.Bid!.Value:intent.PlannedPrice??f.Ask!.Value;var kind=protective?OrderKind.Market:OrderKind.Limit;
        if(!s.AllowedOrderKinds.Contains(kind)){Publish(row,StrategyIntent.Hold("청산 의도 유지 · 해당 세션의 주문 유형 미지원/미확인"),market,now,false);return;}
        var signal=Publish(row,intent,market,now,true);var order=new ManagedOrder{Id=row.Instrument.Key+"|inje|exit|"+(++orderSequence),Instrument=row.Instrument,Buy=false,Price=price,PriceTick=f.TickSize,Quantity=quantity,Kind=kind,PassiveLimit=kind==OrderKind.Limit&&price>f.Bid,Created=now,Strategy=definition.StrategyId,Reason=intent.Reason,TimeoutMilliseconds=definition.CommonParameters.OrderTimeoutMilliseconds};
        evidence[order.Id]=signal;Submit(row,order);row.State=StrategyState.SellPending;
    }
    private void Submit(Row row,ManagedOrder order)
    {
        if(stopped||needsReconciliation||!execution.Synchronized||execution.IsLive||!submitted.Add(order.Id))return;
        try{execution.Submit(order);}catch{needsReconciliation=true;row.State=StrategyState.UnknownOrder;throw;}
    }
    private void ApplyFills()
    {
        while(fillsRead<execution.Fills.Count)
        {
            var fill=execution.Fills[fillsRead++];if(!submitted.Contains(fill.OrderId)||!rows.TryGetValue(fill.Instrument.Key,out var row)||!evidence.TryGetValue(fill.OrderId,out var intent)){needsReconciliation=true;throw new InvalidOperationException("소유 주문/신호 증거 없는 체결");}
            if(fill.Buy){row.Entry??=intent;row.State=StrategyState.Holding;}
            else if(execution.Positions.GetValueOrDefault(fill.Instrument.Key)?.Quantity==0)
            {row.LastExit=fill.Time;row.Entry=null;row.PendingEvidence=null;row.Runtime.OnPositionClosed();row.State=StrategyState.Cooldown;}
        }
    }
    private void CheckDailyLoss(DateTimeOffset now)
    {if(openingEquity-execution.Equity>=definition.CommonParameters.MaxDailyLossCash)dailyLossLatched=true;if(dailyLossLatched)foreach(var o in execution.Orders.Where(o=>o.Active&&o.Buy))execution.Cancel(o,now);}
    private void Mark(DateTimeOffset at){equity.Add(new(at,execution.Currency,execution.Equity));if(equity.Count>200000)equity.RemoveRange(0,1000);}
    private void Reject(string reason)=>rejected[reason]=rejected.GetValueOrDefault(reason)+1;
    private StrategySignal Publish(Row row,StrategyIntent intent,MarketEvent? market,DateTimeOffset now,bool allowed,string[]? blocks=null,bool riskApproved=false)
    {
        var id=$"{definition.StrategyId}|{row.Instrument.Key}|{intent.EpisodeId}|{intent.Type}|{market?.ExchangeTime.UtcTicks??now.UtcTicks}|{market?.Source}|{market?.Sequence}";
        var features=row.LastFeatures;var data=new List<ObservationEvidence>();
        void AddEvidence(string kind,MarketEvent? point){if(point!=null)data.Add(new(kind,point.ExchangeTime,point.ReceivedTime,point.AvailableAt??point.ReceivedTime,point.Source,point.Sequence));}
        AddEvidence("Trade",features?.LastTrade);AddEvidence("Quote",features?.LastQuote);
        if(features?.Reference is {} reference)data.Add(new("Reference",reference.KnownAt,reference.KnownAt,reference.KnownAt,"POINT_IN_TIME_REFERENCE",0));
        var metrics=new Dictionary<string,decimal?>(intent.Metrics){["dayGainPercent"]=features?.DayGainPercent,["rvol"]=features?.Rvol,["spreadPercent"]=features?.SpreadPercent,["spreadTicks"]=features?.SpreadTicks,["dailyTradingValue"]=features?.Reference?.DailyTradingValue,["marketCap"]=features?.Reference?.MarketCap,["float"]=features?.Reference?.Float};
        var common=CommonConditions(features??Features(row,now),now);
        var allConditions=intent.Conditions.Concat(common).ToArray();
        var signal=new StrategySignal(definition.FamilyId,definition.StrategyId,row.Instrument.Key,id,intent.Type,row.LastFeatures?.Price,now,intent.Reason,market?.ExchangeTime??now,market?.ReceivedTime??now,now,market?.Source??"CLOCK",market?.Sequence??0,allConditions,metrics,allowed,blocks??[],intent.EpisodeId,intent.PlannedPrice,intent.OrderKind,intent.MaxChasePercent,intent.ProtectivePrice){RiskApproved=riskApproved||intent.Type==SignalType.Exit&&allowed,UsedObservations=data.ToArray(),SupportCluster=intent.SupportCluster};
        if(intent.Type is SignalType.Enter or SignalType.Exit or SignalType.CancelEntry){signals.Add(signal);if(signals.Count>100000)throw new InvalidOperationException("판단 보존 한도 초과, 세션 분할 필요");}
        var b=blocks??[];
        static EvaluationOutcome Outcome(IEnumerable<StrategyCondition> conditions)
        {var list=conditions.ToArray();return list.Any(c=>c.Outcome==EvaluationOutcome.MissingData)?EvaluationOutcome.MissingData:list.Any(c=>c.Outcome==EvaluationOutcome.Failed)?EvaluationOutcome.Failed:EvaluationOutcome.Passed;}
        var universe=Outcome(common.Where(c=>c.Name.StartsWith("Universe.")));
        var attention=intent.Attention??(intent.Type==SignalType.Enter||intent.EpisodeId!=""?EvaluationOutcome.Passed:intent.Reason.Contains("MissingData")?EvaluationOutcome.MissingData:intent.Conditions.Length>0?Outcome(intent.Conditions):EvaluationOutcome.NotEvaluated);
        var risk=intent.Type is not(SignalType.Enter or SignalType.Exit)?EvaluationOutcome.NotEvaluated:signal.RiskApproved?EvaluationOutcome.Passed:common.Any(c=>c.Outcome==EvaluationOutcome.MissingData)?EvaluationOutcome.MissingData:EvaluationOutcome.Failed;
        row.Latest=new(signal,row.State,universe,attention,intent.Type==SignalType.Enter?EvaluationOutcome.Passed:intent.Reason.Contains("MissingData")?EvaluationOutcome.MissingData:EvaluationOutcome.Failed,risk);sink?.Invoke(row.Latest);return signal;
    }
}
