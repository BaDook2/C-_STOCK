using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.Desktop;

/// <summary>Opt-in published-app verification with real public data, isolated storage and no broker access.</summary>
internal static class NativePublicProbe
{
 internal static string? Output(string[]? args)
 {if(args==null)return null;var at=Array.IndexOf(args,"--verify-public");return at>=0&&at+1<args.Length?Path.GetFullPath(args[at+1]):null;}
 internal static void Attach(IClassicDesktopStyleApplicationLifetime desktop,MainWindow window,MainViewModel model,string output)
 {
  Directory.CreateDirectory(output);var rows=new List<object>();window.Position=new PixelPoint(-18000,-18000);
  window.Opened+=async(_,_)=>
  {
   try
   {
    foreach(var (region,query,symbol) in new[]{("국내","삼성전자","005930"),("국내","SK하이닉스","000660"),("해외","애플","AAPL")})
    {
     model.Region=region;model.Query=query;var watch=Stopwatch.StartNew();model.SearchCommand.Execute(null);await Wait(()=>!model.Busy,TimeSpan.FromSeconds(60));await Task.Delay(500);
     var rendered=window.FindControl<StockChart>("MainPriceChart")?.RenderedCandles??0;var ok=model.SelectedStock?.Symbol==symbol&&model.ChartBars.Length>0&&rendered==model.ChartBars.Length;rows.Add(new{Query=query,Symbol=model.SelectedStock?.Symbol,Success=ok,Bars=model.ChartBars.Length,RenderedCandles=rendered,ElapsedMs=watch.ElapsedMilliseconds,Status=model.Status});
     using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));image.Render(window);image.Save(Path.Combine(output,symbol+".png"),PngBitmapEncoderOptions.Default);
     if(!ok)throw new InvalidOperationException("Published public chart failed: "+symbol);
    }
    File.WriteAllText(Path.Combine(output,"result.json"),Serialization.Pack(new{Success=true,RealPublicData=true,IsolatedStorage=true,NoBrokerCalls=true,OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,Runtime=System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier,Framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,Culture=System.Globalization.CultureInfo.CurrentCulture.Name,Rows=rows}));desktop.Shutdown(0);
   }
   catch(Exception error) when(error is not OutOfMemoryException)
   {File.WriteAllText(Path.Combine(output,"result.json"),Serialization.Pack(new{Success=false,RealPublicData=true,IsolatedStorage=true,NoBrokerCalls=true,Error=error.GetType().Name,Reason=error.Message,Rows=rows}));desktop.Shutdown(1);}
  };
 }
 private static async Task Wait(Func<bool> ready,TimeSpan timeout)
 {var timer=Stopwatch.StartNew();while(!ready()){if(timer.Elapsed>timeout)throw new TimeoutException("Public chart load exceeded "+timeout.TotalSeconds+" seconds");await Task.Delay(50);}}
}
