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
    /// <summary>Merge measured label collisions, including labels spanning adjacent buckets.
    /// Called again after zoom, so independent fills separate as soon as their labels fit.</summary>
    public static IReadOnlyList<T>[] MergeOverlaps<T>(IEnumerable<IReadOnlyList<T>> source,Func<IReadOnlyList<T>,System.Windows.Rect> measure,double gap=4)
    {
        var groups=source.Where(g=>g.Count>0).ToList();
        while(groups.Count>1)
        {
            var boxes=groups.Select(g=>(Items:g,Bounds:measure(g))).OrderBy(x=>x.Bounds.Left).ToArray();
            var parent=Enumerable.Range(0,boxes.Length).ToArray();int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
            var merged=false;
            for(var i=0;i<boxes.Length;i++)for(var j=i+1;j<boxes.Length&&boxes[j].Bounds.Left<=boxes[i].Bounds.Right+gap;j++)
            {
                var bounds=boxes[i].Bounds;bounds.Inflate(gap/2,gap/2);var other=boxes[j].Bounds;other.Inflate(gap/2,gap/2);
                if(!bounds.IntersectsWith(other))continue;var a=Root(i);var b=Root(j);if(a!=b){parent[b]=a;merged=true;}
            }
            if(!merged)return boxes.Select(b=>b.Items).ToArray();
            groups=Enumerable.Range(0,boxes.Length).GroupBy(Root).Select(g=>(IReadOnlyList<T>)g.SelectMany(i=>boxes[i].Items).ToArray()).ToList();
        }
        return groups.ToArray();
    }
}
