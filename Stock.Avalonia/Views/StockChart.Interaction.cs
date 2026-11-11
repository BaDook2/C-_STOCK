using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScottPlot;
using Stock.Core.Research;
using PointerCursor=Avalonia.Input.Cursor;

namespace Stock.Desktop;
public partial class StockChart
{
    private Point? dragOrigin;
    private int dragOffset;
    private (Point Point,bool Volume)? lastHover;
    private string? tooltipText;
    public event Action<DateTimeOffset,bool>? RangeRequested;
    public bool GuideVisible=>GuideLayer.IsVisible;
    public DateTimeOffset? HoveredTime {get;private set;}
    private double RenderScale=>PricePlot.Bounds.Width>0&&PricePlot.Plot.LastRender.FigureRect.Width>0?PricePlot.Plot.LastRender.FigureRect.Width/PricePlot.Bounds.Width:1;
    private double DataWidth=>Math.Max(1,PricePlot.Plot.LastRender.DataRect.Width/RenderScale);
    private double RatioAt(Point p)=>Math.Clamp((p.X*RenderScale-PricePlot.Plot.LastRender.DataRect.Left)/Math.Max(1,PricePlot.Plot.LastRender.DataRect.Width),0,1);
    private int IndexAt(Point p)=>Math.Clamp(viewport.Start+(int)(RatioAt(p)*Math.Max(1,viewport.End-viewport.Start)),viewport.Start,Math.Max(viewport.Start,viewport.End-1));
    private Coordinates CoordinatesAt(Point p)=>PricePlot.Plot.GetCoordinates(new Pixel((float)(p.X*RenderScale),(float)(p.Y*RenderScale)));
    private double TimeCoordinate(DateTimeOffset time)
    {
        if(times.Length==0)return 0;var x=X(time);var index=Array.BinarySearch(times,x);if(index>=0)return index;
        index=~index;if(index==0)return -.5;if(index>=times.Length)return times.Length-.5;
        return index-1+(x-times[index-1])/(times[index]-times[index-1]);
    }
    private void ConfigureInteractions()
    {
        foreach(var control in new[]{PricePlot,VolumePlot})
        {
            var volume=control==VolumePlot;
            control.AddHandler(PointerWheelChangedEvent,(_,e)=>{e.Handled=true;if(times.Length==0||!DetailActive)return;viewport.Zoom(RatioAt(e.GetPosition(control)),e.Delta.Y);QueueAnnotations();},RoutingStrategies.Tunnel);
            control.AddHandler(PointerPressedEvent,(_,e)=>
            {
                var point=e.GetPosition(control);var state=e.GetCurrentPoint(control).Properties;
                if(state.IsRightButtonPressed)
                {
                    e.Handled=true;if(times.Length==0)return;
                    if(OverviewNavigation){viewport.Toggle(RatioAt(point));HideGuide();ApplyInputMode();QueueAnnotations();}
                    else if(RangeRequested!=null){var time=Bars![IndexAt(point)].Time;var menu=new ContextMenu();foreach(var from in new[]{true,false}){var item=new MenuItem{Header=from?"시작 기간 설정":"종료 기간 설정"};item.Click+=(_,_)=>RangeRequested?.Invoke(time,from);menu.Items.Add(item);}control.ContextMenu=menu;menu.Open(control);}
                }
                else if(state.IsLeftButtonPressed){e.Handled=true;if(!DetailActive)return;dragOrigin=point;dragOffset=viewport.Offset;control.Cursor=new PointerCursor(StandardCursorType.SizeWestEast);e.Pointer.Capture(control);}
            },RoutingStrategies.Tunnel);
            control.AddHandler(PointerReleasedEvent,(_,e)=>
            {
                if(e.InitialPressMouseButton!=MouseButton.Left&&e.InitialPressMouseButton!=MouseButton.Right)return;e.Handled=true;
                if(e.InitialPressMouseButton==MouseButton.Left){var p=e.GetPosition(control);var click=dragOrigin is {} start&&Math.Abs(start.X-p.X)<4&&Math.Abs(start.Y-p.Y)<4;dragOrigin=null;control.Cursor=PointerCursor.Default;e.Pointer.Capture(null);if(click&&!volume&&DetailActive)SelectAt(p);ShowHover(p,volume);}
            },RoutingStrategies.Tunnel);
            control.PointerMoved+=(_,e)=>{var p=e.GetPosition(control);if(dragOrigin is {} start&&DetailActive){viewport.Pan(dragOffset,p.X-start.X,DataWidth);HideGuide();QueueAnnotations();}else ShowHover(p,volume);};
            control.PointerExited+=(_,_)=>{if(dragOrigin==null){HideGuide();ApplyInputMode();}};
            control.PointerCaptureLost+=(_,_)=>{dragOrigin=null;control.Cursor=PointerCursor.Default;};
        }
    }
    private void ApplyViewport()
    {
        if(Bars is not {Length:>0} bars||viewport.End<=viewport.Start)return;
        decimal low=decimal.MaxValue,high=decimal.MinValue,maxVolume=1;
        for(var i=viewport.Start;i<viewport.End;i++){low=Math.Min(low,bars[i].Low);high=Math.Max(high,bars[i].High);maxVolume=Math.Max(maxVolume,bars[i].Volume);}
        foreach(var point in indicatorExtents.Where(p=>p.X>=viewport.Start&&p.X<viewport.End)){low=Math.Min(low,point.Y);high=Math.Max(high,point.Y);}
        foreach(var point in (Zones??[]).Where(z=>z.CrashPoint)){low=Math.Min(low,point.Lower);high=Math.Max(high,point.Lower);}
        foreach(var point in (Markers??[]).Where(m=>m.CrashJudgmentLow.HasValue)){low=Math.Min(low,point.CrashJudgmentLow!.Value);high=Math.Max(high,point.CrashJudgmentLow.Value);}
        var pad=(high-low)*.07m;if(pad==0)pad=Math.Max(.01m,high*.01m);
        PricePlot.Plot.Axes.SetLimits(viewport.Start-.5,viewport.End-.5,(double)(low-pad),(double)(high+pad));
        VolumePlot.Plot.Axes.SetLimits(viewport.Start-.5,viewport.End-.5,0,(double)maxVolume*1.12);
        var tickStep=Math.Max(1,(viewport.End-viewport.Start)/5);
        var ticks=Enumerable.Range(viewport.Start,viewport.End-viewport.Start).Where(i=>(i-viewport.Start)%tickStep==0).ToArray();
        VolumePlot.Plot.Axes.Bottom.SetTicks(ticks.Select(i=>(double)i).ToArray(),ticks.Select(i=>bars[i].Time.ToOffset(TimeSpan.FromHours(9)).ToString(ChartPeriods.Intraday(Period)?"HH:mm:ss":"yy.MM.dd")).ToArray());
        SynchronizeAppearance();UpdateAnnotations();PricePlot.Refresh();VolumePlot.Refresh();
        if(lastHover is {} h)ShowHover(h.Point,h.Volume);
    }
    private void HideGuide(){GuideLayer.IsVisible=false;lastHover=null;HoveredTime=null;tooltipText=null;ToolTip.SetTip(PricePlot,null);ToolTip.SetTip(VolumePlot,null);}
    private void ShowHover(Point point,bool volume)
    {
        if(Bars is not {Length:>0} bars||!DetailActive||PricePlot.Plot.LastRender.DataRect.Width<=0){HideGuide();return;}
        var index=IndexAt(point);var bar=bars[index];HoveredTime=bar.Time;lastHover=(point,volume);
        HoverInfo.Text=$"{bar.Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss} (한국) · 시 {PriceLabel(bar.Open)} · 고 {PriceLabel(bar.High)} · 저 {PriceLabel(bar.Low)} · 종 {PriceLabel(bar.Close)} · 거래량 {bar.Volume:N0}";
        if(tooltipText!=HoverInfo.Text){tooltipText=HoverInfo.Text;ToolTip.SetTip(volume?VolumePlot:PricePlot,tooltipText);}
        var data=PricePlot.Plot.LastRender.DataRect;var pixel=PricePlot.Plot.GetPixel(new Coordinates(index,0));var x=pixel.X/RenderScale;
        var bottom=VolumePlot.TranslatePoint(new Point(0,VolumePlot.Bounds.Height-22),GuideLayer)?.Y??GuideLayer.Bounds.Height;
        VerticalGuide.StartPoint=new(x,data.Top/RenderScale);VerticalGuide.EndPoint=new(x,bottom);
        HorizontalGuide.IsVisible=GuidePrice.IsVisible=!volume&&point.Y>=data.Top/RenderScale&&point.Y<=data.Bottom/RenderScale;
        if(HorizontalGuide.IsVisible){HorizontalGuide.StartPoint=new(data.Left/RenderScale,point.Y);HorizontalGuide.EndPoint=new(data.Right/RenderScale,point.Y);Canvas.SetLeft(GuidePrice,data.Right/RenderScale+3);Canvas.SetTop(GuidePrice,Math.Clamp(point.Y-10,0,Math.Max(0,PricePlot.Bounds.Height-24)));GuidePriceText.Text=Currency=="USD"?CoordinatesAt(point).Y.ToString("0.########",System.Globalization.CultureInfo.InvariantCulture):Math.Floor(CoordinatesAt(point).Y).ToString("N0");}
        GuideLayer.IsVisible=true;
    }
}
