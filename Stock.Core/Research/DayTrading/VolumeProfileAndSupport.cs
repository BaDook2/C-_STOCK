using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public enum ProfilePrecision { Approximate, Traded, Missing }
public sealed record VolumeZone(decimal Lower,decimal Center,decimal Upper,decimal Volume,string Source,ProfilePrecision Precision,DateTimeOffset KnownAt)
{
    public decimal WidthPercent=>100*(Upper-Lower)/Center;
}
public sealed record StrategyDSupport(string StableKey,decimal MovingAverage,int MovingAveragePeriod,TimeframeSpec Timeframe,VolumeZone Zone,decimal GapPercent)
{
    public decimal Lower=>Math.Min(MovingAverage,Zone.Lower);
    public decimal Upper=>Math.Max(MovingAverage,Zone.Upper);
    public decimal Center=>(Lower+Upper)/2;
    public bool Approached(decimal price,decimal distance)=>price>=Lower*(1-distance/100)&&price<=Upper*(1+distance/100);
    public bool SamePhysicalZone(StrategyDSupport other,decimal tolerance)=>MovingAveragePeriod==other.MovingAveragePeriod&&Timeframe==other.Timeframe&&
        Math.Abs(Center/other.Center-1)*100<=tolerance&&Math.Max(Lower,other.Lower)<=Math.Min(Upper,other.Upper);
}
public static class StrategyDSupports
{
    public static StrategyDSupport[] Overlap(IEnumerable<(int Period,decimal Price)> averages,TimeframeSpec timeframe,IEnumerable<VolumeZone> zones,SupportPolicy policy,DateTimeOffset asOf)
    {
        var result=new List<StrategyDSupport>();
        foreach(var ma in averages.Where(a=>a.Price>0))
        foreach(var zone in zones.Where(z=>z.Precision!=ProfilePrecision.Missing&&z.KnownAt<=asOf&&z.Lower>0&&z.Lower<=z.Center&&z.Center<=z.Upper))
        {
            var gap=ma.Price<zone.Lower?(zone.Lower-ma.Price)/ma.Price*100:ma.Price>zone.Upper?(ma.Price-zone.Upper)/ma.Price*100:0;
            if(gap>policy.MaxOverlapDistancePercent||zone.WidthPercent>policy.MaxZoneWidthPercent)continue;
            // Episode matching also compares overlapping bounds; provider IDs/timestamps are not identity.
            var key=$"MA:{timeframe.Unit}:{timeframe.Size}:{ma.Period}:VP";
            result.Add(new(key,ma.Price,ma.Period,timeframe,zone,gap));
        }
        return result.OrderBy(s=>s.Center).ToArray();
    }
    public static VolumeZone[] Approximate(IReadOnlyList<PriceBar> closed,OversoldConfig config,DateTimeOffset knownAt)
    {
        var provider=new ApproximateVolumeZoneProvider();
        return closed.Count<config.VolumeZoneLookbackDays?[]:FromBins(provider.Allocate(closed,config),config.VolumeZoneThresholdPercent,config.VolumeZoneMergeRangePercent,"확정 OHLCV 균등배분 근사 매물대",ProfilePrecision.Approximate,knownAt);
    }
    public static VolumeZone[] FromBins(IEnumerable<ApproximateBin> input,decimal thresholdPercent,decimal mergePercent,string source,ProfilePrecision precision,DateTimeOffset knownAt)
    {
        var all=input.Where(b=>b.Volume>0).OrderBy(b=>b.Lower).ToArray();if(all.Length==0)return [];
        var minimum=all.Max(b=>b.Volume)*thresholdPercent/100;var groups=new List<List<ApproximateBin>>();
        foreach(var bin in all.Where(b=>b.Volume>=minimum))
        {if(groups.Count==0||bin.Lower>groups[^1][^1].Upper*(1+mergePercent/100))groups.Add([]);groups[^1].Add(bin);}
        return groups.Select(g=>new VolumeZone(Math.Max(.0001m,g[0].Lower),g.Sum(b=>(b.Lower+b.Upper)/2*b.Volume)/g.Sum(b=>b.Volume),g[^1].Upper,g.Sum(b=>b.Volume),source,precision,knownAt)).ToArray();
    }
}

/// <summary>Incremental actual trade volume. Old trades are subtracted; rare bin growth rebuilds bounded memory.</summary>
public sealed class TradeVolumeProfile
{
    private readonly Instrument instrument;
    private readonly int maxBins,maxEvents;
    private readonly decimal minimumSize;
    private readonly Queue<MarketEvent> trades=[];
    private readonly Dictionary<decimal,decimal> bins=[];
    private decimal size;
    public bool Overflowed {get;private set;}
    public int RetainedEvents=>trades.Count;
    public decimal Volume=>bins.Values.Sum();
    public decimal EffectiveBinSize=>size;
    public DateTimeOffset? Oldest=>trades.Count>0?trades.Peek().ExchangeTime:null;
    public TradeVolumeProfile(Instrument instrument,decimal minimumSize,int maxBins,int maxEvents)
    {
        if(minimumSize<=0||maxBins<2||maxEvents<1)throw new ArgumentOutOfRangeException(nameof(minimumSize));
        this.instrument=instrument;this.minimumSize=minimumSize;this.maxBins=maxBins;this.maxEvents=maxEvents;
    }
    public void Add(MarketEvent trade,DateTimeOffset from)
    {
        if(trade.Kind!=EventKind.Trade)throw new InvalidOperationException("정밀 매물대는 개별 체결만 사용합니다.");
        if(trade.Instrument!=instrument)throw new InvalidOperationException("매물대 종목이 입력 체결과 다릅니다.");
        trade.Validate();Expire(from);
        if(trades.Count>=maxEvents){Overflowed=true;return;}
        var price=trade.TradePrice!.Value;var tick=instrument.Tick(price);var required=decimal.Ceiling(Math.Max(size,minimumSize)/tick)*tick;
        if(size==0)size=required;
        trades.Enqueue(trade);
        if(required!=size){size=required;Rebuild();}else AddBin(trade,1);
        // Widen only when occupied range cannot fit; source events and total volume remain exact.
        while(bins.Count>maxBins||bins.Count>0&&bins.Keys.Max()-bins.Keys.Min()+1>maxBins){size*=2;Rebuild();}
    }
    public void Expire(DateTimeOffset from)
    {while(trades.Count>0&&trades.Peek().ExchangeTime<from)AddBin(trades.Dequeue(),-1);}
    public void KeepLast(int count){while(trades.Count>count)AddBin(trades.Dequeue(),-1);}
    public void Clear(){trades.Clear();bins.Clear();size=0;Overflowed=false;}
    public ApproximateBin[] Bins()=>bins.OrderBy(b=>b.Key).Select(b=>new ApproximateBin(b.Key*size,(b.Key+1)*size,b.Value)).ToArray();
    private void AddBin(MarketEvent e,int sign)
    {var key=decimal.Floor(e.TradePrice!.Value/size);var sum=bins.GetValueOrDefault(key)+sign*e.TradeSize!.Value;if(sum<=0)bins.Remove(key);else bins[key]=sum;}
    private void Rebuild(){bins.Clear();foreach(var trade in trades)AddBin(trade,1);}
}
