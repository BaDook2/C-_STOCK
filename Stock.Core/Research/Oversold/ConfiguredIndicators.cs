using Stock.Core.Research.DayTrading;
namespace Stock.Core.Research.Oversold;

/// <summary>Independent completed series; no fallback to the screen's display interval or to future bars.</summary>
public sealed class ConfiguredIndicators(OversoldConfig config,HistoricalFacts? facts)
{
    public bool Active=>facts?.StrategyFrames.Length>0;
    public KnownStrategyBar[]? Series(string key,DateTimeOffset asOf)
    {
        if(!Active||!config.ParameterTimeframes.TryGetValue(key,out var frame))return null;
        if(asOf.TimeOfDay==TimeSpan.Zero){var date=DateOnly.FromDateTime(asOf.Date);asOf=facts!.Sessions.FirstOrDefault(s=>s.Date==date)?.Close??asOf;}
        return facts!.StrategyFrames.Where(b=>b.Timeframe==frame&&b.KnownAt<=asOf).OrderBy(b=>b.KnownAt).ToArray();
    }
    public DailyIndicators Apply(DailyIndicators current,DateTimeOffset asOf)
    {
        decimal? Draw(string key,int count)
        {
            var rows=Series(key,asOf);if(rows==null)return null;if(rows.Length<count)return 0;
            var last=rows[^1].Bar.Price.Close;return (last/rows.TakeLast(count).Max(b=>b.Bar.Price.High)-1)*100;
        }
        var medium=Draw(nameof(config.CrashLookbackDays),config.CrashLookbackDays)??current.MediumDrawdown;
        var shortDrop=Draw(nameof(config.ShortCrashLookbackDays),config.ShortCrashLookbackDays)??current.ShortDrawdown;
        var rsi=current.Rsi;var rsiRows=Series(nameof(config.RsiPeriod),asOf);
        if(rsiRows!=null)
        {
            var clean=config.Clone();clean.ParameterTimeframes.Clear();var cache=new OversoldIndicatorCache(clean);
            foreach(var row in rsiRows)cache.Append(row.Bar.Price);rsi=cache.Current.Rsi;
        }
        var volume=current.VolumeRatio;var rows=Series(nameof(config.VolumeLookbackDays),asOf);
        if(rows!=null){var previous=rows.SkipLast(1).TakeLast(config.VolumeLookbackDays).ToArray();volume=previous.Length==config.VolumeLookbackDays&&previous.Average(b=>b.Bar.Price.Volume)>0?rows[^1].Bar.Price.Volume/previous.Average(b=>b.Bar.Price.Volume):null;}
        bool m=medium<=config.MinimumDrawdownPercent,s=shortDrop<=config.ShortCrashPercent;
        var trigger=m&&s?"Medium+Short":m?"Medium":s?"Short":"";
        // Preserve baseline warm-up semantics for fields that were not overridden.
        if(Series(nameof(config.CrashLookbackDays),asOf)==null&&!current.Trigger.Contains("Medium"))m=false;
        if(Series(nameof(config.ShortCrashLookbackDays),asOf)==null&&!current.Trigger.Contains("Short"))s=false;
        trigger=m&&s?"Medium+Short":m?"Medium":s?"Short":"";
        var liquidity=Series(nameof(config.AverageTradingValueLookbackDays),asOf)?.SkipLast(1).TakeLast(config.AverageTradingValueLookbackDays).Select(b=>b.Bar.TradingValue).ToArray();
        return new(medium,shortDrop,rsi,volume,trigger){IndependentTradingValues=liquidity};
    }
    public static IReadOnlyList<ISupportProvider> Providers(OversoldConfig c,HistoricalFacts facts,bool intraday=false)
    =>[new Framed(new MovingAverageSupportProvider(),c,facts,nameof(c.MovingAveragePeriods)),new Framed(new ConfirmedPivotHighProvider(intraday),c,facts,nameof(c.PivotLeftBars)),new Framed(new ApproximateVolumeZoneProvider(),c,facts,nameof(c.VolumeZoneLookbackDays))];
    private sealed class Framed(ISupportProvider provider,OversoldConfig c,HistoricalFacts facts,string key):ISupportProvider
    {
        private readonly ConfiguredIndicators data=new(c,facts);
        public IReadOnlyList<SupportCandidate> Get(IReadOnlyList<PriceBar> history,OversoldConfig config)
        {
            if(history.Count==0)return [];
            var rows=data.Series(key,history[^1].Time);if(rows==null)return provider.Get(history,config);
            var prices=rows.Select(b=>b.Bar.Price).ToArray();
            if(provider is ConfirmedPivotHighProvider)
            {
                if(!config.UsePivotSupport)return [];
                var right=(data.Series(nameof(config.PivotRightBars),history[^1].Time)?.Select(b=>b.Bar.Price)??history).ToArray();
                var found=new List<SupportCandidate>();
                for(var i=config.PivotLeftBars;i<prices.Length;i++)
                {
                    var center=prices[i];if(prices.Skip(i-config.PivotLeftBars).Take(config.PivotLeftBars).Any(b=>b.High>=center.High))continue;
                    var after=right.Where(b=>b.Time>center.Time).Take(config.PivotRightBars).ToArray();
                    if(after.Length!=config.PivotRightBars||after.Any(b=>b.High>=center.High))continue;
                    found.Add(new("PIVOT:"+center.Time.ToString("O"),SupportKind.ConfirmedPivotHigh,center.High,center.High,center.High,DailyDates.Of(center),DailyDates.Of(after[^1]),"시점별 확정 고점"));
                }
                return found;
            }
            var actual=provider;
            return actual.Get(prices,config);
        }
    }
}
