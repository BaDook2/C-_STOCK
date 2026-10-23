namespace Stock.Core.Trading;
public sealed class MarketContext(Instrument instrument,TradingSettings settings)
{
    public Instrument Instrument { get; }=instrument;
    public TradingSettings Settings { get; }=settings;
    public List<MarketEvent> Trades { get; }=[];
    public List<(DateTimeOffset Time,decimal Spread)> Spreads { get; }=[];
    public MarketEvent? Last { get; private set; }
    public MarketEvent? Quote { get; private set; }
    public DateTimeOffset Now { get; private set; }
    public decimal Price => Last?.TradePrice??0;
    public decimal? Spread => Quote?.Bid>0 && Quote.Ask>=Quote.Bid ? (Quote.Ask-Quote.Bid)/Quote.Bid*100 : null;
    public decimal ObservedHigh { get; private set; }
    public DateOnly Day { get; private set; }
    public Position? Position { get; set; }
    public bool QuoteFresh => Quote!=null && Now>=Quote.ExchangeTime && (Now-Quote.ExchangeTime).TotalSeconds<=Settings.Execution.StaleSeconds;
    public void Add(MarketEvent e)
    {
        Now=e.ExchangeTime;var day=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(Now,Instrument.Exchange==Exchange.KRX?"Korea Standard Time":"Eastern Standard Time").DateTime);
        if(Day!=day){Trades.Clear();Spreads.Clear();Quote=null;ObservedHigh=0;Day=day;}
        if(e.Kind==EventKind.Quote){Quote=e;if(Spread is decimal sp)Spreads.Add((Now,sp));}
        else {Last=e;Trades.Add(e);ObservedHigh=Math.Max(ObservedHigh,e.TradePrice??0);}
        if(Trades.Count>Settings.Data.MaxHistory)Trades.RemoveRange(0,Trades.Count-Settings.Data.MaxHistory);
        if(Spreads.Count>Settings.Data.MaxHistory)Spreads.RemoveRange(0,Spreads.Count-Settings.Data.MaxHistory);
    }
    public bool Warm(int seconds) => Trades.Count>1 && (Now-Trades[0].ExchangeTime).TotalSeconds>=seconds;
    public List<MarketEvent> Window(int seconds,int offset=0) => Trades.Where(t=>t.ExchangeTime>Now.AddSeconds(-seconds-offset)&&t.ExchangeTime<=Now.AddSeconds(-offset)).ToList();
    public decimal Rate(int seconds,int offset=0) => Window(seconds,offset).Sum(t=>t.TradeSize??0)/seconds;
    public decimal Frequency(int seconds,int offset=0) => (decimal)Window(seconds,offset).Count/seconds;
    public decimal Atr()
    {var n=Settings.Risk.AtrPeriod; if(Trades.Count<n+1)return 0;var a=Trades.TakeLast(n+1).Select(t=>t.TradePrice!.Value).ToArray();return Enumerable.Range(1,n).Average(i=>Math.Abs(a[i]-a[i-1]));}
}
public interface INewsAnalyzer { Task<NewsAnalysisResult> AnalyzeAsync(string symbol); }
public record NewsAnalysisResult(bool Excluded,string Reason);
public sealed class MockNewsAnalyzer(IReadOnlySet<string> excluded) : INewsAnalyzer
{ public Task<NewsAnalysisResult> AnalyzeAsync(string symbol)=>Task.FromResult(new NewsAnalysisResult(excluded.Contains(symbol),excluded.Contains(symbol)?"사용자 수동 악재 제외":"Mock: 외부 뉴스 분석 없음")); }
public sealed class CommonScanner(TradingSettings settings)
{
    public string? Reject(MarketContext c)
    {
        if(settings.Data.ExcludedSymbols.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Contains(c.Instrument.Key))return "수동 악재 제외";
        if(c.Last==null)return "체결 데이터 부족";
        var change=c.Last.PreviousClose>0?(c.Price/c.Last.PreviousClose-1)*100:null;
        foreach(var rule in settings.Filters.Where(r=>r.Enabled))
        {
            decimal? value=rule.Name switch {"가격"=>c.Price,"시가총액"=>c.Last.MarketCap,"일평균 거래량"=>c.Last.AverageDailyVolume,"일평균 거래대금"=>c.Last.AverageDailyTurnover,"당일 거래량"=>c.Last.CumulativeVolume,"상승률 %"=>change>=0?change:null,"하락률 %"=>change<=0?-change:null,"스프레드 %"=>c.QuoteFresh?c.Spread:null,_=>null};
            if(value==null)return rule.Name+": 데이터 없음/방향 불일치";
            if(rule.Minimum?value<rule.Value:value>rule.Value)return rule.Name+": 필터 미충족";
        }
        return null;
    }
}
public interface ITradingStrategy { string Name { get; } StrategySignal Evaluate(MarketContext context); }
public sealed class ScalpingStrategy : ITradingStrategy
{
    public string Name=>"호가 스캘핑";
    public StrategySignal Evaluate(MarketContext c)
    {
        var s=c.Settings.Scalping;StrategySignal Hold(string reason)=>new(Name,null,null,reason);
        if(!s.Enabled)return Hold("OFF");
        if(!c.QuoteFresh||c.Quote?.BidSize==null||c.Quote.AskSize==null)return Hold("신선한 호가/잔량 부족");
        if(c.Position?.Quantity>0)
        {if(c.Position.Owner!=Name)return Hold("다른 전략 보유: 공통 리스크가 청산 관리");var price=s.Exit switch {ScalpExit.Ticks=>c.Position.AveragePrice+s.ExitValue*c.Instrument.Tick(c.Price),ScalpExit.Percent=>c.Position.AveragePrice*(1+s.ExitValue/100),_=>c.Quote.Ask!.Value};return new(Name,false,price,"설정된 호가 스캘핑 청산 목표");}
        if(!c.Warm(Math.Max(s.AverageSeconds+s.CurrentSeconds,s.WeakLongSeconds+s.WeakShortSeconds)))return Hold("거래량 비교 준비 데이터 부족");
        if(c.Price<s.MinPrice||c.Price>s.MaxPrice)return Hold("전략 가격 범위 밖");
        var baseline=c.Rate(s.AverageSeconds,s.CurrentSeconds);var relative=baseline>0?c.Rate(s.CurrentSeconds)/baseline:0;
        var weakBase=c.Rate(s.WeakLongSeconds,s.WeakShortSeconds);var freqBase=c.Frequency(s.WeakLongSeconds,s.WeakShortSeconds);
        var priorSp=c.Spreads.Where(v=>v.Time<c.Now.AddSeconds(-s.WeakShortSeconds)&&v.Time>=c.Now.AddSeconds(-s.WeakLongSeconds-s.WeakShortSeconds)).Select(v=>v.Spread).DefaultIfEmpty(0).Average();
        var weak=(weakBase>0&&c.Rate(s.WeakShortSeconds)<weakBase*s.WeakVolumeRatio?1:0)+(freqBase>0&&c.Frequency(s.WeakShortSeconds)<freqBase*s.WeakTradesRatio?1:0)+(priorSp>0&&c.Spread>priorSp*s.WeakSpreadMultiple?1:0);
        if(weak>=s.WeakCount)return Hold($"거래 약화: {weak}/3 조건");
        if(s.UseLevel2&&(c.Quote.Bids?.Count<2||c.Quote.Asks?.Count<2||c.Quote.Bids==null||c.Quote.Asks==null))return Hold("L2 데이터 부족: 거래 금지");
        var bid=s.UseLevel2?c.Quote.Bids!.Sum(x=>x.Size):c.Quote.BidSize.Value;var ask=s.UseLevel2?c.Quote.Asks!.Sum(x=>x.Size):c.Quote.AskSize.Value;
        if(relative<s.RelativeVolume||c.Frequency(s.CurrentSeconds)<s.TradesPerSecond||c.Spread>s.MaxSpread||bid<s.MinBidSize||ask<s.MinAskSize||ask<=0||bid/ask<s.BidAskRatio)return Hold($"조건 미충족: 상대거래량={relative:F2}, spread={c.Spread:F4}%");
        return new(Name,true,c.Quote.Bid+s.BuyTicks*c.Instrument.Tick(c.Price),$"초당거래량 배수={relative:F2}, 잔량비={bid/ask:F2}");
    }
}
public static class SupportResistanceService
{
    public static int Touches(IReadOnlyList<MarketEvent> history,decimal level,decimal tolerance,int gap)
    {
        var inside=false;DateTimeOffset? previous=null;var count=0;
        foreach(var t in history){var now=Math.Abs(t.TradePrice!.Value-level)<=level*tolerance/100;if(now&&!inside&&(previous==null||(t.ExchangeTime-previous.Value).TotalSeconds>=gap)){count++;previous=t.ExchangeTime;}inside=now;}return count;
    }
    public static IReadOnlyList<decimal> Levels(MarketContext c)
    {
        var s=c.Settings.Analysis;var h=c.Window(s.LookbackSeconds);var levels=new List<decimal>();
        // A pivot only becomes known after its right-hand neighbours have actually arrived.
        for(int i=s.PivotRadius;i<h.Count-s.PivotRadius;i++) {var p=h[i].TradePrice!.Value;var w=h.Skip(i-s.PivotRadius).Take(2*s.PivotRadius+1).Select(x=>x.TradePrice!.Value).ToList();if(p==w.Min()||p==w.Max())if(Touches(h,p,s.TolerancePercent,c.Settings.Box.TouchGapSeconds)>=s.MinimumTouches&&!levels.Any(l=>Math.Abs(l-p)<=p*s.TolerancePercent/100))levels.Add(p);}
        return levels;
    }
}
public static class VolumeProfileService
{
    public static IReadOnlyList<(decimal Price,decimal Volume)> Calculate(MarketContext c)
    {var s=c.Settings.Analysis;var step=c.Instrument.Tick(c.Price)*s.ProfileTicks;return c.Window(s.ProfileSeconds).GroupBy(t=>decimal.Floor(t.TradePrice!.Value/step)*step).Select(g=>(Price:g.Key,Volume:g.Sum(x=>x.TradeSize??0))).OrderByDescending(x=>x.Volume).Take(s.ProfileTop).ToList();}
}
public sealed class BoxStrategy : ITradingStrategy
{
    public string Name=>"박스권";
    public StrategySignal Evaluate(MarketContext c)
    {
        var s=c.Settings.Box;StrategySignal Hold(string why)=>new(Name,null,null,why);if(!s.Enabled)return Hold("OFF");if(!c.Warm(s.LookbackSeconds))return Hold("박스 기간 준비 부족");
        var h=c.Window(s.LookbackSeconds).Where(t=>t.ExchangeTime<c.Now).ToList();if(h.Count<2||(h[^1].ExchangeTime-h[0].ExchangeTime).TotalSeconds<s.MinimumSeconds)return Hold("박스 지속시간 부족");
        var low=h.Min(t=>t.TradePrice!.Value);var high=h.Max(t=>t.TradePrice!.Value);var width=high-low;
        if(c.Price<low*(1-s.BreakPercent/100)||c.Price>high*(1+s.BreakPercent/100))return Hold("기존 박스 붕괴");
        if(width<=0||width/low*100>s.WidthPercent||c.Rate(s.LookbackSeconds)>s.MaxVolumePerSecond)return Hold("박스 폭/거래량 조건 미충족");
        if(SupportResistanceService.Touches(h,low,s.TolerancePercent,s.TouchGapSeconds)<s.SupportTouches||SupportResistanceService.Touches(h,high,s.TolerancePercent,s.TouchGapSeconds)<s.ResistanceTouches)return Hold("독립 지지/저항 터치 부족");
        if(s.DecliningVolume&&c.Rate(s.LookbackSeconds/2)>=c.Rate(s.LookbackSeconds/2,s.LookbackSeconds/2))return Hold("거래량 감소 미충족");
        if(c.Position?.Quantity>0&&c.Position.Owner==Name&&c.Price>=high-width*s.ExitZonePercent/100)return new(Name,false,c.Price,"박스 상단 도달");
        if(s.UseSupportLevels&&!SupportResistanceService.Levels(c).Any(p=>Math.Abs(c.Price/p-1)*100<=s.TolerancePercent))return Hold("확정 지지선 근처 아님");
        if(s.UseVolumeProfile&&!VolumeProfileService.Calculate(c).Any(p=>Math.Abs(c.Price/p.Price-1)*100<=s.TolerancePercent))return Hold("실제 틱 매물대 근처 아님");
        if(!(c.Position?.Quantity>0)&&c.Price<=low+width*s.EntryZonePercent/100)return new(Name,true,c.Price,"박스 하단 + 독립 터치 확인");return Hold("박스 내부 대기");
    }
}
public sealed class ReboundStrategy : ITradingStrategy
{
    private sealed record Candidate(DateTimeOffset Since,decimal Low,int Up,decimal Previous);
    private readonly Dictionary<string,Candidate> candidates=[];
    public string Name=>"급락 반등";
    public StrategySignal Evaluate(MarketContext c)
    {
        var s=c.Settings.Rebound;StrategySignal Hold(string why)=>new(Name,null,null,why);if(!s.Enabled)return Hold("OFF");if(c.Position?.Quantity>0)return Hold("보유중: 공통 손절/익절 관리");
        if(candidates.TryGetValue(c.Instrument.Key,out var prior))
        {
            if((c.Now-prior.Since).TotalSeconds>s.CandidateSeconds){candidates.Remove(c.Instrument.Key);return Hold("반등 후보 만료");}
            var next=prior with {Low=Math.Min(prior.Low,c.Price),Up=c.Price>prior.Previous?prior.Up+1:0,Previous=c.Price};candidates[c.Instrument.Key]=next;
            if(next.Up>=s.ConfirmTicks&&c.Price>=next.Low*(1+s.BouncePercent/100)){candidates.Remove(c.Instrument.Key);return new(Name,true,c.Price,$"저점 {next.Low} 대비 반등 + {next.Up}회 상승 확인");}
            return Hold("급락 후보: 반등 확인 대기");
        }
        var checks=new List<bool>();
        foreach(var x in new[]{(s.Use1,s.Seconds1,s.Drop1),(s.Use2,s.Seconds2,s.Drop2),(s.Use3,s.Seconds3,s.Drop3)}) if(x.Item1){if(!c.Warm(x.Item2)){if(s.RequireAll)return Hold("급락 비교 준비 부족");continue;}var anchor=c.Trades.LastOrDefault(t=>t.ExchangeTime<=c.Now.AddSeconds(-x.Item2));checks.Add(anchor?.TradePrice>0&&(c.Price/anchor.TradePrice-1)*100<=-x.Item3);}
        if(s.UseHigh)checks.Add(c.ObservedHigh>0&&(c.Price/c.ObservedHigh-1)*100<=-s.HighDrop);
        if(checks.Count>0&&(s.RequireAll?checks.All(x=>x):checks.Any(x=>x))) {candidates[c.Instrument.Key]=new(c.Now,c.Price,0,c.Price);return Hold("급락 후보 등록: 즉시 매수하지 않음");}
        return Hold("급락 조건 미충족/데이터 부족");
    }
}
