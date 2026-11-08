using Avalonia;
namespace Stock.Desktop;
internal static class Program
{
    [STAThread]public static int Main(string[] args)
    {try{return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);}catch(Exception error) when(error is not OutOfMemoryException){var path=StartupDiagnostics.Record(error);Console.Error.WriteLine("STOCK 시작 실패 · "+error.GetType().Name+" · 진단: "+(path??"기록 실패"));return 1;}}
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>()
        .UsePlatformDetect()
        // The native window can retain a loading frame while its visual tree already contains
        // completed results. Present Win32 frames on the UI dispatcher to avoid that stalled
        // render-thread path. Keep Avalonia's GPU/software defaults and Mac options unchanged.
        .With(new Win32PlatformOptions{CompositionMode=[Win32CompositionMode.RedirectionSurface],ShouldRenderOnUIThread=true})
        .LogToTrace();
}
