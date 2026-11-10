using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed class DayTradingPreparation(AppServices services,MarketHistoryService history,Func<IAccountBroker?> account)
{
    public async Task<PreparedDayTrading[]> Prepare(StockItem[] items,StrategyDefinition definition,DateOnly first,DateOnly last,HistoricalFacts input,bool assumptions,CancellationToken token)
    {
        if(items.Any(s=>s.Exchange!=Exchange.KRX))throw new InvalidOperationException("전략 D 1차 버전은 국내 일반주식 전용입니다.");
        var frame=definition.DayTrading!.SimpleTimeframe;if(account()==null&&frame.Unit!=TimeframeUnit.TradingDay)throw new InvalidOperationException("오프라인 D 연구는 일봉만 지원합니다. 분·시간봉 또는 원본 재생은 온라인 자료가 필요합니다.");
        var output=new List<PreparedDayTrading>();
        foreach(var stock in items)
        {
            token.ThrowIfCancellationRequested();var acquisition=definition.Configuration.Clone();acquisition.UseCrashStrategy=true;acquisition.CrashStrategy=definition;var loaded=await history.Get(stock,acquisition,input,null,first,last,token);var facts=input.Snapshot();var sessions=facts.Sessions.ToDictionary(s=>s.Date);var seeds=new List<KnownStrategyBar>();
            foreach(var bar in loaded.Dataset.CompletedBars.Where(b=>b.Time<=DateTimeOffset.UtcNow).OrderBy(b=>b.Time))
            {
                var date=DailyDates.Of(bar);var session=sessions.GetValueOrDefault(date)??ResearchMarketSession.For(date,stock.Exchange);sessions.TryAdd(date,session);
                var close=session?.Close??bar.Time;var open=session?.Open??new DateTimeOffset(date.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9));
                var value=facts.TradingValue(stock.Symbol,date,close)??(assumptions?(bar.Open+bar.High+bar.Low+bar.Close)/4*bar.Volume:(decimal?)null);
                seeds.Add(new(new(1,TimeframeUnit.TradingDay),new(bar with{Time=close},open,close,seeds.Count+1,value,loaded.Dataset.Source+(assumptions?" · 연구가정 세션/거래대금 추정":"")),close));
            }
            seeds.AddRange(loaded.Facts.StrategyFrames.Select(s=>!assumptions&&s.Bar.Source.Contains("추정")?s with{Bar=s.Bar with{TradingValue=null}}:s));
            facts.MarketSessions=sessions.Values.OrderBy(s=>s.Open).ToArray();if(assumptions)facts.Source+=" · 연구가정: 관측 OHLC 일자 정규장 시간";facts.Validate();
            StrategyBar[] selected;
            if(frame.Unit==TimeframeUnit.TradingDay)selected=seeds.Where(s=>s.Timeframe==frame).Select(s=>s.Bar).DistinctBy(b=>b.ClosedAt).Where(b=>DailyDates.Of(b.Price)>=first&&DailyDates.Of(b.Price)<=last).ToArray();
            else if(frame.Unit is TimeframeUnit.Minute or TimeframeUnit.Hour)
            {
                var spec=new BarSpecification(frame.Size,frame.Unit==TimeframeUnit.Minute?BarUnit.Minute:BarUnit.Hour);var fetch=definition.Configuration.Clone();fetch.MovingAveragePeriods="1";fetch.UsePivotSupport=false;fetch.UseApproximateVolumeZone=false;
                var data=await history.Get(stock,fetch,facts,spec,first,last,token);var available=loaded.Facts.StrategyFrames.Where(b=>b.Timeframe==frame).Select(b=>b.Bar).ToArray();if(available.Length==0)available=data.Bars!;
                if(!assumptions)available=available.Select(b=>b.Source.Contains("추정")?b with{TradingValue=null}:b).ToArray();
                if(assumptions)facts=data.Facts;seeds.AddRange(available.Select(b=>new KnownStrategyBar(frame,b,b.ClosedAt)));selected=available.Where(b=>DailyDates.Of(b.Price)>=first&&DailyDates.Of(b.Price)<=last).ToArray();
            }
            else selected=seeds.Where(s=>s.Timeframe==frame).Select(s=>s.Bar).Where(b=>DailyDates.Of(b.Price)>=first&&DailyDates.Of(b.Price)<=last).ToArray();
            var begin=new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));var end=new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));
            var raw=services.Store.ReadEvents(stock.Key,"KIWOOM",begin,end).ToArray();output.Add(new(stock,new(new(stock.Symbol,stock.Exchange),definition,new(seeds.ToArray(),facts),definition.Preset),selected,raw));
        }
        return output.ToArray();
    }
    public IMarketDataProvider Feed(StockItem[] stocks)
    {
        if(account() is not KiwoomBroker broker||broker.Exchange!=Exchange.KRX)throw new InvalidOperationException("전략 D 원본 감시는 온라인 키움 국내 연결이 필요합니다. 공개 폴링은 원본 틱으로 대체하지 않습니다.");
        if(stocks.Length is <1 or >20)throw new InvalidOperationException("1차 D 원본 가상 실행은 한 구독의20종목 이내입니다.");
        return new KiwoomMarketDataProvider(broker.GetTokenAsync,stocks.Select(s=>new Instrument(s.Symbol,s.Exchange)).ToArray(),new DataSettings(),_=>{},live:broker.IsLive);
    }
}
