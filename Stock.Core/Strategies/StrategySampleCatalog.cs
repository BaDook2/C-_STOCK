using Stock.Core.Trading;

namespace Stock.Core.Strategies;

/// <summary>Public synthetic-validation presets bundled with the app; installs once without replacing user profiles or selecting a strategy.</summary>
public static class StrategySampleCatalog
{
    private const string InstalledKey="strategy-samples-installed-v1";
    private static readonly string[] Cases=["momentum-pullback","quote-scalping","crash-rebound","support-zone","DowntrendRebound","UptrendBreakout","UptrendPullback","RangeLowerBounce","RangeUpperBreakout"];
    public static StrategyProfile[] All()=>Cases.Select(id=>
    {
        using var stream=typeof(StrategySampleCatalog).Assembly.GetManifestResourceStream("Stock.Core.Strategies.Samples."+id+".json")??throw new InvalidOperationException("샘플 전략 자료가 없습니다: "+id);
        using var reader=new StreamReader(stream);return StrategyProfileJson.Import(reader.ReadToEnd());
    }).ToArray();

    public static void EnsureInstalled(TradingStore store)
    {
        if(store.State<bool>(InstalledKey))return;
        var samples=All();var presets=new StrategyProfileStore(store);presets.EnsureDefault();
        var names=presets.Names.ToList();var updates=new List<(string Key,object Value)>();
        foreach(var profile in samples)
        {
            if(names.Contains(profile.Name))continue;
            names.Add(profile.Name);updates.Add((StrategyProfileStore.Prefix+profile.Name,profile));
        }
        updates.Add(("strategy-profile-names-v1",names.ToArray()));updates.Add((InstalledKey,true));
        store.StatesAtomic(updates.ToArray());
    }
}
