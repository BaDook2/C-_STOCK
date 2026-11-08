using Avalonia;
using Avalonia.Controls;
namespace Stock.Desktop;

/// <summary>WPF Width/Height include Windows chrome; Avalonia Width/Height describe its client.
/// Keep copied window dimensions at the same outer size, once on first opening.</summary>
public sealed class WpfFrameSizing:AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnabledProperty=AvaloniaProperty.RegisterAttached<WpfFrameSizing,Window,bool>("Enabled");
    public static bool GetEnabled(Window window)=>window.GetValue(EnabledProperty);
    public static void SetEnabled(Window window,bool value)=>window.SetValue(EnabledProperty,value);
    static WpfFrameSizing()
    {
        EnabledProperty.Changed.AddClassHandler<Window>((window,change)=>
        {
            if(change.NewValue is not true||!OperatingSystem.IsWindows())return;
            EventHandler? opened=null;
            opened=(_,_)=>
            {
                window.Opened-=opened;
                if(window.WindowDecorations==WindowDecorations.None||window.WindowState!=WindowState.Normal)return;
                if(window.SizeToContent==SizeToContent.Manual&&double.IsFinite(window.Width)&&double.IsFinite(window.Height)&&window.FrameSize is { } frame)
                {
                    var borderWidth=Math.Max(0,frame.Width-window.ClientSize.Width);
                    var borderHeight=Math.Max(0,frame.Height-window.ClientSize.Height);
                    window.Width=Math.Max(window.MinWidth,window.Width-borderWidth);
                    window.Height=Math.Max(window.MinHeight,window.Height-borderHeight);
                }
            };
            window.Opened+=opened;
        });
    }
}
