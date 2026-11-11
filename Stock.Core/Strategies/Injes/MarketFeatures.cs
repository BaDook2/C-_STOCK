using Stock.Core.Research;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public enum FundamentalRiskState { Unknown, Cleared, RiskDetected }
public enum UsMarketPhase { PreMarket, Regular, AfterHours }
public sealed record InjesSessionInfo(string Id,DateOnly MarketDate,UsMarketPhase Phase,DateTimeOffset Open,DateTimeOffset Close,
    bool TradingAllowed,bool CorporateActionsVerified,DateTimeOffset KnownAt)
{
    public OrderKind[] AllowedOrderKinds {get;init;}=[];
}
public sealed record InjesKnownBar(PriceBar Bar,DateTimeOffset ClosedAt,DateTimeOffset KnownAt);
public sealed record SessionVolumePoint(string SessionId,DateOnly MarketDate,UsMarketPhase Phase,int ElapsedSeconds,decimal Volume,DateTimeOffset KnownAt);
public sealed record InjesReferenceData(DateTimeOffset KnownAt)
{
    public decimal? PreviousClose {get;init;} public decimal? SessionOpen {get;init;}
    public decimal? MarketCap {get;init;} public decimal? Float {get;init;}
    public decimal? DailyTradingValue {get;init;} public decimal? AverageDailyTradingValue {get;init;}
    public decimal? CumulativeSessionVolume {get;init;}
    public decimal? TickSize {get;init;}
    public FundamentalRiskState FundamentalRisk {get;init;}
    public bool? Halted {get;init;}
    public InjesKnownBar[] DailyBars {get;init;}=[];
    public SessionVolumePoint[] HistoricalSessionVolumes {get;init;}=[];
}
/// <summary>Point-in-time enrichment belongs to the data adapter, never to a strategy's optimistic defaults.</summary>
public sealed record InjesObservation(MarketEvent Market,DateTimeOffset KnownAt,InjesSessionInfo? Session=null,InjesReferenceData? Reference=null);
public sealed record InjesAdmission(bool Accepted,bool Duplicate,bool Discontinuity,string Reason);
public sealed record WindowFeatures(bool Complete,int Count,decimal Volume,decimal? StartPrice,decimal? High,decimal? Low,decimal? RealizedVolatility)
{
    public decimal? VolumePerSecond(int seconds)=>Complete?Volume/seconds:null;
}
public sealed record InjesFeatures(DateTimeOffset At,decimal? Price,decimal? DayGainPercent,decimal? ObservedHigh,
    decimal? Bid,decimal? Ask,decimal? BidSize,decimal? AskSize,decimal? TickSize,decimal? SpreadPercent,
    decimal? SpreadTicks,decimal? QuoteChangesPerSecond,decimal? Reacceleration,decimal? Rvol,
    IReadOnlyDictionary<int,WindowFeatures> Windows,InjesReferenceData? Reference,InjesSessionInfo? Session,
    MarketEvent? LastQuote,DateTimeOffset? LastTradeAt,DateTimeOffset? LastQuoteAt,bool FreshTrade,bool FreshQuote)
{
    public MarketEvent? LastTrade {get;init;}
    public int MaximumVolumeBins {get;init;}=20000;
    public WindowFeatures Window(int seconds)=>Windows.GetValueOrDefault(seconds)??new(false,0,0,null,null,null,null);
    public decimal? Momentum(int seconds)=>Price>0&&Window(seconds) is {Complete:true,StartPrice:>0} w?(Price/w.StartPrice-1)*100:null;
    public decimal? VolumeRatio(int seconds)
    {var a=Window(seconds);var b=Window(2*seconds);var previous=b.Volume-a.Volume;return a.Complete&&b.Complete&&previous>0?a.Volume/previous:null;}
}

/// <summary>O(1) amortized updates per declared window; no tick-history scan on every evaluation.</summary>
internal sealed class RollingTradeWindow(int seconds)
{
    private readonly Queue<(long Id,DateTimeOffset At,decimal Price,decimal Size,double LogSquare)> values=[];
    private readonly LinkedList<(long Id,decimal Price)> highs=[],lows=[];
    private decimal volume; private double logSum;private long sequence;
    private DateTimeOffset? first;private decimal? previous,baseline;
    public int Count=>values.Count;
    public void Add(MarketEvent e)
    {
        first??=e.ExchangeTime;var price=e.TradePrice!.Value;
        var log=previous>0?Math.Log((double)(price/previous.Value)):0;previous=price;var id=++sequence;
        values.Enqueue((id,e.ExchangeTime,price,e.TradeSize!.Value,log*log));volume+=e.TradeSize.Value;logSum+=log*log;
        while(highs.Last is {} h&&h.Value.Price<=price)highs.RemoveLast();highs.AddLast((id,price));
        while(lows.Last is {} l&&l.Value.Price>=price)lows.RemoveLast();lows.AddLast((id,price));Expire(e.ExchangeTime);
    }
    public WindowFeatures Snapshot(DateTimeOffset at)
    {
        Expire(at);var complete=first<=at.AddSeconds(-seconds)&&values.Count>0;
        return new(complete,values.Count,volume,baseline??(complete&&values.TryPeek(out var v)?v.Price:null),highs.First?.Value.Price,lows.First?.Value.Price,
            complete?(decimal)Math.Sqrt(Math.Max(0,logSum))*100:null);
    }
    private void Expire(DateTimeOffset at)
    {
        while(values.TryPeek(out var v)&&v.At<=at.AddSeconds(-seconds))
        {values.Dequeue();baseline=v.Price;volume-=v.Size;logSum-=v.LogSquare;if(highs.First?.Value.Id==v.Id)highs.RemoveFirst();if(lows.First?.Value.Id==v.Id)lows.RemoveFirst();}
    }
}

/// <summary>Causal per-symbol feature stream. Invalid/late/duplicate data cannot advance candidate state or fill orders.</summary>
public sealed class InjesFeatureEngine
{
    private readonly Instrument instrument;private readonly InjesExecutionPolicy policy;private readonly int[] durations;
    private readonly Dictionary<int,RollingTradeWindow> windows=[];
    private readonly Dictionary<(EventKind Kind,string Source),(long Sequence,DateTimeOffset At)> previous=[];
    private readonly Queue<DateTimeOffset> quoteChanges=[];
    private DateTimeOffset? tradeAt,quoteAt,quoteStart;private DateTimeOffset lastAccepted;
    private decimal? price,high;private MarketEvent? quote,lastTrade;private string sessionId="",source="";
    private InjesReferenceData? reference;private InjesSessionInfo? session;private int buffered;
    private SessionVolumePoint[]? indexedHistory;private Dictionary<(UsMarketPhase Phase,int Elapsed),SessionVolumePoint[]> historyIndex=[];
    public int Rejected {get;private set;} public int Duplicates {get;private set;}
    public InjesFeatureEngine(Instrument instrument,InjesExecutionPolicy policy,int[] windows)
    {instrument.Validate();if(instrument.Currency!="USD")throw new InvalidOperationException("Inje 전략은 미국주식 USD 자료를 사용합니다.");this.instrument=instrument;this.policy=policy;durations=windows.Distinct().ToArray();ResetWindows();}
    private void ResetWindows(){windows.Clear();foreach(var duration in durations)windows[duration]=new(duration);quoteChanges.Clear();quoteStart=null;buffered=0;}
    public InjesAdmission Add(InjesObservation input,DateTimeOffset now)
    {
        var e=input.Market;
        InjesAdmission Reject(string reason){Rejected++;return new(false,false,false,reason);}
        try{e.Validate();}catch(InvalidOperationException x){return Reject(x.Message);}
        if(e.TimeQuality!="거래소시간")return Reject("MissingData: 거래소 발생시각 품질 미확인");
        if(e.Instrument!=instrument||input.KnownAt==default||input.KnownAt>now||input.KnownAt<e.ReceivedTime||e.AvailableAt>input.KnownAt||e.ReceivedTime>now||e.ExchangeTime>e.ReceivedTime||e.ExchangeTime<lastAccepted)return Reject("MissingData: 미래·역순·잘못된 가용시각/종목");
        if((now-e.ExchangeTime).TotalMilliseconds>policy.MaximumDataAgeMilliseconds||(now-e.ReceivedTime).TotalMilliseconds>policy.MaximumDataAgeMilliseconds)return Reject("MissingData: 시세 지연");
        if(input.Session is {} s&&(string.IsNullOrWhiteSpace(s.Id)||s.KnownAt==default||s.KnownAt>now||s.KnownAt>input.KnownAt||s.Open>=s.Close||s.MarketDate!=UsSessionClock.MarketDate(e.ExchangeTime)||e.ExchangeTime<s.Open||e.ExchangeTime>=s.Close||!Enum.IsDefined(s.Phase)||s.AllowedOrderKinds==null||s.AllowedOrderKinds.Any(k=>!Enum.IsDefined(k))))return Reject("MissingData: 세션 시점/형식");
        if(input.Reference is {} r&&(r.KnownAt==default||r.KnownAt>now||r.KnownAt>input.KnownAt||!Enum.IsDefined(r.FundamentalRisk)||r.DailyBars==null||r.HistoricalSessionVolumes==null||r.TickSize<=0||r.MarketCap<0||r.Float<0||r.DailyTradingValue<0||r.AverageDailyTradingValue<0||r.CumulativeSessionVolume<0))return Reject("MissingData: 기준 자료 시점/위험값");
        var sessionChanged=input.Session is {} upcoming&&sessionId!=upcoming.Id;
        if(sessionChanged&&(e.ExchangeTime<input.Session!.Open||e.ExchangeTime>=input.Session.Close))return Reject("MissingData: 새 세션과 시장 발생시각 불일치");
        var sourceChanged=source!=""&&source!=e.Source;
        var key=(e.Kind,e.Source);
        if(!sessionChanged&&previous.TryGetValue(key,out var priorEvent)&&e.Sequence<=priorEvent.Sequence)
        {if(e.Sequence==priorEvent.Sequence&&e.ExchangeTime==priorEvent.At){Duplicates++;return new(false,true,false,"Duplicate");}return Reject("MissingData: 역순/중복 시퀀스");}
        if(previous.Count>64)return Reject("MissingData: 과도한 원천 변경, 새 세션 필요");
        bool discontinuity=sourceChanged||e.Kind==EventKind.Trade&&tradeAt is {} last&&(e.ExchangeTime-last).TotalMilliseconds>policy.MaximumTradeGapMilliseconds;
        if(input.Session is {} next&&sessionId!=next.Id)
        {session=next;sessionId=next.Id;reference=null;high=price=null;tradeAt=quoteAt=null;quote=lastTrade=null;previous.Clear();ResetWindows();discontinuity=true;}
        if(sourceChanged){reference=null;tradeAt=quoteAt=null;quote=lastTrade=null;price=high=null;}
        if(discontinuity)ResetWindows();
        previous[key]=(e.Sequence,e.ExchangeTime);source=e.Source;lastAccepted=e.ExchangeTime;
        if(input.Session!=null)session=input.Session;
        if(input.Reference!=null)reference=input.Reference;
        if(e.Kind==EventKind.Trade)
        {
            foreach(var w in windows.Values)w.Add(e);
            buffered=windows.Count==0?0:windows.Values.Max(w=>w.Count);
            if(buffered>policy.MaximumBufferedEvents){ResetWindows();tradeAt=null;return Reject("MissingData: 체결 버퍼 한도 초과, 재준비 필요");}
            price=e.TradePrice;tradeAt=e.ExchangeTime;lastTrade=e with{AvailableAt=input.KnownAt};high=high==null?price:Math.Max(high.Value,price!.Value);
        }
        else
        {
            if(quoteAt is {} prior&&(e.ExchangeTime-prior).TotalMilliseconds>policy.MaximumTradeGapMilliseconds){quoteChanges.Clear();quoteStart=e.ExchangeTime;}
            quoteStart??=e.ExchangeTime;
            if(quote!=null&&(quote.Bid!=e.Bid||quote.Ask!=e.Ask))quoteChanges.Enqueue(e.ExchangeTime);
            quote=e with{AvailableAt=input.KnownAt};quoteAt=e.ExchangeTime;
            if(quoteChanges.Count>policy.MaximumBufferedEvents){quoteChanges.Clear();quoteStart=e.ExchangeTime;discontinuity=true;}
        }
        return new(true,false,discontinuity,discontinuity?"자료/세션 변경: 새 후보 필요":"Accepted");
    }
    public InjesFeatures Snapshot(DateTimeOffset now,int rvolSessions=20,int quoteWindowSeconds=30)
    {
        var analysisAt=lastAccepted==default?now:lastAccepted;
        var stats=windows.ToDictionary(x=>x.Key,x=>x.Value.Snapshot(analysisAt));
        while(quoteChanges.TryPeek(out var at)&&at<=now.AddSeconds(-quoteWindowSeconds))quoteChanges.Dequeue();
        var freshTrade=tradeAt is {} t&&(now-t).TotalMilliseconds<=policy.MaximumDataAgeMilliseconds;
        var freshQuote=quoteAt is {} q&&(now-q).TotalMilliseconds<=policy.MaximumDataAgeMilliseconds&&quote?.Bid>0&&quote.Ask>quote.Bid&&quote.BidSize>=0&&quote.AskSize>=0;
        var tick=reference?.TickSize;decimal? spread=freshQuote?quote!.Ask-quote.Bid:null;
        decimal? reacceleration=null;
        if(stats.GetValueOrDefault(10) is {Complete:true} current&&stats.GetValueOrDefault(40) is {Complete:true} whole&&whole.Volume>current.Volume)reacceleration=(current.Volume/10)/((whole.Volume-current.Volume)/30);
        decimal? rvol=null;
        if(session is {} s&&reference is {CumulativeSessionVolume:>=0} r&&r.KnownAt<=now)
        {
            var elapsed=(int)((tradeAt??analysisAt)-s.Open).TotalSeconds;
            if(!ReferenceEquals(indexedHistory,r.HistoricalSessionVolumes))
            {indexedHistory=r.HistoricalSessionVolumes;historyIndex=indexedHistory.GroupBy(x=>(x.Phase,x.ElapsedSeconds)).ToDictionary(g=>g.Key,g=>g.OrderByDescending(x=>x.MarketDate).ThenByDescending(x=>x.KnownAt).ToArray());}
            var history=(historyIndex.GetValueOrDefault((s.Phase,elapsed))??[]).Where(x=>!string.IsNullOrWhiteSpace(x.SessionId)&&x.KnownAt!=default&&x.KnownAt<=now&&x.MarketDate<s.MarketDate&&x.Volume>=0)
                .DistinctBy(x=>x.MarketDate).Take(rvolSessions).ToArray();
            if(history.Length==rvolSessions&&history.Average(x=>x.Volume)>0)rvol=r.CumulativeSessionVolume/history.Average(x=>x.Volume);
        }
        return new(now,price,price>0&&reference?.PreviousClose>0?(price/reference.PreviousClose-1)*100:null,high,quote?.Bid,quote?.Ask,quote?.BidSize,quote?.AskSize,tick,
            spread/quote?.Bid*100,tick>0?spread/tick:null,quoteStart<=now.AddSeconds(-quoteWindowSeconds)?(decimal)quoteChanges.Count/quoteWindowSeconds:null,
            reacceleration,rvol,stats,reference,session,quote,tradeAt,quoteAt,freshTrade,freshQuote){LastTrade=lastTrade,MaximumVolumeBins=policy.MaximumVolumeBins};
    }
}

public static class UsSessionClock
{
    private static readonly TimeZoneInfo Eastern=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    public static DateOnly MarketDate(DateTimeOffset at)=>DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at,Eastern).DateTime);
    /// <summary>DST-aware phase bounds only. Provider confirmation is still required for holidays, halts and order permissions.</summary>
    public static InjesSessionInfo Bounds(DateTimeOffset at,UsMarketPhase phase,bool providerConfirmed=false)
    {
        var local=TimeZoneInfo.ConvertTime(at,Eastern);var date=DateOnly.FromDateTime(local.DateTime);
        var (start,end)=phase switch{UsMarketPhase.PreMarket=>(new TimeOnly(4,0),new TimeOnly(9,30)),UsMarketPhase.Regular=>(new TimeOnly(9,30),new TimeOnly(16,0)),UsMarketPhase.AfterHours=>(new TimeOnly(16,0),new TimeOnly(20,0)),_=>throw new InvalidOperationException("잘못된 미국 세션")};
        DateTimeOffset Stamp(TimeOnly time){var dt=date.ToDateTime(time);return new(dt,Eastern.GetUtcOffset(dt));}
        return new($"US:{date:yyyyMMdd}:{phase}",date,phase,Stamp(start),Stamp(end),providerConfirmed,providerConfirmed,at);
    }
}
