using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed record OversoldOwnedState(int Quantity,int Entries,int PeakQuantity,int HarvestBasis,int FirstDay,int LastExit,decimal Average,decimal LastEntry,decimal Peak,decimal? PriorCenter,bool Tp1,bool Tp2,bool Harvest,SupportCluster? InitialCluster);
/// <summary>Durable strategy state and decision context, never raw ticks or a quote cache.</summary>
public sealed record OversoldLiveCheckpoint(int Version,string Key,PriceBar[] History,decimal?[] TradingValues,StrategyBar[] CompletedBars,EntryDetectorState Detector,OversoldOwnedState Owned,DateTimeOffset LastBarTime,long LastBarSequence,long LiveSequence,OversoldSignal? Pending,EntryEvaluation? LatestEntry,string[] EntryOrders,string[] ExecutionIds,string Error,HistoricalFacts Facts);

public sealed partial class OversoldLiveStrategy
{
    public OversoldLiveCheckpoint CapturePublicState()=>throw RetiredStrategy.Error();
    public static OversoldLiveStrategy RestorePublic(Instrument instrument,OversoldLiveConfiguration configuration,OversoldLiveCheckpoint state)=>throw RetiredStrategy.Error();
    public void ExtendPublicFacts(HistoricalFacts updated)=>throw RetiredStrategy.Error();
}
