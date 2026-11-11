using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Core.Strategies.Injes;
using StrategySignal=Stock.Core.Strategies.Injes.StrategySignal;
namespace Stock.Desktop;

public sealed record RealtimeItemRequest(StockItem Stock,string Name,OversoldConfig Configuration,decimal Budget);
public sealed record RealtimeRequest(StockItem[] Stocks,string Name,OversoldConfig Configuration,BarSpecification Specification,HistoricalFacts Facts,bool PaperAssumptions,decimal TotalBudget,decimal DailyLossLimit)
{public RealtimeItemRequest[]? ItemConfigurations {get;init;}}
/// <summary>Original tick/quote Inje host; the existing screen/table consumes the shared session snapshot.</summary>
public sealed class RealtimeScreeningHost:IAsyncDisposable
{
    private readonly AppServices services;private readonly Func<IAccountBroker?> account;private readonly InjesStreamService stream;
    private RealtimeRequest? request;private readonly Dictionary<string,StockItem> stocks=[];
    public RealtimeScreeningHost(AppServices services,MarketHistoryService history,Func<IAccountBroker?> account)
    {
        this.services=services;this.account=account;stream=new(services.Store,services.Clock);stream.StatusChanged+=value=>Progress?.Invoke(value);
        stream.FillConfirmed+=fill=>RecordFill(fill,true);
    }
    private void RecordFill(Fill fill,bool notify)
    {
        var evidence=services.Store.State<InjesSessionJournal>("inje-live:"+stream.RunId)?.Checkpoint?.OrderEvidence.GetValueOrDefault(fill.OrderId);
        var marker=new TradeMarker("inje:"+stream.RunId+":"+fill.OrderId+":"+fill.Time.UtcTicks+":"+fill.Quantity,fill.Instrument.Key,fill.Time,fill.Buy,fill.Quantity,fill.Price,"Inje 가상",Reason:fill.Reason,RealizedPnl:fill.Buy?null:fill.RealizedPnl,AccountScope:"inje-paper:"+stream.RunId,CrashJudgmentLow:fill.Buy?evidence?.Metrics.GetValueOrDefault("crashLow"):null);
        services.Store.State("trade-marker:"+marker.Id,marker);if(notify)FillRecorded?.Invoke(marker);
    }
    public event Action<string>? Progress;public event Action<TradeMarker>? FillRecorded;
    public bool HasProtectedPositions=>stream.HasUnsettled||stream.HasSavedUnsettled;public bool Active=>stream.Running;
    public bool RestoreAvailable=>!stream.Running&&!stream.HasUnsettled&&stream.HasSavedUnsettled;public bool CanResume=>!stream.Running&&stream.Session!=null;public LiveBatchPlan? RecoveredPlan {get;private set;}
    public string PaperRunId=>stream.RunId;public string Error=>"";
    public RealtimeScreeningSnapshot Snapshot
    {
        get
        {
            var snapshot=stream.Snapshot();var rows=stocks.Values.Select(stock=>
            {
                var e=snapshot?.Evaluations.FirstOrDefault(e=>e.Signal.Symbol==stock.Key);var signal=e?.Signal;var conditions=signal?.Conditions??[];var blocks=signal?.BlockReasons??[];
                var state=e?.State??(snapshot?.Ledger.Positions.Any(p=>p.Instrument.Key==stock.Key&&p.Quantity>0)==true?StrategyState.Holding:StrategyState.Idle);
                return new RealtimeScreeningStatusRow(stock.Key,stock.Name,stock.Symbol,signal?.ReferencePrice,signal?.Metrics.GetValueOrDefault("dayGainPercent"),0,conditions.Count(c=>c.Outcome is EvaluationOutcome.Passed or EvaluationOutcome.Disabled),conditions.Length,string.Join(" · ",blocks),state.ToString(),signal?.SignalTime,"원본 틱·호가 · "+(signal?.SignalReason??"MissingData: 아직 수신하지 못함"),"",false,signal?.SignalType==SignalType.Enter&&signal.RiskApproved){MatchesStrategy=signal?.SignalType==SignalType.Enter&&signal.RiskApproved,NearCandidate=state is StrategyState.Candidate or StrategyState.BuyPending or StrategyState.Holding or StrategyState.SellPending or StrategyState.Cooldown||e?.Attention==EvaluationOutcome.Passed,MissingConditions=signal==null||signal.SignalReason.Contains("MissingData")||conditions.Any(c=>c.Outcome==EvaluationOutcome.MissingData),LastQuoteTime=signal?.UsedObservations.FirstOrDefault(o=>o.Kind=="Quote")?.ReceivedAt};
            }).ToArray();
            var missing=rows.Count(r=>r.MissingConditions);var status=stream.Status+(missing>0?$" · MissingData {missing}/{rows.Length}종목 · 전체 판단 표시에서 자료 사유 확인":"");
            return new(stream.Running,snapshot?.EntriesEnabled??false,status,"원본 체결/호가 공급자 · 실제 주문 없음",request?.Name??"Inje's Strategy",null,rows);
        }
    }
    public LiveBatchStatus PaperSnapshot
    {
        get
        {
            var snapshot=stream.Snapshot();var ledger=snapshot?.Ledger;return new("Inje USD 가상 계좌",stream.Running,stream.Status,stocks.Values.Select(s=>
            {var p=ledger?.Positions.FirstOrDefault(p=>p.Instrument.Key==s.Key);var latest=snapshot?.Evaluations.FirstOrDefault(e=>e.Signal.Symbol==s.Key);return new LiveBatchRowStatus(s.Key,s.Name,request?.Name??"Inje",stream.Running,latest?.Signal.SignalReason??"자료 대기",latest?.Signal.SignalType.ToString()??"Hold",p?.Quantity??0,ledger?.Orders.Where(o=>o.Active&&o.Symbol==s.Key).Sum(o=>o.Remaining)??0,p?.Unrealized??0,"");}).ToArray());
        }
    }
    public Task Start(RealtimeRequest value,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();if(Active||HasProtectedPositions)throw new InvalidOperationException("기존 Inje 실행/보유/미체결을 먼저 확인하세요.");
        if(value.Configuration.Bins!=null)throw new InvalidOperationException("Bin 백테스트·스크리닝은 연결되어 있습니다. 실시간 가상 주문은 원장 복원 연결 전까지 시작할 수 없습니다. Inje로 대체 실행하지 않습니다.");
        var definition=InjesPresetStore.Require(value.Configuration);
        if(value.Stocks.Length is <1 or >20||value.Stocks.DistinctBy(s=>s.Key).Count()!=value.Stocks.Length||value.Stocks.Any(s=>s.Currency!="USD"))throw new InvalidOperationException("Inje는 중복 없는 미국 종목1~20개를 동일 전략으로 감시합니다.");
        if(value.ItemConfigurations is {} items)
        {
            if(items.Length!=value.Stocks.Length||items.Any(i=>!value.Stocks.Any(s=>s.Key==i.Stock.Key)))throw new InvalidOperationException("종목별 설정 목록을 확인하세요.");
            var canonical=Canonical(definition);
            if(items.Any(i=>Canonical(InjesPresetStore.Require(i.Configuration))!=canonical))throw new InvalidOperationException("한 실행 세션에서는 동일한 Inje 전략·파라미터만 선택하세요. 전략을 자동 혼합하지 않습니다.");
            if(items.Any(i=>i.Budget<definition.CommonParameters.OrderBudget)||items.Sum(i=>i.Budget)>value.TotalBudget)throw new InvalidOperationException("종목 예산과 전체 한도를 확인하세요.");
        }
        if(value.TotalBudget<=0||value.DailyLossLimit<=0||value.DailyLossLimit>value.TotalBudget)throw new InvalidOperationException("USD 전체 한도와 일 손실 한도를 확인하세요.");
        var broker=account()??throw new InvalidOperationException("MissingData: 미국 원본 틱·호가 연결이 필요합니다. 공개 일봉/현재가 폴링을 틱으로 대체하지 않습니다.");
        definition.InitialCash=value.TotalBudget;definition.CommonParameters.MaxDailyLossCash=Math.Min(definition.CommonParameters.MaxDailyLossCash,value.DailyLossLimit);definition.ExecutionPolicy.MaximumExposureCash=Math.Min(definition.ExecutionPolicy.MaximumExposureCash,value.TotalBudget);
        var instruments=value.Stocks.Select(s=>new Instrument(s.Symbol,s.Exchange)).ToArray();var feed=services.LiveFeedFactory(broker,instruments,new(),message=>Progress?.Invoke(message));
        request=value;stocks.Clear();foreach(var stock in value.Stocks)stocks[stock.Key]=stock;stream.Start(definition,instruments,feed);return Task.CompletedTask;
    }
    private static string Canonical(InjesStrategyDefinition d)=>Serialization.Pack(new{d.FamilyId,d.StrategyId,d.StrategyMode,d.CommonParameters,d.StrategyParameters,d.StandardParameters,d.EffectiveAdvancedParameters,d.ExecutionPolicy});
    public void SetAutomatic(bool enabled)=>stream.SetEntriesEnabled(enabled);
    public void StopResearch()=>stream.StopEntries();public void Emergency(string reason="긴급중지")=>stream.Emergency();
    public RealtimeScreeningChartData ChartData(string key)
    {
        var stock=stocks.GetValueOrDefault(key)??throw new InvalidOperationException("감시 종목이 없습니다.");
        var rows=stream.Trades(key).GroupBy(e=>e.ExchangeTime.UtcTicks/TimeSpan.TicksPerMinute).Select((g,n)=>
        {var first=g.First();var last=g.Last();return new StrategyBar(new(first.ExchangeTime,first.TradePrice!.Value,g.Max(e=>e.TradePrice!.Value),g.Min(e=>e.TradePrice!.Value),last.TradePrice!.Value,g.Sum(e=>e.TradeSize!.Value),false),first.ExchangeTime,last.ExchangeTime,n+1,null,"원본 체결의 표시용 1분 집계 · 전략 판단과 무관");}).ToArray();
        var now=services.Clock();var daily=stream.Trades(key).LastOrDefault(e=>e.StrategyReference?.DailyBars.Length>0)?.StrategyReference?.DailyBars.Where(b=>b.KnownAt>=b.ClosedAt&&b.KnownAt<=now&&b.ClosedAt<=now&&b.Bar.IsComplete).Select(b=>b.Bar).ToArray()??[];
        if(daily.Length==0)daily=(services.Store.State<BarDataset>("bars:"+key)?.CompletedBars??[]).Where(b=>b.Time<now).ToArray();
        return new(stock,request?.Name??"Inje",request!.Configuration,new(1,BarUnit.Minute),new(),rows){DailyIndicatorBars=daily};
    }
    public void RestorePaper()
    {
        var journal=stream.RestoreLatest();stocks.Clear();foreach(var instrument in journal.Instruments)stocks[instrument.Key]=new(instrument.Symbol,instrument.Symbol,instrument.Exchange,instrument.Symbol);
        foreach(var fill in journal.Ledger.Fills)RecordFill(fill,false);
        request=new(stocks.Values.ToArray(),journal.Definition.StrategyName,InjesPresetStore.Wrap(journal.Definition),new(1,BarUnit.Day),new(),false,journal.Definition.InitialCash,journal.Definition.CommonParameters.MaxDailyLossCash);
        RecoveredPlan=new(stocks.Values.Select(stock=>new LiveBatchItem(stock,journal.Definition.StrategyName,InjesTradingSettings.Create(journal.Definition),journal.Definition.CommonParameters.OrderBudget)
        {Oversold=new(InjesPresetStore.Wrap(journal.Definition),new(1,BarUnit.Day),[],new())}).ToArray(),journal.Definition.InitialCash,journal.Definition.CommonParameters.MaxDailyLossCash);
    }
    public StrategySignal? Evidence(TradeMarker marker)
    {
        if(!marker.AccountScope.StartsWith("inje-paper:",StringComparison.Ordinal))return null;
        var journal=services.Store.State<InjesSessionJournal>("inje-live:"+marker.AccountScope["inje-paper:".Length..]);
        var fill=journal?.Ledger.Fills.FirstOrDefault(f=>marker.Id=="inje:"+journal.Id+":"+f.OrderId+":"+f.Time.UtcTicks+":"+f.Quantity);
        return fill==null?null:journal!.Checkpoint?.OrderEvidence.GetValueOrDefault(fill.OrderId);
    }
    public Task ResumePaper(bool assumptions,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var broker=account()??throw new InvalidOperationException("미국 원본 틱·호가 연결을 먼저 준비하세요.");
        if(stream.Session==null)throw new InvalidOperationException("보관된 Inje 원장을 먼저 복원하세요.");
        var instruments=stocks.Values.Select(s=>new Instrument(s.Symbol,s.Exchange)).ToArray();var feed=services.LiveFeedFactory(broker,instruments,new(),message=>Progress?.Invoke(message));stream.ResumeProtection(feed);return Task.CompletedTask;
    }
    public ValueTask DisposeAsync()=>stream.DisposeAsync();
}
