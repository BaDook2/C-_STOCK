using Stock.Core.Research;
namespace Stock.Desktop;
/// <summary>One reusable popup, immutable run context and cancellation generation per navigation.</summary>
public sealed class ResearchBrowserViewModel(StockItem[] items,Func<StockItem,CancellationToken,Task<ResearchResultViewModel>> load,string context):Bindable,IDisposable
{
    private CancellationTokenSource? cancellation;
    private long generation;
    private int index=-1;
    private bool disposed,busy;
    private ResearchResultViewModel? current;
    private string status="자료 준비";
    public ResearchResultViewModel? Current {get=>current;private set=>Set(ref current,value);}
    public bool Busy {get=>busy;private set=>Set(ref busy,value);}
    public string Status {get=>status;private set=>Set(ref status,value);}
    public string Context=>context;
    public AsyncCommand Previous {get;private set;}=null!;
    public AsyncCommand Next {get;private set;}=null!;
    public AsyncCommand Retry {get;private set;}=null!;
    public event Action<string>? Notice;
    public void Initialize(){Previous=new(()=>Go(index-1),()=>index>0);Next=new(()=>Go(index+1),()=>index+1<items.Length);Retry=new(()=>Go(index),()=>index>=0);}
    public async Task Go(int value)
    {
        if(disposed||value<0||value>=items.Length)return;index=value;var version=++generation;cancellation?.Cancel();var stop=new CancellationTokenSource();cancellation=stop;Busy=true;Status=$"{index+1}/{items.Length} · {items[index].Name} 준비 중…";Previous.Refresh();Next.Refresh();Retry.Refresh();
        try{var result=await load(items[value],stop.Token);stop.Token.ThrowIfCancellationRequested();if(!disposed&&version==generation){Current=result;Status=$"{index+1}/{items.Length} · {result.Title}";}}
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        catch(Exception ex) when(ex is not OutOfMemoryException){if(!disposed&&version==generation){Status=OperationError.Describe(ex);Notice?.Invoke(Status);}}
        finally{if(version==generation)Busy=false;stop.Dispose();if(cancellation==stop)cancellation=null;}
    }
    public void Dispose(){disposed=true;generation++;cancellation?.Cancel();}
}
public sealed partial class MainViewModel
{public event Action<ResearchBrowserViewModel>? BrowserPopupRequested;}
