using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record IntradayEntrySignal(string Id,string Symbol,string StrategyInstance,DateTimeOffset Time,decimal ReferencePrice,
    string Reason,decimal? ZoneLow,decimal? ZoneCenter,decimal? ZoneHigh,string EvidenceJson,decimal MaxEntryDistancePercent=0);
public sealed record IntradayExecutionContext(bool HasPosition,bool PendingBuy,int SymbolEntries,int StrategyEntries,int AccountEntries,
    DateTimeOffset? LastExit,string LastExitReason,IntradayEntrySignal? PriorEntry,int ExitBarOrdinal);
public sealed record DayTradingExecutionProfile(string InstanceId,OversoldConfig Parameters,ExitPolicy Exit,SessionPolicy Session,AccountRiskPolicy Risk,ReentryPolicy Reentry,DataQualityPolicy Quality);

/// <summary>A generic strategy port. Replay/paper/live execution consume the same market and signal contract.</summary>
public interface IDayTradingSignalRuntime
{
    Instrument Instrument {get;}
    DayTradingExecutionProfile Profile {get;}
    DailyMarketSession? CurrentSession {get;}
    decimal CurrentPrice {get;}
    int AnalysisBarOrdinal {get;}
    bool ClosedAnalysis {get;}
    int TradingDayOrdinal=>0;
    int TradingDayOrdinalAt(DateTimeOffset at)=>TradingDayOrdinal;
    bool ResearchOnly=>false;
    bool CalendarCloseResearch=>false;
    Stock.Core.Research.PriceBar? ClosedBarEnvelope=>null;
    string State {get;}
    FeedObservation OnMarket(MarketEvent market,DateTimeOffset now);
    IntradayEntrySignal? Evaluate(IntradayExecutionContext context);
    IntradayEntrySignal? EvaluateAdditional(IntradayExecutionContext context,int entryCount,decimal lastFill,bool harvestStarted)=>null;
}
public interface IClosedBarSignalRuntime:IDayTradingSignalRuntime
{
    DailyMarketSession ValidateClosedBar(StrategyBar bar);
    void PrepareBarOpening(StrategyBar bar);
    FeedObservation OnClosedBar(StrategyBar bar);
}

/// <summary>D is an adapter of the common strategy port, not a second paper/backtest implementation.</summary>
public sealed class StrategyDRuntime:IClosedBarSignalRuntime
{
    private readonly StrategyDefinition definition;
    private readonly StrategyDDataEngine data;
    private readonly StrategyDSignalEngine signals;
    private readonly DailyMarketSession[] sessions;
    private DailyMarketSession? clockSession;
    private bool scaleCondition;
    private int scaleStage;
    private long scaleSequence;
    public Instrument Instrument {get;}
    public DayTradingExecutionProfile Profile {get;}
    public DailyMarketSession? CurrentSession=>clockSession??data.Latest?.Session;
    public decimal CurrentPrice=>data.Latest?.Price??0;
    public int AnalysisBarOrdinal=>data.Latest?.AnalysisBarOrdinal??0;
    public bool ClosedAnalysis=>data.LatestClosedAnalysis!=null;
    public int TradingDayOrdinal=>CurrentSession is {} current?Array.FindIndex(sessions,s=>s.Date==current.Date):0;
    public int TradingDayOrdinalAt(DateTimeOffset at)=>Array.FindIndex(sessions,s=>at>=s.Open&&at<=s.Close);
    public bool ResearchOnly {get;}
    public bool CalendarCloseResearch=>ResearchOnly&&definition.DayTrading!.SimpleTimeframe.Unit is TimeframeUnit.TradingDay or TimeframeUnit.Week or TimeframeUnit.Month;
    public Stock.Core.Research.PriceBar? ClosedBarEnvelope {get;private set;}
    public string State=>signals.State.Stage+" · "+signals.State.Reason;
    public StrategyDSignalState SignalState=>signals.State;
    public StrategyDObservation? Observation=>data.Latest;
    public int RetainedTradeEvents=>data.RetainedTradeEvents;
    public Stock.Core.Research.PriceBar[] AnalysisPrices()=>data.AnalysisPrices();
    public StrategyDRuntime(Instrument instrument,StrategyDefinition definition,StrategyDDataSeed seed,string instance,bool closedBarResearch=false)
    {
        this.definition=definition.Snapshot();ResearchOnly=closedBarResearch;Instrument=instrument;sessions=seed.Facts.Sessions.OrderBy(s=>s.Open).ToArray();data=new(instrument,this.definition,seed,closedBarResearch);signals=new(instrument,this.definition,seed.Facts.Sessions,instance,CalendarCloseResearch);
        var p=this.definition.DayTrading!;Profile=new(instance,this.definition.Configuration,p.Exit,p.Session,p.Risk,p.Reentry,p.Quality);
    }
    public FeedObservation OnMarket(MarketEvent e,DateTimeOffset now)
    {
        var observation=data.OnMarket(e,now);
        if(!observation.Accepted&&observation.Decision!=FeedDecision.Duplicate)signals.Invalidate(observation.Reason);
        return observation;
    }
    public void SupplyFrames(IEnumerable<KnownStrategyBar> frames,DateTimeOffset asOf)=>data.SupplyFrames(frames,asOf);
    public DailyMarketSession ValidateClosedBar(StrategyBar bar)=>data.ValidateClosedBar(bar);
    public void PrepareBarOpening(StrategyBar bar)=>clockSession=data.ValidateClosedBar(bar);
    public FeedObservation OnClosedBar(StrategyBar bar)
    {var result=data.OnClosedBar(bar);clockSession=result.Session;ClosedBarEnvelope=bar.Price;return result;}
    public IntradayEntrySignal? Evaluate(IntradayExecutionContext context)
    {
        if(data.Latest is not {} latest)return null;
        StrategyDSupport? prior=null;
        if(context.PriorEntry is {} entry)prior=Serialization.Unpack<StrategyDSignal>(entry.EvidenceJson).Support;
        var p=new StrategyDEntryContext(context.HasPosition,context.PendingBuy,context.SymbolEntries,context.StrategyEntries,context.AccountEntries,context.LastExit,context.LastExitReason,prior,context.ExitBarOrdinal);
        var closed=definition.DayTrading!.Evaluation==SignalEvaluationMode.OnBarClosed;
        var selected=closed?data.LatestClosedAnalysis??latest:latest;
        var state=signals.Observe(selected,p,!closed||data.LatestClosedAnalysis!=null);
        if(closed&&data.LatestClosedAnalysis!=null)signals.TrackBoundaryPrice(latest);
        return state.Signal is {} signal?new(signal.Id,signal.Symbol,signal.Instance,signal.Time,signal.Price,signal.Reason,
            signal.Support.Zone.Lower,signal.Support.Zone.Center,signal.Support.Zone.Upper,Serialization.Pack(signal),definition.DayTrading.Support.MaxEntryDistanceFromSupportPercent):null;
    }
    public IntradayEntrySignal? EvaluateAdditional(IntradayExecutionContext context,int entryCount,decimal lastFill,bool harvestStarted)
    {
        var c=definition.Configuration;
        if(!context.HasPosition||context.PendingBuy||harvestStarted||entryCount>=c.MaxEntries||data.Latest is not {} current||context.PriorEntry is not {} prior)return null;
        if(scaleStage!=entryCount){scaleStage=entryCount;scaleCondition=false;}
        var support=current.Supports.Where(s=>s.Approached(current.Price,c.SupportDistancePercent)).OrderBy(s=>Math.Abs(s.Center/current.Price-1)).FirstOrDefault();
        var passed=current.Feed.CanEnter&&current.SecurityTradable==true&&current.FundamentalRisk==false&&current.Price<=lastFill*(1+c.AdditionalEntryDropPercent/100)&&(!c.RequireSupportForAdditionalEntry||support!=null);
        var crossed=passed&&!scaleCondition;scaleCondition=passed;if(!crossed)return null;
        var evidence=Serialization.Pack(new{PriorSignal=prior.Id,EntryStage=entryCount+1,PreviousFill=lastFill,DropPercent=c.AdditionalEntryDropPercent,Threshold=lastFill*(1+c.AdditionalEntryDropPercent/100),UsedSupport=support,Decision=current});
        return new(prior.Id+"|scale:"+(entryCount+1)+":"+(++scaleSequence),Instrument.Key,Profile.InstanceId,current.Time,current.Price,"Entry"+(entryCount+1),support?.Zone.Lower??prior.ZoneLow,support?.Zone.Center??prior.ZoneCenter,support?.Zone.Upper??prior.ZoneHigh,evidence,definition.DayTrading!.Support.MaxEntryDistanceFromSupportPercent);
    }
}

/// <summary>Nonblocking execution port; a live adapter must reconcile and authorize again at transmission.</summary>
public interface IDayTradingExecutionAdapter
{
    bool IsLive {get;}
    bool Synchronized {get;}
    string Currency {get;}
    decimal Cash {get;}
    decimal Equity {get;}
    decimal ReservedCash {get;}
    IReadOnlyDictionary<string,Position> Positions {get;}
    IReadOnlyList<ManagedOrder> Orders {get;}
    IReadOnlyList<Fill> Fills {get;}
    void OnMarket(MarketEvent market);
    void Submit(ManagedOrder order);
    void Cancel(ManagedOrder order,DateTimeOffset now);
}
/// <summary>Uses the existing next-event/partial-fill broker with one shared account and unmodified input prices.</summary>
public sealed class DayTradingPaperAdapter:IDayTradingExecutionAdapter
{
    private readonly PaperBroker broker;
    private readonly ExecutionSettings settings;
    public bool IsLive=>false;
    public bool Synchronized=>true;
    public string Currency=>broker.Currency;
    public decimal Cash=>broker.Cash;
    public decimal Equity=>broker.Equity;
    public decimal ReservedCash=>broker.ReservedCash;
    public IReadOnlyDictionary<string,Position> Positions=>broker.Positions;
    public IReadOnlyList<ManagedOrder> Orders=>broker.Orders;
    public IReadOnlyList<Fill> Fills=>broker.Fills;
    public DayTradingPaperAdapter(string currency,decimal cash,ExecutionSettings settings)
    {if(cash<=0)throw new ArgumentOutOfRangeException(nameof(cash));this.settings=Serialization.Unpack<ExecutionSettings>(Serialization.Pack(settings));broker=new(currency,cash,this.settings);}
    public void OnMarket(MarketEvent market)=>broker.Process(market);
    public void Submit(ManagedOrder order)=>broker.Submit(order);
    public void Cancel(ManagedOrder order,DateTimeOffset now)=>broker.CancelLocally(order,now);
    /// <summary>Explicit next-open assumption for closed-bar research; never sent to the market-data or signal pipeline.</summary>
    public void ProcessAssumedOpening(Instrument instrument,StrategyBar bar,int maximumShares)
    {
        if(settings.UseSpread)throw new InvalidOperationException("호가 없는 봉 근사 체결에서는 호가 체결모형을 사용할 수 없습니다.");
        if(maximumShares<=0)throw new InvalidOperationException("봉 시가 모형의 최대 체결수량을 입력하세요.");
        var time=bar.OpenTime.AddTicks(Math.Max(1,(long)settings.LatencyMs*TimeSpan.TicksPerMillisecond));
        if(time>=bar.ClosedAt)return;
        broker.Process(new(instrument,EventKind.Trade,time,time,"BAR_FILL_MODEL",0){TradePrice=bar.Price.Open,TradeSize=maximumShares});
    }
    public void MarkClosed(Instrument instrument,decimal price)
    {if(broker.Positions.TryGetValue(instrument.Key,out var position))position.Last=price;}
}
