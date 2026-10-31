using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;
public sealed record ScreeningBarPreference(bool Daily,string Value,string Unit);
public sealed record ScreeningPeriodPreference(DateTime From,DateTime To);
public sealed partial class WorkspaceViewModel
{
    private bool screenDaily=true;private string screenBarValue="1",screenBarUnit="분";
    private sealed record ScreeningBarCacheEntry(DateTimeOffset StoredAt,StrategyBar[] Bars);
    private readonly Dictionary<string,ScreeningBarCacheEntry> screeningBarCache=[];
    private readonly object screeningBarCacheGate=new();
    private DateTime? screenFrom=KoreaClock.Today.ToDateTime(TimeOnly.MinValue).AddDays(-7),screenTo=KoreaClock.Today.ToDateTime(TimeOnly.MinValue);
    public DateTime? ScreenFrom {get=>screenFrom;set{screenFrom=value;Changed();}}
    public DateTime? ScreenTo {get=>screenTo;set{screenTo=value;Changed();}}
    private (DateOnly From,DateOnly To) ScreenPeriod()
    {
        if(ScreenFrom==null||ScreenTo==null)throw new InvalidOperationException("스크리닝 시작일과 종료일을 모두 선택하세요.");
        var first=DateOnly.FromDateTime(ScreenFrom.Value);var last=DateOnly.FromDateTime(ScreenTo.Value);
        if(first>last)throw new InvalidOperationException("스크리닝 시작일은 종료일 이하여야 합니다.");
        if(last>KoreaClock.Today)throw new InvalidOperationException("미래 날짜는 스크리닝할 수 없습니다.");
        return(first,last);
    }
    public bool ScreenDaily {get=>!IsOnline||screenDaily;set{screenDaily=value;Changed();Changed(nameof(ScreenCustomBars));}}
    public bool ScreenCustomBars {get=>IsOnline&&!screenDaily;set{if(IsOnline)ScreenDaily=!value;else ScreenDaily=true;}}
    public string ScreenBarValue {get=>screenBarValue;set{screenBarValue=value;Changed();}}
    public string ScreenBarUnit {get=>screenBarUnit;set{var normalized=TimeBarSelection.NormalizeUnit(value);if(screenBarUnit==normalized)return;screenBarUnit=normalized;screenBarValue=TimeBarSelection.NormalizeValue(screenBarValue,normalized);Changed();Changed(nameof(ScreenBarValue));Changed(nameof(ScreenBarValues));}}
    public string[] ScreenBarUnits {get;}=["분","시"];
    public TimeBarUnitOption[] ScreenBarUnitOptions=>TimeBarSelection.Options;
    public string[] ScreenBarValues=>TimeBarSelection.Values(ScreenBarUnit);
    public OversoldScreeningRun? LatestScreening {get;private set;}
    private long screeningGeneration;
    private void PublishScreening(OversoldScreeningRun run,bool persist=true)
    {
        if(persist){store.State(run.Id,run);store.State("oversold-screen-latest",run.Id);}
        LatestScreening=run;Changed(nameof(LatestScreening));Actions["스크리닝결과"].Refresh();
    }
    private void InitializeScreeningResults()
    {
        if(store.State<ScreeningBarPreference>("oversold-screen-bars") is {} saved){screenDaily=saved.Daily;screenBarUnit=TimeBarSelection.NormalizeUnit(saved.Unit);screenBarValue=TimeBarSelection.NormalizeValue(saved.Value,screenBarUnit);}
        if(store.State<ScreeningPeriodPreference>("oversold-screen-period") is {} period){screenFrom=period.From;screenTo=period.To;}
        if(store.State<string>("oversold-screen-latest") is {} id)LatestScreening=store.State<OversoldScreeningRun>(id);
        Actions["스크리닝결과"]=new(()=>{var dialog=new ScreeningResultDialog(()=>LatestScreening,ScreeningTradeDetails,(stock,run)=>OpenScreeningStock(stock,run));if(System.Windows.Application.Current?.MainWindow is {} owner&&owner.IsVisible)dialog.Owner=owner;dialog.Show();return Task.CompletedTask;},()=>LatestScreening!=null);
    }
    private async Task<StrategyBar[]> ScreeningBars(StockItem item,BarSpecification? spec,HistoricalFacts facts,DateTimeOffset asOf,CancellationToken token,DateOnly? from=null,DateOnly? to=null)
    {
        if(spec==null)
        {
            var data=store.State<BarDataset>("bars:"+item.Key);
            if(data==null||data.Stock.Key!=item.Key||DateTimeOffset.UtcNow-data.RetrievedAt>TimeSpan.FromMinutes(10))
            {data=await provider.History(item,token);store.State("bars:"+item.Key,data);}
            return data.Bars.Select((b,i)=>{var day=DailyDates.Of(b);return new StrategyBar(b,b.Time,b.Time,i,facts.TradingValue(item.Symbol,day,b.Time),data.Source);}).ToArray();
        }
        // Existing raw event repository is authoritative only when its capture coverage was verified.
        var coverage=store.State<BarCoverage>("verified-tick-coverage:"+item.Key);
        if(coverage!=null)
        {
            var raw=store.ReadEvents(item.Key,"KIWOOM",coverage.From,coverage.To);
            var aggregate=StrategyBarAggregator.FromTrades(raw,new(item.Symbol,item.Exchange),spec,facts.Sessions,coverage,asOf);
            if(aggregate.Missing>0||aggregate.Bars.Length==0)throw new InvalidOperationException("자료 부족 · "+aggregate.Reason);
            return aggregate.Bars;
        }
        if(spec.Unit!=BarUnit.Tick&&IsOnline&&broker is IMinuteHistoryBroker minute&&broker.Exchange==item.Exchange)
        {
            var cacheKey=$"{item.Key}|{spec.Unit}|{spec.Value}|{from}|{to}|{broker.Id}";
            lock(screeningBarCacheGate)if(screeningBarCache.TryGetValue(cacheKey,out var cached)&&DateTimeOffset.UtcNow-cached.StoredAt<TimeSpan.FromMinutes(2))return cached.Bars;
            var targetMinutes=checked(spec.Value*(spec.Unit==BarUnit.Hour?60:1));var sourceMinutes=1;
            PriceBar[] source;
            if(minute is IRangedMinuteIntervalHistoryBroker interval&&from is {} intervalFrom&&to is {} intervalTo&&interval.SupportsMinuteInterval(targetMinutes))
            {sourceMinutes=targetMinutes;source=await interval.MinuteHistory(item.Symbol,intervalFrom,intervalTo,sourceMinutes,token);}
            else source=minute is IRangedMinuteHistoryBroker ranged&&from is {} rangeFrom&&to is {} rangeTo
                ?await ranged.MinuteHistory(item.Symbol,rangeFrom,rangeTo,token)
                :await minute.MinuteHistory(item.Symbol,token);
            if(source.Length==0)throw new InvalidOperationException("자료 부족 · 연결 계좌가 1분봉을 반환하지 않았습니다.");
            var minuteRows=source.DistinctBy(b=>b.Time).OrderBy(b=>b.Time).Select((bar,index)=>
            {
                var open=new DateTimeOffset(bar.Time.Year,bar.Time.Month,bar.Time.Day,bar.Time.Hour,bar.Time.Minute,0,bar.Time.Offset);
                var close=open.AddMinutes(sourceMinutes);var complete=bar.IsComplete&&close<=asOf;
                var normalized=new PriceBar(close,bar.Open,bar.High,bar.Low,bar.Close,bar.Volume,complete);
                // Kiwoom ka10080 exposes OHLCV but no per-minute traded notional. Keep the
                // estimate explicit in the source so results never present it as an exact API value.
                var estimatedTradingValue=(bar.Open+bar.High+bar.Low+bar.Close)/4m*bar.Volume;
                return new StrategyBar(normalized,open,close,index+1,estimatedTradingValue,$"{broker.Name} 공식 {sourceMinutes}분봉 · 거래대금 OHLC 평균×거래량 추정");
            }).ToArray();
            var days=minuteRows.Select(b=>DateOnly.FromDateTime(b.OpenTime.ToOffset(TimeSpan.FromHours(9)).DateTime)).Distinct().ToArray();
            var known=facts.Sessions.Where(s=>days.Contains(s.Date)).ToDictionary(s=>s.Date);
            var sessions=days.Select(day=>known.GetValueOrDefault(day)??new DailyMarketSession(day,new DateTimeOffset(day.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(day.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)))).OrderBy(s=>s.Open).ToArray();
            StrategyBar[] result;
            if(sourceMinutes==targetMinutes)
            {
                result=minuteRows.Where(b=>b.Price.IsComplete&&sessions.Any(s=>b.OpenTime>=s.Open&&b.ClosedAt<=s.Close)).ToArray();
                if(result.Length==0)throw new InvalidOperationException("자료 부족 · 선택 기간에 완성된 증권사 지정봉이 없습니다.");
            }
            else
            {
                var aggregate=StrategyBarAggregator.FromMinutes(minuteRows,spec,sessions,asOf);
                if(aggregate.Bars.Length==0)throw new InvalidOperationException("자료 부족 · "+aggregate.Reason);
                result=aggregate.Bars;
            }
            lock(screeningBarCacheGate)
            {
                screeningBarCache[cacheKey]=new(DateTimeOffset.UtcNow,result);
                if(screeningBarCache.Count>128)foreach(var old in screeningBarCache.OrderBy(x=>x.Value.StoredAt).Take(screeningBarCache.Count-128).Select(x=>x.Key).ToArray())screeningBarCache.Remove(old);
            }
            return result;
        }
        throw new InvalidOperationException("자료 부족 · 선택한 틱/분/시 기준의 연속 수집 원본이 없습니다. 다른 주기로 대체하지 않습니다.");
    }
    public string ScreeningTradeDetails(StockItem stock)
    {
        var results=History.Where(h=>h.Stock.Key==stock.Key&&h.OversoldResultId!=null).Select(h=>store.State<OversoldResult>(h.OversoldResultId!)).Where(r=>r!=null).ToArray();
        var events=results.SelectMany(r=>r!.Trades.SelectMany(t=>t.Executions)).Select(e=>(Time:e.ExecutionTime,Text:$"[시뮬레이션 · {e.ExecutionMode}] {e.Reason}\n신호 {e.SignalTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss} → 체결 {e.ExecutionTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss}\n{(e.Buy?"매수":"매도")} {e.Quantity:N0}주 × {e.Price:N2} · 금액 {e.Quantity*e.Price:N2} · 평균 {e.AveragePriceAfter:N2} · 실현손익 {e.RealizedPnl:N2}\n"+DecisionSnapshotText.Format(e.Snapshot)));
        var signals=results.SelectMany(r=>r!.Signals).Select(s=>(Time:s.SignalTime,Text:$"[백테스트 전략 신호 · 체결 아님] {s.SignalTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss} {s.Action} {s.Reason}\n"+DecisionSnapshotText.Format(s.Snapshot)));
        var rows=events.Concat(signals).OrderBy(e=>e.Time).ToArray();
        return rows.Length==0?"발생한 낙주 전략 체결 기록이 없습니다. 스크리닝은 신호만 평가하며 매수·매도 기록을 만들지 않습니다.":string.Join("\n\n",rows.Select(e=>e.Text));
    }
}
