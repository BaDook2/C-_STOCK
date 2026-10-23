using System.Collections.ObjectModel;
using Stock.Core;
using Stock.Core.Pipeline;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    private SymbolObservation[] pipelineObservations=[];
    public bool IsPipeline=>strategyConfiguration.StrategyType==StrategyType.Pipeline;
    public ParameterChoice[] PipelineStrategyChoices {get;}=[new("TickScalping","Strategy A · 틱떼기"),new("Farmer","Strategy B · 농부매매")];
    public string PipelineStrategyChoice {get=>strategyConfiguration.Pipeline.Advanced.Strategy.ToString();set{if(!CanConfigure||value==null||value==PipelineStrategyChoice)return;strategyConfiguration.Pipeline.Advanced.Strategy=Enum.Parse<PipelineStrategy>(value);strategyConfiguration.State=StrategyConfigState.Custom;Changed();Changed(nameof(ConfigurationState));}}
    public string RankingInput {get;set;}="Symbol,At,SearchRank,PreviousRank,PreviousRankAt,MarketCap,DayTurnover,DayVolume,Average20DayVolume,SameTime5MinAverageVolume,Vwap,PreviousLow,Resistance,RecentHigh,RecentHighDays";
    public string PipelineStatus {get;private set;}="수동/CSV 자료에는 관측시각(예: 2026-09-29T09:00:00+09:00)을 입력하세요. 빈 수치는 자료 없음으로 처리합니다.";
    public ObservableCollection<PipelineStageResult> PipelineStages {get;}=[];
    private void InitializePipeline()
    {
        pipelineObservations=store.State<SymbolObservation[]>("pipeline-observations-v1")??[];RankingInput=store.State<string>("pipeline-ranking-input-v1")??RankingInput;
        Add("순위자료적용",ApplyRankingInput,()=>CanConfigure);
        Add("순위CSV",ImportRankingCsv,()=>CanConfigure);
        Add("파이프라인Mock",RunPipelineMock,()=>CanConfigure&&IsOffline);
    }
    private async Task ImportRankingCsv()
    {var dialog=new Microsoft.Win32.OpenFileDialog{Filter="CSV 파일|*.csv",Title="시점별 순위·보조 자료"};if(dialog.ShowDialog()!=true)return;RankingInput=await System.IO.File.ReadAllTextAsync(dialog.FileName,Token);Changed(nameof(RankingInput));await ApplyRankingInput();}
    private async Task ApplyRankingInput()
    {
        ApplyParameterFields();var config=strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode);var rows=CsvImportProvider.Parse(RankingInput).Select(r=>r with{Source=config.Universe.RankingSource.ToString()}).ToArray();
        var source=RankingProviderFactory.Create(config.Universe.RankingSource,rows);var current=await source.ReadAsync(DateTimeOffset.UtcNow,Token);
        pipelineObservations=rows;store.State("pipeline-observations-v1",rows);store.State("pipeline-ranking-input-v1",RankingInput);
        PipelineStatus=$"{source.Source} · 시점 자료 {rows.Length:N0}개 저장 · 현재 유효 후보 {current.Count:N0}개 · 과거 테스트는 해당 시점 이전 자료만 사용합니다.";Changed(nameof(PipelineStatus));
    }
    private SymbolObservation[] PipelineObservationRows()
    {
        var config=strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode);if(config.Universe.RankingSource is RankingSource.Toss or RankingSource.Kiwoom)throw new InvalidOperationException("선택한 검색순위 API는 아직 연결되지 않았습니다. Manual / CSV / Mock을 선택하세요.");
        var watch=WatchGroups.Where(g=>g.IncludeInScreening).SelectMany(g=>g.Items).Select(x=>x.Stock.Key).ToHashSet();
        return pipelineObservations.Where(r=>r.Source==config.Universe.RankingSource.ToString()).Select(r=>r with{IsWatchlist=watch.Contains(r.Symbol)}).ToArray();
    }
    private void ShowPipelineEvaluation(PipelineEvaluation e)
    {Replace(PipelineStages,e.Stages);PipelineStatus=e.Features.At.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss")+" 한국시간 · "+e.Summary;Changed(nameof(PipelineStatus));}
    private async Task RunPipelineMock()
    {
        if(!IsPipeline)throw new InvalidOperationException("Universe → 틱떼기 / 농부매매를 먼저 선택하세요.");
        ApplyParameterFields();var cfg=strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode);cfg.Universe.RankingSource=RankingSource.Mock;
        var instrument=new Instrument("MOCK",Exchange.NASDAQ);var events=DemoMarketDataProvider.Sample(instrument,1200).Select(e=>e with{Open=2,PreviousClose=2,BuyerInitiated=e.Kind==EventKind.Trade?e.Sequence%3!=0:null}).ToArray();
        var observations=events.Where(e=>e.Kind==EventKind.Trade).Select(e=>new SymbolObservation{Symbol=instrument.Key,At=e.ExchangeTime,SearchRank=5,PreviousRank=100,PreviousRankAt=e.ExchangeTime.AddMinutes(-1),MarketCap=10000000,DayTurnover=10000000,DayVolume=10000000,Average20DayVolume=100000,SameTime5MinAverageVolume=100,Vwap=2,PreviousLow=1.9m,Resistance=3,RecentHigh=3,Source="Mock",IsWatchlist=true}).ToArray();
        var mock=new MockRankingProvider(observations);_ = await mock.ReadAsync(events[^1].ExchangeTime,Token);
        var baseline=tickSettings.Clone();baseline.Risk.AccountCapital=InitialCash();var settings=PipelinePresetMapper.ToEngine(cfg,baseline);settings.PipelineObservations=observations;settings.Backtest.Instrument=instrument.Key;settings.Backtest.InitialCash=InitialCash();
        var result=await Task.Run(()=>new BacktestRunner(store).Run(settings,events,Token),Token);
        var context=new MarketContext(instrument,settings);foreach(var e in events)context.Add(e);ShowPipelineEvaluation(new PipelineEvaluator().Evaluate(context,cfg,observations));
        ResultInfo=$"MOCK 검증 전용 · 실제 시세/수익 아님\n이벤트 {result.Events:N0}개 · 모의 체결 {result.Fills:N0}건 · 모의 손익 {result.NetPnl:N0}";PipelineStatus="MOCK 검증 전용 · "+PipelineStatus;Changed(nameof(PipelineStatus));Log("Mock 파이프라인 검사 완료 · 실제 계좌 주문 없음");
    }
}
