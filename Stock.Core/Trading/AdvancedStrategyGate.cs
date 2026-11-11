namespace Stock.Core.Trading;
/// <summary>Retired journal contract; never dispatches an old algorithm.</summary>
public sealed class AdvancedStrategyGate(ITradingStrategy inner):ITradingStrategy
{public string Name=>inner.Name;public StrategySignal Evaluate(MarketContext context)=>throw RetiredStrategy.Error();}
