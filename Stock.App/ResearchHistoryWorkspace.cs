using System.Collections.ObjectModel;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record ResearchHistoryRow(string Id,DateTimeOffset At,string Kind,string Name,string Period,string Status)
{public string TimeText=>At.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss");}

public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string,ResearchHistoryRow> researchRecords=[];
    public ObservableCollection<ResearchHistoryRow> ResearchHistory {get;}=[];
    private DateTime? researchFrom=DateTime.Today.AddMonths(-1),researchTo=DateTime.Today;
    public DateTime? ResearchFrom {get=>researchFrom;set{researchFrom=value;Changed();FilterResearchHistory();}}
    public DateTime? ResearchTo {get=>researchTo;set{researchTo=value;Changed();FilterResearchHistory();}}
    private ResearchHistoryRow? selectedResearchRecord;
    public ResearchHistoryRow? SelectedResearchRecord {get=>selectedResearchRecord;set{selectedResearchRecord=value;Changed();if(Actions.TryGetValue("테스트상세",out var action))action.Refresh();}}
    private string researchHistoryLoadError="";
    public string ResearchHistoryInfo {get;private set;}="";
    private void InitializeResearchHistory()
    {
        foreach(var saved in store.States("daytrading-history:"))
        {
            var record=Serialization.Unpack<DayTradingHistoryRecord>(saved.Json);
            researchRecords[record.Id]=new(record.Id,record.At,"전략 D",record.Name,record.Period,"완료 · 원본/봉 근사 구분");
        }
        foreach(var run in History)AddBacktestHistory(run,false);
        try{foreach(var row in store.States("oversold-screen:"))
        {
            try{var run=Serialization.Unpack<OversoldScreeningRun>(row.Json);if(run.Status!="진행 중")AddScreeningHistory(run);}catch(System.Text.Json.JsonException){Log("스크리닝 기록 JSON 오류: "+row.Key);}
        }
        }catch(Exception){researchHistoryLoadError="스크리닝 기록 읽기 오류 · 저장소 점검 필요";Log(researchHistoryLoadError);}
        History.CollectionChanged+=(_,e)=>{if(e.NewItems!=null)foreach(SavedBacktest run in e.NewItems)AddBacktestHistory(run,true);};
        Actions["테스트상세"]=new(()=>OpenResearchRecord(),()=>!Busy&&SelectedResearchRecord!=null);
        Actions["테스트기록조회"]=new(()=>{FilterResearchHistory();return Task.CompletedTask;},()=>!Busy);
        FilterResearchHistory();
    }
    private StrategyBar[]? ReadScreeningBars(string runId,string key,BarSpecification? specification)
    {
        try{var hash=store.State<string>("screen-bars:"+runId+":"+key+":"+(specification?.ToString()??"일봉"));return hash==null?null:store.State<StrategyBar[]>("screen-data:"+hash);}
        catch(System.Text.Json.JsonException){return store.State<StrategyBar[]>("screen-bars:"+runId+":"+key+":"+(specification?.ToString()??"일봉"));}
    }
    private string? realtimeHistoryId;
    private DateTimeOffset realtimeHistoryAt;
    private void BeginRealtimeHistory(){realtimeHistoryId="oversold-screen:"+Guid.NewGuid().ToString("N");realtimeHistoryAt=multiClock();}
    private OversoldScreeningRun? StoreRealtimeHistory(RealtimeScreeningSnapshot? snapshot,string reason)
    {
        if(snapshot?.Running!=true||realtimeHistoryId==null||realtimeScreening==null)return null;
        var id=realtimeHistoryId;realtimeHistoryId=null;
        try
        {
            var data=realtimeScreening.Targets.Select(s=>realtimeScreening.ChartData(s.Key)).Where(d=>d.Bars.Length>0).ToArray();if(data.Length==0)return null;
            var first=data.Min(d=>DailyDates.Of(d.Bars[0].Price));var last=DailyQuoteMerger.Date(multiClock(),Exchange.KRX);
            var run=OversoldScreeningRun.Start(data.Select(d=>d.Stock),data[0].Configuration,data[0].Specification,multiClock(),first,last,snapshot.StrategyName) with{Id=id,FactsJson=Serialization.Pack(data[0].Facts),Status="실시간 스크리닝 · "+reason+$" · 시작 {realtimeHistoryAt.ToOffset(TimeSpan.FromHours(9)):MM-dd HH:mm}"};
            foreach(var item in data)
            {
                var state=snapshot.Rows.Single(r=>r.Key==item.Stock.Key);var evaluation=realtimeScreening.EntrySnapshot(item.Stock.Key);var bars=item.Bars;
                var hash=TradingStore.Hash(Serialization.Pack(bars));store.State("screen-data:"+hash,bars);store.State("screen-bars:"+id+":"+item.Stock.Key+":"+item.Specification,hash);
                store.State("screen-facts:"+id+":"+item.Stock.Key,item.Facts);
                run=run.Append(new(item.Stock,state.LastAnalyzedAt,bars[^1].Price.Close,evaluation?.Indicators.MediumDrawdown,evaluation?.Indicators.Rsi,evaluation?.Indicators.VolumeRatio,evaluation?.Selected?.Count??0,state.MatchesStrategy||state.NearCandidate,state.StrategyState,evaluation==null?"":Serialization.Pack(evaluation),bars[0].OpenTime,bars[^1].ClosedAt){Proximity=state.Proximity,IsNearCandidate=state.NearCandidate&&!state.MatchesStrategy});
            }
            store.State(id,run);return run;
        }
        catch(Exception){Log("실시간 스크리닝 기록 저장 실패 · 저장소 점검을 확인하세요. 기존 기록은 보존했습니다.");return null;}
    }
    private void AddBacktestHistory(SavedBacktest run,bool capture)
    {
        if(capture&&run.OversoldResultId==null&&dataset?.Stock.Key==run.Stock.Key)
        {
            var exact=new[]{dataset.Bars,dataset.CompletedBars,rangeBars}.FirstOrDefault(b=>TradingStore.Hash(Serialization.Pack(b))==run.DataHash);
            if(exact!=null)store.State("test-bars:"+run.Id,exact);
        }
        researchRecords[run.Id]=new(run.Id,run.At,run.Period.StartsWith("정기매수",StringComparison.Ordinal)?"정기매수 백테스트":"전략 백테스트",run.Stock.Name,run.Period,$"체결 {run.Fills.Length:N0}건 · 수익률 {run.ReturnText}");FilterResearchHistory();
    }
    private void AddScreeningHistory(OversoldScreeningRun run)
    {researchRecords[run.Id]=new(run.Id,run.ScreeningTime,"스크리닝",run.StrategyName,$"{run.From:yyyy-MM-dd} ~ {run.To:yyyy-MM-dd} · {run.BarSpecification?.ToString()??"일봉"}",$"{run.Status} · 검사 {run.Results.Length:N0} · 후보 {run.FinalCandidates.Length:N0}");FilterResearchHistory();}
    private void FilterResearchHistory()
    {
        var from=researchFrom?.Date??DateTime.MinValue;var to=researchTo?.Date??DateTime.MaxValue.Date;
        Replace(ResearchHistory,researchRecords.Values.Where(r=>{var day=r.At.ToOffset(TimeSpan.FromHours(9)).Date;return day>=from&&day<=to;}).OrderByDescending(r=>r.At));
        ResearchHistoryInfo=from>to?"실행 시작일은 종료일 이하여야 합니다.":$"실행일 기준 {ResearchHistory.Count:N0}건 · 더블클릭하면 당시 설정·결과·차트를 확인합니다.";if(researchHistoryLoadError.Length>0)ResearchHistoryInfo+=" · "+researchHistoryLoadError;Changed(nameof(ResearchHistoryInfo));
    }
    private async Task OpenResearchRecord()
    {
        if(SelectedResearchRecord is not {} record)return;
        try
        {
            if(record.Kind=="전략 D")
            {
                var savedD=store.State<DayTradingHistoryRecord>(record.Id)??throw new InvalidOperationException("전략 D 기록을 찾지 못했습니다.");
                var resultD=store.State<Stock.Core.Research.DayTrading.IntradayReplayResult>("daytrading-result:"+savedD.ResultId)??throw new InvalidOperationException("전략 D 결과 snapshot이 없습니다.");
                DayTrading.ShowResult(resultD);ShowResearchWindow(new DayTradingDialog(DayTrading,1));return;
            }
            if(record.Kind=="스크리닝")
            {
                var run=store.State<OversoldScreeningRun>(record.Id)??throw new InvalidOperationException("저장된 스크리닝 결과를 찾지 못했습니다.");
                var window=new ScreeningResultDialog(()=>run,ScreeningTradeDetails,(stock,snapshot)=>OpenScreeningStock(stock,snapshot));ShowResearchWindow(window);return;
            }
            var saved=store.State<SavedBacktest>(record.Id)??throw new InvalidOperationException("저장된 백테스트 결과를 찾지 못했습니다.");
            var result=saved.OversoldResultId is {} resultId?store.State<OversoldResult>(resultId):null;
            var bars=store.State<PriceBar[]>("oversold-bars-snapshot:"+saved.DataHash)??store.State<PriceBar[]>("test-bars:"+saved.Id);
            if(bars==null||bars.Length==0)throw new InvalidOperationException("이전 기록에 당시 차트 원본이 저장되지 않았습니다. 요약: "+saved.Summary+" 새 테스트부터 차트 자료도 함께 보존합니다.");
            var first=result?.From??DailyDates.Of(bars[0]);var last=result?.To??DailyDates.Of(bars[^1]);
            var specification=result?.BarSpecification;var period=specification?.ToString()??"일봉";
            var exact=bars.Where(b=>DailyDates.Of(b)>=first&&DailyDates.Of(b)<=last).Select(b=>specification==null||specification.Unit==BarUnit.Day?b:b with{Time=b.Time.AddMinutes(-specification.Value*(specification.Unit==BarUnit.Hour?60:1))}).ToArray();
            var executions=result?.Trades.SelectMany((trade,index)=>trade.Executions.Select(e=>(Fill:e,Number:index+1))).ToArray();
            var markers=executions!=null?executions.Select((e,i)=>new TradeMarker(saved.Id+":"+i,saved.Stock.Key,e.Fill.ExecutionTime,e.Fill.Buy,e.Fill.Quantity,e.Fill.Price,"저장된 백테스트",TradeNumber:e.Number,Reason:e.Fill.Reason,RealizedPnl:e.Fill.Buy?null:e.Fill.RealizedPnl)).ToArray():saved.Fills.Select((e,i)=>new TradeMarker(saved.Id+":"+i,saved.Stock.Key,e.Time,e.Side.Contains("매수"),e.Quantity,e.Price,"저장된 백테스트",Reason:e.Reason,RealizedPnl:e.Realized)).ToArray();
            var trades=executions!=null?executions.Select(e=>new ScreeningChartTrade(e.Fill.ExecutionTime,e.Fill.Buy,e.Fill.Quantity,e.Fill.Price,e.Fill.Reason,"저장된 백테스트",e.Fill,e.Number)).ToArray():markers.Select(m=>new ScreeningChartTrade(m.Time,m.Buy,m.Quantity,m.Price!.Value,m.Reason,m.Source,RealizedPnl:m.RealizedPnl)).ToArray();
            var runSnapshot=OversoldScreeningRun.Start([saved.Stock],result?.Configuration??new(),specification,saved.At,first,last,record.Name);
            var zones=(result?.Trades??[]).SelectMany(t=>t.Executions.Where(e=>e.Buy&&e.Snapshot!=null).SelectMany(e=>e.Snapshot!.Clusters.Select(z=>new ScreeningSupportZone(e.SignalTime,t.LastExecutionDate??bars[^1].Time,z.Lower,z.Upper,z.UsedForEntry,z.Count,string.Join(", ",z.Supports.Select(p=>p.Id)))))).ToArray();
            var snapshot=new ScreeningChartSnapshot(saved.Stock,[new(period,period,exact,"실행 당시 저장 원본")],markers,trades,zones,saved.Summary,result,runSnapshot);
            ShowResearchWindow(new ScreeningChartDialog(snapshot));
        }
        catch(InvalidOperationException ex){alert(ex.Message);}
        catch(Exception){alert("테스트 기록을 읽지 못했습니다. 저장소 점검을 확인하세요. 기존 기록은 보존했습니다.");}
        await Task.CompletedTask;
    }
    private static void ShowResearchWindow(System.Windows.Window window)
    {if(System.Windows.Application.Current?.MainWindow is {} owner&&owner.IsVisible)window.Owner=owner;window.Show();}
}
