using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
namespace Stock.Desktop;
public partial class App:Application
{
    public override void Initialize()=>AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var smoke=NativeSmoke.Output(desktop.Args);var liveProbe=NativePublicProbe.Output(desktop.Args);var market=smoke==null?null:new NativeSmoke.Market();var verification=smoke??liveProbe;
            var dataDirectory=verification==null?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Stock.Avalonia"):Path.Combine(verification,"isolated-data-"+Guid.NewGuid().ToString("N"));
            AppServices? services=null;
            try{services=new AppServices(dataDirectory,market,brokerFactory:verification==null?null:(_,_)=>throw new InvalidOperationException("Verification cannot connect an account"),credentials:verification==null?null:new Stock.Core.Security.BrokerCredentialVault(new Stock.Core.Security.MemorySecretStore(),dataDirectory));Stock.Core.Strategies.StrategySampleCatalog.EnsureInstalled(services.Store);var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};desktop.MainWindow=window;if(smoke!=null)NativeSmoke.Attach(desktop,window,model,market!,smoke);else if(liveProbe!=null)NativePublicProbe.Attach(desktop,window,model,liveProbe);}
            catch(Exception error) when(error is not OutOfMemoryException)
            {
                services?.Dispose();var path=StartupDiagnostics.Record(error);desktop.MainWindow=new Avalonia.Controls.Window{Title="STOCK 시작 오류",Width=640,Height=320,Content=new Avalonia.Controls.TextBlock{Margin=new(24),TextWrapping=Avalonia.Media.TextWrapping.Wrap,Text="프로그램을 시작할 수 없습니다. 원본 WPF 자료는 변경하지 않았습니다.\n오류: "+error.GetType().Name+"\n앱 자료: "+dataDirectory+"\n진단: "+(path??"기록 실패")}};
            }
            desktop.Exit+=(_,_)=>services?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
