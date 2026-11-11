using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Core.Strategies.Injes;
namespace Stock.App;
public sealed record MockBatchRecord(string Id,DateTimeOffset Started,StockItem[] Stocks,DateOnly From,DateOnly To,Dictionary<string,string> Results,string Status);
public sealed partial class WorkspaceViewModel
{
    private readonly Func<IAccountBroker,IReadOnlyList<Instrument>,DataSettings,Action<string>,IMarketDataProvider>? multiFeedFactory;
    private readonly Func<DateTimeOffset> multiClock;
    private bool IsMultiOrder(string number)=>session!=null&&store.State<LiveBatchJournal>("live-batch:"+session.Scope)?.Orders.Any(o=>o.Number==number)==true;
    private MultiTradeViewModel? multiTrading;private LiveStrategyBatch? liveBatch;private RealtimePaperStrategyBatch? realtimePaperBatch;private CancellationTokenSource? multiStop;private Task? multiTask;private bool mockBatchLocked,multiPaperActive;
    public bool MultiSessionExists=>multiTask is {IsCompleted:false}||mockBatchLocked||injesStream?.Running==true||injesStream?.HasUnsettled==true;
    public string MultiStatus {get;private set;}="Inje 미국 원본 틱·호가 · 가상/기록 재생 · 실제 자동 주문 잠금";
    public bool CanOpenMulti=>CanConfigure&&(IsOffline||IsOnline&&session!=null);
    public bool CanOpenRealtimePaper=>CanConfigure;
    public string VirtualTradingHelp=>IsOnline?RealtimeMockSupported?"연결 증권사 시세로 가상 계좌에서 매매합니다.":"현재 연결 계좌는 실시간 가상 전략매매 피드를 지원하지 않습니다.":"원본 미국 틱·호가 기록으로 리플레이합니다. 공개 일봉이나 현재가 폴링을 틱으로 바꾸지 않습니다.";
    public MultiTradeViewModel MultiTrading=>multiTrading??=new(provider,store,StartMulti,StopMulti,ReviewMulti,LoadMultiPreset,()=>IsOffline?"오프라인 · 원본 미국 틱·호가 기록 재생":broker==null?"연결된 실제 시세 계정이 없습니다.":broker.Name+" · "+broker.MaskedAccount+" · "+(MultiSupported?"실시간 모의 또는 선택한 국내 실계좌":"이 계좌의 자동매매 피드/체결 대사는 미지원"),()=>MultiSupported,()=>IsOffline,StartMockMulti,()=>LatestScreening?.FinalCandidates.AsEnumerable()??[],StartRealtimeMockMulti,()=>RealtimeMockSupported,()=>Presets);
    private bool MultiSupported=>false;
    private bool RealtimeMockSupported=>IsOnline&&broker is KiwoomBroker;
    public Task RunSelectedMockBatch()=>Run(StartMockMulti);
    private async Task StartMockMulti()
    {
        var selections=MultiTrading.SelectedStrategies();if(selections.Length is <1 or >20||From==null||To==null||From>To)throw new InvalidOperationException("미국 종목1~20개와 리플레이 기간을 확인하세요.");
        var definitions=selections.Select(s=>InjesPresetStore.Require(LoadMultiOversoldPreset(s.Preset))).ToArray();EnsureSingleInjes(definitions);
        var first=DateOnly.FromDateTime(From.Value);var last=DateOnly.FromDateTime(To.Value);var events=InjesResearchBridge.Read(store,selections.Select(s=>s.Stock).ToArray(),first,last);
        if(events.Length==0)throw new InvalidOperationException("MissingData: 원본 미국 틱·호가 재생 자료가 없습니다.");
        var definition=definitions[0];definition.InitialCash=InjesMoney(MultiTrading.TotalBudget);
        var result=await Task.Run(()=>new InjesReplayEngine().Run(new(definition,selections.Select(s=>new Instrument(s.Stock.Symbol,s.Stock.Exchange)).ToArray(),events,events.Min(e=>e.KnownAt),events.Max(e=>e.KnownAt).AddMilliseconds(1),definition.InitialCash,"기록 원본 틱·호가",true,true),Token),Token);
        store.State("inje-replay:"+result.Id,result);MultiStatus=$"Inje 리플레이 · 완결 {result.Performance.Trades}회 · 실현손익 {result.Performance.RealizedProfit:0.####} USD · 실제 주문 없음";MultiTrading.ApplyMock(MultiStatus,selections.ToDictionary(s=>s.Stock.Key,s=>MultiStatus),true);Changed(nameof(MultiStatus));
    }
    private TradingSettings LoadMultiPreset(string name,decimal equity)
    {
        var cfg=LoadMultiOversoldPreset(name);return BuildOversoldTradingSettings(cfg,equity,!MultiTrading.UseMock);
    }
    private TradingSettings BuildOversoldTradingSettings(OversoldConfig cfg,decimal equity,bool requireEmergencyStop)
    {
        var d=InjesPresetStore.Require(cfg);if(requireEmergencyStop)throw new InvalidOperationException("Inje 실계좌 자동 주문은 잠겨 있습니다.");return InjesTradingSettings.Create(d);
    }
    private OversoldConfig LoadMultiOversoldPreset(string name)
    {
        var cfg=store.State<OversoldConfig>(OversoldPresetPrefix+name)??(string.Equals(name,activeOversoldPreset,StringComparison.Ordinal)?oversoldConfig.Clone():null)??throw new InvalidOperationException("등록 전략을 찾을 수 없습니다: "+name);
        cfg.Validate();return cfg.Clone();
    }
    private async Task StartRealtimeMockMulti()
    {
        await StartInjesMulti();
    }
    private async Task StartMulti()
    {
        await Task.CompletedTask;throw new InvalidOperationException("Inje 실계좌 자동 주문은 잠겨 있습니다. 가상 실행을 사용하세요.");
    }
    public async Task StopMulti(string? key=null)
    {
        if(key!=null&&injesStream?.Session!=null){injesStream.StopSymbolEntries(key);RefreshInjesScreen();return;}
        if(key==null&&(RealtimeScreeningRunning||RealtimeScreeningProtectionActive))await StopRealtimeScreening(false);
        if(multiPaperActive&&realtimePaperBatch is {} paper)
        {
            paper.Stop(key);
            if(key==null){multiPaperActive=false;multiStop?.Cancel();try{if(multiTask!=null)await multiTask;}catch{Status="실시간 모의 피드 종료 상태를 확인하지 못했습니다.";}finally{multiTask=null;multiStop?.Dispose();multiStop=null;}}
            if(!disposed){MultiTrading.Apply(paper.Snapshot());MultiStatus="실시간 모의 중지 · 가상 보유분 자동청산 없음 · 실제 주문 없음";Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();}return;
        }
        var active=liveBatch;if(active==null){if(mockBatchLocked){mockBatchLocked=false;MultiStatus="모의 실행 종료 · 기록은 보존됩니다.";MultiTrading.ApplyMock(MultiStatus,new Dictionary<string,string>(),false);Changed(nameof(MultiStatus));Refresh();}return;}active.RequestStop(key);
        try{await Task.Run(()=>active.Stop(key));}
        finally{if(key==null){multiStop?.Cancel();try{if(multiTask!=null)await multiTask;}catch{Status="피드 종료 상태를 확인하지 못했습니다. 신규 주문은 중지됐습니다.";}finally{multiTask=null;multiStop?.Dispose();multiStop=null;liveBatch=null;}}if(!disposed){MultiTrading.Apply(active.Snapshot());MultiStatus="중지 · 보유분 자동청산 없음 · 미체결 상태를 확인하세요.";Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();}}
    }
    private async Task ReviewMulti()
    {
        if(InjesStream.HasSavedUnsettled&&!InjesStream.Running&&!InjesStream.HasUnsettled)
        {
            var journal=InjesStream.RestoreLatest();injesTargets=journal.Instruments.Select(i=>new StockItem(i.Symbol,i.Symbol,i.Exchange,i.Symbol)).ToArray();
            var frozen=journal.Definition.Snapshot();var prefix=frozen.StrategyName.Length>58?frozen.StrategyName[..58]:frozen.StrategyName;frozen.StrategyName=UniqueOversoldName(prefix+" · 보관");new InjesPresetStore(store).Save(frozen);store.State(OversoldPresetPrefix+frozen.StrategyName,InjesPresetStore.Wrap(frozen));Presets.Add(frozen.StrategyName);MultiTrading.RestoreInjesTargets(injesTargets,frozen.StrategyName,frozen.CommonParameters.OrderBudget,frozen.InitialCash,frozen.CommonParameters.MaxDailyLossCash);
            RefreshInjesScreen();MultiStatus="Inje 원장 복원 · 신규 매수 OFF · 실시간 모의 실행을 눌러 같은 전략 보호 감시를 재개하세요.";Changed(nameof(MultiStatus));await Task.CompletedTask;return;
        }

        if(session==null||!MultiSupported)throw new InvalidOperationException("키움 국내 계좌가 필요합니다.");
        if(!confirm("증권사 앱에서 이전 자동 주문의 응답·체결·취소를 확인했고 해당 종목의 보유와 미체결이 없습니까? 이 확인은 주문 재전송이나 보유분 인수가 아닙니다."))return;
        var review=new LiveStrategyBatch(session,store);await Task.Run(review.ConfirmPreviousClosed);MultiStatus="이전 실행 외부 확인 완료 · 일괄수행은 별도 실행";Changed(nameof(MultiStatus));
    }
}
