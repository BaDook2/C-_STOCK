using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ScottPlot.WPF;
using Stock.Core.Trading;
namespace Stock.App;

/// <summary>Explicit test entry point only. Measures WPF PaintSurface completion, not offscreen raster export.</summary>
public static class TickPerformanceProbe
{
    public static async Task Run(Application app,string directory)
    {
        Directory.CreateDirectory(directory);var rows=new List<object>();var plot=new WpfPlot();
        var window=new Window{Title="STOCK · Raw Tick 성능 검사 (합성 자료)",Width=1200,Height=700,Content=plot,ShowActivated=false};
        window.Show();await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);
        var renderTimes=new List<double>();var renderClock=new Stopwatch();plot.Plot.RenderManager.RenderStarting+=(_,_)=>renderClock.Restart();plot.Plot.RenderManager.RenderFinished+=(_,_)=>renderTimes.Add(renderClock.Elapsed.TotalMilliseconds);var proc=Process.GetCurrentProcess();var stall=new List<double>();var clock=Stopwatch.StartNew();double last=clock.Elapsed.TotalMilliseconds;
        var timer=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(10)};timer.Tick+=(_,_)=>{double now=clock.Elapsed.TotalMilliseconds;stall.Add(Math.Max(0,now-last-10));last=now;};timer.Start();
        async Task<double> Frame(Action mutation)
        {
            var tcs=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var watch=Stopwatch.StartNew();
            EventHandler<ScottPlot.RenderDetails>? handler=null;handler=(_,_)=>{plot.Plot.RenderManager.RenderFinished=(plot.Plot.RenderManager.RenderFinished-handler)!;app.Dispatcher.BeginInvoke(()=>tcs.TrySetResult(),DispatcherPriority.Background);};
            plot.Plot.RenderManager.RenderFinished+=handler;mutation();plot.Refresh();await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30));return watch.Elapsed.TotalMilliseconds;
        }
        try
        {
            foreach(var count in new[]{100000,250000,500000,750000,1000000,1500000,2000000})
            {
                var build=Stopwatch.StartNew();var data=await Task.Run(()=>
                {
                    var xs=new double[count];var ys=new double[count];long time=0;var start=new DateTimeOffset(2026,9,29,9,0,0,TimeSpan.FromHours(9));var instrument=new Instrument("PERF",Exchange.NASDAQ);
                    for(int i=0;i<count;i++){time+=i%29==0?0:(i%7+1)*1000;var e=new MarketEvent(instrument,EventKind.Trade,start.AddTicks(time),start.AddTicks(time),"PERFORMANCE_FIXTURE",i){TradePrice=100+(decimal)Math.Sin(i*.003)*2+i*.000001m,TradeSize=1};xs[i]=(e.ExchangeTime.UtcTicks-start.UtcTicks)/(double)TimeSpan.TicksPerSecond;ys[i]=(double)e.TradePrice!.Value;}return(xs,ys);
                });build.Stop();
                GC.Collect();GC.WaitForPendingFinalizers();int[] before=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();var cpu=proc.TotalProcessorTime;var elapsed=Stopwatch.StartNew();stall.Clear();renderTimes.Clear();last=clock.Elapsed.TotalMilliseconds;
                plot.Plot.Clear();var create=Stopwatch.StartNew();plot.Plot.Add.SignalXY(data.xs,data.ys);create.Stop();
                var first=await Frame(()=>plot.Plot.Axes.SetLimits(0,Math.Max(.001,data.xs[^1]),95,110));var pan=new List<double>();var zoom=new List<double>();
                for(int i=0;i<24;i++){var width=data.xs[^1]*(.25+(i%4)*.15);var left=data.xs[^1]*.2*Math.Sin(i*.2);pan.Add(await Frame(()=>plot.Plot.Axes.SetLimitsX(left,left+width)));zoom.Add(await Frame(()=>plot.Plot.Axes.SetLimitsX(left+width*.1,left+width*.9)));await Task.Delay(8);}
                proc.Refresh();var row=new {RenderExecution=Stats(renderTimes),Count=count,FixtureConversionMs=build.Elapsed.TotalMilliseconds,SeriesCreationMs=create.Elapsed.TotalMilliseconds,FirstRenderMs=first,Pan=Stats(pan),Zoom=Stats(zoom),DispatcherStall=Stats(stall),CpuMs=(proc.TotalProcessorTime-cpu).TotalMilliseconds,WallMs=elapsed.Elapsed.TotalMilliseconds,ManagedBytes=GC.GetTotalMemory(false),PrivateBytes=proc.PrivateMemorySize64,PeakWorkingSet=proc.PeakWorkingSet64,GC=Enumerable.Range(0,3).Select(g=>System.GC.CollectionCount(g)-before[g]).ToArray(),RenderCount=plot.Plot.RenderManager.RenderCount};rows.Add(row);
                File.WriteAllText(Path.Combine(directory,"measurements.json"),Serialization.Pack(new{Machine=Environment.MachineName,OS=Environment.OSVersion.ToString(),Processors=Environment.ProcessorCount,Framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,Package="ScottPlot.WPF 5.1.59",Size="1200x700 DIP",Screen=$"{SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}",Method="WPF Window.Show requested with ShowActivated=false; process launched hidden; native compositor visibility not independently verified. Real SKElement PaintSurface / RenderFinished followed by Dispatcher.Background. 24 automated axis pan + 24 zoom per scale; no physical mouse latency or subjective smoothness measurement. Not SavePng.",Rows=rows}));
            }
            timer.Stop();window.Close();app.Shutdown(0);
        }
        catch(Exception ex){timer.Stop();File.WriteAllText(Path.Combine(directory,"error.txt"),ex.ToString());window.Close();app.Shutdown(1);}
    }
    private static object Stats(List<double> values){var s=values.Order().ToArray();return new{N=s.Length,P50=s.Length>0?s[(int)((s.Length-1)*.5)]:0,P95=s.Length>0?s[(int)((s.Length-1)*.95)]:0,Max=s.LastOrDefault()};}
}
