using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed record OversoldLiveConfiguration(OversoldConfig Configuration,BarSpecification BarSpecification,StrategyBar[] Warmup,HistoricalFacts Facts,bool ResearchOnly=false,bool PublicPaper=false);
public sealed record OversoldPositionSnapshot(int Quantity,int Entries,decimal Average,decimal LastEntry,decimal Peak,bool TakeProfit1,bool TakeProfit2);
/// <summary>Retired algorithm contract used only by older journal/account DTOs. No strategy logic remains here.</summary>
public sealed partial class OversoldLiveStrategy:ITradingStrategy
{
    public StrategyBar[] CompletedBars=>[];public string Name=>"제거된 전략";public OversoldDecisionSnapshot? Snapshot=>null;public EntryEvaluation? LatestEntryEvaluation=>null;
    public DateTimeOffset LastAnalyzedAt=>default;public DateTimeOffset? ReadySignalTime=>null;public DateTimeOffset? PendingSignalTime=>null;public int EntryStage=>0;
    public OversoldPositionSnapshot Position=>new(0,0,0,0,0,false,false);public string Stage=>"제거된 전략";
    public OversoldLiveStrategy(Instrument instrument,OversoldLiveConfiguration configuration,CancellationToken cancellationToken=default)=>throw RetiredStrategy.Error();
    internal void AppendResearchWarmup(StrategyBar bar)=>throw RetiredStrategy.Error();
    public void OnPublicBar(StrategyBar bar)=>throw RetiredStrategy.Error();
    public void OnPublicObservation(DateTimeOffset time)=>throw RetiredStrategy.Error();
    public void OnMarket(MarketEvent market)=>throw RetiredStrategy.Error();
    public StrategySignal Evaluate(MarketContext context)=>throw RetiredStrategy.Error();
    public int OrderQuantity(decimal price)=>throw RetiredStrategy.Error();
    public void OnExecution(BrokerExecution execution,string reason,OversoldDecisionSnapshot? snapshot)=>throw RetiredStrategy.Error();
}
