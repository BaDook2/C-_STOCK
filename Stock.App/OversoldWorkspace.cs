using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Core;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record OversoldUsageGroup(ParameterUsageSection Metadata,IReadOnlyList<ParameterGroup> Groups)
{
    public int Count=>Groups.Sum(g=>g.Fields.Count);
    public bool IsEmpty=>Count==0;
}
public sealed partial class WorkspaceViewModel
{
    private OversoldConfig oversoldConfig=new();
    private HistoricalFacts oversoldFacts=new();
    private OversoldResult? oversoldResult;
    public ObservableCollection<ParameterGroup> OversoldGroups {get;}=[];
    public ObservableCollection<OversoldUsageGroup> OversoldUsageGroups {get;}=[];
    public ObservableCollection<RecurringResultCard> OversoldCards {get;}=[];
    private string oversoldFactsJson="";
    public string OversoldFactsJson {get=>oversoldFactsJson;set{oversoldFactsJson=value;Changed();}}
    public string OversoldMessage {get;private set;}="국내 일반주식 일봉 전용 · 과거 시점 자료를 입력한 뒤 실행하세요.";
    public string OversoldResultSummary=>oversoldResult==null?"아직 실행한 결과가 없습니다.":$"{oversoldResult.Stock.Name} · {oversoldResult.From:yyyy.MM.dd} ~ {oversoldResult.To:yyyy.MM.dd}\n완결 {oversoldResult.Performance.Trades:N0}회 · 미청산 {oversoldResult.Performance.OpenTrades:N0}회 · 다음 시가 대기 {(oversoldResult.PendingOrderAtEnd?"있음":"없음")}";
    public bool HasOversoldHistory=>SelectedRun?.OversoldResultId!=null;
    private static readonly JsonSerializerOptions FactsJsonOptions=new(Serialization.Json){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    private void InitializeOversold()
    {
        oversoldConfig=store.State<OversoldConfig>("oversold-config-v1")??new();oversoldFacts=store.State<HistoricalFacts>("oversold-facts-v1")??new();OversoldFactsJson=JsonSerializer.Serialize(oversoldFacts,FactsJsonOptions);
        BuildOversoldGroups();
        Add("낙주설정저장",()=>{SaveOversold();return Task.CompletedTask;},()=>CanConfigure);
        Add("낙주자료불러오기",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="시점별 자료 JSON|*.json",Title="과거 종목 상태·거래대금·위험·시장일 자료"};if(dialog.ShowDialog()==true){var json=File.ReadAllText(dialog.FileName);ParseOversoldFacts(json);OversoldFactsJson=json;OversoldMessage="파일을 읽었습니다. 적용 및 저장을 누르면 검사 후 보관합니다.";Changed(nameof(OversoldMessage));}return Task.CompletedTask;},()=>CanConfigure);
        Add("낙주백테스트",RunOversold,()=>CanConfigure&&dataset?.Stock.Exchange==Exchange.KRX&&!IsTick);
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
        await ExecuteOversoldBacktest(data,cfg,facts,first,last,token);
    }
    private async Task ExecuteOversoldBacktest(Stock.Core.Research.BarDataset data,OversoldConfig cfg,HistoricalFacts facts,DateOnly first,DateOnly last,CancellationToken token)
    {
        var result=await oversoldBacktests.RunAsync(new(data,cfg,first,last,facts),token);
        if(result.Trades.Count==0)
        {
            var reasons=result.Decisions.GroupBy(d=>d.Reason).OrderByDescending(g=>g.Count()).Take(4).Select(g=>$"{g.Key} {g.Count():N0}일").ToArray();
            result.Notices.Add("거래 0건: "+(result.Decisions.Count==0?"선택 기간에 검증된 거래 세션이 없습니다.":string.Join(" · ",reasons))+" 실제 시점별 자료와 설정을 확인하세요.");
        }
        store.State("oversold-facts-snapshot:"+result.FactsHash,facts);store.State("oversold-bars-snapshot:"+result.DataHash,data.CompletedBars);store.State(result.Id,result);var history=result.ToHistory();store.State(history.Id,history);History.Insert(0,history);LatestResult=history;SelectedRun=history;oversoldResult=result;Changed(nameof(LatestResult));ResultInfo=history.Summary+(result.Trades.Count==0?"\n"+result.Notices[^1]:"");WorkspaceToolTab=2;
        ShowBacktestTradeMarkers=true;
        var periodBars=data.Bars.Where(b=>{var day=DailyDates.Of(b);return day>=first&&day<=last;}).ToArray();
        if(periodBars.Length>0){ChartBars=periodBars;ChartWindowCount=Math.Max(25,Math.Min(400,ChartBars.Count));Changed(nameof(ChartWindowCount));}
        var m=result.Performance;string P(decimal? v)=>v.HasValue?$"{v:N2}%":"미산출";
        Replace(OversoldCards,new[]{new RecurringResultCard("최종 평가자산",$"{m.FinalEquity:N0}원"),new RecurringResultCard("완결 거래 승률",P(m.WinRate)),new RecurringResultCard("최대 낙폭",P(m.MaxDrawdown)),new RecurringResultCard("CAGR",P(m.Cagr)),new RecurringResultCard("기대 손익",m.Expectancy is {} e?$"{e:N0}원":"미산출"),new RecurringResultCard("Profit Factor",m.InfiniteProfitFactor?"∞":m.ProfitFactor?.ToString("N2")??"미산출")});
        OversoldMessage=string.Join("\n",result.Notices);Changed(nameof(OversoldMessage));Changed(nameof(OversoldResultSummary));Log("국내 과매도·낙주 일봉 백테스트와 상세 기록 저장 완료");
    }
    private void RestoreSelectedBacktestChartRange()
    {
        if(dataset==null||SelectedRun is not {OversoldResultId:{Length:>0} id} run||run.Stock.Key!=dataset.Stock.Key)return;
        var result=oversoldResult?.Id==id?oversoldResult:store.State<OversoldResult>(id);
        if(result==null)return;
        var periodBars=dataset.Bars.Where(b=>{var day=DailyDates.Of(b);return day>=result.From&&day<=result.To;}).ToArray();
        if(periodBars.Length==0)return;
        ChartBars=periodBars;ChartWindowCount=Math.Max(25,Math.Min(400,ChartBars.Count));Changed(nameof(ChartWindowCount));
    }
    private static void ShowOversoldDetails(OversoldResult result)
    {var window=new OversoldResultDialog(result);if(System.Windows.Application.Current?.MainWindow is {} owner&&owner.IsVisible)window.Owner=owner;window.ShowDialog();}
}
