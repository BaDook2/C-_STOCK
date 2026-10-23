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
        bool probe=e.Args.Length==2&&e.Args[0]=="--tick-benchmark";
        bool smoke=e.Args.Length==2&&(e.Args[0]=="--smoke-test"||e.Args[0]=="--public-smoke"||probe);
        if(smoke)ShutdownMode=ShutdownMode.OnExplicitShutdown;
        instance = new Mutex(true, "Local\\StockPaperTrading.SingleInstance"+(smoke?".Smoke":""), out bool first);
        if (!first) { MessageBox.Show("STOCK 앱이 이미 실행 중입니다. 기존 창을 사용하세요."); Shutdown(); return; }
        base.OnStartup(e);
        if(probe)await TickPerformanceProbe.Run(this,e.Args[1]);
        else if(smoke)await PublishedSmoke.Run(this,e.Args[1],e.Args[0]=="--public-smoke");
        else new MainWindow().Show();
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
