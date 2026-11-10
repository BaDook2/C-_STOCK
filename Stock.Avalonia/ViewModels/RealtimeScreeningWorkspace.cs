using System.Collections.ObjectModel;
using Avalonia.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed class RealtimeDisplayRow:Bindable
{
    public RealtimeScreeningStatusRow Snapshot {get;private set;}
    private LiveBatchRowStatus? order;
    public RealtimeDisplayRow(RealtimeScreeningStatusRow row){Snapshot=row;}
    public string Key=>Snapshot.Key;public string Name=>Snapshot.Name;public string Code=>Snapshot.Code;
    public string Price=>Snapshot.Price is {} p?DisplayNumber.Price(p):"—";
    public string Change=>Snapshot.ChangePercent is {} p?p.ToString("+0.00;-0.00;0.00")+"%":"—";
    public string Proximity=>Snapshot.Proximity.ToString("0.00")+"점";
    public string Conditions=>$"{Snapshot.MetConditions}/{Snapshot.TotalConditions}";
    public string Unmet=>Snapshot.UnmetConditions;public string State=>Snapshot.StrategyState;
    public string Data=>Snapshot.DataState;public string Time=>Snapshot.LastQuoteTime?.ToOffset(TimeSpan.FromHours(9)).ToString("HH:mm:ss")??"—";
    public string Orders=>order==null?"신호만":$"{order.Status} · 보유{order.Owned}주 · 대기{order.Pending}주";
    public string Error=>Snapshot.Error;
    public void Update(RealtimeScreeningStatusRow row,LiveBatchRowStatus? value){Snapshot=row;order=value;foreach(var key in new[]{nameof(Price),nameof(Change),nameof(Proximity),nameof(Conditions),nameof(Unmet),nameof(State),nameof(Data),nameof(Time),nameof(Orders),nameof(Error)})Changed(key);}
}
public sealed record LiveFillRow(TradeMarker Marker)
{public string Time=>Marker.Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss");public string Side=>Marker.SideLabel;public string Price=>Marker.Price is {} value?DisplayNumber.Price(value):"—";public decimal Quantity=>Marker.Quantity;public string Profit=>Marker.ProfitText;public string Reason=>Marker.Reason;}
public sealed class LiveChartViewModel(RealtimeScreeningChartData data,Func<TradeMarker,OversoldDecisionSnapshot?>? evidence=null):Bindable
{
    public string Key=>data.Stock.Key;
    public string Title=>data.Stock.Name+" · "+data.StrategyName+" · "+data.Specification;
    public string Period=>data.Specification.Unit==BarUnit.Day?"일봉":data.Specification.Value+(data.Specification.Unit==BarUnit.Hour?"시":"분");
    public PriceBar[] DailyBars {get;private set;}=data.Facts.StrategyFrames.Where(s=>s.Timeframe==new Stock.Core.Research.DayTrading.TimeframeSpec(1,Stock.Core.Research.DayTrading.TimeframeUnit.TradingDay)).Select(s=>s.Bar.Price).ToArray();
    public PriceBar[] Bars {get;private set;}=data.Bars.Select(b=>b.Price with{Time=b.OpenTime}).ToArray();
    public TradeMarker[] Markers {get;private set;}=[];
    public LiveFillRow[] Trades {get;private set;}=[];
    public ChartSupportZone[] Zones {get;private set;}=[];
    private LiveFillRow? selected;
    public LiveFillRow? Selected {get=>selected;set{if(Set(ref selected,value))Changed(nameof(Detail));}}
    public string Summary {get;private set;}="실제 가상 체결만 표시 · 백테스트의 가정 체결과 구분합니다.";
    public string Detail=>Selected==null?"체결을 선택하면 당시 시간·가격·사유를 표시합니다.":$"{Selected.Side}\n{Selected.Time}\n가격 {Selected.Price} · {Selected.Quantity}주\n{Selected.Profit}\n{Selected.Reason}\n"+(evidence?.Invoke(Selected.Marker) is {} snapshot?Stock.Core.Research.DayTrading.IntradayPresentation.PrettyJson(Serialization.Pack(snapshot)):"당시 판단 snapshot을 확인하지 못했습니다. 현재 지지를 재계산해 붙이지 않습니다.");
    public void Update(RealtimeScreeningChartData current,RealtimeScreeningStatusRow? quote,TradeMarker[] fills)
    {
        DailyBars=current.Facts.StrategyFrames.Where(s=>s.Timeframe==new Stock.Core.Research.DayTrading.TimeframeSpec(1,Stock.Core.Research.DayTrading.TimeframeUnit.TradingDay)).Select(s=>s.Bar.Price).ToArray();Changed(nameof(DailyBars));Bars=current.Bars.Select(b=>b.Price with{Time=b.OpenTime}).ToArray();var markers=fills.Where(f=>f.StockKey==Key).OrderBy(f=>f.Time).ToArray();if(!markers.SequenceEqual(Markers)){var previous=Selected?.Marker.Id;Markers=markers;Trades=markers.Select(m=>new LiveFillRow(m)).ToArray();Selected=Trades.FirstOrDefault(r=>r.Marker.Id==previous);Changed(nameof(Markers));Changed(nameof(Trades));}Summary=$"현재가 {(quote?.Price is {} price?DisplayNumber.Price(price):"—")} · {quote?.ChangePercent:+0.00;-0.00;0.00}% · {quote?.DataState} · 관측모형 가상 체결 {Trades.Length:N0}건";
        var end=current.Bars.LastOrDefault()?.ClosedAt;var zones=Markers.Where(m=>m.Buy).Select(m=>evidence?.Invoke(m)).Where(s=>s!=null).SelectMany(s=>s!.Clusters.Select(c=>new ChartSupportZone(s.SignalTime,end??s.SignalTime,c.Lower,c.Upper,c.UsedForEntry,string.Join(", ",c.Supports.Select(p=>p.Id))))).ToArray();if(!zones.SequenceEqual(Zones)){Zones=zones;Changed(nameof(Zones));}Changed(nameof(Detail));Changed(nameof(Bars));Changed(nameof(Summary));
    }
}
public sealed class RealtimeScreeningWorkspace:Bindable,IAsyncDisposable
{
    private readonly AppServices services;
    private readonly StrategyEditor strategies;
    private readonly ScreeningWorkspace selection;
    private readonly Func<StockItem[]> watch;
    private readonly Func<string> facts;
    private readonly RealtimeScreeningHost host;
    private readonly DispatcherTimer timer;
    private CancellationTokenSource? preparation;
    private bool busy,automatic,assumptions,disposed,showAll,hideOldRows,explicitBatch;
    private string status="장중에 시작하세요. 자동매매는 기본 꺼짐이며 가상 계좌만 사용합니다.";
    private RealtimeDisplayRow? selected;
    private readonly List<TradeMarker> fills=[];
    private readonly List<WeakReference<LiveChartViewModel>> openCharts=[];
    private readonly Dictionary<string,OversoldDecisionSnapshot> evidenceCache=[];
    public ObservableCollection<RealtimeDisplayRow> Rows {get;}=[];
    public bool Busy {get=>busy;private set{Set(ref busy,value);Start.Refresh();Stop.Refresh();}}
    public bool Running=>host.Snapshot.Running;
    public bool HasProtectedPositions=>host.HasProtectedPositions;
    public bool CanAutomatic=>true;
    public LiveBatchPlan? RecoveredPlan=>host.RecoveredPlan;
    public AsyncCommand RestoreSaved {get;}
    public AsyncCommand ResumeSaved {get;}
    public bool ShowAll {get=>showAll;set{Set(ref showAll,value);Refresh();}}
    public bool PaperAssumptions {get=>assumptions;set=>Set(ref assumptions,value);}
    public string TotalBudget {get;set;}="100000000";
    public string DailyLossLimit {get;set;}="1000000";
    public bool Automatic
    {
        get=>automatic;
        set
        {
            if(automatic==value)return;
            try{if(Running||!value)host.SetAutomatic(value);automatic=value;Changed();}
            catch(Exception ex) when(ex is not OutOfMemoryException){automatic=false;Changed();Notice?.Invoke(ex.Message);}
        }
    }
    public string Status {get=>status;private set=>Set(ref status,value);}
    public RealtimeDisplayRow? Selected {get=>selected;set{Set(ref selected,value);OpenChart.Refresh();}}
    public AsyncCommand Start {get;}
    public AsyncCommand Stop {get;}
    public AsyncCommand StopAll {get;}
    public AsyncCommand OpenChart {get;}
    public event Action<string>? Notice;
    public event Action<LiveChartViewModel>? PopupRequested;
    public event Action<TradeMarker>? FillRecorded;
    public RealtimeScreeningWorkspace(AppServices services,StrategyEditor strategies,ScreeningWorkspace selection,MarketHistoryService history,Func<IAccountBroker?> account,Func<StockItem[]> watch,Func<string> facts)
    {
        this.services=services;this.strategies=strategies;this.selection=selection;this.watch=watch;this.facts=facts;host=new(services,history,account);
        host.Progress+=text=>Dispatcher.UIThread.Post(()=>{if(!disposed)Status=text;});host.FillRecorded+=marker=>Dispatcher.UIThread.Post(()=>{if(disposed)return;fills.Add(marker);FillRecorded?.Invoke(marker);});
        Start=new(Execute,()=>!Busy&&!Running);Stop=new(()=>{preparation?.Cancel();host.StopResearch();automatic=false;Changed(nameof(Automatic));Refresh();return Task.CompletedTask;},()=>Busy||Running||HasProtectedPositions);OpenChart=new(OpenSelected,()=>Selected!=null);
        timer=new(){Interval=TimeSpan.FromSeconds(1)};timer.Tick+=(_,_)=>{try{Refresh();}catch(Exception ex) when(ex is not OutOfMemoryException){Emergency();Status="실시간 화면 갱신 오류 · 신규 주문 차단";Notice?.Invoke(ex.Message);}};timer.Start();
        strategies.PropertyChanged+=StrategyChanged;
        selection.PropertyChanged+=SelectionChanged;
        StopAll=new(()=>{preparation?.Cancel();host.Emergency("전략 전체 중지");automatic=false;Changed(nameof(Automatic));Refresh();return Task.CompletedTask;});
        RestoreSaved=new(async()=>{Busy=true;try{await Task.Run(host.RestorePaper);automatic=false;Changed(nameof(Automatic));Changed(nameof(RecoveredPlan));Status=host.PaperSnapshot.Status;fills.Clear();foreach(var row in services.Store.States("trade-marker:").Select(s=>Serialization.Unpack<TradeMarker>(s.Json)).Where(m=>m.AccountScope=="paper-live:"+host.PaperRunId))fills.Add(row);Refresh();}catch(Exception ex) when(ex is not OutOfMemoryException){Notice?.Invoke(OperationError.Describe(ex));}finally{Busy=false;}},()=>!Busy&&!Running&&!HasProtectedPositions);
        ResumeSaved=new(async()=>{preparation?.Cancel();preparation=new();Busy=true;try{await host.ResumePaper(PaperAssumptions,preparation.Token);explicitBatch=true;automatic=false;Changed(nameof(Automatic));Refresh();}catch(Exception ex) when(ex is not OutOfMemoryException){Notice?.Invoke(OperationError.Describe(ex));}finally{Busy=false;}},()=>!Busy&&!Running&&RecoveredPlan!=null);
    }
    private async Task Execute()
    {
        preparation?.Dispose();preparation=new();var token=preparation.Token;Busy=true;
        try
        {
            if(!LiveBatchSupport.RegularSession(services.Clock()))throw new InvalidOperationException("현재는 장 외 시간입니다. 국내 정규 연속매매 시간(09:00~15:20)에 시작하세요.");
            var spec=selection.Daily?new BarSpecification(1,BarUnit.Day):new(int.Parse(selection.Value),selection.Unit=="분"?BarUnit.Minute:BarUnit.Hour);var config=strategies.Current();var input=Serialization.Unpack<HistoricalFacts>(facts());
            var name=strategies.Selected;var market=selection.Market;var sector=selection.Sector;var top=selection.TopCount;var useWatch=selection.UseWatchlist;var assumptions=PaperAssumptions;var budget=decimal.Parse(TotalBudget,System.Globalization.CultureInfo.InvariantCulture);var loss=decimal.Parse(DailyLossLimit,System.Globalization.CultureInfo.InvariantCulture);
            StockItem[] targets=(useWatch?watch():await selection.Catalog(market,top,token)).Where(s=>Matches(s,market)&&(sector=="전체"||s.Sector==sector)).DistinctBy(s=>s.Key).ToArray();
            var requested=new RealtimeRequest(targets,name,config,spec,input,assumptions,budget,loss);
            explicitBatch=false;await Activate(requested,token);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){Status="실시간 준비 취소";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=OperationError.Describe(ex);Notice?.Invoke(Status);}
        finally{Busy=false;}
    }
    public async Task StartConfigured(RealtimeRequest request)
    {
        if(Busy||Running||HasProtectedPositions)throw new InvalidOperationException("기존 실시간 실행·가상 보유/미체결을 먼저 확인하세요.");
        preparation?.Dispose();preparation=new();var token=preparation.Token;Busy=true;try{explicitBatch=true;automatic=true;Changed(nameof(Automatic));await Activate(request,token);}catch(OperationCanceledException ex) when(!token.IsCancellationRequested){throw new TimeoutException(OperationError.ResponseTimeout,ex);}finally{Busy=false;}
    }
    private async Task Activate(RealtimeRequest requested,CancellationToken token)
    {await host.Start(requested,token);openCharts.Clear();evidenceCache.Clear();hideOldRows=false;Rows.Clear();fills.Clear();Selected=null;var enabled=automatic;automatic=false;Changed(nameof(Automatic));if(enabled)Automatic=true;Refresh();}
    public Dictionary<string,string> SessionStatuses(){var states=host.Snapshot.Rows.ToDictionary(r=>r.Key,r=>r.StrategyState);foreach(var row in host.PaperSnapshot.Rows)states[row.Key]=row.Status;return states;}
    private static bool Matches(StockItem stock,string market)=>market switch{"코스피"=>stock.Board=="KOSPI","코스닥"=>stock.Board=="KOSDAQ","코스피+코스닥"=>stock.Exchange==Exchange.KRX,_=>stock.Exchange!=Exchange.KRX};
    private void StrategyChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs args)
    {
        if(args.PropertyName!=nameof(StrategyEditor.Selected)||explicitBatch)return;if(Busy){preparation?.Cancel();return;}if(!Running)return;
        // Frozen definitions never mix across versions. Restart is explicit when positions need protection.
        host.StopResearch();openCharts.Clear();hideOldRows=true;automatic=false;Rows.Clear();Selected=null;Changed(nameof(Automatic));Status="전략이 변경되어 감시를 중지했습니다. 새 전략으로 다시 시작하세요. 기존 가상 보유분 보호는 유지됩니다.";Changed(nameof(Running));Start.Refresh();
    }
    public void Refresh()
    {
        if(disposed)return;var snapshot=host.Snapshot;var orders=host.PaperSnapshot.Rows.ToDictionary(r=>r.Key);var wanted=hideOldRows?[]:snapshot.Rows.Where(r=>ShowAll||r.MatchesStrategy||r.NearCandidate).ToArray();
        foreach(var old in Rows.Where(r=>!wanted.Any(w=>w.Key==r.Key)).ToArray())Rows.Remove(old);
        foreach(var value in wanted){var row=Rows.FirstOrDefault(r=>r.Key==value.Key);if(row==null){row=new(value);Rows.Add(row);}row.Update(value,orders.GetValueOrDefault(value.Key));}
        if(!Busy&&!hideOldRows&&snapshot.Rows.Count>0)Status=$"{snapshot.StrategyName} · {snapshot.BarSpecification} · {snapshot.Status} · {snapshot.ConnectionState}"+(host.Error.Length>0?" · "+host.Error:"");Changed(nameof(Running));Changed(nameof(CanAutomatic));Start.Refresh();Stop.Refresh();
        foreach(var reference in openCharts.ToArray())if(reference.TryGetTarget(out var chart)&&snapshot.Rows.Any(r=>r.Key==chart.Key))chart.Update(host.ChartData(chart.Key),snapshot.Rows.FirstOrDefault(r=>r.Key==chart.Key),fills.ToArray());else openCharts.Remove(reference);
        RestoreSaved?.Refresh();ResumeSaved?.Refresh();
    }
    private Task OpenSelected()
    {
        if(Selected==null)return Task.CompletedTask;try{var model=new LiveChartViewModel(host.ChartData(Selected.Key),PaperEvidence);model.Update(host.ChartData(Selected.Key),Selected.Snapshot,fills.ToArray());openCharts.Add(new(model));PopupRequested?.Invoke(model);}catch(Exception ex) when(ex is not OutOfMemoryException){Notice?.Invoke(ex.Message);}return Task.CompletedTask;
    }
    private OversoldDecisionSnapshot? PaperEvidence(TradeMarker marker)
    {
        if(evidenceCache.TryGetValue(marker.Id,out var cached))return cached;if(!marker.AccountScope.StartsWith("paper-live:",StringComparison.Ordinal))return null;
        var journal=services.Store.State<LiveBatchJournal>("paper-live-batch:"+marker.AccountScope["paper-live:".Length..]);var execution=journal?.Executions.FirstOrDefault(e=>"trade-marker:"+TradingStore.Hash("paper-live:"+e.Id)==marker.Id);var snapshot=execution==null?null:journal!.Orders.FirstOrDefault(o=>o.ClientId==execution.OrderNumber)?.OversoldSnapshot;if(snapshot!=null)evidenceCache[marker.Id]=snapshot;return snapshot;
    }
    public void Emergency(){preparation?.Cancel();openCharts.Clear();host.Emergency();automatic=false;Changed(nameof(Automatic));Refresh();}
    private void SelectionChanged(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName==nameof(ScreeningWorkspace.Daily))Changed(nameof(CanAutomatic));}
    public async ValueTask DisposeAsync(){if(disposed)return;disposed=true;preparation?.Cancel();strategies.PropertyChanged-=StrategyChanged;selection.PropertyChanged-=SelectionChanged;timer.Stop();await host.DisposeAsync().ConfigureAwait(false);preparation?.Dispose();}
}
