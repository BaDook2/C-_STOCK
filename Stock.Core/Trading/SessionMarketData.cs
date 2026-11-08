namespace Stock.Core.Trading;

/// <summary>Process-lifetime market data. Reloadable caches are bounded; raw events are never written to disk.</summary>
internal sealed class SessionMarketData
{
    private const int MaximumCacheEntries=128,MaximumCacheCharacters=32*1024*1024;
    private readonly Dictionary<string,(string Json,long Used)> cache=[];
    private readonly Dictionary<string,MarketEvent> events=[];
    private long clock,characters;
    public static bool IsCache(string key)=>key=="catalog"||key.StartsWith("bars:",StringComparison.Ordinal)||key.StartsWith("live-ticks:",StringComparison.Ordinal)||key.StartsWith("verified-tick-coverage:",StringComparison.Ordinal);
    public string? Get(string key)
    {if(!cache.TryGetValue(key,out var row))return null;cache[key]=(row.Json,++clock);return row.Json;}
    public void Put(string key,string json)
    {
        Remove(key);if(json.Length>MaximumCacheCharacters)return;cache[key]=(json,++clock);characters+=json.Length;
        while(cache.Count>MaximumCacheEntries||characters>MaximumCacheCharacters)Remove(cache.MinBy(x=>x.Value.Used).Key);
    }
    public bool Remove(string key){if(!cache.Remove(key,out var row))return false;characters-=row.Json.Length;return true;}
    public (string Key,string Json)[] States(string prefix)=>cache.Where(x=>x.Key.StartsWith(prefix,StringComparison.Ordinal)).Select(x=>(x.Key,x.Value.Json)).ToArray();
    public int Insert(IEnumerable<MarketEvent> rows)
    {int added=0;foreach(var row in rows){row.Validate();if(events.TryAdd(TradingStore.Hash(Serialization.Pack(row)),row))added++;}return added;}
    public MarketEvent[] Read(string key,string source,DateTimeOffset from,DateTimeOffset to)=>events.Values.Where(e=>e.Key==key&&e.Source==source&&e.ExchangeTime>=from&&e.ExchangeTime<=to).OrderBy(e=>e.ExchangeTime).ThenBy(e=>e.Sequence).ToArray();
    public string[] Datasets()=>events.Values.GroupBy(e=>(e.Key,e.Source)).Select(g=>$"{g.Key.Key} / {g.Key.Source} / {g.Count()} events (메모리)").ToArray();
    public void Clear(){cache.Clear();events.Clear();characters=0;}
}
