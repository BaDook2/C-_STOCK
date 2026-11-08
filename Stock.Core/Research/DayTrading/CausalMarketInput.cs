using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public enum FeedDecision { Accepted, Duplicate, Invalid, OutOfOrder, Stale, OutsideSession, UnverifiedContinuity }
public sealed record FeedObservation(FeedDecision Decision,bool CanEnter,string Reason,DailyMarketSession? Session)
{
    public bool Accepted=>Decision is FeedDecision.Accepted or FeedDecision.UnverifiedContinuity;
    public bool Discontinuity {get;init;}
    public bool Approximate {get;init;}
}

/// <summary>Per-instrument causal admission. Quote sequences and trade sequences are independent.</summary>
public sealed class CausalMarketInput
{
    private readonly Instrument instrument;
    private readonly DataQualityPolicy policy;
    private readonly DailyMarketSession[] sessions;
    private readonly Dictionary<EventKind,MarketEvent> prior=[];
    private TickCoverageTracker coverage=new();
    private DateOnly? sessionDate;
    private DateTimeOffset lastAccepted;
    public BarCoverage? Coverage=>coverage.Current;
    public int Duplicates {get;private set;}
    public int Rejected {get;private set;}

    public CausalMarketInput(Instrument instrument,DataQualityPolicy policy,IReadOnlyList<DailyMarketSession> sessions)
    {
        instrument.Validate();this.instrument=instrument;this.policy=policy;
        if(sessions.Any(s=>s.Open>=s.Close)||sessions.OrderBy(s=>s.Open).Zip(sessions.OrderBy(s=>s.Open).Skip(1)).Any(p=>p.First.Close>p.Second.Open))
            throw new InvalidOperationException("시장 세션은 겹치지 않는 유효 시간이어야 합니다.");
        this.sessions=sessions.OrderBy(s=>s.Open).ToArray();
    }
    public FeedObservation Observe(MarketEvent e,DateTimeOffset now)
    {
        FeedObservation Reject(FeedDecision d,string reason){Rejected++;return new(d,false,reason,null);}
        try{e.Validate();}catch(InvalidOperationException ex){return Reject(FeedDecision.Invalid,ex.Message);}
        if(e.Instrument!=instrument||e.ReceivedTime==default||e.ExchangeTime>now||e.ReceivedTime>now)
            return Reject(FeedDecision.Invalid,"종목/수신시각 또는 미래 이벤트를 확인하세요.");
        if((now-e.ExchangeTime).TotalMilliseconds>policy.MaximumAgeMilliseconds||(now-e.ReceivedTime).TotalMilliseconds>policy.MaximumAgeMilliseconds)
            return Reject(FeedDecision.Stale,"시세 지연 · 신규 진입 차단");
        var session=sessions.FirstOrDefault(s=>e.ExchangeTime>=s.Open&&e.ExchangeTime<s.Close);
        if(session==null)return Reject(FeedDecision.OutsideSession,"확인된 시장 세션 밖 자료");
        if(sessionDate!=session.Date){prior.Clear();coverage=new();sessionDate=session.Date;}
        if(prior.TryGetValue(e.Kind,out var previous))
        {
            if(e==previous){Duplicates++;return new(FeedDecision.Duplicate,false,"중복 이벤트",session);}
            if(e.ExchangeTime<previous.ExchangeTime||e.Source==previous.Source&&e.Sequence<=previous.Sequence)
                return Reject(FeedDecision.OutOfOrder,"역순 또는 충돌한 이벤트 식별자");
        }
        if(e.ExchangeTime<lastAccepted)return Reject(FeedDecision.OutOfOrder,"체결/호가 간 역순 이벤트");
        prior[e.Kind]=e;lastAccepted=e.ExchangeTime;
        var beforeCoverage=coverage.Current;
        if(e.Kind==EventKind.Trade)coverage.Observe(e);
        var broken=e.Kind==EventKind.Trade&&beforeCoverage!=null&&(coverage.Current==null||coverage.Current.From!=beforeCoverage.From);
        var continuous=!policy.RequireContinuousTrades||coverage.Current is {} span&&span.Source==e.Source&&span.To>=e.ExchangeTime;
        if(policy.RequireVerifiedExchangeTime&&e.TimeQuality!="거래소시간")
            return new(FeedDecision.UnverifiedContinuity,false,"거래소 시각 품질 미확인 · 신규 진입 차단",session){Discontinuity=broken};
        return new(continuous?FeedDecision.Accepted:FeedDecision.UnverifiedContinuity,continuous,
            continuous?"확인된 체결 입력":"연속 체결 수집 구간 미확인 · 신규 진입 차단",session){Discontinuity=broken};
    }
}

/// <summary>Bounded active bucket, no gap filling. Consumes admitted trades and exposes active/closed separately.</summary>
public sealed class CausalBarBuilder
{
    private readonly TimeframeSpec spec;
    private readonly int maximumEvents;
    private DateTimeOffset open,end,lastTradeTime;
    private decimal first,high,low,last,volume,notional;
    private int count;
    private long sequence;
    private string source="";
    private bool completeCoverage;
    public StrategyBar? Active=>count==0?null:Build(false,lastTradeTime);
    public bool Overflowed {get;private set;}

    public CausalBarBuilder(TimeframeSpec spec,int maximumEvents)
    {spec.Validate();if(maximumEvents<=0)throw new ArgumentOutOfRangeException(nameof(maximumEvents));this.spec=spec;this.maximumEvents=maximumEvents;}
    public StrategyBar[] Add(MarketEvent e,DailyMarketSession session,BarCoverage? coverage)
    {
        if(e.Kind!=EventKind.Trade)throw new InvalidOperationException("봉 생성에는 개별 체결이 필요합니다.");
        e.Validate();if(e.ExchangeTime<session.Open||e.ExchangeTime>=session.Close)throw new InvalidOperationException("세션 밖 봉 입력입니다.");
        if(e.ExchangeTime<lastTradeTime)throw new InvalidOperationException("봉 입력은 시간순이어야 합니다.");
        var closed=new List<StrategyBar>();
        var bucketOpen=spec.Unit==TimeframeUnit.Trade?e.ExchangeTime:spec.Unit==TimeframeUnit.TradingDay?session.Open:
            session.Open.AddTicks((e.ExchangeTime-session.Open).Ticks/spec.Duration!.Value.Ticks*spec.Duration.Value.Ticks);
        var bucketEnd=spec.Unit==TimeframeUnit.Trade?e.ExchangeTime:spec.Unit==TimeframeUnit.TradingDay?session.Close:bucketOpen+spec.Duration!.Value;
        if(count>0&&(e.ExchangeTime>=end&&spec.Unit!=TimeframeUnit.Trade||source!=e.Source||open<session.Open))
        {
            if(spec.Unit!=TimeframeUnit.Trade&&end<=session.Close&&completeCoverage&&coverage is {} c&&c.From<=open&&c.To>=end)
                closed.Add(Build(true,end));
            Reset();
        }
        if(count==0)
        {
            open=bucketOpen;end=bucketEnd;first=high=low=e.TradePrice!.Value;source=e.Source;
            completeCoverage=coverage is {} c&&c.Complete&&c.Source==e.Source&&c.From<=open;
        }
        // Once continuity is proven it may cover the first event of the active bucket too.
        if(coverage is {} segment&&segment.Complete&&segment.Source==source&&segment.From<=open)completeCoverage=true;
        if(coverage==null||coverage.Source!=source||coverage.From>open)completeCoverage=false;
        var price=e.TradePrice!.Value;high=Math.Max(high,price);low=Math.Min(low,price);last=price;
        volume+=e.TradeSize!.Value;notional+=price*e.TradeSize.Value;count++;lastTradeTime=e.ExchangeTime;
        if(count>maximumEvents){Overflowed=true;Reset();return closed.ToArray();}
        if(spec.Unit==TimeframeUnit.Trade&&count==spec.Size)
        {if(completeCoverage)closed.Add(Build(true,e.ExchangeTime));Reset();}
        return closed.ToArray();
    }
    /// <summary>A feed watermark proves that an idle bucket ended. No watermark means no fabricated close.</summary>
    public StrategyBar? CloseThrough(DateTimeOffset watermark,DailyMarketSession session,BarCoverage? coverage)
    {
        if(spec.Unit==TimeframeUnit.Trade||count==0||end>watermark||end>session.Close||!completeCoverage||coverage is not {Complete:true} c||c.From>open||c.To<end)return null;
        var result=Build(true,end);Reset();return result;
    }
    private StrategyBar Build(bool closed,DateTimeOffset time)=>new(new(time,first,high,low,last,volume,closed),open,closed?time:end,closed?++sequence:sequence,notional,source);
    private void Reset(){count=0;volume=notional=0;completeCoverage=false;}
}
