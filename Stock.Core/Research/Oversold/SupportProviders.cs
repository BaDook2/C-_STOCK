using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
public enum SupportKind { MovingAverage,ConfirmedPivotHigh,ApproximateVolumeZone }
public sealed record SupportCandidate(string Id,SupportKind Type,decimal Lower,decimal Center,decimal Upper,DateOnly Occurred,DateOnly Confirmed,string Description);
public sealed record SupportCluster(decimal Lower,decimal Center,decimal Upper,SupportCandidate[] Supports)
{
    public int Count=>Supports.Length;
    public SupportKind[] Types=>Supports.Select(s=>s.Type).Distinct().ToArray();
    public string Signature=>string.Join("|",Supports.Select(s=>s.Id).Order(StringComparer.Ordinal));
    public bool Approached(decimal price,decimal distance)=>price>=Lower*(1-distance/100)&&price<=Upper*(1+distance/100);
}
public interface ISupportProvider { IReadOnlyList<SupportCandidate> Get(IReadOnlyList<PriceBar> history,OversoldConfig config); }
public static class DailyDates { public static DateOnly Of(PriceBar bar)=>DateOnly.FromDateTime(bar.Time.ToOffset(TimeSpan.FromHours(9)).DateTime); }
public sealed class MovingAverageSupportProvider:ISupportProvider
{
    public IReadOnlyList<SupportCandidate> Get(IReadOnlyList<PriceBar> history,OversoldConfig c)
    {
        if(history.Count==0||!c.UseMovingAverageSupport)return [];var date=DailyDates.Of(history[^1]);
        return c.MaPeriods().Where(n=>history.Count>=n).Select(n=>{var price=history.Skip(history.Count-n).Average(b=>b.Close);return new SupportCandidate("MA:"+n,SupportKind.MovingAverage,price,price,price,date,date,"이동평균 "+n+"봉");}).ToArray();
    }
}
public sealed class ConfirmedPivotHighProvider(bool intraday=false):ISupportProvider
{
    private readonly List<SupportCandidate> confirmed=[];private int processed;
    public IReadOnlyList<SupportCandidate> Get(IReadOnlyList<PriceBar> h,OversoldConfig c)
    {
        if(!c.UsePivotSupport)return [];
        for(int arrived=processed;arrived<h.Count;arrived++)
        {
            int center=arrived-c.PivotRightBars;if(center<c.PivotLeftBars)continue;var high=h[center].High;
            if(Enumerable.Range(center-c.PivotLeftBars,c.PivotLeftBars).All(i=>h[i].High<high)&&Enumerable.Range(center+1,c.PivotRightBars).All(i=>h[i].High<high))confirmed.Add(new("PIVOT:"+(intraday?h[center].Time.ToString("O")+":"+center:DailyDates.Of(h[center]).ToString()),SupportKind.ConfirmedPivotHigh,high,high,high,DailyDates.Of(h[center]),DailyDates.Of(h[arrived]),"오른쪽 확인 완료 고점"));
        }
        processed=h.Count;return confirmed.ToArray();
    }
}
public sealed record ApproximateBin(decimal Lower,decimal Upper,decimal Volume);
/// <summary>Daily-range volume allocation. This is deliberately not a true traded-price volume profile.</summary>
public sealed class ApproximateVolumeZoneProvider:ISupportProvider
{
    public decimal EffectiveBinSize {get;private set;}
    public IReadOnlyList<ApproximateBin> Allocate(IReadOnlyList<PriceBar> history,OversoldConfig c)
    {
        var bars=history.TakeLast(c.VolumeZoneLookbackDays).ToArray();if(bars.Length==0)return [];
        var low=bars.Min(b=>b.Low);var high=bars.Max(b=>b.High);var tick=new Instrument("005930",Exchange.KRX).Tick(bars[^1].Close);
        var size=Math.Max(c.VolumeZoneBinSize,(high-low)/Math.Max(1,c.VolumeZoneMaxBins-1));size=decimal.Ceiling(size/tick)*tick;EffectiveBinSize=size;
        var bins=new SortedDictionary<decimal,decimal>();
        foreach(var bar in bars){decimal first=decimal.Floor(bar.Low/size),last=decimal.Floor(bar.High/size);var count=last-first+1;var allocation=bar.Volume/count;decimal assigned=0;for(decimal i=first;i<=last;i++){var amount=i==last?bar.Volume-assigned:allocation;bins[i*size]=bins.GetValueOrDefault(i*size)+amount;assigned+=amount;}}
        return bins.Select(p=>new ApproximateBin(p.Key,p.Key+size,p.Value)).ToArray();
    }
    public IReadOnlyList<SupportCandidate> Get(IReadOnlyList<PriceBar> history,OversoldConfig c)
    {
        if(!c.UseApproximateVolumeZone||history.Count<c.VolumeZoneLookbackDays)return [];
        var all=Allocate(history,c);if(all.Count==0||all.Max(b=>b.Volume)<=0)return [];var threshold=all.Max(b=>b.Volume)*c.VolumeZoneThresholdPercent/100;
        var groups=new List<List<ApproximateBin>>();foreach(var bin in all.Where(b=>b.Volume>=threshold)){if(groups.Count==0||bin.Lower>groups[^1][^1].Upper*(1+c.VolumeZoneMergeRangePercent/100))groups.Add([]);groups[^1].Add(bin);}
        var date=DailyDates.Of(history[^1]);return groups.Select(g=>{var lo=Math.Max(.0001m,g[0].Lower);var hi=g[^1].Upper;var center=g.Sum(b=>(b.Lower+b.Upper)/2*b.Volume)/g.Sum(b=>b.Volume);return new SupportCandidate("APPROX:"+lo+":"+hi,SupportKind.ApproximateVolumeZone,lo,center,hi,date,date,$"일봉 근사 거래량 구간 · bin {EffectiveBinSize:N0}원");}).ToArray();
    }
}
public static class SupportClustering
{
    public static SupportCluster[] Build(IEnumerable<SupportCandidate> source,decimal range)
    {
        var rows=source.GroupBy(s=>s.Id).Select(g=>g.First()).Where(s=>s.Center>0&&s.Lower>0&&s.Lower<=s.Center&&s.Center<=s.Upper).OrderBy(s=>s.Center).ThenBy(s=>s.Id,StringComparer.Ordinal).ToArray();var groups=new List<List<SupportCandidate>>();
        foreach(var row in rows){if(groups.Count==0||(row.Center/groups[^1][^1].Center-1)*100>range)groups.Add([]);groups[^1].Add(row);}
        return groups.Select(g=>new SupportCluster(g.Min(s=>s.Lower),g.Average(s=>s.Center),g.Max(s=>s.Upper),g.ToArray())).ToArray();
    }
}
public sealed record DailyIndicators(decimal MediumDrawdown,decimal ShortDrawdown,decimal? Rsi,decimal? VolumeRatio,string Trigger);
/// <summary>Per-symbol sequential cache; append only, never computes from later bars.</summary>
public sealed class OversoldIndicatorCache(OversoldConfig c,bool allowEqualTime=false)
{
    private readonly List<PriceBar> history=[];private decimal gain,loss;private int changes;
    public IReadOnlyList<PriceBar> History=>history;public DailyIndicators Current {get;private set;}=new(0,0,null,null,"");
    public DailyIndicators Append(PriceBar bar)
    {
        bar.Validate();if(history.Count>0&&(bar.Time<history[^1].Time||!allowEqualTime&&bar.Time==history[^1].Time))throw new InvalidOperationException("봉은 시간순이며 같은 시각은 순서가 검증된 틱 집계에서만 허용합니다.");
        decimal? rsi=null;if(history.Count>0){var delta=bar.Close-history[^1].Close;var up=Math.Max(0,delta);var down=Math.Max(0,-delta);changes++;if(changes<=c.RsiPeriod){gain+=up/c.RsiPeriod;loss+=down/c.RsiPeriod;}else{gain=(gain*(c.RsiPeriod-1)+up)/c.RsiPeriod;loss=(loss*(c.RsiPeriod-1)+down)/c.RsiPeriod;}if(changes>=c.RsiPeriod)rsi=loss==0?(gain==0?50:100):100-100/(1+gain/loss);}
        decimal? ratio=null;if(history.Count>=c.VolumeLookbackDays){var avg=history.TakeLast(c.VolumeLookbackDays).Average(b=>b.Volume);if(avg>0)ratio=bar.Volume/avg;}
        history.Add(bar);var medium=(bar.Close/history.TakeLast(c.CrashLookbackDays).Max(b=>b.High)-1)*100;var shortDrop=(bar.Close/history.TakeLast(c.ShortCrashLookbackDays).Max(b=>b.High)-1)*100;
        bool m=history.Count>=c.CrashLookbackDays&&medium<=c.MinimumDrawdownPercent,s=history.Count>=c.ShortCrashLookbackDays&&shortDrop<=c.ShortCrashPercent;
        return Current=new(medium,shortDrop,rsi,ratio,m&&s?"Medium+Short":m?"Medium":s?"Short":"");
    }
}
