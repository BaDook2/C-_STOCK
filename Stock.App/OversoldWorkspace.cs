using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record OversoldUsageGroup(ParameterUsageSection Metadata,IReadOnlyList<ParameterGroup> Groups)
{
    public int Count=>Groups.Sum(g=>g.Fields.Count);
    public bool IsEmpty=>Count==0;
}
public sealed record OversoldBacktestBarPreference(bool Daily,string Value,string Unit="분");
public sealed partial class WorkspaceViewModel
{
    private OversoldConfig oversoldConfig=new();
    private HistoricalFacts oversoldFacts=new();
    private OversoldResult? oversoldResult;
    public ObservableCollection<ParameterGroup> OversoldGroups {get;}=[];
    public ObservableCollection<OversoldUsageGroup> OversoldUsageGroups {get;}=[];
    public ObservableCollection<RecurringResultCard> OversoldCards {get;}=[];
    private string oversoldFactsJson="";
    private bool backtestDaily=true;
    private string backtestBarValue="5",backtestBarUnit="분";
    public string OversoldFactsJson {get=>oversoldFactsJson;set{oversoldFactsJson=value;Changed();}}
    public bool BacktestDaily {get=>!IsOnline||backtestDaily;set{if(!IsOnline&&value==false)return;if(backtestDaily==value)return;backtestDaily=value;Changed();Changed(nameof(BacktestCustomBars));Changed(nameof(BacktestBarDescription));Refresh();}}
    public bool BacktestCustomBars {get=>IsOnline&&!backtestDaily;set{if(IsOnline)BacktestDaily=!value;else BacktestDaily=true;}}
    public string BacktestBarValue {get=>backtestBarValue;set{backtestBarValue=value;Changed();Changed(nameof(BacktestBarDescription));}}
    public string BacktestBarUnit {get=>backtestBarUnit;set{var normalized=TimeBarSelection.NormalizeUnit(value);if(backtestBarUnit==normalized)return;backtestBarUnit=normalized;backtestBarValue=TimeBarSelection.NormalizeValue(backtestBarValue,normalized);Changed();Changed(nameof(BacktestBarValue));Changed(nameof(BacktestBarValues));Changed(nameof(BacktestBarDescription));}}
    public TimeBarUnitOption[] BacktestBarUnitOptions=>TimeBarSelection.Options;
    public string[] BacktestBarValues=>TimeBarSelection.Values(BacktestBarUnit);
    public string BacktestBarDescription=>BacktestCustomBars?$"선택한 저장 전략 · 위 기간의 {BacktestBarValue}{BacktestBarUnit}봉으로 실행":"선택한 저장 전략 · 위 기간의 일봉으로 실행";
    public string OversoldMessage {get;private set;}="국내 일반주식 · 일봉 또는 온라인 공식 분·시간봉 · 과거 시점 자료를 입력한 뒤 실행하세요.";
    public string OversoldResultSummary=>oversoldResult==null?"아직 실행한 결과가 없습니다.":$"{oversoldResult.Stock.Name} · {oversoldResult.From:yyyy.MM.dd} ~ {oversoldResult.To:yyyy.MM.dd}\n완결 {oversoldResult.Performance.Trades:N0}회 · 미청산 {oversoldResult.Performance.OpenTrades:N0}회 · 다음 시가 대기 {(oversoldResult.PendingOrderAtEnd?"있음":"없음")}";
    public bool HasOversoldHistory=>SelectedRun?.OversoldResultId!=null;
    private static readonly JsonSerializerOptions FactsJsonOptions=new(Serialization.Json){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    private void InitializeOversold()
    {
        oversoldConfig=store.State<OversoldConfig>("oversold-config-v1")??new();oversoldFacts=store.State<HistoricalFacts>("oversold-facts-v1")??new();OversoldFactsJson=JsonSerializer.Serialize(oversoldFacts,FactsJsonOptions);
        if(store.State<OversoldBacktestBarPreference>("oversold-backtest-bars") is {} bars){backtestDaily=bars.Daily;backtestBarUnit=TimeBarSelection.NormalizeUnit(bars.Unit);backtestBarValue=TimeBarSelection.NormalizeValue(bars.Value,backtestBarUnit);}
        BuildOversoldGroups();
        Add("낙주설정저장",()=>{SaveOversold();return Task.CompletedTask;},()=>CanConfigure);
        Add("낙주자료불러오기",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="시점별 자료 JSON|*.json",Title="과거 종목 상태·거래대금·위험·시장일 자료"};if(dialog.ShowDialog()==true){var json=File.ReadAllText(dialog.FileName);ParseOversoldFacts(json);OversoldFactsJson=json;OversoldMessage="파일을 읽었습니다. 적용 및 저장을 누르면 검사 후 보관합니다.";Changed(nameof(OversoldMessage));}return Task.CompletedTask;},()=>CanConfigure);
        Add("낙주백테스트",RunOversold,()=>CanConfigure&&dataset?.Stock.Exchange==Exchange.KRX&&(BacktestDaily||CanUseMinutes));
        Add("낙주결과상세",()=>{if(oversoldResult!=null)ShowOversoldDetails(oversoldResult);return Task.CompletedTask;},()=>!Busy&&oversoldResult!=null);
        Add("낙주기록상세",()=>{if(SelectedRun?.OversoldResultId is {} id&&store.State<OversoldResult>(id) is {} result)ShowOversoldDetails(result);return Task.CompletedTask;},()=>!Busy&&HasOversoldHistory);
    }
    private void BuildOversoldGroups()
    {
        OversoldGroups.Clear();OversoldUsageGroups.Clear();
        var fields=ParameterCatalog.Properties(oversoldConfig).Select(p=>new ParameterField(oversoldConfig,p,()=>{OversoldMessage="수정 중 · 적용 및 저장 후 같은 값으로 일봉 백테스트를 실행합니다.";Changed(nameof(OversoldMessage));})).ToArray();
        foreach(var section in OversoldParameterMetadata.Sections)
        {
            var byKey=fields.ToDictionary(f=>f.Metadata.Key);
            var groups=OversoldParameterMetadata.Rows.Where(r=>r.Scope==section.Scope).GroupBy(r=>r.Group).Select(g=>new ParameterGroup(g.Key,g.SelectMany(r=>r.Keys).Where(k=>k!=null).Select(k=>byKey[k!]).ToArray(),g.Select(r=>new ParameterRow(r.Keys.Select(k=>new ParameterCell(k==null?null:byKey[k])).ToArray())).ToArray())).ToArray();
            foreach(var group in groups)OversoldGroups.Add(group);
            OversoldUsageGroups.Add(new(section,groups));
        }
    }
    private static HistoricalFacts ParseOversoldFacts(string json)
    {
        try{var facts=JsonSerializer.Deserialize<HistoricalFacts>(json,FactsJsonOptions)??throw new InvalidOperationException("시점별 자료가 비어 있습니다.");facts.Validate();return facts;}
        catch(JsonException ex){throw new InvalidOperationException($"시점별 JSON 형식을 확인하세요. 위치: {ex.Path}, 줄 {(ex.LineNumber??0)+1:N0}");}
    }
    private void SaveOversold(bool includeFacts=true)
    {
        foreach(var f in OversoldGroups.SelectMany(g=>g.Fields))f.Apply();oversoldConfig.Validate();var facts=includeFacts?ParseOversoldFacts(OversoldFactsJson):oversoldFacts;oversoldFacts=facts;
        store.State("oversold-config-v1",oversoldConfig);store.State("oversold-facts-v1",facts);
        OversoldMessage=$"저장 완료 · 시장일 {facts.MarketSessions.Length:N0}개 · 과거 상태 {facts.StatusRecords.Length:N0}개 · 실제 거래대금 {facts.TradingValues.Length:N0}개 · 위험 자료 {facts.RiskRecords.Length:N0}개";Changed(nameof(OversoldMessage));
    }
    private async Task RunOversold()
    {
        if(dataset==null||From==null||To==null||From>To)throw new InvalidOperationException("국내 종목과 시작·종료일을 선택하세요.");
        SaveOversold();var data=dataset;var cfg=oversoldConfig.Clone();var facts=oversoldFacts.Snapshot();var first=DateOnly.FromDateTime(From.Value);var last=DateOnly.FromDateTime(To.Value);var token=Token;
        if(last>KoreaClock.Today)throw new InvalidOperationException("미래 날짜는 백테스트할 수 없습니다.");
        store.State("oversold-backtest-bars",new OversoldBacktestBarPreference(BacktestDaily,BacktestBarValue,BacktestBarUnit));
        if(BacktestDaily){await ExecuteOversoldBacktest(data,cfg,facts,first,last,token);return;}
        if(!IsOnline||broker is not IMinuteHistoryBroker||broker.Exchange!=data.Stock.Exchange)throw new InvalidOperationException("분·시간봉 백테스트는 온라인 투자에서 선택 종목과 같은 시장의 분봉 API 연결이 필요합니다.");
        var specification=TimeBarSelection.Parse(BacktestBarValue,BacktestBarUnit);
        var requiredBars=new[]{cfg.AverageTradingValueLookbackDays,cfg.CrashLookbackDays,cfg.ShortCrashLookbackDays,cfg.RsiPeriod+1,cfg.VolumeLookbackDays+1,cfg.PivotLeftBars+cfg.PivotRightBars+1,cfg.VolumeZoneLookbackDays,cfg.MaPeriods().DefaultIfEmpty(1).Max()}.Max();
        var minutesPerBar=checked(specification.Value*(specification.Unit==BarUnit.Hour?60:1));var barsPerDay=Math.Max(1,390/minutesPerBar);var warmupTradingDays=(int)Math.Ceiling(requiredBars/(double)barsPerDay);var calendarDays=Math.Max(14,(int)Math.Ceiling(warmupTradingDays*7d/5d)+14);var warmupFrom=first.AddDays(-calendarDays);
        var now=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));var rangeEnd=new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));var asOf=rangeEnd<now?rangeEnd:now;
        var rows=await ScreeningBars(data.Stock,specification,facts,asOf,token,warmupFrom,last);
        var dates=rows.Select(b=>DailyDates.Of(b.Price)).Distinct().ToArray();var known=facts.Sessions.GroupBy(s=>s.Date).ToDictionary(g=>g.Key,g=>g.OrderByDescending(s=>s.Close).First());
        var added=dates.Where(d=>!known.ContainsKey(d)).Select(d=>new DailyMarketSession(d,new DateTimeOffset(d.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(d.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9))));
        facts.MarketSessions=[..facts.Sessions.Concat(added).OrderBy(s=>s.Open)];if(string.IsNullOrWhiteSpace(facts.Source))facts.Source="연결 증권사 장중봉의 KRX 정규장 세션";facts.Validate();
        await ExecuteOversoldBacktest(data,cfg,facts,first,last,token,specification,rows);
    }
    private async Task ExecuteOversoldBacktest(Stock.Core.Research.BarDataset data,OversoldConfig cfg,HistoricalFacts facts,DateOnly first,DateOnly last,CancellationToken token,BarSpecification? specification=null,StrategyBar[]? strategyBars=null)
    {
        var result=await oversoldBacktests.RunAsync(new(data,cfg,first,last,facts,specification,strategyBars),token);
        if(result.Trades.Count==0)
        {
            var reasons=result.Decisions.GroupBy(d=>d.Reason).OrderByDescending(g=>g.Count()).Take(4).Select(g=>$"{g.Key} {g.Count():N0}일").ToArray();
            result.Notices.Add("거래 0건: "+(result.Decisions.Count==0?"선택 기간에 검증된 거래 세션이 없습니다.":string.Join(" · ",reasons))+" 실제 시점별 자료와 설정을 확인하세요.");
        }
        var storedBars=strategyBars?.Select(b=>b.Price).ToArray()??data.CompletedBars;store.State("oversold-facts-snapshot:"+result.FactsHash,facts);store.State("oversold-bars-snapshot:"+result.DataHash,storedBars);store.State(result.Id,result);var history=result.ToHistory();store.State(history.Id,history);History.Insert(0,history);LatestResult=history;SelectedRun=history;oversoldResult=result;Changed(nameof(LatestResult));ResultInfo=history.Summary+(result.Trades.Count==0?"\n"+result.Notices[^1]:"");WorkspaceToolTab=2;
        ShowBacktestTradeMarkers=true;
        var periodBars=(strategyBars?.Select(b=>b.Price)??data.Bars).Where(b=>{var day=DailyDates.Of(b);return day>=first&&day<=last;}).ToArray();
        if(periodBars.Length>0)ShowOversoldBacktestChart(specification,periodBars);
        var m=result.Performance;string P(decimal? v)=>v.HasValue?$"{v:N2}%":"미산출";
        Replace(OversoldCards,new[]{new RecurringResultCard("최종 평가자산",$"{m.FinalEquity:N0}원"),new RecurringResultCard("완결 거래 승률",P(m.WinRate)),new RecurringResultCard("최대 낙폭",P(m.MaxDrawdown)),new RecurringResultCard("CAGR",P(m.Cagr)),new RecurringResultCard("기대 손익",m.Expectancy is {} e?$"{e:N0}원":"미산출"),new RecurringResultCard("Profit Factor",m.InfiniteProfitFactor?"∞":m.ProfitFactor?.ToString("N2")??"미산출")});
        OversoldMessage=string.Join("\n",result.Notices);Changed(nameof(OversoldMessage));Changed(nameof(OversoldResultSummary));Log($"국내 과매도·낙주 {specification?.ToString()??"일봉"} 백테스트와 상세 기록 저장 완료");
    }
    private void ShowOversoldBacktestChart(BarSpecification? specification,IReadOnlyList<PriceBar> bars)
    {
        if(specification==null)ChartBars=bars.ToArray();
        else
        {
            StopTicks();tickBars.Clear();minuteBars=bars.ToArray();minuteStock=dataset?.Stock.Key??"";chartPeriod=specification.ToString();chartBars=minuteBars;
            Changed(nameof(ChartPeriod));Changed(nameof(IsTick));Changed(nameof(TickSelected));Changed(nameof(DailySelected));Changed(nameof(WeeklySelected));Changed(nameof(MonthlySelected));Changed(nameof(SelectedTickInterval));Changed(nameof(SelectedMinuteInterval));Changed(nameof(StockDetail));Changed(nameof(ChartBars));Changed(nameof(PriceText));Changed(nameof(ChangeText));Changed(nameof(PriceColor));Changed(nameof(PriceDate));Changed(nameof(ChartMessage));
        }
        ChartWindowCount=Math.Max(25,Math.Min(400,ChartBars.Count));Changed(nameof(ChartWindowCount));
    }
    private void RestoreSelectedBacktestChartRange()
    {
        if(dataset==null||SelectedRun is not {OversoldResultId:{Length:>0} id} run||run.Stock.Key!=dataset.Stock.Key)return;
        var result=oversoldResult?.Id==id?oversoldResult:store.State<OversoldResult>(id);
        if(result==null)return;
        var saved=result.BarSpecification==null?null:store.State<PriceBar[]>("oversold-bars-snapshot:"+result.DataHash);var periodBars=(saved??dataset.Bars).Where(b=>{var day=DailyDates.Of(b);return day>=result.From&&day<=result.To;}).ToArray();
        if(periodBars.Length==0)return;
        ShowOversoldBacktestChart(result.BarSpecification,periodBars);
    }
    private static void ShowOversoldDetails(OversoldResult result)
    {var window=new OversoldResultDialog(result);if(System.Windows.Application.Current?.MainWindow is {} owner&&owner.IsVisible)window.Owner=owner;window.ShowDialog();}
}
