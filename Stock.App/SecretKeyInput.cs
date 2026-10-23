using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Stock.App;

/// <summary>Single synchronized secret value; masked by default and cleared when its owner closes.</summary>
public sealed class SecretKeyInput : Grid
{
    private readonly PasswordBox masked=new();
    private readonly TextBox visible=new(){Visibility=Visibility.Collapsed};
    private bool synchronizing;
    public string Value {get=>masked.Password;set{masked.Password=value;}}
    public bool Revealed=>visible.Visibility==Visibility.Visible;
    public SecretKeyInput()
    {
        Children.Add(masked);Children.Add(visible);
        masked.Padding=visible.Padding=new Thickness(9);
        masked.PasswordChanged+=(_,_)=>Sync(true);visible.TextChanged+=(_,_)=>Sync(false);
        // Revealing permits reading, not accidental clipboard disclosure (including Ctrl+C/X).
        CommandManager.AddPreviewExecutedHandler(this,(_,e)=>{if(e.Command==ApplicationCommands.Copy||e.Command==ApplicationCommands.Cut)e.Handled=true;});
        CommandManager.AddPreviewCanExecuteHandler(this,(_,e)=>{if(e.Command==ApplicationCommands.Copy||e.Command==ApplicationCommands.Cut){e.CanExecute=false;e.Handled=true;}});
        visible.ContextMenu=new ContextMenu();masked.ContextMenu=new ContextMenu();
    }
    private void Sync(bool fromMasked)
    {
        if(synchronizing)return;synchronizing=true;
        try{if(fromMasked)visible.Text=masked.Password;else masked.Password=visible.Text;}finally{synchronizing=false;}
    }
    public void Reveal(bool reveal){visible.Visibility=reveal?Visibility.Visible:Visibility.Collapsed;masked.Visibility=reveal?Visibility.Collapsed:Visibility.Visible;}
    public void Clear(){Value="";Reveal(false);}
}
