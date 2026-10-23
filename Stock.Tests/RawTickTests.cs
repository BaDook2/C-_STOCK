using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void RawTickChecks(string directory)
    {
        Directory.CreateDirectory(directory);var existing=Directory.GetFiles(directory,"raw-fixture-*.db").FirstOrDefault();var path=existing??Path.Combine(directory,"raw-fixture-"+Guid.NewGuid().ToString("N")+".db");using var db=new TradingStore(path);const int count=2000000;var watch=Stopwatch.StartNew();long ticks=Epoch.UtcTicks;
        IEnumerable<MarketEvent> Generate(){for(int i=0;i<count;i++){if(i>=250000)ticks+=(i%7+1)*1000;var time=new DateTimeOffset(ticks,TimeSpan.Zero);yield return new(Us,EventKind.Trade,time,time,"KIWOOM",i){TradePrice=2+i%17*.0001m,TradeSize=1,PreviousClose=2};}}
        int inserted=existing==null?0:count;if(existing==null)foreach(var batch in Generate().Chunk(10000))inserted+=db.InsertEvents(batch);var insertMs=watch.Elapsed.TotalMilliseconds;Check(inserted==count,"2M original events stored in isolated fixture database");
        var repo=new RawTickRepository(path);var extent=repo.Extent(Us.Key,"KIWOOM",default).GetAwaiter().GetResult()!;Check(extent.Count==count,"composite index extent counts all equal-time executions");
        var page=repo.Range(Us.Key,"KIWOOM",Epoch.UtcTicks,Epoch.UtcTicks,1000,default).GetAwaiter().GetResult();Check(page.More&&page.Ticks.Length==1000&&page.Ticks.Select(t=>t.Sequence).SequenceEqual(Enumerable.Range(0,1000).Select(i=>(long)i)),"same timestamp page preserves ingest sequence and exact decimal");
        long total=0,lastRow=0;var key=new TickKey(Epoch.UtcTicks,0);watch.Restart();
        while(true){var next=repo.After(Us.Key,"KIWOOM",key,50000,default).GetAwaiter().GetResult();if(next.Ticks.Length==0)break;CheckOrder(next.Ticks);total+=next.Ticks.Length;key=next.Ticks[^1].Key;lastRow=key.RowId;}
        var pagingMs=watch.Elapsed.TotalMilliseconds;Check(total==count&&lastRow==extent.Last.RowId,"keyset forward paging visits all 2M rows without omission or infinite loop");
        var backwards=repo.Before(Us.Key,"KIWOOM",new(extent.Last.UtcTicks,long.MaxValue),1000,default).GetAwaiter().GetResult();Check(backwards.Ticks[^1].Key==extent.Last&&backwards.Ticks[0].Sequence==count-1000,"backward keyset returns stable ascending page");
        var options=new TickChartLoadOptions{MaxVisibleRawTicks=1000,MaxLoadedTicks=3000};var cache=new TickChartWindow(repo,Us.Key,"KIWOOM",options);var initial=cache.Range(Epoch.UtcTicks,extent.Last.UtcTicks,default).GetAwaiter().GetResult();Check(initial.Ticks.Length<=1000&&initial.Shortened,"time window shrinks independently of database count");
        for(int i=0;i<10;i++){var rows=cache.Adjacent(new(Epoch.UtcTicks,i*1000),true,default).GetAwaiter().GetResult();Check(rows.Length<=1000&&cache.CachedTicks<=3000,"bounded cache and per-page cap after eviction");}
        var beforeQueries=cache.Queries;cache.Adjacent(new(Epoch.UtcTicks,0),true,default).GetAwaiter().GetResult();Check(cache.Queries>beforeQueries,"evicted page reloads from original database");
        var gap=cache.Range(Epoch.AddDays(3).UtcTicks,Epoch.AddDays(4).UtcTicks,default).GetAwaiter().GetResult();Check(gap.Ticks.Length>0,"empty holiday range finds nearest saved data");
        using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();Throws<OperationCanceledException>(()=>repo.After(Us.Key,"KIWOOM",key,1000,cancellation.Token).GetAwaiter().GetResult(),"cancelled range query cannot return new data");}
        var precise=Epoch.AddTicks(1234567);var fills=new[]{new BarFill(precise,"매수",10,2,.01m,0,"진입 이유"),new BarFill(precise.AddTicks(5),"매도",4,3,.01m,4,"부분 청산"),new BarFill(precise.AddSeconds(1),"매도",6,4,.01m,12,"최종 청산")};var markers=TickTradeMatcher.Match("run",fills);Check(markers.Length==4&&markers[1].Time==precise.AddTicks(5)&&markers[1].Detail.Quantity==4&&markers[3].Detail.Quantity==6,"partial fill markers preserve original 100ns timestamp and FIFO quantity");
        var settings=Settings();settings.Backtest.Instrument=Us.Key;settings.Data.MaxHistory=100;watch.Restart();var result=StreamingBacktest.Run(db,settings,db.EnumerateEvents(Us.Key,"KIWOOM",Epoch,new(extent.Last.UtcTicks,TimeSpan.Zero),default),default,recordDecisions:false);var backtestMs=watch.Elapsed.TotalMilliseconds;
        Check(result.Events==count&&result.Equity.Count==count,"backtest engine consumes all 2M raw events independently of 1000-point chart cap");Check(repo.Extent(Us.Key,"KIWOOM",default).GetAwaiter().GetResult()!.Count==count,"rendering and backtest never delete or aggregate stored ticks");
        File.WriteAllText(Path.Combine(directory,"raw-results.json"),Serialization.Pack(new{Success=true,Database=path,OriginalCount=count,ReusedFixture=existing!=null,InsertMs=insertMs,PagingMs=pagingMs,BacktestMs=backtestMs,EngineEvents=result.Events,EngineEquity=result.Equity.Count,ChartCap=options.MaxVisibleRawTicks,CachedTicks=cache.CachedTicks}));
        RawTickUi(path,directory,markers);Console.WriteLine($"PASS: {passed} Raw Tick checks");
    }
    private static void CheckOrder(RawTick[] rows){for(int i=1;i<rows.Length;i++)if(rows[i-1].Key.CompareTo(rows[i].Key)>=0)throw new Exception("unstable raw order");}
    private static void RawTickUi(string path,string directory,TickFillMarker[] markers)
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));var app=new Stock.App.App{ManualWindowStartup=true};app.InitializeComponent();using var model=new RawTickChartModel(path,Us.Key,markers,new(){MaxVisibleRawTicks=1000,MaxLoadedTicks=3000});var view=new RawTickChartView{DataContext=model};var window=new Window{Content=view,Width=1200,Height=700,ShowActivated=false,Left=-20000};window.Show();PumpUntil(()=>!model.Busy&&model.Rows.Length>0);window.UpdateLayout();
        Check(model.Rows.Length<=1000&&view.Chart.Plot.GetPlottables().Any(),"actual WPF SignalXY receives at most configured raw cap");var before=model.Rows[0].Key;var task=model.Move(false);PumpUntil(()=>task.IsCompleted);Check(model.Rows[0].Key.CompareTo(before)<0,"WPF model pans backward into older database rows");
        var retreat=model.Move(false);PumpUntil(()=>retreat.IsCompleted);var prior=model.Rows.ToArray();var advance=model.Move(true);PumpUntil(()=>advance.IsCompleted);Check(model.Rows.IntersectBy(prior.Select(r=>r.Key),r=>r.Key).Count()==Math.Min(prior.Length,900)&&model.Rows.First(r=>r.Key.CompareTo(prior[^1].Key)>0).Sequence==prior[^1].Sequence+1,"forward navigation preserves overlap and does not skip adjacent original ticks");
        var older=model.Range(Epoch.UtcTicks,Epoch.UtcTicks);var newer=model.Reset();PumpUntil(()=>older.IsCompleted&&newer.IsCompleted);Check(!model.Busy&&model.Rows.Length<=1000&&model.Rows[^1].Time>Epoch,"overlapping range requests retain newest generation only");Check(model.CachedTicks+model.Rows.Length<=model.Options.MaxLoadedTicks,"cache plus visible raw rows obey total resident budget");
        model.Select(markers[1]);var zoom=model.ZoomTrade();PumpUntil(()=>zoom.IsCompleted);Check(model.TradeSummary.Contains("부분 청산")&&model.Rows.Length>0,"marker selection and trade zoom load original surrounding range");
        view.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var origin=Epoch.UtcDateTime.Date.Ticks;var point=new ScottPlot.Coordinates((markers[1].Time.UtcTicks-origin)/(double)TimeSpan.TicksPerSecond,(double)markers[1].Price);view.SelectAtPixel(view.Chart.Plot.GetPixel(point));Check(model.SelectedTrade?.Id==markers[1].Detail.Id,"rendered marker pixel hit test selects the exact partial fill");
        model.FollowLatest=false;var state=model.Rows[^1].Key;var previousQuery=model.QueryCount;var limits=view.Chart.Plot.Axes.GetLimits();view.Chart.Plot.Axes.SetLimitsX(limits.Left,limits.Right+(limits.Right-limits.Left)*.2);view.Chart.Refresh();PumpUntil(()=>model.QueryCount>previousQuery&&!model.Busy);Check(model.Rows.Length<=1000&&!model.FollowLatest,"ScottPlot axis pan triggers bounded asynchronous navigation without live jump");
        Render((FrameworkElement)window.Content,Path.Combine(directory,"raw-tick-view.png"));window.Close();app.Shutdown();
    }
}
