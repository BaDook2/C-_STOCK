using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;
/// <summary>Opt-in executable startup verification. Synthetic provider, unique database, no account/network calls.</summary>
internal static class NativeSmoke
{
    internal sealed class Market:IPublicMarketData,ICurrentMarketData
    {
        public int Quotes;
        private static readonly StockItem Stock=new("005930","시작 검사 · 합성 삼성전자",Exchange.KRX,"005930"){Board="KOSPI"};
        public Task<IReadOnlyList<StockItem>> Search(string query,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([Stock]);
        public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([Stock]);
        public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>Task.FromResult(stock);
        public Task<BarDataset> History(StockItem stock,CancellationToken token)
        {var last=DateOnly.FromDateTime(DateTime.Today.AddDays(-1));return Task.FromResult(new BarDataset(stock,Enumerable.Range(0,600).Select(n=>new PriceBar(ResearchMarketSession.For(last.AddDays(n-599),Exchange.KRX).Close,10000+n,10020+n,9990+n,10010+n,1000+n)).ToArray(),"SYNTHETIC_STARTUP_ONLY",DateTimeOffset.UtcNow));}
        public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)
        {var n=Interlocked.Increment(ref Quotes);var now=DateTimeOffset.UtcNow;return Task.FromResult(new CurrentMarketSnapshot(stock,new(stock.Name,10610+n,10+n,(10+n)/10600m*100,now,"OPEN"),now,10600,10630,10590,50000,530000000,"SYNTHETIC_STARTUP_ONLY",TimeSpan.FromSeconds(1)));}
        public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
    }
    public static string? Output(string[]? args)
    {if(args==null)return null;var index=Array.IndexOf(args,"--startup-smoke");return index>=0&&index+1<args.Length?Path.GetFullPath(args[index+1]):null;}
    public static void Attach(IClassicDesktopStyleApplicationLifetime desktop,MainWindow window,MainViewModel model,Market market,string output)
    {
        Directory.CreateDirectory(output);window.Position=new PixelPoint(-18000,-18000);var started=DateTimeOffset.UtcNow;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        window.Opened+=(_,_)=>{model.SearchCommand.Execute(null);timer.Start();};timer.Tick+=(_,_)=>
        {
            if(DateTimeOffset.UtcNow-started<TimeSpan.FromSeconds(2))return;var ready=window.IsVisible&&!model.Busy&&model.ChartBars.Length>0&&market.Quotes>=2;
            if(!ready&&DateTimeOffset.UtcNow-started<TimeSpan.FromSeconds(20))return;timer.Stop();
            try
            {
                if(ready){using var frame=new RenderTargetBitmap(new PixelSize(1480,980));frame.Render(window);frame.Save(Path.Combine(output,"native-window.png"),PngBitmapEncoderOptions.Default);}
                File.WriteAllText(Path.Combine(output,"result.json"),Serialization.Pack(new{Success=ready,NoBrokerCalls=true,SyntheticOnly=true,Candles=model.ChartBars.Length,Quotes=market.Quotes,Visible=window.IsVisible,Runtime=System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription}));desktop.Shutdown(ready?0:1);
            }
            catch(Exception error) when(error is not OutOfMemoryException){StartupDiagnostics.Record(error);File.WriteAllText(Path.Combine(output,"result.json"),Serialization.Pack(new{Success=false,NoBrokerCalls=true,ErrorType=error.GetType().Name}));desktop.Shutdown(1);}
        };
    }
}
