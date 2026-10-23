namespace Stock.Core.Research;

public sealed class TickChartLoadOptions
{
    // Selected conservatively from the WPF measurements, see docs/performance/RawTick.md.
    public int MaxVisibleRawTicks {get;set;}=100000;
    public int MaxLoadedTicks {get;set;}=300000;
    public double PreloadThresholdPercent {get;set;}=20;
    public TimeSpan PreloadDuration {get;set;}=TimeSpan.FromHours(6);
    public int DebounceMilliseconds {get;set;}=140;
    public void Validate(){if(MaxVisibleRawTicks<2||MaxLoadedTicks<MaxVisibleRawTicks||MaxLoadedTicks>10000000||PreloadThresholdPercent is <=0 or >=50||PreloadDuration<=TimeSpan.Zero||DebounceMilliseconds<20)throw new ArgumentOutOfRangeException(nameof(TickChartLoadOptions));}
}
public sealed record TickWindowResult(RawTick[] Ticks,bool Shortened,string Description);
/// <summary>Bounded page cache. All mutations are serialized by the caller; DB work never runs on UI.</summary>
public sealed class TickChartWindow(RawTickRepository repository,string symbol,string source,TickChartLoadOptions options)
{
    private readonly LinkedList<(string Key,RawTick[] Rows)> cache=[];
    public int CachedTicks=>cache.Sum(p=>p.Rows.Length);
    public long Queries {get;private set;}
    public TickChartLoadOptions Options=>options;
    public Task<TickExtent?> Extent(CancellationToken token)=>repository.Extent(symbol,source,token);
    public async Task<TickWindowResult> Range(long from,long to,CancellationToken token,long? focus=null)
    {
        options.Validate();if(from>to)(from,to)=(to,from);bool shortened=false;var original=to-from;var initialFrom=from;var initialTo=to;
        TickPage page;while(true){token.ThrowIfCancellationRequested();Queries++;page=await repository.Range(symbol,source,from,to,options.MaxVisibleRawTicks,token);if(!page.More||to-from<=1)break;shortened=true;var span=(to-from)/2;if(focus is {} center){from=Math.Max(initialFrom,center-span/2);to=Math.Min(initialTo,from+span);}else from=to-span;}
        if(page.More&&to-from<=1){Queries++;page=await repository.Before(symbol,source,new(to,long.MaxValue),options.MaxVisibleRawTicks,token);}
        if(page.Ticks.Length==0){Queries++;page=await repository.Before(symbol,source,new(to,long.MaxValue),options.MaxVisibleRawTicks,token);if(page.Ticks.Length==0){Queries++;page=await repository.After(symbol,source,new(from,0),options.MaxVisibleRawTicks,token);}}
        Store("range:"+from+":"+to,page.Ticks);return new(page.Ticks,shortened||page.More,$"{page.Ticks.Length:N0}틱 · 요청 {TimeSpan.FromTicks(original).TotalHours:N2}시간"+(shortened||page.More?" · 표시 상한에 맞춰 구간 축소":""));
    }
    public async Task<RawTick[]> Adjacent(TickKey edge,bool forward,CancellationToken token)
    {
        var key=$"{forward}:{edge.UtcTicks}:{edge.RowId}";var node=cache.First;while(node!=null){if(node.Value.Key==key){var cached=node.Value.Rows;cache.Remove(node);cache.AddLast((key,cached));return cached;}node=node.Next;}
        token.ThrowIfCancellationRequested();Queries++;var page=forward?await repository.After(symbol,source,edge,options.MaxVisibleRawTicks,token):await repository.Before(symbol,source,edge,options.MaxVisibleRawTicks,token);
        // Limit preloaded time where density permits, while keyset queries cross holidays without empty loops.
        var rows=page.Ticks;if(rows.Length>0){var anchor=forward?rows[0].Key.UtcTicks:rows[^1].Key.UtcTicks;var within=rows.Where(t=>Math.Abs(t.Key.UtcTicks-anchor)<=options.PreloadDuration.Ticks).ToArray();if(within.Length>0)rows=within;}
        Store(key,rows);return rows;
    }
    private void Store(string key,RawTick[] rows){var budget=options.MaxLoadedTicks-options.MaxVisibleRawTicks;if(rows.Length>budget)return;cache.AddLast((key,rows));while(CachedTicks>budget&&cache.First!=null)cache.RemoveFirst();while(cache.Count>6)cache.RemoveFirst();}
    public static int Nearest(RawTick[] rows,long ticks)
    {if(rows.Length==0)return -1;int low=0,high=rows.Length;while(low<high){int middle=low+(high-low)/2;if(rows[middle].Key.UtcTicks<ticks)low=middle+1;else high=middle;}if(low==0)return 0;if(low==rows.Length)return low-1;return ticks-rows[low-1].Key.UtcTicks<=rows[low].Key.UtcTicks-ticks?low-1:low;}
}
