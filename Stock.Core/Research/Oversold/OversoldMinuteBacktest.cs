using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
/// <summary>Retired bar-strategy API. The native tick replay has no legacy fallback.</summary>
public sealed partial class OversoldBacktest
{
    public OversoldResult Run(StockItem stock,IReadOnlyList<StrategyBar> input,BarSpecification specification,OversoldConfig requested,DateOnly from,DateOnly to,HistoricalFacts inputFacts,CancellationToken token=default,IReadOnlyList<ISupportProvider>? supportProviders=null)=>throw Stock.Core.Trading.RetiredStrategy.Error();
}
