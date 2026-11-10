using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record KnownStrategyBar(TimeframeSpec Timeframe,StrategyBar Bar,DateTimeOffset KnownAt);
public sealed record StrategyDDataSeed(KnownStrategyBar[] Bars,HistoricalFacts Facts);
public sealed record CrashSnapshot(decimal? Medium,decimal? Short,decimal? Session,decimal? Rolling,bool? Passed,string Types);
public sealed record StrategyDObservation(DateTimeOffset Time,long Sequence,decimal Price,DailyMarketSession Session,
    FeedObservation Feed,CrashSnapshot Crash,StrategyDSupport[] Supports,decimal? Rsi,decimal? VolumeRatio,decimal? AverageTradingValue,
    bool? SecurityTradable,bool? FundamentalRisk,bool IndicatorProvisional,bool ClosedBarChanged,string DataReason)
{public int AnalysisBarOrdinal {get;init;}}

/// <summary>Single causal data pipeline for D screening, replay and paper execution. No orders or persistence.</summary>
public sealed class StrategyDDataEngine
{
    private sealed class Series(int capacity,int rsiPeriod,int volumePeriod)
    {
        public readonly Queue<StrategyBar> Bars=[];
        public int Version {get;private set;}
        public decimal? Rsi {get;private set;}
        public decimal? VolumeRatio {get;private set;}
        public decimal? AverageTradingValue {get;private set;}
        public int TradingValuePeriod {get;set;}
        public DateTimeOffset KnownAt {get;private set;}
        private decimal? previous;private decimal gain,loss;private int changes;
        public void Add(StrategyBar bar,DateTimeOffset knownAt)
        {
            if(!bar.Price.IsComplete||Bars.Count>0&&bar.ClosedAt<=Bars.Last().ClosedAt)return;
            if(previous is {} prev)
            {
                var delta=bar.Price.Close-prev;var up=Math.Max(0,delta);var down=Math.Max(0,-delta);changes++;
                if(changes<=rsiPeriod){gain+=up/rsiPeriod;loss+=down/rsiPeriod;}
                else{gain=(gain*(rsiPeriod-1)+up)/rsiPeriod;loss=(loss*(rsiPeriod-1)+down)/rsiPeriod;}
                if(changes>=rsiPeriod)Rsi=loss==0?(gain==0?50:100):100-100/(1+gain/loss);
            }
            var prior=Bars.TakeLast(volumePeriod).ToArray();
            VolumeRatio=prior.Length==volumePeriod&&prior.Average(b=>b.Price.Volume)>0?bar.Price.Volume/prior.Average(b=>b.Price.Volume):null;
            Bars.Enqueue(bar);while(Bars.Count>capacity)Bars.Dequeue();previous=bar.Price.Close;KnownAt=knownAt;Version++;
            var liquidity=Bars.TakeLast(TradingValuePeriod).ToArray();
            AverageTradingValue=liquidity.Length==TradingValuePeriod&&liquidity.All(b=>b.TradingValue.HasValue)?liquidity.Average(b=>b.TradingValue!.Value):null;
        }
    }
    private static readonly TimeframeSpec Daily=new(1,TimeframeUnit.TradingDay);
    private readonly Instrument instrument;
    private readonly StrategyDefinition definition;
    private readonly OversoldConfig shared;
    private readonly DayTradingPolicy policy;
    private readonly HistoricalFacts facts;
    private readonly CausalMarketInput input;
    private readonly Dictionary<TimeframeSpec,Series> series=[];
    private readonly Dictionary<TimeframeSpec,CausalBarBuilder> builders=[];
    private readonly KnownStrategyBar[] seeds;
    private readonly Queue<MarketEvent> rolling=[];
    private readonly LinkedList<MarketEvent> rollingMax=[];
    private readonly TradeVolumeProfile profile;
    private readonly bool closedBarResearch;
    private readonly Queue<StrategyBar> approximateRolling=[];
    private DateTimeOffset? previousApproximateClose,approximateCaptureStart;
    private readonly TimeframeSpec maFrame,profileFrame;
    private int lastClosedAnalysisVersion=-1;
    private int seedIndex,maVersion=-1,profileVersion=-1;
    private DateOnly? day;
    private decimal sessionHigh;
    private bool rollingOverflow;
    private DateTimeOffset captureStart;
    private (int Period,decimal Sum,int Count)[] maTotals=[];
    private VolumeZone[] approximate=[];
    private int dailyVersion=-1,priorDailyCount,supportMaVersion=-1,supportProfileVersion=-1;
    private DateOnly cachedDailyDate,cachedMaDate;
    private decimal mediumPriorHigh,shortPriorHigh;
    private StrategyDSupport[] cachedSupports=[];
    public StrategyDObservation? Latest {get;private set;}
    public StrategyDObservation? LatestClosedAnalysis {get;private set;}
    public MarketEvent? LatestQuote {get;private set;}
    public int RejectedEvents=>input.Rejected;
    public int DuplicateEvents=>input.Duplicates;
    public int RetainedTradeEvents=>rolling.Count+profile.RetainedEvents;
    public Stock.Core.Research.PriceBar[] AnalysisPrices()=>series[policy.SimpleTimeframe].Bars.Select(b=>b.Price with{Time=b.OpenTime})
        .Concat(builders[policy.SimpleTimeframe].Active is {} active?[active.Price with{Time=active.OpenTime}]:[]).OrderBy(b=>b.Time).TakeLast(2000).ToArray();

    public StrategyDDataEngine(Instrument instrument,StrategyDefinition requested,StrategyDDataSeed seed,bool closedBarResearch=false)
    {
        definition=requested.Snapshot();this.closedBarResearch=closedBarResearch;if(!definition.IsStrategyD)throw new InvalidOperationException("전략 D 입력 정의가 필요합니다.");
        if(instrument.Exchange!=Exchange.KRX)throw new InvalidOperationException("전략 D 1차 실행은 국내 일반주식으로 제한합니다.");
        this.instrument=instrument;shared=definition.Configuration;policy=definition.DayTrading!;facts=seed.Facts.Snapshot();
        maFrame=policy.AdvancedTimeframes&&policy.IndicatorTimeframes.TryGetValue(IndicatorKind.MovingAverage,out var maOverride)?maOverride:policy.Support.MovingAverageTimeframe;
        profileFrame=policy.AdvancedTimeframes&&policy.IndicatorTimeframes.TryGetValue(IndicatorKind.VolumeProfile,out var vpOverride)?vpOverride:policy.Support.ProfileTimeframe;
        input=new(instrument,policy.Quality,facts.Sessions);
        profile=new(instrument,shared.VolumeZoneBinSize,shared.VolumeZoneMaxBins,policy.Quality.MaximumBufferedEvents);
        seeds=seed.Bars.Select(b=>b with{}).OrderBy(b=>b.KnownAt).ThenBy(b=>b.Bar.ClosedAt).ToArray();
        foreach(var item in seeds)
        {
            item.Timeframe.Validate();item.Bar.Price.Validate();
            if(!item.Bar.Price.IsComplete||item.KnownAt<item.Bar.ClosedAt||item.Bar.OpenTime>item.Bar.ClosedAt||string.IsNullOrWhiteSpace(item.Bar.Source))
                throw new InvalidOperationException("준비 봉은 확정/출처/사용가능시각을 확인해야 합니다.");
        }
        foreach(var spec in new[]{Daily,policy.SimpleTimeframe,policy.Support.MovingAverageTimeframe,policy.Support.ProfileTimeframe}.Concat(policy.IndicatorTimeframes.Values).Distinct())
        {
            var capacity=Math.Max(shared.MaPeriods().Max()+1,Math.Max(shared.VolumeZoneLookbackDays,Math.Max(shared.RsiPeriod+1,Math.Max(shared.VolumeLookbackDays+1,shared.AverageTradingValueLookbackDays))));
            series[spec]=new(capacity,shared.RsiPeriod,shared.VolumeLookbackDays){TradingValuePeriod=shared.AverageTradingValueLookbackDays};builders[spec]=new(spec,policy.Quality.MaximumBufferedEvents);
        }
    }
    private TimeframeSpec Indicator(IndicatorKind kind)=>policy.AdvancedTimeframes&&policy.IndicatorTimeframes.TryGetValue(kind,out var spec)?spec:policy.SimpleTimeframe;
    public FeedObservation OnMarket(MarketEvent e,DateTimeOffset now)
    {
        if(closedBarResearch)throw new InvalidOperationException("근사 봉 연구와 원본 체결 입력을 혼합하지 마세요.");
        var admitted=input.Observe(e,now);if(!admitted.Accepted)return admitted;
        if(e.Kind==EventKind.Quote){LatestQuote=e;return admitted;}
        var session=admitted.Session!;
        if(day!=session.Date){day=session.Date;sessionHigh=0;rolling.Clear();rollingMax.Clear();rollingOverflow=false;}
        if(input.Coverage is {} coverage)captureStart=coverage.From;
        else{profile.Clear();captureStart=e.ExchangeTime;}
        while(seedIndex<seeds.Length&&seeds[seedIndex].KnownAt<=e.ExchangeTime)
        {
            var item=seeds[seedIndex++];if(series.TryGetValue(item.Timeframe,out var existing))existing.Add(item.Bar,item.KnownAt);
            if(item.Timeframe.Unit!=TimeframeUnit.TradingDay&&DailyQuoteMerger.Date(item.Bar.ClosedAt,instrument.Exchange)==session.Date)sessionHigh=Math.Max(sessionHigh,item.Bar.Price.High);
        }
        bool closed=false;StrategyBar? closedAnalysis=null;LatestClosedAnalysis=null;
        foreach(var (spec,builder) in builders)
        foreach(var bar in builder.Add(e,session,input.Coverage)){series[spec].Add(bar,e.ExchangeTime);closed=true;if(spec==policy.SimpleTimeframe)closedAnalysis=bar;}
        var rollingFrom=RollingStart(e.ExchangeTime);
        while(rolling.Count>0&&rolling.Peek().ExchangeTime<rollingFrom)
        {var old=rolling.Dequeue();if(rollingMax.First?.Value==old)rollingMax.RemoveFirst();}
        if(closedAnalysis!=null&&policy.SimpleTimeframe.Unit!=TimeframeUnit.Trade)
            LatestClosedAnalysis=Calculate(e.ExchangeTime,e.Sequence,closedAnalysis.Price.Close,admitted,session,true);
        var price=e.TradePrice!.Value;sessionHigh=Math.Max(sessionHigh,price);
        if(rolling.Count>=policy.Quality.MaximumBufferedEvents){rollingOverflow=true;rolling.Dequeue();}
        rolling.Enqueue(e);while(rollingMax.Last is {} tail&&tail.Value.TradePrice<=price)rollingMax.RemoveLast();rollingMax.AddLast(e);
        var profileFrom=ProfileStart(e.ExchangeTime,session);
        if(policy.Support.ProfileSource==VolumeProfileSource.TradeBased)
        {
            if(profileFrame.Unit==TimeframeUnit.Trade)profile.KeepLast(checked(profileFrame.Size*shared.VolumeZoneLookbackDays)-1);
            profile.Add(e,profileFrom);
        }
        Latest=Calculate(e.ExchangeTime,e.Sequence,e.TradePrice!.Value,admitted,session,closed);
        if(closedAnalysis!=null&&policy.SimpleTimeframe.Unit==TimeframeUnit.Trade)LatestClosedAnalysis=Latest;
        return Latest.Feed;
    }
    public void SupplyFrames(IEnumerable<KnownStrategyBar> input,DateTimeOffset asOf)
    {
        foreach(var seed in input.Where(b=>b.KnownAt<=asOf).OrderBy(b=>b.KnownAt))
        {
            if(seed.KnownAt<seed.Bar.ClosedAt||!seed.Bar.Price.IsComplete)throw new InvalidOperationException("미확정 전략 봉은 사용할 수 없습니다.");
            if(series.TryGetValue(seed.Timeframe,out var existing))existing.Add(seed.Bar,seed.KnownAt);
        }
    }
    public DailyMarketSession ValidateClosedBar(StrategyBar bar)
    {
        bar.Price.Validate();
        if(!closedBarResearch||!bar.Price.IsComplete||bar.OpenTime>=bar.ClosedAt||string.IsNullOrWhiteSpace(bar.Source))throw new InvalidOperationException("확정봉 연구용 자료/출처가 필요합니다.");
        return facts.Sessions.FirstOrDefault(s=>bar.OpenTime>=s.Open&&bar.ClosedAt<=s.Close)??throw new InvalidOperationException("확인된 시장 세션 안의 봉이 아닙니다.");
    }
    /// <summary>Explicit close-only approximation. It never invents constituent ticks, their lows or intrabar ordering.</summary>
    public FeedObservation OnClosedBar(StrategyBar bar)
    {
        var session=ValidateClosedBar(bar);
        if(previousApproximateClose is {} previous&&bar.ClosedAt<=previous)return new(FeedDecision.Duplicate,false,"중복/역순 확정봉",session){Approximate=true};
        var gap=previousApproximateClose is {} end&&end<bar.OpenTime&&day==session.Date;
        if(day!=session.Date){day=session.Date;sessionHigh=0;approximateRolling.Clear();approximateCaptureStart=bar.OpenTime;}
        if(gap){approximateRolling.Clear();approximateCaptureStart=bar.OpenTime;}
        previousApproximateClose=bar.ClosedAt;
        var beforeAnalysis=lastClosedAnalysisVersion;
        while(seedIndex<seeds.Length&&seeds[seedIndex].KnownAt<=bar.ClosedAt)
        {var seed=seeds[seedIndex++];if(series.TryGetValue(seed.Timeframe,out var existing))existing.Add(seed.Bar,seed.KnownAt);}
        if(policy.SimpleTimeframe.Unit==TimeframeUnit.TradingDay||policy.SimpleTimeframe.Duration==bar.ClosedAt-bar.OpenTime)series[policy.SimpleTimeframe].Add(bar,bar.ClosedAt);sessionHigh=Math.Max(sessionHigh,bar.Price.High);
        var cutoff=RollingStart(bar.ClosedAt);
        while(approximateRolling.Count>0&&approximateRolling.Peek().ClosedAt<=cutoff)approximateRolling.Dequeue();
        approximateRolling.Enqueue(bar);
        var overflow=approximateRolling.Count>policy.Quality.MaximumBufferedEvents;
        while(approximateRolling.Count>policy.Quality.MaximumBufferedEvents)approximateRolling.Dequeue();
        if(overflow)approximateCaptureStart=approximateRolling.Peek().OpenTime;
        decimal? rollingDrop=approximateCaptureStart<=cutoff?(bar.Price.Close/approximateRolling.Max(b=>b.Price.High)-1)*100:null;
        var feed=new FeedObservation(FeedDecision.Accepted,!gap&&!overflow,overflow?"봉 메모리 한도 · 연속 구간 재확보 필요":"완성봉 종가 근사 · 장중 틱 순서 미재현",session){Approximate=true,Discontinuity=gap||overflow};
        Latest=Calculate(bar.ClosedAt,bar.Sequence,bar.Price.Close,feed,session,true,rollingDrop,true);LatestClosedAnalysis=series[policy.SimpleTimeframe].Version>beforeAnalysis?Latest:null;lastClosedAnalysisVersion=series[policy.SimpleTimeframe].Version;
        return Latest.Feed;
    }
    private StrategyDObservation Calculate(DateTimeOffset asOf,long sequence,decimal price,FeedObservation admitted,DailyMarketSession session,bool closed,decimal? approximateRolling=null,bool approximateMode=false)
    {
        var rollingFrom=RollingStart(asOf);
        var profileFrom=ProfileStart(asOf,session);
        if(dailyVersion!=series[Daily].Version||cachedDailyDate!=session.Date)
        {
            dailyVersion=series[Daily].Version;cachedDailyDate=session.Date;
            var daily=series[Daily].Bars.Where(b=>DailyQuoteMerger.Date(b.ClosedAt,instrument.Exchange)<session.Date).ToArray();priorDailyCount=daily.Length;
            mediumPriorHigh=daily.TakeLast(shared.CrashLookbackDays-1).Select(b=>b.Price.High).DefaultIfEmpty(0).Max();
            shortPriorHigh=daily.TakeLast(shared.ShortCrashLookbackDays-1).Select(b=>b.Price.High).DefaultIfEmpty(0).Max();
        }
        decimal? Draw(int n,decimal priorHigh)=>priorDailyCount>=n-1?(price/Math.Max(sessionHigh,priorHigh)-1)*100:null;
        var medium=Draw(shared.CrashLookbackDays,mediumPriorHigh);var shortTerm=Draw(shared.ShortCrashLookbackDays,shortPriorHigh);
        var sessionDrop=(price/sessionHigh-1)*100;var rollingDrop=approximateMode?approximateRolling:captureStart<=rollingFrom&&rollingMax.First is {} max?(price/max.Value.TradePrice!.Value-1)*100:(decimal?)null;
        var independent=new ConfiguredIndicators(shared,facts).Apply(new(medium??0,shortTerm??0,null,null,""),asOf);
        if(shared.ParameterTimeframes.ContainsKey(nameof(shared.CrashLookbackDays)))medium=independent.MediumDrawdown;
        if(shared.ParameterTimeframes.ContainsKey(nameof(shared.ShortCrashLookbackDays)))shortTerm=independent.ShortDrawdown;
        var dailyPassed=Combine(policy.Crash.DailyCombination,(policy.Crash.MediumEnabled,medium.HasValue?medium<=shared.MinimumDrawdownPercent:null),(policy.Crash.ShortEnabled,shortTerm.HasValue?shortTerm<=shared.ShortCrashPercent:null));
        var intradayPassed=Combine(policy.Crash.IntradayCombination,(policy.Crash.SessionHighEnabled,sessionDrop<=policy.Crash.SessionDrawdownPercent),(policy.Crash.RollingHighEnabled,rollingDrop.HasValue?rollingDrop<=policy.Crash.RollingDrawdownPercent:null));
        var passed=Combine(policy.Crash.DailyIntradayCombination,(policy.Crash.DailyEnabled,dailyPassed),(policy.Crash.IntradayEnabled,intradayPassed));
        var kinds=new List<string>();if(policy.Crash.DailyEnabled&&policy.Crash.MediumEnabled&&medium<=shared.MinimumDrawdownPercent)kinds.Add("DailyMedium");if(policy.Crash.DailyEnabled&&policy.Crash.ShortEnabled&&shortTerm<=shared.ShortCrashPercent)kinds.Add("DailyShort");if(policy.Crash.IntradayEnabled&&policy.Crash.SessionHighEnabled&&sessionDrop<=policy.Crash.SessionDrawdownPercent)kinds.Add("SessionHigh");if(policy.Crash.IntradayEnabled&&policy.Crash.RollingHighEnabled&&rollingDrop<=policy.Crash.RollingDrawdownPercent)kinds.Add("RollingHigh");
        var maSeries=series[maFrame];
        if(maVersion!=maSeries.Version||cachedMaDate!=session.Date)
        {
            maVersion=maSeries.Version;cachedMaDate=session.Date;
            var history=maSeries.Bars.Where(b=>maFrame.Unit!=TimeframeUnit.TradingDay||DailyQuoteMerger.Date(b.ClosedAt,instrument.Exchange)<session.Date).ToArray();
            maTotals=shared.MaPeriods().Select(n=>{var count=n-(policy.Support.IncludeActiveMovingAverageBar?1:0);var bars=history.TakeLast(count).ToArray();return(Period:n,Sum:bars.Sum(b=>b.Price.Close),Count:bars.Length);}).ToArray();
        }
        var averages=maTotals.Where(m=>m.Count==m.Period-(policy.Support.IncludeActiveMovingAverageBar?1:0)).Select(m=>(m.Period,Price:(m.Sum+(policy.Support.IncludeActiveMovingAverageBar?price:0))/m.Period)).ToArray();
        VolumeZone[] zones;
        if(policy.Support.ProfileSource==VolumeProfileSource.Approximate)
        {
            var profileSeries=series[profileFrame];
            if(profileVersion!=profileSeries.Version){profileVersion=profileSeries.Version;approximate=StrategyDSupports.Approximate(profileSeries.Bars.Select(b=>b.Price).ToArray(),shared,profileSeries.KnownAt);}
            zones=approximate;
        }
        else zones=!profile.Overflowed&&captureStart<=profileFrom&&(profileFrame.Unit!=TimeframeUnit.Trade||profile.RetainedEvents>=profileFrame.Size*shared.VolumeZoneLookbackDays)?StrategyDSupports.FromBins(profile.Bins(),shared.VolumeZoneThresholdPercent,shared.VolumeZoneMergeRangePercent,"수신한 개별 체결량",ProfilePrecision.Traded,asOf):[];
        StrategyDSupport[] supports;
        if(policy.Support.ProfileSource==VolumeProfileSource.Approximate&&!policy.Support.IncludeActiveMovingAverageBar)
        {
            if(supportMaVersion!=maSeries.Version||supportProfileVersion!=series[profileFrame].Version)
            {supportMaVersion=maSeries.Version;supportProfileVersion=series[profileFrame].Version;cachedSupports=StrategyDSupports.Overlap(averages,maFrame,zones,policy.Support,asOf);}
            supports=cachedSupports.ToArray();
        }
        else supports=StrategyDSupports.Overlap(averages,maFrame,zones,policy.Support,asOf);
        var averageValue=series[Indicator(IndicatorKind.Liquidity)].AverageTradingValue;
        var status=facts.Status(instrument.Symbol,session.Date,asOf);var risk=facts.Risk(instrument.Symbol,session.Date,asOf);
        var security=status==null||status.Kind==HistoricalSecurityKind.Unknown||status.Board==HistoricalBoard.Unknown||status.Suspended==null||status.Administrative==null?null:(bool?)HistoricalFacts.Tradable(status);
        var dataReason=rollingOverflow||profile.Overflowed||builders.Values.Any(b=>b.Overflowed)?"시장자료 버퍼 한도 초과 · 신규 진입 차단":admitted.Reason;
        if(dataReason.Contains("한도"))admitted=admitted with{CanEnter=false,Reason=dataReason};
        return new(asOf,sequence,price,session,admitted,new(medium,shortTerm,sessionDrop,rollingDrop,passed,string.Join("+",kinds)),supports,series[Indicator(IndicatorKind.Rsi)].Rsi,series[Indicator(IndicatorKind.Volume)].VolumeRatio,averageValue,security,risk,policy.Support.IncludeActiveMovingAverageBar,closed,dataReason){AnalysisBarOrdinal=series[policy.SimpleTimeframe].Version};
    }
    private DateTimeOffset RollingStart(DateTimeOffset now)
    {
        var window=policy.Crash.RollingWindow;
        if(window.Duration is {} duration)return now-duration;
        if(window.Unit==TimeframeUnit.Week)return now.AddDays(-7*window.Size);
        if(window.Unit==TimeframeUnit.Month)return now.AddMonths(-window.Size);
        return facts.Sessions.Where(s=>s.Open<=now).TakeLast(window.Size).FirstOrDefault()?.Open??now.AddDays(-window.Size);
    }
    private DateTimeOffset ProfileStart(DateTimeOffset now,DailyMarketSession current)
    {
        var spec=profileFrame;
        if(spec.Duration is {} duration)
        {var ticks=(decimal)duration.Ticks*shared.VolumeZoneLookbackDays;return ticks>=(now-DateTimeOffset.MinValue).Ticks?DateTimeOffset.MinValue:now-TimeSpan.FromTicks((long)ticks);}
        if(spec.Unit==TimeframeUnit.TradingDay)
        {var sessions=facts.Sessions.Where(s=>s.Open<=now).TakeLast(shared.VolumeZoneLookbackDays).ToArray();return sessions.Length==shared.VolumeZoneLookbackDays?sessions[0].Open:DateTimeOffset.MinValue;}
        if(spec.Unit is TimeframeUnit.Week or TimeframeUnit.Month)return spec.Unit==TimeframeUnit.Week?now.AddDays(-7L*spec.Size*shared.VolumeZoneLookbackDays):now.AddMonths(-Math.Min(120000,spec.Size*shared.VolumeZoneLookbackDays));
        // A trade-count profile needs the complete requested count, not an elapsed-time substitute.
        return captureStart;
    }
    private static bool? Combine(RuleCombination mode,params (bool Enabled,bool? Passed)[] rules)
    {
        var enabled=rules.Where(r=>r.Enabled).ToArray();if(enabled.Length==0)return false;
        if(mode==RuleCombination.Any&&enabled.Any(r=>r.Passed==true))return true;
        if(mode==RuleCombination.All&&enabled.Any(r=>r.Passed==false))return false;
        if(enabled.Any(r=>r.Passed==null))return null;return mode==RuleCombination.All;
    }
}
