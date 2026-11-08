using System.Collections.ObjectModel;
using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Research.DayTrading;
using Stock.Core.Trading;

namespace Stock.Desktop;

public sealed record DayTradingParameterGroup(string Name,DayTradingParameter[] Fields);
public sealed record DayTradingHistoryRecord(string Id,DateTimeOffset At,string ResultId,string Name,string Period);
public sealed record PreparedDayTrading(StockItem Stock,IntradayReplayItem Item,StrategyBar[] Bars,MarketEvent[] Events);
public sealed record DayTradingResearchRow(StockItem Stock,IntradayReplayResult? Result,string State,string Error)
{
    public string Symbol=>Stock.Symbol;public string Name=>Stock.Name;
    public int Signals=>Result?.Signals.Length??0;public int Trades=>Result?.Performance.Trades??0;
    public string Profit=>Result==null?"—":DisplayNumber.SignedPrice(Result.Account.Equity-Result.InitialCash);
}

/// <summary>Application-owned D workspace. Research, screening and paper execution share the same definition and core ports.</summary>
public sealed class DayTradingViewModel:Bindable,IAsyncDisposable
{
    private const string StoreKey="daytrading-definitions-v2";
    private readonly TradingStore store;
    private readonly IStrategyFileDialogs files;
    private readonly Action<string> alert;
    private readonly Func<StockItem[],StrategyDefinition,DateOnly,DateOnly,HistoricalFacts,bool,CancellationToken,Task<PreparedDayTrading[]>> prepare;
    private readonly Func<StockItem[],IMarketDataProvider> feed;
    private readonly Func<StockItem[]> targets;
    private readonly Func<DateTimeOffset> clock;
    private readonly IntradayBacktestService research=new();
    private readonly DayTradingSessionService session=new();
    private readonly Dictionary<string,StrategyDefinition> saved=[];
    private readonly HashSet<string> persistedFills=[];
    private StrategyDEditorModel editor;
    private StrategyDefinition working;
    private CancellationTokenSource? operation;
    private bool busy,advanced,automatic;
    private string name,selected="",message="전략 D는 실험용입니다. 실제 주문 연결은 잠겨 있습니다. 설정을 확인하고 가상 계좌로 먼저 검증하세요.";
    private DayTradingResearchRow? selectedResearch;
    private DayTradingRowSnapshot? selectedLive;
    private string paperPeriod="분봉";
    public ObservableCollection<string> Presets {get;}=[];
    public ObservableCollection<DayTradingParameterGroup> Groups {get;}=[];
    public ObservableCollection<DayTradingResearchRow> Results {get;}=[];
    public ObservableCollection<DayTradingRowSnapshot> LiveRows {get;}=[];
    public ObservableCollection<IntradayFillRow> VisibleFills {get;}=[];
    public PriceBar[] ChartBars {get;private set;}=[];
    public TradeMarker[] Markers {get;private set;}=[];
    public ChartSupportZone[] Zones {get;private set;}=[];
    public string ChartPeriod {get;private set;}="일봉";
    private static string Period(StrategyDefinition value)=>value.DayTrading!.SimpleTimeframe.Unit switch{TimeframeUnit.TradingDay=>"일봉",TimeframeUnit.Minute=>value.DayTrading.SimpleTimeframe.Size+"분",TimeframeUnit.Hour=>value.DayTrading.SimpleTimeframe.Size+"시",_=>value.DayTrading.SimpleTimeframe.ToString()};
    public Dictionary<string,AsyncCommand> Commands {get;}=[];
    public string Name {get=>name;set{name=value;Changed();}}
    public string SelectedPreset {get=>selected;set{if(busy||string.IsNullOrEmpty(value)||value==selected)return;if(saved.TryGetValue(value,out var definition))Load(definition);}}
    public bool Advanced {get=>advanced;set{advanced=value;BuildGroups();Changed();}}
    public bool Busy {get=>busy;private set{busy=value;Changed();Changed(nameof(Editable));foreach(var command in Commands.Values)command.Refresh();}}
    public bool Editable=>!Busy;
    public bool Running=>session.IsRunning;
    public bool HasUnsettled=>session.HasUnsettled;
    public string Connection=>session.Connection;
    public string Message {get=>message;private set{message=value;Changed();}}
    public DateTime StartDate {get;set;}=DateTime.Today.AddDays(-7);
    public DateTime EndDate {get;set;}=DateTime.Today;
    public bool RawReplay {get;set;}
    public bool ResearchAssumptions {get;set;}
    public string SeedCash {get;set;}="10000000";
    public string FactsJson {get;set;}=Serialization.Pack(new HistoricalFacts());
    public string Detail {get;private set;}="체결/신호를 선택하면 당시 근거를 표시합니다.";
    public bool AutomaticEntries
    {
        get=>automatic;
        set
        {
            try{session.SetEntriesEnabled(value,clock());automatic=value;Changed();}
            catch(InvalidOperationException ex){automatic=false;Changed();alert(ex.Message);}
        }
    }
    public DayTradingResearchRow? SelectedResearch {get=>selectedResearch;set{selectedResearch=value;Changed();if(value?.Result is {} result)ShowResult(result);}}
    public DayTradingRowSnapshot? SelectedLive {get=>selectedLive;set{selectedLive=value;Changed();}}
    public IntradayReplayResult? Latest {get;private set;}
    public int DisplayedPage {get;set;}
    public event Action<DayTradingHistoryRecord>? ResearchCompleted;
    public event Action? PaperFillsChanged;
    public DayTradingViewModel(TradingStore store,IStrategyFileDialogs files,Action<string> alert,
        Func<StockItem[],StrategyDefinition,DateOnly,DateOnly,HistoricalFacts,bool,CancellationToken,Task<PreparedDayTrading[]>> prepare,
        Func<StockItem[],IMarketDataProvider> feed,Func<StockItem[]> targets,Func<DateTimeOffset>? clock=null)
    {
        this.store=store;this.files=files;this.alert=alert;this.prepare=prepare;this.feed=feed;this.targets=targets;this.clock=clock??(()=>DateTimeOffset.UtcNow);
        foreach(var definition in store.State<StrategyDefinition[]>(StoreKey)??[]){definition.Validate();if(definition.IsStrategyD)saved[definition.Preset]=definition;}
        working=StrategyDDefaults.Create(DateTimeOffset.UtcNow);editor=new(working);name=working.Preset;
        foreach(var key in saved.Keys)Presets.Add(key);if(Presets.Count>0)Load(saved[Presets[0]]);else{Presets.Add(working.Preset);selected=working.Preset;BuildGroups();}
        void Command(string key,Func<Task> action,Func<bool>? can=null)=>Commands[key]=new(()=>Run(action),()=>!Busy&&(can?.Invoke()??true));
        Command("추가",()=>{Load(StrategyDDefaults.Create(DateTimeOffset.UtcNow) with{Preset="전략 D · 새 설정"});return Task.CompletedTask;});
        Command("저장",()=>{var definition=Apply();saved[definition.Preset]=definition;if(!Presets.Contains(definition.Preset))Presets.Add(definition.Preset);store.State(StoreKey,saved.Values.ToArray());Load(definition);Message="전략 D 설정 저장 · 기존61개 프리셋은 유지됩니다.";return Task.CompletedTask;});
        Command("복구",()=>{Load(saved.GetValueOrDefault(selected)??working);return Task.CompletedTask;});
        Command("불러오기",async()=>{var path=await files.Import();if(path==null)return;var definition=StrategyDefinitionJson.Import(File.ReadAllText(path));if(!definition.IsStrategyD)throw new InvalidOperationException("기존 낙주 전략 JSON은 기존 설정의 불러오기를 사용하세요.");Load(definition);});
        Command("내보내기",async()=>{var definition=Apply();var path=await files.Export("전략D-"+DateTime.Today.ToString("yyyyMMdd")+".json");if(path!=null)File.WriteAllText(path,StrategyDefinitionJson.Export(definition));});
        Command("연구",()=>Research(false));Command("스크리닝",()=>Research(true));Command("가상시작",StartPaper,()=>!Running);
        Commands["취소"]=new(()=>{operation?.Cancel();return Task.CompletedTask;},()=>Busy);
        Commands["진입중지"]=new(()=>{AutomaticEntries=false;Message="신규·추가매수 중지 · 기존 보유 보호 감시는 유지합니다.";return Task.CompletedTask;},()=>Running);
        Commands["긴급중지"]=new(()=>{EmergencyStop();return Task.CompletedTask;},()=>true);
    }
    private void Load(StrategyDefinition definition)
    {working=definition.Snapshot();editor=new(working);name=working.Preset;selected=name;BuildGroups();Changed(nameof(Name));Changed(nameof(SelectedPreset));Changed(nameof(ChartPeriod));}
    private void BuildGroups()
    {Groups.Clear();foreach(var group in editor.Parameters.Where(p=>Advanced||!p.Advanced).GroupBy(p=>p.Group))Groups.Add(new(group.Key,group.ToArray()));}
    private StrategyDefinition Apply(){working=editor.Apply(Name);Changed(nameof(ChartPeriod));return working.Snapshot();}
    private async Task Run(Func<Task> action)
    {
        if(Busy)return;operation=new();Busy=true;
        try{await action();}catch(OperationCanceledException) when(operation.IsCancellationRequested){Message="취소됨 · 이미 계산한 결과는 유지됩니다.";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Message=OperationError.Describe(ex);alert(Message);}
        finally{operation.Dispose();operation=null;Busy=false;}
    }
    private decimal Cash(){if(!decimal.TryParse(SeedCash,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var amount)||amount<=0)throw new InvalidOperationException("가상 시드머니를 양수로 입력하세요.");return amount;}
    private HistoricalFacts Facts(){try{var value=Serialization.Unpack<HistoricalFacts>(FactsJson);value.Validate();return value;}catch(Exception ex) when(ex is not OutOfMemoryException){throw new InvalidOperationException("시점 자료 JSON을 확인하세요.",ex);}}
    private ExecutionSettings Costs(StrategyDefinition definition)=>new(){UseSpread=false,FeePercent=definition.Configuration.CommissionPercent,TaxPercent=definition.Configuration.TaxPercent,SlippagePercent=definition.Configuration.SlippagePercent,LatencyMs=100,ParticipationPercent=10,AllowPartial=true,TimeoutMilliseconds=30000};
    private async Task Research(bool screening)
    {
        var definition=Apply();var items=targets();if(items.Length==0)throw new InvalidOperationException("먼저 종목을 검색하거나 스크리닝 대상/관심종목을 선택하세요.");if(!screening)items=[items[0]];
        var from=DateOnly.FromDateTime(StartDate);var to=DateOnly.FromDateTime(EndDate);if(from>to||to>KoreaClock.Today)throw new InvalidOperationException("연구 기간의 순서와 미래 날짜를 확인하세요.");
        var token=operation!.Token;Results.Clear();SelectedResearch=null;Message="자료 준비 중 · 화면/백테스트가 같은 D 런타임을 사용합니다.";
        var prepared=await prepare(items,definition,from,to,Facts(),ResearchAssumptions,token);
        var first=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));var last=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)).AddTicks(-1);if(last>DateTimeOffset.UtcNow)last=DateTimeOffset.UtcNow;
        foreach(var item in prepared)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                IntradayReplayResult result=RawReplay?await research.RunAsync(new IntradayReplayRequest([item.Item],item.Events,new("현재 프로세스의 원본 체결·수신시각",true,true),first,last,DateTimeOffset.UtcNow,Cash(),Costs(definition),definition.DayTrading!.Risk),token):
                    await research.RunAsync(new ClosedBarReplayRequest([new(item.Item,item.Bars)],"공급자 확정 OHLCV · 장중 순서 미제공",first,last,DateTimeOffset.UtcNow,Cash(),Costs(definition),definition.DayTrading!.Risk,ResearchAssumptions),token);
                var state=result.Signals.Length>0?"매수 신호 관측":result.Account.Rows.FirstOrDefault()?.State??"신호 없음";
                Results.Add(new(item.Stock,result,state,""));
                // Reproduction stores bar/signal/fill snapshots, never the original trade-event stream.
                store.State("daytrading-result:"+result.Id,result);store.State("daytrading-result-bars:"+result.Id,item.Bars.Select(b=>b.Price with{Time=b.OpenTime}).ToArray());store.State("daytrading-result-definition:"+result.Id,definition);
                var history=new DayTradingHistoryRecord("daytrading-history:"+Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,result.Id,definition.Preset+" · "+item.Stock.Name,$"{from} ~ {to} · {Period(definition)}");store.State(history.Id,history);ResearchCompleted?.Invoke(history);
                if(SelectedResearch==null||!screening)SelectedResearch=Results.Last();
            }
            catch(OperationCanceledException){throw;}
            catch(Exception ex) when(ex is not OutOfMemoryException){Results.Add(new(item.Stock,null,"자료/조건 오류",ex.Message));}
            Message=$"{Results.Count:N0}/{prepared.Length:N0}종목 완료 · 확정봉 연구는 틱 재현과 구분합니다.";
        }
        if(Results.All(r=>r.Result==null))throw new InvalidOperationException(Results.FirstOrDefault()?.Error??"연구 결과가 없습니다.");
    }
    public void ShowResult(IntradayReplayResult result)
    {
        Latest=result;ChartPeriod=Period(store.State<StrategyDefinition>("daytrading-result-definition:"+result.Id)??working);ChartBars=store.State<PriceBar[]>("daytrading-result-bars:"+result.Id)??[];VisibleFills.Clear();foreach(var fill in result.Fills)VisibleFills.Add(new(fill));
        Markers=MakeMarkers(result.Fills,result.Trades);Zones=MakeZones(result.Signals,result.To);Detail=string.Join("\n",result.Notices)+$"\n완결거래 {result.Performance.Trades} · 실현손익 {DisplayNumber.SignedPrice(result.Trades.Sum(t=>t.RealizedProfit))} · MDD {result.Performance.MaxDrawdown:N2}%\n공통 성과지표: "+Serialization.Pack(result.Performance);
        Changed(nameof(Latest));NotifyChart();Changed(nameof(Detail));
    }
    public void SelectFill(Fill fill)
    {
        Detail=$"{(fill.Buy?"매수":"매도")} · {IntradayPresentation.Reason(fill.Reason)}\n{fill.Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss.fff} · 가격 {DisplayNumber.Price(fill.Price)}원 · 수량 {fill.Quantity:N0}주 · 수수료 {DisplayNumber.Price(fill.Fee)}원";
        if(!fill.Buy)Detail+=$"\n실현손익 {DisplayNumber.SignedPrice(fill.RealizedPnl)}원";
        if(Latest is {} result){var trade=result.Trades.FirstOrDefault(t=>t.Fills.Contains(fill));if(trade?.Entry is {} entry)Detail+=$"\n진입 당시 매물대 {DisplayNumber.Price(entry.ZoneLow??0)} ~ {DisplayNumber.Price(entry.ZoneHigh??0)}원\n"+IntradayPresentation.PrettyJson(entry.EvidenceJson);if(result.ExitSnapshots.TryGetValue(fill.OrderId,out var exit))Detail+="\n청산 판단\n"+IntradayPresentation.PrettyJson(exit);}
        if(session.EntryEvidence(fill) is {} liveEntry)Detail+="\n가상 체결 당시 진입 근거\n"+IntradayPresentation.PrettyJson(liveEntry.EvidenceJson);
        Changed(nameof(Detail));
    }
    private async Task StartPaper()
    {
        var definition=Apply();var items=targets();if(items.Length==0)throw new InvalidOperationException("실행할 국내 종목을 선택하세요.");
        var today=DailyQuoteMerger.Date(clock(),Exchange.KRX);var input=await prepare(items,definition,today,today,Facts(),false,operation!.Token);
        var now=clock();if(input.Any(i=>!i.Item.Seed.Facts.Sessions.Any(s=>now>=s.Open&&now<s.Close)))throw new InvalidOperationException("장 외 시간이거나 오늘의 확인된 시장 세션 자료가 없습니다. 실시간 가상 감시는 거래 중인 시장 세션에서 시작하세요.");
        var market=feed(items);try{session.Start(market,input.Select(p=>new StrategyDRuntime(p.Item.Instrument,definition,p.Item.Seed,definition.Preset)).ToArray(),Cash(),Costs(definition),definition.DayTrading!.Risk,definition.DayTrading.Quality.MaximumQueuedEvents,clock);}
        catch{await market.DisposeAsync();throw;}
        paperPeriod=Period(definition);if(automatic)session.SetEntriesEnabled(true,clock());Message="가상 감시 시작 · 실제 주문 없음 · 전략 snapshot 고정 · 팝업을 닫아도 실행됩니다.";RefreshLive();
    }
    public void RefreshLive()
    {
        if(session.Snapshot() is not {} snapshot)return;
        PersistPaperFills(true);
        var selectedKey=SelectedLive?.Symbol;LiveRows.Clear();foreach(var row in snapshot.Rows)LiveRows.Add(row);SelectedLive=LiveRows.FirstOrDefault(r=>r.Symbol==selectedKey)??LiveRows.FirstOrDefault();
        Changed(nameof(Running));Changed(nameof(Connection));foreach(var command in Commands.Values)command.Refresh();
        if(DisplayedPage==2&&SelectedLive is {} current)
        {ChartPeriod=paperPeriod;ChartBars=session.AnalysisPrices(current.Symbol);var fills=session.Fills.Where(f=>f.Instrument.Key==current.Symbol).ToArray();VisibleFills.Clear();foreach(var fill in fills)VisibleFills.Add(new(fill));Markers=MakeMarkers(fills,[]);Zones=MakeZones(session.Signals.Where(s=>s.Symbol==current.Symbol),DateTimeOffset.UtcNow);NotifyChart();}
    }
    public void EmergencyStop(){operation?.Cancel();session.EmergencyStop(clock());automatic=false;Changed(nameof(AutomaticEntries));RefreshLive();}
    public void BlockNewEntries(string reason){session.SetEntriesEnabled(false,clock());automatic=false;Message=reason;Changed(nameof(AutomaticEntries));}
    private void PersistPaperFills(bool notify)
    {
        var updated=false;
        foreach(var fill in session.Fills)
        {
            var key=TradingStore.Hash(Serialization.Pack(fill));if(persistedFills.Contains(key))continue;
            var signal=session.EntryEvidence(fill);
            store.State("daytrading-paper-fill:"+key,new{Fill=fill,Entry=signal});
            store.State("trade-marker:D:"+key,new TradeMarker(key,fill.Instrument.Key,fill.Time,fill.Buy,fill.Quantity,fill.Price,"전략 D 모의매매",Reason:fill.Reason,RealizedPnl:fill.Buy?null:fill.RealizedPnl));
            persistedFills.Add(key);updated=true;
        }
        if(updated&&notify)PaperFillsChanged?.Invoke();
    }
    private void NotifyChart(){Changed(nameof(ChartBars));Changed(nameof(Markers));Changed(nameof(Zones));Changed(nameof(ChartPeriod));}
    public static string FillKey(Fill fill)=>fill.OrderId+"|"+fill.Time.UtcTicks+"|"+fill.Quantity;
    private static TradeMarker[] MakeMarkers(IEnumerable<Fill> fills,IntradayRoundTrip[] trades)=>fills.Select(f=>new TradeMarker(FillKey(f),f.Instrument.Key,f.Time,f.Buy,f.Quantity,f.Price,"전략 D 가상/연구",TradeNumber:trades.FirstOrDefault(t=>t.Fills.Contains(f))?.Number,Reason:f.Reason,RealizedPnl:f.Buy?null:f.RealizedPnl)).ToArray();
    private static ChartSupportZone[] MakeZones(IEnumerable<IntradayEntrySignal> signals,DateTimeOffset end)=>signals.Where(s=>s.ZoneLow.HasValue&&s.ZoneHigh.HasValue).Select(s=>new ChartSupportZone(s.Time,end,s.ZoneLow!.Value,s.ZoneHigh!.Value,true,s.Reason+" · 진입 당시 MA+매물대")).ToArray();
    public async ValueTask DisposeAsync(){operation?.Cancel();await session.DisposeAsync().ConfigureAwait(false);PersistPaperFills(false);}
}
