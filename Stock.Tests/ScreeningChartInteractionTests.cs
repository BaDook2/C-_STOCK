using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static ScreeningChartSnapshot ReviewFixture()
    {
        var start=new DateTimeOffset(2025,1,1,9,0,0,TimeSpan.FromHours(9));
        var hours=Enumerable.Range(0,12).SelectMany(day=>Enumerable.Range(0,7).Select(h=>new PriceBar(start.AddDays(day).AddHours(h),100+h,106+h,96+h,102+h,1000,true))).ToArray();
        var marker=new TradeMarker("review",DailyStock.Key,hours[30].Time.AddMinutes(10),true,10,104,"검증 모형체결",TradeNumber:1);
        var run=OversoldScreeningRun.Start([DailyStock],DailyConfig(),new(1,BarUnit.Hour),start.AddDays(12),new(2025,1,1),new(2025,1,12),"차트 검증") with{Status="실시간 감시 스냅샷"};
        return new(DailyStock,[new("1시","1시",hours,"검증 시간봉")],[marker],[new(marker.Time,true,10,104,"Entry1",marker.Source)],[],"실제 시세나 주문을 사용하지 않는 UI 검증 자료",null,run);
    }

    private static void ScreeningChartInteractionTests(string output)
    {
        var snapshot=ReviewFixture();var origin=snapshot.Initial.Bars[0].Time;var now=origin.AddHours(1);int minutesRead=0,dailyRead=0;
        ScreeningChartQuote? quote=new(snapshot.Stock.Key,74000,2.345m,now,"실시간 수신",true);
        async Task<ScreeningChartSeries> Load(ScreeningChartSnapshot captured,bool minute,CancellationToken token)
        {
            if(minute)minutesRead++;else dailyRead++;await Task.Delay(20,token);
            var rows=minute?Enumerable.Range(0,390).Select(i=>new PriceBar(origin.AddMinutes(i),100,105,95,101,10,true)).ToArray():Enumerable.Range(0,12).Select(i=>new PriceBar(origin.AddDays(i),100,105,95,101,1000,true)).ToArray();
            return new(minute?"1분":"일봉",minute?"1분":"일봉",rows,"검증 원본");
        }
        using var vm=new ScreeningChartViewModel(snapshot,Load,()=>true,_=>quote,clock:()=>now);
        var dialog=new ScreeningChartDialog(snapshot,viewModel:vm){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();dialog.UpdateLayout();
        try
        {
            Check(dialog.AnalysisPeriodLabel.Text.Contains("1시")&&!Descendants(dialog).OfType<ComboBox>().Any(c=>c.Items.Cast<object>().Any(item=>item is ScreeningChartSeries)),"screening analysis period is read-only and the old series dropdown is removed");
            var chart=dialog.ReviewChart;Check(chart.ScreeningOverviewLocked&&chart.Bars?.Count==84,"screening popup initially displays the complete original period");
            chart.ToggleScreeningReviewView(chart.ActualWidth/2);var focused=chart.VisibleBarCount;
            Check(focused==25,"84 hourly bars focus to the initial 25-bar review window");
            chart.ZoomAt(chart.ActualWidth/2,120);Check(chart.VisibleBarCount<focused,"short hourly review can wheel-zoom beyond 25 bars after right click");
            for(var i=0;i<20;i++)chart.ZoomAt(chart.ActualWidth/2,120);Check(chart.VisibleBarCount==5,"focused review supports a five-candle close-up");
            for(var i=0;i<30;i++)chart.ZoomAt(chart.ActualWidth/2,-120);Check(chart.VisibleBarCount==focused&&chart.ScreeningMaximumVisibleBars==focused,"wheel zoom-out cannot exceed the first right-click focus range");
            chart.ToggleScreeningReviewView(chart.ActualWidth/2);chart.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.PreviewMouseWheelEvent});Check(chart.ScreeningOverviewLocked&&chart.VisibleBarCount==84,"overview handles wheel input without changing the locked range");
            vm.BarUnit="분";vm.BarValue="7";PumpUntil(()=>!vm.Busy);dialog.UpdateLayout();
            Check(minutesRead==1&&vm.DisplayPeriod=="7분"&&chart.Bars?.Count==56&&chart.ScreeningOverviewLocked,"numeric and unit selectors coalesce and render exact seven-minute OHLCV bars");
            vm.BarValue="15";PumpUntil(()=>!vm.Busy);Check(minutesRead==1&&vm.DisplaySeries.Bars.Length==26,"another minute interval reuses the same minute basis without another API request");
            vm.BarUnit="시";PumpUntil(()=>!vm.Busy);Check(vm.BarValues.Length==12&&vm.DisplayPeriod=="12시","hour selector clamps the numeric range to one through twelve");
            vm.WeeklySelected=true;PumpUntil(()=>!vm.Busy);vm.MonthlySelected=true;PumpUntil(()=>!vm.Busy);
            Check(dailyRead==1&&vm.MonthlySelected&&vm.DisplaySeries.Bars.Length==1&&vm.AnalysisPeriod=="1시"&&ReferenceEquals(chart.TradeMarkers,snapshot.Markers),"day/week/month share daily history while analysis snapshot and execution markers stay immutable");
            vm.CustomSelected=true;PumpUntil(()=>!vm.Busy);Check(vm.DisplayPeriod=="12시"&&minutesRead==1,"custom-bar tab reactivates the existing numeric selection after viewing daily bars");
            quote=quote with{Price=73000,ChangePercent=-1.2m,Time=now.AddSeconds(1)};
            PumpUntil(()=>vm.PriceText.Contains("73,000"),3);
            Check(vm.ChangeText=="전일 대비 -1.20%"&&vm.PriceColor=="#3182F6","popup timer displays live price and two-decimal previous-close return");
            now=now.AddSeconds(30);vm.RefreshQuote();Check(vm.QuoteStatus.Contains("지연"),"stale quotes keep their original time and show delayed status");
            quote=quote with{Running=false};vm.RefreshQuote();Check(vm.QuoteStatus.Contains("감시 중지")&&vm.PriceText.Contains("73,000"),"stopping screening retains the explicitly labeled last observed price");
            Render((FrameworkElement)dialog.Content,Path.Combine(output,"screening-chart-controls.png"));
        }
        finally{dialog.Close();}

        var stale=new TaskCompletionSource<ScreeningChartSeries>(TaskCreationOptions.RunContinuationsAsynchronously);int loadCount=0;
        using(var race=new ScreeningChartViewModel(snapshot,async(_,minute,token)=>{loadCount++;if(minute)return await stale.Task;return await Load(snapshot,false,token);},()=>true))
        {
            race.RequestPeriod("5분");PumpUntil(()=>loadCount==1);race.RequestPeriod("일봉");PumpUntil(()=>!race.Busy);stale.SetResult(new("1분","1분",snapshot.Initial.Bars,"늦은 이전 결과"));
            var delay=Task.Delay(50);PumpUntil(()=>delay.IsCompleted);Check(race.DisplayPeriod=="일봉"&&race.DisplaySeries.Source!="늦은 이전 결과","late cancelled period loads cannot replace the current symbol or period");
        }
        using(var unsupported=new ScreeningChartViewModel(snapshot))
        {unsupported.RequestPeriod("3분");PumpUntil(()=>!unsupported.Busy);Check(!unsupported.CanUseMinutes&&unsupported.DisplayPeriod=="1시"&&unsupported.Message.Contains("API"),"unavailable minute data preserves the current chart with an explicit reason");}
        var benchmark=BenchmarkReviewChart();File.WriteAllText(Path.Combine(output,"screening-chart-performance.json"),JsonSerializer.Serialize(benchmark,new JsonSerializerOptions{WriteIndented=true}));
        var hoverChart=new CandleChart{Bars=snapshot.Initial.Bars,Period="1시"};hoverChart.Measure(new Size(800,500));hoverChart.Arrange(new Rect(0,0,800,500));
        hoverChart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseMoveEvent});var previousHelp=hoverChart.ToolTip?.ToString();
        var index=hoverChart.HoverBarIndex(Mouse.GetPosition(hoverChart).X);var changed=snapshot.Initial.Bars.ToArray();changed[index]=changed[index] with{Close=110,High=111};hoverChart.Bars=changed;
        hoverChart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseMoveEvent});
        Check(hoverChart.ToolTip?.ToString()!=previousHelp&&hoverChart.ToolTip?.ToString()?.Contains("110")==true,"live candle replacement invalidates the same-candle tooltip cache");
    }

    private static object BenchmarkReviewChart()
    {
        var start=new DateTimeOffset(2025,1,1,9,0,0,TimeSpan.FromHours(9));
        var bars=Enumerable.Range(0,20000).Select(i=>new PriceBar(start.AddMinutes(i),100+i%50,106+i%50,96+i%50,102+i%50,1000,true)).ToArray();
        var markers=Enumerable.Range(0,160).Select(i=>new TradeMarker("perf"+i,DailyStock.Key,bars[100+i*120].Time,i%2==0,1,bars[100+i*120].Close,"성능 검증")).ToArray();
        var chart=new CandleChart{Bars=bars,TradeMarkers=markers,Period="1분",MovingAverages=true,Ichimoku=false};chart.Measure(new Size(1100,620));chart.Arrange(new Rect(0,0,1100,620));chart.EnableScreeningReviewMode();chart.ToggleScreeningReviewView(550);
        void Frame(){Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);chart.UpdateLayout();var image=new RenderTargetBitmap(1100,620,96,96,PixelFormats.Pbgra32);image.Render(chart);}
        Frame();var indicatorBuilds=chart.IndicatorCacheBuildCount;var mappingBuilds=chart.MarkerMapBuildCount;var samples=new List<double>();
        for(var i=0;i<30;i++){var watch=Stopwatch.StartNew();chart.ZoomAt(400,i%2==0?120:-120);chart.PanFromGesture(i*3);Frame();watch.Stop();samples.Add(watch.Elapsed.TotalMilliseconds);}
        samples.Sort();Check(chart.IndicatorCacheBuildCount==indicatorBuilds&&chart.MarkerMapBuildCount==mappingBuilds,"30 pan/zoom frames reuse price indicators and binary-search trade mappings");
        Check(chart.RenderFrameCount>=30,"performance samples include actual chart redraws after each viewport change");
        Check(chart.VisibleBarCount<=chart.ScreeningMaximumVisibleBars&&chart.Bars.Count==20000,"interactive rendering limits the viewport without dropping source bars");
        return new{Bars=20000,Markers=160,Frames=samples.Count,P50Ms=samples[samples.Count/2],P95Ms=samples[(int)(samples.Count*.95)],MaxMs=samples[^1],IndicatorBuilds=indicatorBuilds,MarkerMapBuilds=mappingBuilds};
    }

    private static void ScreeningChartDemo(string output)
    {
        var snapshot=ReviewFixture();var now=DateTimeOffset.UtcNow;decimal price=74000;
        var vm=new ScreeningChartViewModel(snapshot,async(_,minute,token)=>{await Task.Delay(50,token);var source=snapshot.Initial;return minute?new("1분","1분",Enumerable.Range(0,390).Select(i=>new PriceBar(source.Bars[0].Time.AddMinutes(i),100,106,96,102,10,true)).ToArray(),"UI 검증 1분봉"):new("일봉","일봉",source.Bars.Where((_,i)=>i%7==0).ToArray(),"UI 검증 일봉");},()=>true,_=>new(snapshot.Stock.Key,price,2.35m,now,"검증 시세 · 실제 주문 없음",true));
        var dialog=new ScreeningChartDialog(snapshot,viewModel:vm);var telemetry=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
        telemetry.Tick+=(_,_)=>{now=DateTimeOffset.UtcNow;price=74000+(now.Second%3)*100;File.WriteAllText(Path.Combine(output,"demo-state.json"),JsonSerializer.Serialize(new{vm.DisplayPeriod,vm.PriceText,vm.ChangeText,Locked=dialog.ReviewChart.ScreeningOverviewLocked,Visible=dialog.ReviewChart.VisibleBarCount,Maximum=dialog.ReviewChart.ScreeningMaximumVisibleBars,Offset=dialog.ReviewChart.ViewOffset,Focused=dialog.ReviewChart.IsKeyboardFocused}));};
        dialog.Closed+=(_,_)=>telemetry.Stop();telemetry.Start();dialog.ShowDialog();
    }
}
