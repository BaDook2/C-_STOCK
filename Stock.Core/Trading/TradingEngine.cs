namespace Stock.Core.Trading;
public sealed class RiskManager(TradingSettings settings)
{
    public bool Stopped { get; private set; }
    public void Stop()=>Stopped=true;
    public decimal Distance(MarketContext c,decimal price,DistanceMode mode,decimal value)=>mode switch {DistanceMode.Percent=>price*value/100,DistanceMode.Ticks=>c.Instrument.Tick(price)*value,_=>c.Atr()*value};
    public int Size(MarketContext c,decimal equity,decimal price)
    {
        var s=settings.Risk;var distance=Distance(c,price,s.StopMode,s.StopValue);var qty=s.Sizing switch {SizingMode.FixedCash=>s.FixedCash/price,SizingMode.EquityPercent=>equity*s.EquityPercent/100/price,SizingMode.FixedShares=>s.FixedShares,_=>distance>0?Math.Min(s.MaxTradeLoss,equity*s.RiskPercent/100)/distance:0};
        return (int)Math.Min(s.MaxShares,Math.Max(0,decimal.Floor(qty)));
    }
    public string? Check(ManagedOrder o,PaperBroker broker,MarketContext c,decimal dayOpening,int dailyOrders)
    {
        var s=settings.Risk;
        if(Stopped)return "긴급중지 상태";
        if(o.Quantity<=0||o.Price<=0)return "양의 가격/정수 수량 필요";
        if(o.Instrument.Currency!=broker.Currency)return "다른 통화 주문 금지";
        if(broker.Orders.Any(x=>x.Active&&x.Symbol==o.Symbol))return "동일 종목 대기 주문/전략 충돌";
        var p=broker.Positions.GetValueOrDefault(o.Symbol);
        if(o.Buy&&p?.Quantity>0&&p.Owner!=o.Strategy)return "다른 전략의 보유 포지션과 충돌";
        if(!o.Buy)return p==null||p.Quantity<o.Quantity?"매도 가능 보유수량 부족":null;
        var equity=broker.Equity;var loss=dayOpening-equity;
        if(loss>=s.MaxDailyLoss||dayOpening>0&&loss/dayOpening*100>=s.MaxDailyLossPercent)return "일 손실 한도 도달";
        if(dailyOrders>=s.MaxDailyTrades)return "일 주문 횟수 한도";
        var value=o.Price*o.Quantity;var fee=value*settings.Execution.FeePercent/100;
        if(value>s.MaxOrderCash||value+(p?.Quantity??0)*o.Price>s.MaxSymbolCash)return "주문/종목 투자금 한도";
        if(o.Quantity+(p?.Quantity??0)>s.MaxShares)return "종목 보유수량 한도";
        var held=broker.Positions.Values.Count(x=>x.Quantity>0)+broker.Orders.Where(x=>x.Active&&x.Buy&&!broker.Positions.Any(p=>p.Key==x.Symbol&&p.Value.Quantity>0)).Select(x=>x.Symbol).Distinct().Count();
        if(!(p?.Quantity>0)&&held>=s.MaxPositions)return "동시 보유 종목 한도";
        if(value+fee>broker.Cash-broker.ReservedCash)return "예약 잔고 반영 후 현금 부족";
        var distance=Distance(c,o.Price,s.StopMode,s.StopValue);
        if(distance<=0)return "손절 계산/ATR 준비 부족";
        if(distance*o.Quantity+fee>s.MaxTradeLoss||distance/o.Price*100>s.MaxTradeLossPercent)return "건별 손실 한도";
        if(broker.Orders.Any(x=>x.Buy==o.Buy&&x.Symbol==o.Symbol&&x.Quantity==o.Quantity&&x.Price==o.Price&&x.State!=OrderState.Rejected&&(o.Created-x.Created).TotalSeconds<settings.Execution.DuplicateSeconds))return "동일 조건 중복 주문";
        return null;
    }
}
public sealed class PaperBroker(string currency,decimal initialCash,ExecutionSettings settings)
{
    public string Currency { get; }=currency;
    public decimal InitialCash { get; }=initialCash;
    public decimal Cash { get; private set; }=initialCash;
    public List<ManagedOrder> Orders { get; }=[];
    public Dictionary<string,Position> Positions { get; }=[];
    public List<Fill> Fills { get; }=[];
    public decimal ReservedCash=>Orders.Where(o=>o.Active&&o.Buy).Sum(o=>o.Remaining*o.Price*(1+settings.FeePercent/100));
    public decimal Equity=>Cash+Positions.Values.Sum(p=>p.Quantity*p.Last);
    public Action<string,object,DateTimeOffset>? Record { get; set; }
    public void Submit(ManagedOrder o){Orders.Add(o);Transition(o,OrderState.Submitted,o.Created);}
    private void Transition(ManagedOrder o,OrderState state,DateTimeOffset now){o.State=state;Record?.Invoke("주문",o,now);}
    public void RequestCancel(ManagedOrder o,DateTimeOffset now){if(o.Active&&o.State!=OrderState.Unknown)Transition(o,OrderState.CancelRequested,now);}
    public IReadOnlyList<ManagedOrder> Process(MarketEvent e)
    {
        if(e.Instrument.Currency!=Currency)throw new InvalidOperationException("통화 혼합 불가");
        if(Positions.TryGetValue(e.Key,out var pos))pos.Last=e.TradePrice??e.Bid??pos.Last;
        var cancelled=new List<ManagedOrder>();
        decimal liquidity=0;decimal? price=null;
        foreach(var o in Orders.Where(o=>o.Active&&o.Symbol==e.Key).ToArray())
        {
            if(o.State==OrderState.Unknown)continue;
            if(o.State==OrderState.CancelRequested){Transition(o,OrderState.Cancelled,e.ExchangeTime);cancelled.Add(o);continue;}
            if((e.ExchangeTime-o.Created).TotalMilliseconds>=(o.TimeoutMilliseconds??settings.TimeoutMilliseconds??settings.TtlSeconds*1000)){RequestCancel(o,e.ExchangeTime);continue;}
            // Never match the event that caused the order; match only later events after latency.
            if(e.ExchangeTime<=o.Created||(e.ExchangeTime-o.Created).TotalMilliseconds<settings.LatencyMs)continue;
            if(settings.UseSpread)
            {if(e.Kind!=EventKind.Quote)continue;price=o.Buy?e.Ask:e.Bid;liquidity=(o.Buy?e.AskSize:e.BidSize)??0;}
            else
            {if(e.Kind!=EventKind.Trade)continue;price=e.TradePrice;liquidity=e.TradeSize??0;if(o.Kind==OrderKind.Limit&&(o.Buy?price>=o.Price:price<=o.Price))continue;}
            if(price==null||liquidity<=0)continue;
            var fillPrice=price.Value*(1+(o.Buy?1:-1)*settings.SlippagePercent/100);
            if(o.Kind==OrderKind.Limit&&(o.Buy?fillPrice>o.Price:fillPrice<o.Price))continue;
            var qty=(int)Math.Min(o.Remaining,decimal.Floor(liquidity*settings.ParticipationPercent/100));
            if(!settings.AllowPartial&&qty<o.Remaining||qty<=0)continue;
            var fee=qty*fillPrice*(settings.FeePercent+(o.Buy?0:settings.TaxPercent))/100;
            if(o.Buy&&qty*fillPrice+fee>Cash){Transition(o,OrderState.Rejected,e.ExchangeTime);continue;}
            if(!Positions.TryGetValue(o.Symbol,out pos)){pos=new(o.Instrument);Positions[o.Symbol]=pos;}
            decimal realized=0;
            if(o.Buy){if(pos.Quantity==0){pos.Owner=o.Strategy;pos.OpenedAt=e.ExchangeTime;}pos.AveragePrice=(pos.AveragePrice*pos.Quantity+qty*fillPrice)/(pos.Quantity+qty);pos.Quantity+=qty;pos.CostFees+=fee;Cash-=qty*fillPrice+fee;}
            else {if(pos.Quantity<qty){Transition(o,OrderState.Rejected,e.ExchangeTime);continue;}var entryFee=pos.CostFees*qty/pos.Quantity;realized=(fillPrice-pos.AveragePrice)*qty-fee-entryFee;pos.CostFees-=entryFee;pos.Quantity-=qty;Cash+=qty*fillPrice-fee;}
            pos.Last=fillPrice;o.Filled+=qty;var fill=new Fill(o.Id,e.ExchangeTime,o.Instrument,o.Buy,qty,fillPrice,fee,realized,o.Strategy,o.Reason);Fills.Add(fill);Record?.Invoke("체결",fill,e.ExchangeTime);
            Transition(o,o.Remaining==0?OrderState.Filled:OrderState.PartiallyFilled,e.ExchangeTime);
            if(o.Remaining>0&&settings.CancelRemainder)RequestCancel(o,e.ExchangeTime);
        }
        return cancelled;
    }
}
public sealed class TradingEngine
{
    public const string ModelVersion="STOCK-quant-1.2/pipeline-opt-in-next-event-v1";
    public TradingSettings Settings { get; }
    public PaperBroker Broker { get; }
    public RiskManager Risk { get; }
    public Dictionary<string,MarketContext> Contexts { get; }=[];
    public List<Decision> Decisions { get; }=[];
    public List<EquityPoint> Equity { get; }=[];
    private readonly ITradingStrategy[] strategies=[new AdvancedStrategyGate(new ScalpingStrategy()),new AdvancedStrategyGate(new BoxStrategy()),new AdvancedStrategyGate(new ReboundStrategy())];
    private readonly CommonScanner scanner;
    private readonly Stock.Core.Pipeline.PipelineRuntime pipeline=new();
    private readonly Action<string,object,DateTimeOffset>? record;
    private DateOnly day;
    private decimal dayOpening;
    private int dailyOrders,counter;
    private DateTimeOffset now;
    public TradingEngine(TradingSettings settings,string currency,decimal cash,Action<string,object,DateTimeOffset>? record=null)
    {settings.Validate();Settings=settings.Clone();scanner=new(Settings);Risk=new(Settings);Broker=new(currency,cash,Settings.Execution){Record=record};this.record=record;dayOpening=cash;}
    private void Log(Decision d){Decisions.Add(d);if(Decisions.Count>300)Decisions.RemoveAt(0);record?.Invoke("판단",d,d.Time);}
    public void OnEvent(MarketEvent e,bool evaluate=true)
    {
        e.Validate();if(e.Instrument.Currency!=Broker.Currency)return;now=e.ExchangeTime;
        if(!Contexts.TryGetValue(e.Key,out var c)){c=new(e.Instrument,Settings);Contexts[e.Key]=c;}
        var d=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now,e.Instrument.Exchange==Exchange.KRX?"Korea Standard Time":"Eastern Standard Time").DateTime);
        if(day!=d){day=d;dayOpening=Broker.Equity;dailyOrders=0;}
        c.Add(e);var cancelled=Broker.Process(e);c.Position=Broker.Positions.GetValueOrDefault(e.Key);
        var dayLoss=dayOpening-Broker.Equity;
        if(dayLoss>=Settings.Risk.MaxDailyLoss||dayOpening>0&&dayLoss/dayOpening*100>=Settings.Risk.MaxDailyLossPercent)
            foreach(var pending in Broker.Orders.Where(o=>o.Active&&o.Buy).ToArray())Broker.RequestCancel(pending,now);
        foreach(var o in cancelled)if(Settings.Execution.Retry&&!Risk.Stopped&&o.Retry<Settings.Execution.MaxRetries&&o.Remaining>0)
            Submit(c,o.Buy,o.Remaining,o.Price+(o.Buy?1:-1)*Settings.Execution.RetryTicks*c.Instrument.Tick(c.Price),o.Strategy,"취소 확인 후 제한 재주문",o.Kind,o.Retry+1);
        if(Risk.Stopped){if(Settings.Risk.FlattenOnStop&&c.Position?.Quantity>0&&!Broker.Orders.Any(o=>o.Active&&o.Symbol==e.Key))Submit(c,false,c.Position.Quantity,c.Price,"긴급청산","별도 강제청산 설정",OrderKind.Market,allowStopExit:true);return;}
        if(Settings.Pipeline==null&&e.Kind==EventKind.Trade&&c.Position?.Quantity>0)
        {
            var p=c.Position;var stop=Risk.Distance(c,p.AveragePrice,Settings.Risk.StopMode,Settings.Risk.StopValue);var take=Risk.Distance(c,p.AveragePrice,Settings.Risk.TakeMode,Settings.Risk.TakeValue);
            if(stop>0&&c.Price<=p.AveragePrice-stop||take>0&&c.Price>=p.AveragePrice+take||Settings.Advanced is {} advanced&&p.OpenedAt is {} opened&&(now-opened).TotalSeconds>=advanced.Common.MaxHoldingSeconds)
            {foreach(var o in Broker.Orders.Where(o=>o.Active&&o.Symbol==e.Key))Broker.RequestCancel(o,now);if(!Broker.Orders.Any(o=>o.Active&&o.Symbol==e.Key))Submit(c,false,p.Quantity,c.Price,"리스크 청산","손절/익절/최대 보유시간 도달",OrderKind.Market);}
        }
        if(evaluate&&e.Kind==EventKind.Trade&&Settings.Pipeline==null)
        {
            var rejected=scanner.Reject(c);
            if(rejected!=null)Log(new(now,e.Key,"스캐너","제외",rejected,c.Price));
            else foreach(var strategy in strategies)
            {var signal=strategy.Evaluate(c);Log(new(now,e.Key,signal.Strategy,signal.Buy==null?"대기":signal.Buy.Value?"매수":"매도",signal.Reason,signal.Price));if(signal.Buy is bool buy&&signal.Price is decimal price){if(buy&&Settings.Advanced is {StrategyType:StrategyType.Rebound,Rebound.AutoBuy:false})continue;var qty=buy?Risk.Size(c,Math.Min(Broker.Equity,Settings.Risk.AccountCapital),price):c.Position?.Quantity??0;Submit(c,buy,qty,price,strategy.Name,signal.Reason,Settings.Execution.Kind);}}
        }
        if(evaluate&&e.Kind==EventKind.Trade&&Settings.Pipeline!=null)
        {
            var order=pipeline.Evaluate(c,Settings,Broker,out var reason);
            Log(new(now,e.Key,"Pipeline",order==null?"대기":order.Buy?"매수":"매도",reason??pipeline.Last!.Summary,c.Price));
            if(order!=null)Submit(c,order.Buy,order.Quantity,order.Price,order.Owner,order.Reason,order.Kind,timeout:order.TimeoutMilliseconds);
        }
        Equity.Add(new(now,Broker.Currency,Broker.Equity));
    }
    public ManagedOrder? Manual(Instrument instrument,bool buy,int qty,decimal price,OrderKind kind)
    {if(!Contexts.TryGetValue(instrument.Key,out var c)||now-c.Now>TimeSpan.FromSeconds(Settings.Execution.StaleSeconds))throw new InvalidOperationException("현재 세션의 신선한 시세가 필요합니다.");return Submit(c,buy,qty,price,"수동","사용자 주문",kind);}
    private ManagedOrder? Submit(MarketContext c,bool buy,int qty,decimal price,string strategy,string reason,OrderKind kind,int retry=0,bool allowStopExit=false,int? timeout=null)
    {
        if(Settings.Advanced is {} config&&kind==OrderKind.Limit&&strategy!="수동")price+=(buy?1:-1)*config.Execution.LimitOrderOffsetTicks*c.Instrument.Tick(price);
        price=decimal.Floor(price/c.Instrument.Tick(price))*c.Instrument.Tick(price);
        var o=new ManagedOrder{Id=(++counter).ToString("D8"),Instrument=c.Instrument,Buy=buy,Quantity=qty,Price=price,Kind=kind,Created=now,Strategy=strategy,Reason=reason,Retry=retry,TimeoutMilliseconds=timeout};
        var fail=allowStopExit&&!buy?null:Risk.Check(o,Broker,c,dayOpening,dailyOrders);
        if(fail==null&&Settings.Advanced is {} advanced)fail=AdvancedPaperRisk.Check(advanced,o,Broker);
        if(fail!=null){Log(new(now,c.Instrument.Key,strategy,"리스크 거절",fail,price));return null;}
        if(buy&&Settings.Execution.UseSpread&&!c.QuoteFresh){Log(new(now,c.Instrument.Key,strategy,"리스크 거절","호가 stale/미수신",price));return null;}
        Broker.Submit(o);dailyOrders++;return o;
    }
    public void EmergencyStop(){Risk.Stop();foreach(var o in Broker.Orders.Where(o=>o.Active).ToArray())Broker.RequestCancel(o,now);Log(new(now,"전체","긴급중지","중지","신규 주문 차단·미체결 취소 요청"));}
    public void Cancel(string id){var o=Broker.Orders.FirstOrDefault(x=>x.Id==id)??throw new InvalidOperationException("주문번호 없음");Broker.RequestCancel(o,now);}
}
public sealed class BacktestRunner(TradingStore store)
{
    public BacktestResult Run(TradingSettings settings,IReadOnlyList<MarketEvent> data,CancellationToken token,IProgress<int>? progress=null)
    {
        settings.Validate();var instrument=Instrument.Parse(settings.Backtest.Instrument);if(data.Count==0)throw new InvalidOperationException("선택 기간/종목/소스의 저장 데이터가 없습니다.");
        if(data.Any(e=>e.Instrument!=instrument))throw new InvalidOperationException("백테스트는 한 통화/종목의 데이터로 실행하세요.");
        var ordered=data.Select((e,i)=>(e,i)).OrderBy(x=>x.e.ExchangeTime).ThenBy(x=>x.i).Select(x=>x.e).ToList();
        var hash=TradingStore.Hash(string.Join('\n',ordered.Select(Serialization.Pack)));var run=store.BeginRun("Backtest:"+TradingEngine.ModelVersion,settings,hash);var engine=new TradingEngine(settings,instrument.Currency,settings.Backtest.InitialCash,(cat,obj,t)=>store.Audit(run,cat,obj,t));
        int processed=0;string status="완료";var quality=new DataQualityGate(settings.Data,settings.Execution);
        try{foreach(var e in ordered){token.ThrowIfCancellationRequested();if(quality.Accept(e,false,out var reason))engine.OnEvent(e);else store.Audit(run,"데이터 제외",new{e.Key,reason},e.ExchangeTime);processed++;if(processed%100==0)progress?.Report(processed*100/ordered.Count);}}
        catch(OperationCanceledException){status="취소";}
        // Open positions remain marked to observed prices; no fictional end-of-test liquidation.
        decimal peak=engine.Broker.InitialCash,drawdown=0;foreach(var point in engine.Equity){peak=Math.Max(peak,point.Equity);if(peak>0)drawdown=Math.Max(drawdown,(peak-point.Equity)/peak*100);}
        var sells=engine.Broker.Fills.Where(f=>!f.Buy).ToList();var result=new BacktestResult(run,processed,engine.Broker.Fills.Count,engine.Broker.Fills.Sum(f=>f.Fee),engine.Broker.Equity-engine.Broker.InitialCash,drawdown,sells.Count==0?0:100m*sells.Count(f=>f.RealizedPnl>0)/sells.Count,instrument.Currency,hash,status,engine.Equity,engine.Broker.Fills);
        store.FinishRun(run,new{Result=result,OpenOrders=engine.Broker.Orders.Where(o=>o.Active),OpenPositions=engine.Broker.Positions.Values.Where(p=>p.Quantity>0),Model=TradingEngine.ModelVersion});progress?.Report(100);return result;
    }
}
