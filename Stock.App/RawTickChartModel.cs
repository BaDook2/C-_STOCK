using System.ComponentModel;
using System.Runtime.CompilerServices;
using Stock.Core.Research;
namespace Stock.App;
/// <summary>Chart-only async state; no trading or backtest limit is set here.</summary>
public sealed class RawTickChartModel:INotifyPropertyChanged,IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<bool>? Updated;
    private TickChartWindow window;private readonly RawTickRepository repository;private readonly CancellationTokenSource lifetime=new();private CancellationTokenSource? request;private long generation;
    private string source="KIWOOM";public string Source=>source;public string[] Sources {get;private set;}=["KIWOOM"];
    private readonly SemaphoreSlim serial=new(1);private bool busy;private bool disposed;
    public string Symbol {get;}
    public RawTick[] Rows {get;private set;}=[];public long Origin {get;private set;} public double[] XValues {get;private set;}=[];public double[] YValues {get;private set;}=[];
    public TickFillMarker[] Markers {get;set;}=[];
    public TickTradeDetail? SelectedTrade {get;private set;}
    public string TradeSummary=>SelectedTrade?.Summary??"매수 ▲ / 매도 ▼를 선택하면 해당 체결의 FIFO 거래 상세를 표시합니다.";
    public bool Busy {get=>busy;private set{busy=value;Changed();}}
    private bool followLatest;public bool FollowLatest {get=>followLatest;set{followLatest=value;Changed();}}
    public string Status {get;private set;}="저장된 원본 체결을 조회합니다.";
    public TickChartLoadOptions Options=>window.Options;
    public long QueryCount=>window.Queries;
    public int CachedTicks=>window.CachedTicks;
    public RawTickChartModel(string path,string symbol,TickFillMarker[] markers,TickChartLoadOptions? options=null)
    {Symbol=symbol;Markers=markers;repository=new(path);window=new(repository,symbol,source,options??new());}
    public async Task LoadSources(){try{var sources=await repository.Sources(Symbol,lifetime.Token);if(disposed)return;Sources=sources.Length>0?sources:["KIWOOM"];if(!Sources.Contains(source)){source=Sources[0];window=new(repository,Symbol,source,Options);Changed(nameof(Source));}Changed(nameof(Sources));}catch(OperationCanceledException){}catch(Exception ex){if(!disposed){Status="자료 소스 조회 실패: "+ex.Message;Changed(nameof(Status));}}}
    public Task SwitchSource(string selected){if(selected==source)return Task.CompletedTask;source=selected;window=new(repository,Symbol,source,Options);Changed(nameof(Source));return Reset();}
    public Task Reset()=>Execute(async token=>{var extent=await window.Extent(token);if(extent==null)return new([],false,"데이터가 없습니다. 실제 체결을 수신하거나 원본 자료를 저장하세요.");return await window.Range(Math.Max(DateTimeOffset.MinValue.UtcTicks,extent.Last.UtcTicks-TimeSpan.FromHours(24).Ticks),extent.Last.UtcTicks,token);},true);
    public Task Range(long from,long to,bool reset=true)=>Execute(token=>window.Range(from,to,token),reset);
    public Task Move(bool forward)=>Execute(async token=>
    {
        if(Rows.Length==0)return new([],false,"데이터가 없습니다.");var edge=forward?Rows[^1].Key:Rows[0].Key;var adjacent=await window.Adjacent(edge,forward,token);
        if(adjacent.Length==0)return new(Rows,false,forward?"마지막 저장 체결입니다.":"처음 저장 체결입니다.");
        int keep=Math.Min(Rows.Length,Options.MaxVisibleRawTicks*9/10);int incoming=Options.MaxVisibleRawTicks-keep;
        var merged=(forward?Rows.TakeLast(keep).Concat(adjacent.Take(incoming)):adjacent.TakeLast(incoming).Concat(Rows.Take(keep))).DistinctBy(r=>r.Key).OrderBy(r=>r.Key).ToArray();
        var selected=forward?merged.TakeLast(Options.MaxVisibleRawTicks).ToArray():merged.Take(Options.MaxVisibleRawTicks).ToArray();return new(selected,false,$"{selected.Length:N0}틱 · 원본 시간/행 순서 유지");
    },false);
    public async Task Preload(bool forward)
    {
        if(disposed||Rows.Length==0||Busy||!await serial.WaitAsync(0))return;
        try{await window.Adjacent(forward?Rows[^1].Key:Rows[0].Key,forward,lifetime.Token);}catch(OperationCanceledException){}catch{ /* Foreground navigation surfaces read errors. */ }finally{serial.Release();}
    }
    private async Task Execute(Func<CancellationToken,Task<TickWindowResult>> load,bool reset)
    {
        if(disposed)return;request?.Cancel();var own=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);request=own;long version=++generation;Busy=true;
        try
        {
            await serial.WaitAsync(own.Token);TickWindowResult result;try{result=await load(own.Token);}finally{serial.Release();}
            var plotOrigin=Origin!=0?Origin:result.Ticks.Length>0?result.Ticks[0].Time.UtcDateTime.Date.Ticks:0;
            var coordinates=await Task.Run(()=>{var x=new double[result.Ticks.Length];var y=new double[x.Length];for(int i=0;i<x.Length;i++){own.Token.ThrowIfCancellationRequested();x[i]=(result.Ticks[i].Key.UtcTicks-plotOrigin)/(double)TimeSpan.TicksPerSecond;if(i>0&&x[i]<=x[i-1])x[i]=Math.BitIncrement(x[i-1]);y[i]=(double)result.Ticks[i].Price;}return (x,y);},own.Token);
            if(disposed||version!=generation)return;Origin=plotOrigin;XValues=coordinates.x;YValues=coordinates.y;Rows=result.Ticks;Status=source+" · "+result.Description+(Rows.Length>0?$" · {Rows[0].Time.ToOffset(TimeSpan.FromHours(9)):MM.dd HH:mm:ss.fff} ~ {Rows[^1].Time.ToOffset(TimeSpan.FromHours(9)):MM.dd HH:mm:ss.fff} · 메모리 {CachedTicks:N0}틱":"");Changed(nameof(Status));Updated?.Invoke(reset);
        }
        catch(OperationCanceledException){}catch(Exception ex){if(version==generation){Status="조회 실패: "+ex.Message;Changed(nameof(Status));}}
        finally{if(version==generation){Busy=false;request=null;}own.Dispose();}
    }
    public void Select(TickFillMarker marker){SelectedTrade=marker.Detail;Changed(nameof(TradeSummary));}
    public Task ZoomTrade()=>SelectedTrade==null?Task.CompletedTask:Execute(token=>window.Range(SelectedTrade.EntryTime.AddSeconds(-30).UtcTicks,(SelectedTrade.ExitTime??SelectedTrade.EntryTime).AddSeconds(30).UtcTicks,token,SelectedTrade.EntryTime.UtcTicks),true);
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();request?.Cancel();}
    private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
