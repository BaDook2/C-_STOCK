using System.Globalization;
using Stock.Core.Research;

namespace Stock.Core.Strategies.Injes;

public sealed record InjesSupport(string Id,string Type,decimal Lower,decimal Center,decimal Upper,DateTimeOffset KnownAt);
public sealed record InjesSupportCluster(decimal ClusterLower,decimal ClusterCenter,decimal ClusterUpper,int SupportCount,
    string[] SupportTypes,string[] SupportIds,DateTimeOffset KnownAt)
{
    public InjesSupport[] Supports {get;init;}=[];
    public bool Approached(decimal price,decimal distance)=>price>=ClusterLower*(1-distance/100)&&price<=ClusterUpper*(1+distance/100);
    public string Identity=>string.Join('|',SupportIds.Order(StringComparer.Ordinal));
}
public sealed record InjesVolumeBin(decimal Lower,decimal Upper,decimal Volume);

/// <summary>Completed daily bars only. OHLCV allocation is explicitly approximate, not trade-at-price data.</summary>
public static class InjesSupportCalculator
{
    public static InjesSupportCluster[] Calculate(InjesReferenceData data,SupportZoneParameters p,DateTimeOffset asOf,int maxBins)
    {
        var bars=data.DailyBars.Where(b=>b.KnownAt!=default&&b.ClosedAt!=default&&b.KnownAt>=b.ClosedAt&&b.KnownAt<=asOf&&b.ClosedAt<=asOf&&b.Bar.IsComplete&&b.Bar.Time<=b.ClosedAt)
            .OrderBy(b=>b.ClosedAt).ToArray();
        if(bars.Select(b=>b.ClosedAt).Distinct().Count()!=bars.Length)throw new InvalidOperationException("중복 일봉 자료");
        foreach(var bar in bars)bar.Bar.Validate();
        var sources=p.SupportSources.Split(',',StringSplitOptions.TrimEntries);var levels=new List<InjesSupport>();
        if(sources.Contains("MA"))foreach(var period in p.MovingAveragePeriods.Split(',').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)))
        {
            if(bars.Length<period)continue;var selected=bars.TakeLast(period).ToArray();var average=selected.Average(b=>b.Bar.Close);
            levels.Add(new("MA:Daily:"+period,"MA",average,average,average,selected.Max(b=>b.KnownAt)));
        }
        if(sources.Contains("Pivot"))for(int i=p.PivotLeftBars;i+p.PivotRightBars<bars.Length;i++)
        {
            var v=bars[i];if(!bars.Skip(i-p.PivotLeftBars).Take(p.PivotLeftBars).All(b=>b.Bar.High<v.Bar.High)||!bars.Skip(i+1).Take(p.PivotRightBars).All(b=>b.Bar.High<v.Bar.High))continue;
            levels.Add(new("Pivot:"+v.ClosedAt.UtcTicks,"Pivot",v.Bar.High,v.Bar.High,v.Bar.High,bars.Skip(i-p.PivotLeftBars).Take(p.PivotLeftBars+p.PivotRightBars+1).Max(b=>b.KnownAt)));
        }
        if(sources.Contains("VolumeZone")&&bars.Length>=p.VolumeProfileLookbackBars&&data.TickSize>0)
        {
            var tick=data.TickSize.Value;var size=decimal.Ceiling(p.VolumeBinSize/tick)*tick;var selected=bars.TakeLast(p.VolumeProfileLookbackBars).ToArray();
            var bins=Allocate(selected.Select(b=>b.Bar).ToArray(),size,maxBins);var peak=bins.Select(b=>b.Volume).DefaultIfEmpty(0).Max();
            // Highest-volume contiguous plateau, with one independent profile identity across all its bins.
            var concentrated=bins.Where(b=>b.Volume>0&&b.Volume==peak).OrderBy(b=>b.Lower).ToArray();
            var groups=new List<List<InjesVolumeBin>>();
            foreach(var bin in concentrated){if(groups.Count==0||groups[^1][^1].Upper!=bin.Lower)groups.Add([]);groups[^1].Add(bin);}
            foreach(var group in groups)
            {var lower=group[0].Lower;var upper=group[^1].Upper;var center=group.Sum(b=>(b.Lower+b.Upper)/2*b.Volume)/group.Sum(b=>b.Volume);levels.Add(new("ApproximateVolumeZone","ApproximateVolumeZone",lower,center,upper,selected.Max(b=>b.KnownAt)));}
        }
        return Cluster(levels,p.SupportClusterRangePercent);
    }
    public static InjesVolumeBin[] Allocate(IReadOnlyList<PriceBar> bars,decimal size,int maxBins)
    {
        if(size<=0||maxBins<1)throw new InvalidOperationException("매물대 구간 폭·메모리 한도를 확인하세요.");
        var bins=new Dictionary<decimal,decimal>();
        foreach(var b in bars)
        {
            if(!b.IsComplete||b.Low<=0||b.High<b.Low||b.Volume<0||b.Close<b.Low||b.Close>b.High||b.Open<b.Low||b.Open>b.High)throw new InvalidOperationException("유효한 완료 OHLCV가 필요합니다.");
            var first=decimal.Floor(b.Low/size);var last=b.High==b.Low?first:decimal.Ceiling(b.High/size)-1;
            if(last-first+1>maxBins)throw new InvalidOperationException("MissingData: 매물대 bin 한도 초과. 폭을 명시적으로 조정하세요.");
            decimal remaining=b.Volume;
            for(var k=first;k<=last;k++)
            {
                var overlap=Math.Max(0,Math.Min(b.High,(k+1)*size)-Math.Max(b.Low,k*size));
                var allocation=k==last?remaining:b.Volume*overlap/(b.High-b.Low);remaining-=allocation;
                bins[k]=bins.GetValueOrDefault(k)+allocation;
                if(bins.Count>maxBins)throw new InvalidOperationException("MissingData: 매물대 전체 bin 한도 초과");
            }
        }
        return bins.OrderBy(b=>b.Key).Select(b=>new InjesVolumeBin(b.Key*size,(b.Key+1)*size,b.Value)).ToArray();
    }
    public static InjesSupportCluster[] Cluster(IEnumerable<InjesSupport> candidates,decimal range)
    {
        var groups=new List<List<InjesSupport>>();
        foreach(var s in candidates.Where(x=>x.Lower>0&&x.Lower<=x.Center&&x.Center<=x.Upper&&100*(x.Upper-x.Lower)/x.Lower<=range).OrderBy(x=>x.Center))
        {
            var group=groups.LastOrDefault();
            if(group==null||100*(s.Center-group[^1].Center)/group[^1].Center>range||100*(Math.Max(s.Upper,group.Max(x=>x.Upper))-Math.Min(s.Lower,group.Min(x=>x.Lower)))/Math.Min(s.Lower,group.Min(x=>x.Lower))>range)groups.Add([s]);else group.Add(s);
        }
        return groups.Select(g=>new InjesSupportCluster(g.Min(x=>x.Lower),g.Average(x=>x.Center),g.Max(x=>x.Upper),g.Select(x=>x.Id).Distinct().Count(),g.Select(x=>x.Type).Distinct().ToArray(),g.Select(x=>x.Id).Distinct().Order(StringComparer.Ordinal).ToArray(),g.Max(x=>x.KnownAt)){Supports=g.ToArray()}).ToArray();
    }
}
internal sealed class SupportZoneRuntime(SupportZoneParameters p):EpisodeRuntime
{
    private InjesSupportCluster? approached;private decimal low,previousBounce;private string consumed="";
    private DateTimeOffset nextAvailability;private InjesKnownBar[]? cachedBars;private decimal? cachedTick;private InjesSupportCluster[] clusters=[];private string calculationError="";
    public override void ResetCandidate(){approached=null;low=previousBounce=0;Emitted=false;Episode="";}
    public override StrategyIntent Evaluate(StrategyContext c)
    {
        var f=c.Features;
        if(c.Position?.Quantity>0)
        {
            var lower=c.EntryEvidence?.Metrics.GetValueOrDefault("clusterLower");var stop=lower*(1-p.SupportBreakPercent/100);
            return stop>0&&f.Price<=stop?new(SignalType.Exit,"SupportBreak",c.EntryEvidence!.EpisodeId,[],new Dictionary<string,decimal?>(),ProtectivePrice:stop):Wait("보유 · 진입 당시 지지선·공통 보호 평가");
        }
        if(c.Event?.Kind!=Stock.Core.Trading.EventKind.Trade||f.Price is not {} price||!f.FreshTrade||!f.FreshQuote||f.Reference is not {} data)return Wait("MissingData: 체결·호가·확정 일봉 필요");
        if(data.AverageDailyTradingValue is not {} average)return Wait("MissingData: 실제 평균 일 거래대금");
        if(!data.DailyBars.Any(b=>b.Bar.IsComplete&&b.ClosedAt!=default&&b.KnownAt>=b.ClosedAt&&b.KnownAt<=c.Now&&b.ClosedAt<=c.Now))return Wait("MissingData: 가용시각이 확인된 완료 일봉 없음");
        if(average>p.MaxAverageDailyTradingValue)return Wait("저유동성 거래대금 상한 초과") with{Attention=EvaluationOutcome.Failed};
        // Recompute only on newly available reference data/completed bars, not by scanning history on each tick.
        if(!ReferenceEquals(cachedBars,data.DailyBars)||cachedTick!=data.TickSize||c.Now>=nextAvailability)
        {
            calculationError="";try{clusters=InjesSupportCalculator.Calculate(data,p,c.Now,f.MaximumVolumeBins);}catch(InvalidOperationException error){clusters=[];ResetCandidate();calculationError="MissingData: 지지 자료 계산 불가 · "+error.Message;}
            cachedBars=data.DailyBars;cachedTick=data.TickSize;nextAvailability=data.DailyBars.Select(b=>b.KnownAt>b.ClosedAt?b.KnownAt:b.ClosedAt).Where(t=>t>c.Now).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();
        }
        if(calculationError!="")return Wait(calculationError);
        var cluster=clusters.Where(s=>s.KnownAt<=c.Now&&s.SupportCount>=p.MinimumSupportCount&&s.Approached(price,p.SupportDistancePercent)).OrderBy(s=>Math.Abs(s.ClusterCenter-price)).FirstOrDefault();
        if(cluster==null){ResetCandidate();consumed="";return Wait("독립 지지 근거·접근 조건 대기") with{Attention=EvaluationOutcome.Passed};}
        if(consumed==cluster.Identity)return Wait("동일 지지 접근 사건 소비 완료 · 이탈 후 새 접근 필요");
        if(approached?.Identity!=cluster.Identity){approached=cluster;low=price;previousBounce=0;NewEpisode();if(p.RequireReboundConfirmation)return Wait("지지 접근 · 이후 저점/새 반등 추적");}
        low=Math.Min(low,price);var bounce=(price/low-1)*100;var crossed=previousBounce<p.ReboundPercent&&bounce>=p.ReboundPercent;previousBounce=bounce;
        if(p.RequireReboundConfirmation&&!crossed)return Wait("지지 접근 후 반등 상향 돌파 대기");
        Emitted=true;consumed=cluster.Identity;
        return new(SignalType.Enter,p.RequireReboundConfirmation?"지지 접근 → 새 반등":"지지구간 최초 접근",Episode,[new("Support.IndependentEvidence",EvaluationOutcome.Passed,string.Join(", ",cluster.SupportIds)+" · KnownAt "+cluster.KnownAt.ToString("O"))],new Dictionary<string,decimal?>{{"clusterLower",cluster.ClusterLower},{"clusterCenter",cluster.ClusterCenter},{"clusterUpper",cluster.ClusterUpper},{"supportCount",cluster.SupportCount},{"observedLow",low}},f.Ask,ProtectivePrice:cluster.ClusterLower*(1-p.SupportBreakPercent/100)){SupportCluster=cluster};
    }
}
