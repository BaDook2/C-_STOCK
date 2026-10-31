using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed record OversoldLiveConfiguration(OversoldConfig Configuration,BarSpecification BarSpecification,StrategyBar[] Warmup,HistoricalFacts Facts);
public sealed record OversoldPositionSnapshot(int Quantity,int Entries,decimal Average,decimal LastEntry,decimal Peak,bool TakeProfit1,bool TakeProfit2);
/// <summary>Common detector/position rules driven by completed bars; holdings change only on broker executions.</summary>
public sealed class OversoldLiveStrategy:ITradingStrategy
{
    private readonly OversoldConfig config;private readonly BarSpecification spec;private readonly HistoricalFacts facts;private readonly Instrument instrument;
    private readonly OversoldIndicatorCache cache;private readonly OversoldEntryDetector detector=new();private readonly TickCoverageTracker coverage=new();
    private readonly ISupportProvider[] providers=[new MovingAverageSupportProvider(),new ConfirmedPivotHighProvider(true),new ApproximateVolumeZoneProvider()];
    private readonly List<decimal?> values=[];private readonly List<MarketEvent> bucket=[];private readonly List<MarketEvent> unverified=[];private readonly HashSet<string> filledEntryOrders=[],executionIds=[];
    private readonly DailyMarketSession[] sessions;private SupportCluster? initialCluster;private SupportCandidate[] candidates=[];
    private int quantity,entries,peakQuantity,harvestBasis,firstDay,lastExit=-1;private decimal average,lastEntry,peak;private decimal? priorCenter;private bool tp1,tp2,harvest;
    private DateTimeOffset lastBarTime,bucketStart,bucketEnd,segmentStart;private long lastBarSequence,liveSequence;private OversoldSignal? pendingSignal,readySignal;private MarketEvent? latest;private string error="";
    public string Name=>"국내 과매도·낙주 반등";
    public OversoldDecisionSnapshot? Snapshot=>readySignal?.Snapshot;
    public int EntryStage=>entries+1;
    public OversoldPositionSnapshot Position=>new(quantity,entries,average,lastEntry,peak,tp1,tp2);
    public string Stage=>pendingSignal?.Reason??"감시";
    public OversoldLiveStrategy(Instrument instrument,OversoldLiveConfiguration requested)
    {
        this.instrument=instrument;config=requested.Configuration.Clone();config.Validate();spec=requested.BarSpecification;spec.Validate();facts=requested.Facts.Snapshot();sessions=facts.Sessions.OrderBy(s=>s.Open).ToArray();cache=new(config,true);
        foreach(var bar in requested.Warmup.OrderBy(b=>b.ClosedAt).ThenBy(b=>b.Sequence))EvaluateBar(bar,false);
        pendingSignal=null;
    }
    public void OnMarket(MarketEvent e)
    {
        if(e.Instrument!=instrument||e.Kind!=EventKind.Trade)return;
        if(error!="")return;
        e.Validate();latest=e;var priorSignal=pendingSignal;coverage.Observe(e);
        if(coverage.Current is not {} segment)
        {
            var previous=unverified.LastOrDefault();
            if(previous==null||previous.Source!=e.Source||e.ExchangeTime<previous.ExchangeTime||e.Sequence<=previous.Sequence||e.CumulativeVolume-previous.CumulativeVolume!=e.TradeSize)unverified.Clear();
            unverified.Add(e);if(unverified.Count>200000)error="원본 체결 버퍼 한도 · 다시 준비 후 시작";
            bucket.Clear();pendingSignal=readySignal=null;segmentStart=default;
            return;
        }
        if(segmentStart!=segment.From)
        {
            bucket.Clear();bucketStart=bucketEnd=default;segmentStart=segment.From;pendingSignal=readySignal=null;
            foreach(var first in unverified.Where(t=>t.ExchangeTime>=segment.From))AppendTrade(first,segment);
            unverified.Clear();
        }
        else readySignal=priorSignal!=null&&e.ExchangeTime>priorSignal.SignalTime?priorSignal:null;
        AppendTrade(e,segment);
    }
    private void AppendTrade(MarketEvent e,BarCoverage segment)
    {
        var session=sessions.FirstOrDefault(s=>e.ExchangeTime>=s.Open&&e.ExchangeTime<s.Close);
        if(session==null){error="검증된 시장 세션 밖 실제 체결";return;}
        if(spec.Unit==BarUnit.Tick)
        {
            bucket.Add(e);if(bucket.Count>spec.Value){error="틱 집계 크기 불일치";return;}
            if(bucket.Count==spec.Value){Emit(bucket[0].ExchangeTime,e.ExchangeTime,segment);bucket.Clear();}
            return;
        }
        long span=checked((long)spec.Value*(spec.Unit==BarUnit.Hour?TimeSpan.TicksPerHour:TimeSpan.TicksPerMinute));
        if(span>(session.Close-session.Open).Ticks){error="시장 세션보다 긴 지정 봉은 사용할 수 없습니다.";return;}
        var start=session.Open.AddTicks((e.ExchangeTime-session.Open).Ticks/span*span);var end=start.AddTicks(span);
        if(bucket.Count>0&&start!=bucketStart)
        {
            if(bucketEnd<=e.ExchangeTime&&bucketEnd<=session.Close)Emit(bucketStart,bucketEnd,segment);
            bucket.Clear();
        }
        if(bucket.Count==0){bucketStart=start;bucketEnd=end;}
        bucket.Add(e);
        if(bucket.Count>200000)error="원본 체결 버퍼 한도 · 다시 준비 후 시작";
    }
    private void Emit(DateTimeOffset start,DateTimeOffset end,BarCoverage segment)
    {
        if(bucket.Count==0||start<segment.From||end>segment.To)return;
        var price=new PriceBar(end,bucket[0].TradePrice!.Value,bucket.Max(t=>t.TradePrice!.Value),bucket.Min(t=>t.TradePrice!.Value),bucket[^1].TradePrice!.Value,bucket.Sum(t=>t.TradeSize!.Value));
        var bar=new StrategyBar(price,start,end,++liveSequence,bucket.Sum(t=>t.TradePrice!.Value*t.TradeSize!.Value),segment.Source);
        if(bar.ClosedAt>lastBarTime||bar.ClosedAt==lastBarTime&&bar.Sequence>lastBarSequence)EvaluateBar(bar,true);
    }
    private void EvaluateBar(StrategyBar bar,bool trading)
    {
        if(!bar.Price.IsComplete)return;
        int day=Array.FindIndex(sessions,s=>bar.OpenTime>=s.Open&&bar.ClosedAt<=s.Close);
        if(day<0){error="검증된 시장 세션 자료 없음";return;}
        if(cache.History.Count>0&&bar.ClosedAt<lastBarTime)return;
        var indicators=cache.Append(bar.Price);candidates=providers.SelectMany(p=>p.Get(cache.History,config)).ToArray();lastBarTime=bar.ClosedAt;lastBarSequence=bar.Sequence;
        var valid=HistoricalFacts.Tradable(facts.Status(instrument.Symbol,sessions[day].Date,bar.ClosedAt))&&facts.Risk(instrument.Symbol,sessions[day].Date,bar.ClosedAt)==false;
        string? reason=null;bool buy=false;SupportCluster? selected=null;AdditionalEntryEvaluation? additional=null;PositionRuleEvaluation? rules=null;
        if(quantity==0)
        {
            detector.Observe(bar.Price,day,config);
            var entry=detector.Evaluate(bar.Price,day,sessions[day].Date,config,indicators,candidates,facts.Status(instrument.Symbol,sessions[day].Date,bar.ClosedAt),facts.Risk(instrument.Symbol,sessions[day].Date,bar.ClosedAt),values.TakeLast(config.AverageTradingValueLookbackDays).ToArray(),lastExit,priorCenter);
            if(entry.Enter){reason="Entry1";buy=true;selected=entry.Selected;}
        }
        else
        {
            rules=OversoldPositionRules.Evaluate(config,bar.Price,initialCluster!.Lower,average,peak,day-firstDay+1,tp1);
            reason=rules.Exit;
            if(reason==null)
            {
                if(config.UseTakeProfit1&&!tp1&&rules.High>=average*(1+config.TakeProfit1Percent/100))reason="TP1";
                else if(config.UseTakeProfit2&&!tp2&&rules.High>=average*(1+config.TakeProfit2Percent/100))reason="TP2";
                else if(!harvest&&entries<config.MaxEntries)
                {
                    selected=SupportClustering.Build(candidates,config.SupportClusterRangePercent).Where(c=>c.Count>=config.MinimumSupportCount&&c.Approached(bar.Price.Close,config.SupportDistancePercent)).OrderBy(c=>Math.Abs(c.Center/bar.Price.Close-1)).ThenBy(c=>c.Center).FirstOrDefault();
                    additional=OversoldPositionRules.Additional(config,lastEntry,bar.Price.Close,selected,valid);
                    if(additional.FinalDecision){reason="Entry"+(entries+1);buy=true;}
                }
            }
            if(rules.TrailingActive)peak=Math.Max(peak,rules.High);
        }
        values.Add(bar.TradingValue);
        if(!trading)return;
        pendingSignal=reason==null?null:new(bar.ClosedAt,buy?"Buy":"Sell",reason,indicators,selected??initialCluster,OversoldState.Holding,OversoldPositionRules.Snapshot(bar.ClosedAt,bar.Price.Close,indicators,candidates,config,selected,additional,rules,reason));
    }
    public StrategySignal Evaluate(MarketContext context)
    {
        if(error!="")throw new InvalidOperationException(error);
        if(readySignal==null||latest==null||latest.ExchangeTime<=readySignal.SignalTime)return new(Name,null,null,"완성봉 이후 실제 체결 대기");
        return new(Name,readySignal.Action=="Buy",context.Price,readySignal.Reason);
    }
    public int OrderQuantity(decimal price)
    {
        if(readySignal==null||price<=0)return 0;
        if(readySignal.Action=="Buy")return entries>=config.MaxEntries?0:(int)Math.Min(int.MaxValue,decimal.Floor(config.PlannedInvestment*config.Ratios()[entries]/100/price));
        int basis=harvestBasis>0?harvestBasis:peakQuantity;
        return Math.Min(quantity,readySignal.Reason=="TP1"?(int)decimal.Floor(basis*config.TakeProfit1Ratio/100):readySignal.Reason=="TP2"?(int)decimal.Floor(basis*config.TakeProfit2Ratio/100):quantity);
    }
    public void OnExecution(BrokerExecution execution,string reason,OversoldDecisionSnapshot? snapshot)
    {
        if(execution.Symbol!=instrument.Symbol||execution.Quantity<=0||execution.Price<=0||string.IsNullOrWhiteSpace(execution.Id)||string.IsNullOrWhiteSpace(execution.OrderNumber))throw new InvalidOperationException("전략과 실제 체결의 식별자·종목·수량이 일치하지 않습니다.");
        if(!executionIds.Add(execution.Id))return;
        var day=Array.FindIndex(sessions,s=>execution.Time>=s.Open&&execution.Time<=s.Close);
        if(day<0)throw new InvalidOperationException("실제 체결의 시장 세션을 확인할 수 없습니다.");
        if(execution.Buy)
        {
            if(quantity==0){initialCluster=snapshot?.Clusters.Where(c=>c.UsedForEntry).Select(c=>new SupportCluster(c.Lower,c.Center,c.Upper,c.Supports)).FirstOrDefault()??throw new InvalidOperationException("최초 체결의 진입 군집 snapshot 없음");firstDay=day;peak=execution.Price;}
            average=(average*quantity+execution.Price*execution.Quantity)/(quantity+execution.Quantity);quantity+=execution.Quantity;peakQuantity=Math.Max(peakQuantity,quantity);lastEntry=execution.Price;
            if(filledEntryOrders.Add(execution.OrderNumber))entries++;
        }
        else
        {
            if(execution.Quantity>quantity)throw new InvalidOperationException("전략 소유 수량을 초과하는 체결입니다.");quantity-=execution.Quantity;
            if(reason is "TP1" or "TP2"){if(!harvest){harvestBasis=peakQuantity;peak=execution.Price;}harvest=true;if(reason=="TP1")tp1=true;else tp2=true;}
            if(quantity==0){priorCenter=initialCluster!.Center;lastExit=day;detector.Reset();entries=peakQuantity=harvestBasis=0;tp1=tp2=harvest=false;average=lastEntry=peak=0;initialCluster=null;filledEntryOrders.Clear();}
        }
        pendingSignal=readySignal=null;
    }
}
