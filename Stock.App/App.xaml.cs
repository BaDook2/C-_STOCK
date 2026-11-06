using System.Threading;
using System.Windows;

namespace Stock.App;
public partial class App : Application
{
    private Mutex? instance;
    public bool ManualWindowStartup { get; set; }
    protected override async void OnStartup(StartupEventArgs e)
    {
        if (ManualWindowStartup)
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            return;
        }
        if(e.Args.Length==3&&e.Args[0]=="--storage-check")
        {
            ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try{var report=await Task.Run(()=>Stock.Core.Trading.StorageDiagnostics.Inspect(e.Args[1]));System.IO.File.WriteAllText(e.Args[2],System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));Shutdown(report.Healthy?0:2);}
            catch{Shutdown(3);}return;
        }
        bool probe=e.Args.Length==2&&e.Args[0]=="--tick-benchmark";
        bool smoke=e.Args.Length==2&&(e.Args[0]=="--smoke-test"||e.Args[0]=="--public-smoke"||probe);
        if(smoke){ManualWindowStartup=true;ShutdownMode=ShutdownMode.OnExplicitShutdown;}
        instance = new Mutex(true, "Local\\StockPaperTrading.SingleInstance"+(smoke?".Smoke":""), out bool first);
        if (!first)
        {
            // A blocking MessageBox here created a second invisible Stock.exe process when the
            // existing window was behind Explorer. Exit the duplicate immediately instead.
            instance.Dispose();
            instance=null;
            Shutdown(0);
            return;
        }
        base.OnStartup(e);
        if(probe)await TickPerformanceProbe.Run(this,e.Args[1]);
        else if(smoke)await PublishedSmoke.Run(this,e.Args[1],e.Args[0]=="--public-smoke");
        else try{new MainWindow().Show();}catch(Stock.Core.Trading.StorageException ex){MessageBox.Show(ex.Message,"저장소 오류 · 원본 보존",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown(2);}
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
