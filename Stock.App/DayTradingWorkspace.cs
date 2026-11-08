using System.Windows.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    public DayTradingViewModel DayTrading {get;private set;}=null!;
    private DispatcherTimer? dayTradingTimer;
    private StockItem[] dayTradingTargets=[];
    private void InitializeDayTrading()
    {
        DayTrading=new(store,strategyFileDialogs,alert,PrepareDayTrading,CreateDayTradingFeed,()=>dayTradingTargets);
        DayTrading.ResearchCompleted+=record=>{researchRecords[record.Id]=new(record.Id,record.At,"전략 D",record.Name,record.Period,"완료 · 원본/봉 근사 구분");FilterResearchHistory();};
        DayTrading.PaperFillsChanged+=ReloadTradeMarkers;
        dayTradingTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};dayTradingTimer.Tick+=(_,_)=>
        {
            try{DayTrading.RefreshLive();}
            catch(Exception ex) when(ex is not OutOfMemoryException){dayTradingTimer.Stop();DayTrading.BlockNewEntries("가상 실행 표시/기록 오류: "+ex.Message);alert("전략 D의 신규 진입을 중지했습니다. 기존 보유 보호는 유지합니다.\n"+ex.Message);}
        };dayTradingTimer.Start();
    }
    public void PrepareDayTradingWindow(bool screening)
    {
        dayTradingTargets=screening?(ScreenWatchlist?WatchGroups.Where(g=>g.IncludeInScreening).SelectMany(g=>g.Items).Select(e=>e.Stock):Catalog.Where(MatchesScreeningMarket)).Where(s=>s.Exchange==Exchange.KRX).DistinctBy(s=>s.Key).Take(100).ToArray():
            (selected??dataset?.Stock) is {} stock?[stock]:[];
        DayTrading.FactsJson=OversoldFactsJson;DayTrading.StartDate=(screening?ScreenFrom:From)??DateTime.Today.AddDays(-7);DayTrading.EndDate=(screening?ScreenTo:To)??DateTime.Today;
    }
    private IMarketDataProvider CreateDayTradingFeed(StockItem[] items)
    {
        if(!IsOnline||broker is not KiwoomBroker kiwoom||broker.Exchange!=Exchange.KRX)throw new InvalidOperationException("전략 D 원본 체결 감시는 온라인 키움 국내 연결이 필요합니다. 공개 가격 폴링을 원본 틱으로 대체하지 않습니다.");
        if(items.Length is <1 or >20)throw new InvalidOperationException("전략 D 1차 가상 실행은 한 구독의 국내20종목 이내입니다. 기존 전략 기능의 대상 수는 변경하지 않습니다.");
        var instruments=items.Select(s=>new Instrument(s.Symbol,s.Exchange)).ToArray();
        return multiFeedFactory?.Invoke(broker,instruments,tickSettings.Data,_=>{})??new KiwoomMarketDataProvider(kiwoom.GetTokenAsync,instruments,tickSettings.Data,_=>{},live:true);
    }
    private async Task<PreparedDayTrading[]> PrepareDayTrading(StockItem[] items,StrategyDefinition definition,DateOnly first,DateOnly last,HistoricalFacts requestedFacts,bool assumptions,CancellationToken token)
    {
        if(items.Any(s=>s.Exchange!=Exchange.KRX))throw new InvalidOperationException("전략 D 1차 버전은 국내 일반주식 전용입니다.");
        var output=new List<PreparedDayTrading>();var now=DateTimeOffset.UtcNow;var frame=definition.DayTrading!.SimpleTimeframe;
        if(!IsOnline&&frame.Unit!=TimeframeUnit.TradingDay)throw new InvalidOperationException("오프라인 연구는 일봉만 지원합니다. 온라인 분·시간봉 또는 현재 프로세스에 수집한 원본 자료를 사용하세요.");
        foreach(var stock in items)
        {
            token.ThrowIfCancellationRequested();var data=store.State<BarDataset>("bars:"+stock.Key);
            if(data==null||DateTimeOffset.UtcNow-data.RetrievedAt>TimeSpan.FromMinutes(10)){data=await provider.History(stock,token);store.State("bars:"+stock.Key,data);}
            var facts=requestedFacts.Snapshot();var daily=new List<KnownStrategyBar>();var sessions=facts.Sessions.ToDictionary(s=>s.Date);
            foreach(var price in data.CompletedBars.Where(b=>b.Time<=now).OrderBy(b=>b.Time))
            {
                var date=DailyDates.Of(price);var known=sessions.GetValueOrDefault(date);
                if(known==null&&assumptions)
                {
                    known=new(date,new DateTimeOffset(date.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(date.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)));sessions[date]=known;
                }
                var close=known?.Close??price.Time;var open=known?.Open??new DateTimeOffset(date.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9));
                var value=facts.TradingValue(stock.Symbol,date,close)??(assumptions?(price.Open+price.High+price.Low+price.Close)/4*price.Volume:(decimal?)null);
                var source=data.Source+(assumptions?" · 연구가정 정규장/거래대금 OHLC평균×량":" · 당시 거래대금/세션 미제공은 미확인");
                var normalized=price with{Time=close};daily.Add(new(new(1,TimeframeUnit.TradingDay),new(normalized,open,close,daily.Count+1,value,source),close));
            }
            facts.MarketSessions=sessions.Values.OrderBy(s=>s.Open).ToArray();if(assumptions)facts.Source+=(facts.Source.Length>0?" · ":"")+"연구가정: 관측 OHLC 일자 정규장 시간, 실제 휴장/단축장 달력 아님";facts.Validate();
            StrategyBar[] bars;
            if(frame.Unit==TimeframeUnit.TradingDay)bars=daily.Where(b=>DailyDates.Of(b.Bar.Price)>=first&&DailyDates.Of(b.Bar.Price)<=last).Select(b=>b.Bar).ToArray();
            else if(frame.Unit is TimeframeUnit.Minute or TimeframeUnit.Hour)
            {
                var spec=new BarSpecification(frame.Size,frame.Unit==TimeframeUnit.Minute?BarUnit.Minute:BarUnit.Hour);
                var fetch=definition.Configuration.Clone();fetch.MovingAveragePeriods="1";fetch.UsePivotSupport=false;fetch.UseApproximateVolumeZone=false;
                var warmupFirst=RealtimeWarmup.BacktestHistoryStart(first,spec,fetch);
                var available=await ScreeningBars(stock,spec,facts,now,token,warmupFirst,last);
                if(!assumptions)available=available.Select(b=>b.Source.Contains("추정",StringComparison.Ordinal)?b with{TradingValue=null}:b).ToArray();
                if(assumptions)
                {
                    foreach(var date in available.Select(b=>DailyDates.Of(b.Price)).Distinct().Where(d=>!sessions.ContainsKey(d)))sessions[date]=new(date,new DateTimeOffset(date.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(date.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)));
                    facts.MarketSessions=sessions.Values.OrderBy(s=>s.Open).ToArray();facts.Validate();
                }
                daily.AddRange(available.Select(b=>new KnownStrategyBar(frame,b,b.ClosedAt)));
                bars=available.Where(b=>DailyDates.Of(b.Price)>=first&&DailyDates.Of(b.Price)<=last).ToArray();
            }
            else bars=[];
            var start=new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));var end=new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));
            var raw=store.MemoryMarketData?store.ReadEvents(stock.Key,"KIWOOM",start,end).ToArray():[];
            output.Add(new(stock,new(new(stock.Symbol,stock.Exchange),definition,new(daily.ToArray(),facts),definition.Preset),bars,raw));
        }
        return output.ToArray();
    }
}
