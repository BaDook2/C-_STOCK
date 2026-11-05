using Stock.Core.Trading;

namespace Stock.Core.Research.Oversold;

public sealed record RealtimeScreeningPlan(LiveBatchItem[] Items,string StrategyName,BarSpecification BarSpecification);
public sealed record RealtimeScreeningSignal(string Key,string Name,DateTimeOffset SignalTime,string Reason,bool AutoTradingEligible);
public sealed record RealtimeScreeningStatusRow(
    string Key,string Name,string Code,decimal? Price,decimal? ChangePercent,decimal Proximity,
    int MetConditions,int TotalConditions,string UnmetConditions,string StrategyState,
    DateTimeOffset? LastAnalyzedAt,string DataState,string Error,bool Focused,bool BuySignal);
public sealed record RealtimeScreeningSnapshot(
    bool Running,bool AutoTradingEnabled,string Status,string ConnectionState,string StrategyName,
    BarSpecification? BarSpecification,IReadOnlyList<RealtimeScreeningStatusRow> Rows);

/// <summary>
/// Application-lifetime real-time research service. It reuses OversoldLiveStrategy for causal bar
/// construction and entry evaluation, but never submits an order. Order coordination is a separate
/// consumer of SignalRaised and the existing batch execution services.
/// </summary>
public sealed class RealtimeOversoldScreeningService
{
    private sealed class Row
    {
        public readonly LiveBatchItem Item;
        public readonly MarketContext Context;
        public readonly OversoldLiveStrategy Strategy;
        public readonly DataQualityGate Quality;
        public DateTimeOffset? LastSignalTime;
        public DateTimeOffset? LastReceived;
        public CurrentMarketSnapshot? PublicSnapshot;
        public string DataState="실시간 시세 대기",Error="";
        public bool Enabled=true;
        public Row(LiveBatchItem item)
        {
            Item=item;
            Context=new(new(item.Stock.Symbol,item.Stock.Exchange),item.Settings);
            var research=item.Oversold! with {ResearchOnly=true};
            Strategy=new(Context.Instrument,research);
            Quality=new(item.Settings.Data,item.Settings.Execution);
        }
    }

    private readonly object sync=new();
    private readonly Func<DateTimeOffset> clock;
    private readonly Dictionary<string,Row> rows=[];
    private bool running,autoTrading;
    private DateTimeOffset autoEnabledAt;
    private string status="중지",connectionState="연결 안 됨",strategyName="";
    private BarSpecification? specification;

    public event Action<RealtimeScreeningSignal>? SignalRaised;
    public RealtimeOversoldScreeningService(Func<DateTimeOffset>? clock=null)
    {this.clock=clock??(()=>DateTimeOffset.UtcNow);}

    public void Start(RealtimeScreeningPlan plan)
    {
        if(plan.Items.Length is <1 or >100)throw new InvalidOperationException("실시간 스크리닝 대상은 1~100개여야 합니다.");
        if(plan.Items.Select(i=>i.Stock.Key).Distinct().Count()!=plan.Items.Length)throw new InvalidOperationException("실시간 스크리닝 종목이 중복되었습니다.");
        plan.BarSpecification.Validate();
        lock(sync)
        {
            if(running)throw new InvalidOperationException("기존 실시간 스크리닝을 먼저 중지하세요.");
            rows.Clear();
            foreach(var item in plan.Items)
            {
                if(item.Stock.Exchange!=Exchange.KRX||item.Oversold==null)throw new InvalidOperationException(item.Stock.Name+": 국내 전략 준비 자료를 확인하세요.");
                item.Settings.Validate();item.Oversold.Configuration.Validate();
                rows.Add(item.Stock.Key,new Row(item));
            }
            strategyName=plan.StrategyName;specification=plan.BarSpecification;running=true;
            autoTrading=false;autoEnabledAt=DateTimeOffset.MaxValue;connectionState="시세 연결 중";
            status=$"{rows.Count:N0}종목 실시간 조건 감시 준비";
        }
    }

    public void SetConnectionState(string value)
    {lock(sync){connectionState=string.IsNullOrWhiteSpace(value)?"상태 미확인":value.Trim();}}

    public void SetAutoTrading(bool enabled)
    {
        lock(sync)
        {
            autoTrading=enabled;
            autoEnabledAt=enabled?clock():DateTimeOffset.MaxValue;
            status=enabled?"자동매매 활성 · 이후 새 신호만 전달":"신호 감시 중 · 자동매매 꺼짐";
        }
    }

    public void OnMarket(MarketEvent marketEvent)
    {
        lock(sync)
        {
            if(!running||!rows.TryGetValue(marketEvent.Key,out var row)||!row.Enabled)return;
            try
            {
                if(!row.Quality.Accept(marketEvent,true,out var reason))
                {row.DataState="시세 제외 · "+reason;return;}
                row.Context.Add(marketEvent);row.Strategy.OnMarket(marketEvent);
                row.LastReceived=clock();row.DataState="실시간 수신";
            }
            catch(Exception ex)
            {
                row.Enabled=false;row.DataState="종목 오류";
                row.Error=ex is InvalidOperationException?ex.Message:"유효하지 않은 실시간 시세";
            }
        }
    }

    /// <summary>Public completed candles feed the existing detector without creating raw executions.</summary>
    public void OnPublicBar(string key,StrategyBar bar)
    {
        lock(sync)
        {
            if(!running||!rows.TryGetValue(key,out var row)||!row.Enabled||row.Item.Oversold?.PublicPaper!=true)return;
            try{row.Strategy.OnPublicBar(bar);}
            catch(Exception ex){row.Enabled=false;row.DataState="전략 오류";row.Error=ex is InvalidOperationException?ex.Message:"공개 분봉 분석 실패";}
        }
    }

    /// <summary>Public quote observations update research state only; they are never stored as ticks or routed to an account.</summary>
    public void OnPublicSnapshot(CurrentMarketSnapshot snapshot)
    {
        lock(sync)
        {
            if(!running||!rows.TryGetValue(snapshot.Stock.Key,out var row)||!row.Enabled||row.Item.Oversold?.PublicPaper!=true)return;
            var previous=row.PublicSnapshot;
            if(previous!=null&&snapshot.Quote.TradeTime<=previous.Quote.TradeTime)return;
            try
            {
                var delta=previous!=null&&DailyQuoteMerger.Date(previous.Quote.TradeTime,Exchange.KRX)==DailyQuoteMerger.Date(snapshot.Quote.TradeTime,Exchange.KRX)
                    ?Math.Max(0,(snapshot.Volume??0)-(previous.Volume??0)):0;
                var observation=new MarketEvent(row.Context.Instrument,EventKind.Trade,snapshot.Quote.TradeTime,snapshot.ReceivedAt,"NAVER_SCREEN_OBSERVATION",snapshot.Quote.TradeTime.UtcTicks)
                {TradePrice=snapshot.Quote.Price,TradeSize=delta,PreviousClose=snapshot.Quote.Price-snapshot.Quote.Change,TimeQuality="공개 현재가 · 원본 체결 아님"};
                row.Context.Add(observation);row.PublicSnapshot=snapshot;row.LastReceived=clock();
                var fresh=snapshot.Quote.State=="OPEN"&&(clock()-snapshot.ReceivedAt).Duration()<=TimeSpan.FromSeconds(20)
                    &&(clock()-snapshot.Quote.TradeTime).Duration()<=TimeSpan.FromSeconds(20);
                if(fresh){row.Strategy.OnPublicObservation(snapshot.Quote.TradeTime);row.DataState="공개 시세 수신 · 지연 가능";}
                else row.DataState=snapshot.Quote.State=="OPEN"?"공개 시세 지연 · 신호 보류":"장 마감 · 마지막 공개가";
            }
            catch(Exception ex){row.Enabled=false;row.DataState="종목 오류";row.Error=ex is InvalidOperationException?ex.Message:"유효하지 않은 공개 시세";}
        }
    }

    public void Pulse()
    {
        List<RealtimeScreeningSignal> signals=[];
        lock(sync)
        {
            if(!running)return;
            foreach(var row in rows.Values)
            {
                if(!row.Enabled||row.Context.Last==null)continue;
                try
                {
                    var publicData=row.Item.Oversold?.PublicPaper==true;
                    var staleSeconds=publicData?20:Math.Min(10,row.Item.Settings.Execution.StaleSeconds);
                    if(row.LastReceived is not {} received||(clock()-received).Duration().TotalSeconds>staleSeconds)
                    {row.DataState="시세 지연 · 신규 주문 차단";continue;}
                    if(publicData&&row.DataState!="공개 시세 수신 · 지연 가능")continue;
                    var signal=row.Strategy.Evaluate(row.Context);
                    if(signal.Buy==true&&row.Strategy.ReadySignalTime is {} signalTime&&row.LastSignalTime!=signalTime)
                    {
                        row.LastSignalTime=signalTime;
                        signals.Add(new(row.Item.Stock.Key,row.Item.Stock.Name,signalTime,signal.Reason,autoTrading&&signalTime>autoEnabledAt));
                    }
                }
                catch(Exception ex)
                {row.Enabled=false;row.DataState="전략 오류";row.Error=ex is InvalidOperationException?ex.Message:"전략 평가 실패";}
            }
            if(rows.Values.All(r=>!r.Enabled)){running=false;status="모든 종목 감시 중지";}
        }
        foreach(var signal in signals)SignalRaised?.Invoke(signal);
    }

    public void FeedFault(string reason)
    {
        lock(sync)
        {
            running=false;connectionState="연결 끊김";autoTrading=false;autoEnabledAt=DateTimeOffset.MaxValue;
            foreach(var row in rows.Values.Where(r=>r.Enabled)){row.DataState="피드 중단 · 신규 주문 차단";if(row.Error=="")row.Error=reason;}
            status="실시간 시세 중단 · 자동매매 해제";
        }
    }

    public void Stop(string reason="사용자 중지")
    {
        lock(sync)
        {
            running=false;autoTrading=false;autoEnabledAt=DateTimeOffset.MaxValue;connectionState="중지";status=reason;
            foreach(var row in rows.Values){row.Enabled=false;if(row.DataState=="실시간 수신")row.DataState="중지";}
        }
    }

    public RealtimeScreeningSnapshot Snapshot()
    {
        lock(sync)
        {
            var output=rows.Values.Select(ToStatus).OrderByDescending(r=>r.BuySignal).ThenByDescending(r=>r.Proximity).ThenBy(r=>r.Name,StringComparer.Ordinal).ToArray();
            return new(running,autoTrading,status,connectionState,strategyName,specification,output);
        }
    }

    private static RealtimeScreeningStatusRow ToStatus(Row row)
    {
        var evaluation=row.Strategy.LatestEntryEvaluation;
        var relevant=evaluation?.Conditions.Where(c=>!c.Diagnostic&&c.Condition is not("전체" or "최종")&&c.Outcome!=ConditionOutcome.Disabled).ToArray()??[];
        int met=relevant.Count(c=>c.Outcome==ConditionOutcome.Passed),total=relevant.Length;
        var unmet=relevant.Where(c=>c.Outcome!=ConditionOutcome.Passed).Select(c=>$"{c.Condition}({Outcome(c.Outcome)})").ToArray();
        var proximity=evaluation==null?0:RealtimeConditionProximity.Calculate(evaluation,row.Item.Oversold!.Configuration);
        var buy=row.Strategy.ReadySignalTime!=null;
        var state=buy?"매수 신호 발생":evaluation?.State switch
        {
            OversoldState.WaitingForRebound=>"집중 감시 · 반등 대기",
            OversoldState.SupportApproached=>"집중 감시 · 지지 접근",
            OversoldState.CrashTriggered=>"집중 감시 · 급락 감지",
            _ when proximity>=60=>"집중 감시",
            _=>"관찰 후보"
        };
        var last=row.Context.Last;decimal? change=last?.PreviousClose is >0&&row.Context.Price>0?(row.Context.Price/last.PreviousClose.Value-1)*100:null;
        return new(row.Item.Stock.Key,row.Item.Stock.Name,row.Item.Stock.Symbol,row.Context.Price>0?row.Context.Price:null,change,proximity,met,total,
            unmet.Length==0?"없음":string.Join(", ",unmet),state,row.Strategy.LastAnalyzedAt==default?null:row.Strategy.LastAnalyzedAt,row.DataState,row.Error,state.StartsWith("집중",StringComparison.Ordinal)||buy,buy);
    }

    private static string Outcome(ConditionOutcome outcome)=>outcome switch
    {ConditionOutcome.Failed=>"미충족",ConditionOutcome.MissingData=>"자료 부족",ConditionOutcome.NotEvaluated=>"미평가",_=>outcome.ToString()};
}

/// <summary>Equal-condition proximity; a research distance score, never a probability.</summary>
public static class RealtimeConditionProximity
{
    public static decimal Calculate(EntryEvaluation evaluation,OversoldConfig config)
    {
        var conditions=evaluation.Conditions.Where(c=>!c.Diagnostic&&c.Condition is not("전체" or "최종")&&c.Outcome!=ConditionOutcome.Disabled).ToArray();
        if(conditions.Length==0)return 0;
        decimal Score(ConditionEvaluation c)
        {
            if(c.Outcome==ConditionOutcome.Passed)return 1;
            if(c.Outcome is ConditionOutcome.MissingData or ConditionOutcome.NotEvaluated)return 0;
            return c.Condition switch
            {
                "Crash"=>Math.Clamp(Math.Max(Ratio(evaluation.Indicators.MediumDrawdown,config.MinimumDrawdownPercent),Ratio(evaluation.Indicators.ShortDrawdown,config.ShortCrashPercent)),0,1),
                "RSI" when evaluation.Indicators.Rsi is {} r&&config.MaximumRsi<100=>Math.Clamp((100-r)/(100-config.MaximumRsi),0,1),
                "Volume" when evaluation.Indicators.VolumeRatio is {} v&&config.CrashVolumeRatio>0=>Math.Clamp(v/config.CrashVolumeRatio,0,1),
                _=>0
            };
        }
        return decimal.Round(100*conditions.Sum(Score)/conditions.Length,1,MidpointRounding.AwayFromZero);
    }
    private static decimal Ratio(decimal? current,decimal required)=>current.HasValue&&required<0?Math.Abs(current.Value)/Math.Abs(required):0;
}
