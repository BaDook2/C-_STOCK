using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

/// <summary>Native family-specific definitions; no legacy/D envelope or implicit migration into a different algorithm.</summary>
public sealed class InjesPresetStore(TradingStore store)
{
    public const string Prefix="inje-preset:";
    public string[] Names=>store.State<string[]>("inje-presets-v1")??[];
    public void EnsureDefaults()
    {
        var names=Names.ToList();
        foreach(var factory in StrategyRegistry.Injes.All)
        {var d=InjesStrategyDefinition.Create(factory.StrategyId,DateTimeOffset.UtcNow);if(!names.Any(name=>store.State<InjesStrategyDefinition>(Prefix+name)?.StrategyId==factory.StrategyId)){store.State(Prefix+d.StrategyName,d);names.Add(d.StrategyName);}}
        store.State("inje-presets-v1",names.ToArray());
    }
    public InjesStrategyDefinition Get(string name)
    {var d=store.State<InjesStrategyDefinition>(Prefix+name)??throw new InvalidOperationException("저장된 Inje 전략이 없습니다: "+name);return d.Snapshot();}
    public void Save(InjesStrategyDefinition definition)
    {
        var d=definition.Snapshot();var names=Names.ToList();if(!names.Contains(d.StrategyName))names.Add(d.StrategyName);
        store.StatesAtomic((Prefix+d.StrategyName,d),("inje-presets-v1",names.ToArray()));
    }
    public void Rename(string oldName,InjesStrategyDefinition definition)
    {
        if(Names.Contains(definition.StrategyName))throw new InvalidOperationException("같은 이름의 전략이 있습니다.");
        var names=Names.Select(n=>n==oldName?definition.StrategyName:n).ToArray();
        store.StatesAtomic((Prefix+definition.StrategyName,definition.Snapshot()),("inje-presets-v1",names));store.DeleteState(Prefix+oldName);
    }
    public static OversoldConfig Wrap(InjesStrategyDefinition definition)=>new(){Injes=definition.Snapshot(),InitialCapital=definition.InitialCash,PlannedInvestment=definition.CommonParameters.OrderBudget,CommissionPercent=definition.ExecutionPolicy.FeePercent,TaxPercent=definition.ExecutionPolicy.TaxPercent,SlippagePercent=definition.ExecutionPolicy.SlippagePercent};
    public static InjesStrategyDefinition Require(OversoldConfig config)=>config.Injes?.Snapshot()??throw new InvalidOperationException("기존 전략은 제거되었습니다. Inje's Strategy 설정을 선택하세요.");
}
