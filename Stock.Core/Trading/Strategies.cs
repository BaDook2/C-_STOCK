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
/// <summary>Compatibility contracts for old account journals. Retired algorithms contain no executable entry/exit logic.</summary>
public static class RetiredStrategy
{
    public static InvalidOperationException Error()=>new("기존 전략은 제거되었습니다. Inje's Strategy와 원본 미국 틱·호가 자료를 사용하세요.");
}
public sealed class ScalpingStrategy:ITradingStrategy
{public string Name=>"제거된 전략";public StrategySignal Evaluate(MarketContext context)=>throw RetiredStrategy.Error();}
public sealed class BoxStrategy:ITradingStrategy
{public string Name=>"제거된 전략";public StrategySignal Evaluate(MarketContext context)=>throw RetiredStrategy.Error();}
public sealed class ReboundStrategy:ITradingStrategy
{public string Name=>"제거된 전략";public StrategySignal Evaluate(MarketContext context)=>throw RetiredStrategy.Error();}
