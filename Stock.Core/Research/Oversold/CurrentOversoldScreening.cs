namespace Stock.Core.Research.Oversold;
public sealed partial class OversoldScreeningEngine
{
    public ScreeningObservation EvaluateCurrent(StockItem stock,IReadOnlyList<StrategyBar> input,OversoldConfig requested,HistoricalFacts historical,DateTimeOffset asOf,DailySecurityStatus? currentStatus=null,bool? currentRisk=null,CancellationToken token=default,DateOnly? from=null,DateOnly? to=null)=>throw Stock.Core.Trading.RetiredStrategy.Error();
}
