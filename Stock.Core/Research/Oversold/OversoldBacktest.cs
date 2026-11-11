using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
/// <summary>Retired bar-strategy API. The native tick replay has no legacy fallback.</summary>
public sealed partial class OversoldBacktest
{
    public OversoldResult Run(BarDataset dataset,OversoldConfig requested,DateOnly from,DateOnly to,HistoricalFacts input,CancellationToken token=default,IReadOnlyList<ISupportProvider>? supportProviders=null)=>throw Stock.Core.Trading.RetiredStrategy.Error();
}
