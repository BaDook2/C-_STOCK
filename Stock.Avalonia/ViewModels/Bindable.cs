using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace Stock.Desktop;
public abstract class Bindable:INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    protected bool Set<T>(ref T field,T value,[CallerMemberName]string? name=null){if(EqualityComparer<T>.Default.Equals(field,value))return false;field=value;Changed(name);return true;}
}
public sealed class AsyncCommand(Func<Task> execute,Func<bool>? enabled=null):ICommand
{
    private bool running;
    public static event Action<Exception>? Failed;
    public static int FailureCount {get;private set;}
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter)=>!running&&(enabled?.Invoke()??true);
    public async void Execute(object? parameter){if(!CanExecute(parameter))return;running=true;Refresh();try{await execute();}catch(Exception ex) when(ex is not OutOfMemoryException){FailureCount++;if(Failed==null)throw;Failed(ex);}finally{running=false;Refresh();}}
    public void Refresh()=>CanExecuteChanged?.Invoke(this,EventArgs.Empty);
}
