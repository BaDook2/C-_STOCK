namespace Stock.App;

/// <summary>Groups trade markers that would overlap inside a fixed-width chart interval.</summary>
public static class ChartMarkerAggregation
{
    public const double TargetBucketPixels=92;

    public sealed record Group<T>(bool Buy,int Count,double Position,decimal? Price,IReadOnlyList<T> Items);

    public static Group<T>[] Cluster<T>(
        IEnumerable<T> source,
        Func<T,bool> buy,
        Func<T,double> position,
        Func<T,decimal?> price,
        double visibleStart,
        double visibleEnd,
        double plotWidth,
        double targetBucketPixels=TargetBucketPixels)
    {
        if(visibleEnd<=visibleStart||plotWidth<=0||targetBucketPixels<=0)return [];
        var bucketCount=Math.Max(1,(int)Math.Floor(plotWidth/targetBucketPixels));
        var bucketSpan=(visibleEnd-visibleStart)/bucketCount;
        return source
            .Select(item=>(Item:item,Buy:buy(item),Position:position(item),Price:price(item)))
            .Where(x=>double.IsFinite(x.Position)&&x.Position>=visibleStart&&x.Position<visibleEnd)
            .GroupBy(x=>(x.Buy,Bucket:Math.Clamp((int)((x.Position-visibleStart)/bucketSpan),0,bucketCount-1)))
            .OrderBy(g=>g.Key.Bucket).ThenByDescending(g=>g.Key.Buy)
            .Select(g=>
            {
                var items=g.Select(x=>x.Item).ToArray();
                var prices=g.Where(x=>x.Price.HasValue).Select(x=>x.Price!.Value).ToArray();
                return new Group<T>(g.Key.Buy,items.Length,g.Average(x=>x.Position),prices.Length==0?null:prices.Average(),items);
            }).ToArray();
    }
}
