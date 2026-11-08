using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using ScottPlot.Avalonia;
using Stock.Core.Research;
using Stock.Desktop;

internal static partial class Program
{
    private static void ChartInteractionChecks()
    {
        var view=new CandleViewport();view.Reset(120,false);view.Zoom(.25,1);
        var backed=new CandleViewport();backed.Reset(1500,false,60);backed.Pan(0,100,800);Check(backed.Visible==60&&backed.Offset>0,"initial60-bar period retains1500 backing bars and pans immediately without wheel zoom");
        Check(view.Visible==105&&view.Offset==11,"WPF parity: one wheel event changes 120 to105 candles and preserves the quarter-position anchor");
        for(var i=0;i<50;i++)view.Zoom(.3,1);Check(view.Visible==25,"normal wheel zoom stops at the original25-candle minimum");
        for(var i=0;i<200;i++)view.Zoom(.7,-1);Check(view.Visible==120&&view.Offset==0,"zoom out stops at available bars without empty future space");
        view.Reset(3000,false);Check(view.Visible==2000,"normal full view respects the WPF2000-candle display maximum without reducing source data");
        view.Reset(400,true);Check(!view.Enabled,"screening400-bar overview is locked even though it is below the500-bar ceiling");view.Toggle(.5);Check(view.Visible==100&&view.Focused,"screening right click shows one quarter of the period like WPF");view.Zoom(.5,-1);Check(view.Visible==100,"screening wheel cannot zoom out past its initial focus range");view.Toggle(.5);Check(view.Visible==400&&!view.Enabled,"second screening right click restores the entire locked range");view.UpdateCount(410);Check(view.Visible==410&&view.Start==0,"new data keeps a locked screening overview on its entire period");
        view.Reset(20,true);Check(view.Enabled,"small overview that cannot be reduced is immediately interactive");
        view.Reset(200,false);view.Zoom(.5,1);view.Pan(0,40,1000);Check(view.Offset==7,"a40px right drag reveals seven older bars with the WPF4px dead zone");view.Pan(0,2,1000);Check(view.Offset==0,"dragging back into the initial dead zone restores the original viewport");view.Pan(0,40,1000);view.Pan(view.Offset,-10000,1000);Check(view.Offset==0,"left drag clamps at the latest candle");view.Pan(0,10000,1000);Check(view.Offset==15,"right drag clamps at the earliest complete viewport");
        var start=new DateTimeOffset(2026,10,1,15,30,0,TimeSpan.FromHours(9));
        var bars=Enumerable.Range(0,120).Select(i=>new PriceBar(start.AddDays(i+i/5*2),100+i,110+i,90+i,101+i,1000+i)).ToArray();
        var chart=new StockChart{Bars=bars,Period="일봉"};var window=new Window{Width=1100,Height=700,Content=chart};window.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var plot=chart.FindControl<AvaPlot>("PricePlot")!;var pixel=plot.Plot.GetPixel(new ScottPlot.Coordinates(30,145));var point=plot.TranslatePoint(new(pixel.X,pixel.Y),window)!.Value;window.MouseMove(point);Dispatcher.UIThread.RunJobs();
        Check(chart.GuideVisible&&chart.HoveredTime==bars[30].Time,"real pointer movement displays a snapped candle crosshair and its original timestamp");
        Check(ToolTip.GetTip(plot)?.ToString()?.Contains("거래량")==true,"hover restores the original OHLC and volume tooltip");
        var guide=chart.FindControl<Avalonia.Controls.Shapes.Line>("VerticalGuide")!;Check(guide.EndPoint.Y>plot.Bounds.Height,"vertical crosshair continues from price through the volume area");
        var originalY=plot.Plot.Axes.GetLimits();window.MouseWheel(point,new Vector(0,1));Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();var next=plot.Plot.Axes.GetLimits();
        Check(chart.VisibleBarCount==105&&Math.Abs((next.Right-next.Left)-105)<.001,"installed Avalonia pointer wheel uses a15-candle step rather than ScottPlot's proportional XY zoom");
        Check(next.Top!=originalY.Top&&next.Top-next.Bottom<originalY.Top-originalY.Bottom,"Y prices automatically refit the visible OHLC range after zoom");
        var financial=plot.Plot.GetPlottables<ScottPlot.Plottables.CandlestickPlot>().Single();Check(financial.Sequential&&financial.Data.GetOHLCs().Count==120,"weekends have no empty candle slots and every original OHLC remains in the series");
        window.MouseMove(new Point(1,1));Dispatcher.UIThread.RunJobs();Check(!chart.GuideVisible,"leaving the plot clears crosshair and stale hover information");
        Check(ToolTip.GetTip(plot)==null,"leaving the chart also clears the OHLC tooltip");
        foreach(var period in new[]{"1분","1시","일봉","주봉","월봉"}){chart.Period=period;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();window.MouseMove(point);Dispatcher.UIThread.RunJobs();Check(chart.GuideVisible,"shared crosshair is available for "+period);window.MouseMove(new Point(1,1));}
        chart.OverviewNavigation=true;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();window.MouseMove(point);Dispatcher.UIThread.RunJobs();Check(!chart.GuideVisible,"locked screening overview suppresses crosshair until right-click focus");
        var hourBars=new[]{new PriceBar(new DateTimeOffset(start.Date.AddHours(9),start.Offset),1,1,1,1,1),new PriceBar(new DateTimeOffset(start.Date.AddHours(10),start.Offset),1,1,1,1,1)};var lateFill=new TradeMarker("hour","KRX:005930",new DateTimeOffset(start.Date.AddHours(10).AddMinutes(40),start.Offset),true,1,1,"SYNTHETIC");
        Check(StockChart.TradeCandleIndex(hourBars,lateFill,"1시")==1,"a fill40 minutes into an hourly candle maps to that candle, not the nearest next timestamp");
        var monday=new DateTimeOffset(2026,10,5,15,30,0,start.Offset);var weekBars=new[]{new PriceBar(monday,1,1,1,1,1),new PriceBar(monday.AddDays(7),1,1,1,1,1)};
        Check(StockChart.TradeCandleIndex(weekBars,lateFill with{Time=monday.AddDays(3)},"주봉")==0,"weekly execution markers use the market week instead of jumping to the following candle");
        chart.OverviewNavigation=false;chart.DisplayBarCount=60;chart.Revision++;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();var dragPoint=plot.TranslatePoint(new Point(plot.Bounds.Width*.5,plot.Bounds.Height*.5),window)!.Value;window.MouseDown(dragPoint,MouseButton.Left);window.MouseMove(dragPoint+new Vector(100,0),RawInputModifiers.LeftMouseButton);window.MouseUp(dragPoint+new Vector(100,0),MouseButton.Left);Dispatcher.UIThread.RunJobs();var oldOffset=chart.ViewOffset;var oldCount=chart.VisibleBarCount;
        chart.DisplayBarCount=61;chart.Bars=[..bars,bars[^1] with{Time=bars[^1].Time.AddDays(1)}];Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Check(oldOffset>0&&chart.ViewOffset==oldOffset+1&&chart.VisibleBarCount==oldCount,"a live appended candle preserves the panned historical window when the period count changes");
        chart.Revision++;Dispatcher.UIThread.RunJobs();Check(chart.ViewOffset==0&&chart.VisibleBarCount==61,"an explicit period revision still resets the initial visible count after live updates");
        window.Close();
    }
}
