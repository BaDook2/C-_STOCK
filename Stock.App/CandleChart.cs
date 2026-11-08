using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.Core.Research;
namespace Stock.App;
public sealed class CandleChart : FrameworkElement
{
    public static readonly DependencyProperty EquityCurveProperty=DependencyProperty.Register(nameof(EquityCurve),typeof(IReadOnlyList<RecurringEquity>),typeof(CandleChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<RecurringEquity>? EquityCurve {get=>(IReadOnlyList<RecurringEquity>?)GetValue(EquityCurveProperty);set=>SetValue(EquityCurveProperty,value);}
    public static readonly DependencyProperty DisplayBarCountProperty=DependencyProperty.Register(nameof(DisplayBarCount),typeof(int),typeof(CandleChart),new FrameworkPropertyMetadata(100,FrameworkPropertyMetadataOptions.AffectsRender,(d,e)=>{var chart=(CandleChart)d;chart.visible=Math.Clamp((int)e.NewValue,25,2000);chart.offset=0;}));
    public int DisplayBarCount {get=>(int)GetValue(DisplayBarCountProperty);set=>SetValue(DisplayBarCountProperty,value);}
    public static readonly DependencyProperty EmptyMessageProperty=DependencyProperty.Register(nameof(EmptyMessage),typeof(string),typeof(CandleChart),new FrameworkPropertyMetadata("데이터가 없습니다.",FrameworkPropertyMetadataOptions.AffectsRender));
    public string EmptyMessage {get=>(string)GetValue(EmptyMessageProperty);set=>SetValue(EmptyMessageProperty,value);}
    public static readonly DependencyProperty TradeMarkersProperty=DependencyProperty.Register(nameof(TradeMarkers),typeof(IReadOnlyList<TradeMarker>),typeof(CandleChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<TradeMarker>? TradeMarkers {get=>(IReadOnlyList<TradeMarker>?)GetValue(TradeMarkersProperty);set=>SetValue(TradeMarkersProperty,value);}
    public static readonly DependencyProperty SupportZonesProperty=DependencyProperty.Register(nameof(SupportZones),typeof(IReadOnlyList<ScreeningSupportZone>),typeof(CandleChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<ScreeningSupportZone>? SupportZones {get=>(IReadOnlyList<ScreeningSupportZone>?)GetValue(SupportZonesProperty);set=>SetValue(SupportZonesProperty,value);}
    public event Action<TradeMarker>? TradeMarkerClicked;
    public static readonly DependencyProperty PeriodProperty=DependencyProperty.Register(nameof(Period),typeof(string),typeof(CandleChart),new FrameworkPropertyMetadata("일봉",FrameworkPropertyMetadataOptions.AffectsRender,(d,_)=>((CandleChart)d).tooltipIndex=-1));
    public string Period {get=>(string)GetValue(PeriodProperty);set=>SetValue(PeriodProperty,value);}
    public event Action<DateTimeOffset,bool>? RangeRequested;
    private DateTimeOffset? contextDate;
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if(screeningReviewMode)ToggleScreeningReviewView(e.GetPosition(this).X);
        else OpenRangeMenu(e.GetPosition(this));
        e.Handled=true;
    }
    public void OpenRangeMenu(Point point)
    {
        var b=Bars;if(b==null||b.Count==0)return;
        int end=Math.Max(1,b.Count-Math.Min(offset,Math.Max(0,b.Count-2))),start=Math.Max(0,end-visible);
        double step=Math.Max(50,ActualWidth-88)/(end-start);
        int index=start+(int)((point.X-6)/step);
        if(index<start||index>=end)return;
        contextDate=b[index].Time;
        var menu=new ContextMenu();
        foreach(var startDate in new[]{true,false}){var item=new MenuItem{Header=startDate?"시작 기간 설정":"종료 기간 설정"};item.Click+=(_,_)=>{if(contextDate is {} date)RangeRequested?.Invoke(date,startDate);};menu.Items.Add(item);}
        ContextMenu=menu;menu.PlacementTarget=this;menu.IsOpen=true;
    }
    public static readonly DependencyProperty BarsProperty=DependencyProperty.Register(nameof(Bars),typeof(IReadOnlyList<PriceBar>),typeof(CandleChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender,(d,e)=>{
        var chart=(CandleChart)d;
        chart.indicatorCache=null;
        chart.tooltipIndex=-1;
        if(chart.offset>0&&e.OldValue is IReadOnlyList<PriceBar> old&&e.NewValue is IReadOnlyList<PriceBar> next&&old.Count>0&&next.Count>old.Count&&next[0].Time==old[0].Time)chart.offset+=next.Count-old.Count;
    }));
    public static readonly DependencyProperty MovingAveragesProperty=DependencyProperty.Register(nameof(MovingAverages),typeof(bool),typeof(CandleChart),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IchimokuProperty=DependencyProperty.Register(nameof(Ichimoku),typeof(bool),typeof(CandleChart),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FastPeriodProperty=DependencyProperty.Register(nameof(FastPeriod),typeof(int),typeof(CandleChart),new FrameworkPropertyMetadata(5,FrameworkPropertyMetadataOptions.AffectsRender,(d,e)=>((CandleChart)d).indicatorCache=null));
    public static readonly DependencyProperty SlowPeriodProperty=DependencyProperty.Register(nameof(SlowPeriod),typeof(int),typeof(CandleChart),new FrameworkPropertyMetadata(20,FrameworkPropertyMetadataOptions.AffectsRender,(d,e)=>((CandleChart)d).indicatorCache=null));
    public int FastPeriod {get=>(int)GetValue(FastPeriodProperty);set=>SetValue(FastPeriodProperty,value);}
    public int SlowPeriod {get=>(int)GetValue(SlowPeriodProperty);set=>SetValue(SlowPeriodProperty,value);}
    public IReadOnlyList<PriceBar>? Bars {get=>(IReadOnlyList<PriceBar>?)GetValue(BarsProperty);set=>SetValue(BarsProperty,value);}
    public bool MovingAverages {get=>(bool)GetValue(MovingAveragesProperty);set=>SetValue(MovingAveragesProperty,value);}
    public bool Ichimoku {get=>(bool)GetValue(IchimokuProperty);set=>SetValue(IchimokuProperty,value);}
    private int visible=100,offset;private Point? hover,drag;private int dragOffset;private bool interactiveRenderQueued;private IndicatorCache? indicatorCache;private int indicatorCacheBuildCount;private int tooltipIndex=-1;
    private IReadOnlyList<PriceBar>? mappedBars;private IReadOnlyList<TradeMarker>? mappedMarkers;private IReadOnlyList<ScreeningSupportZone>? mappedZones;private string? mappedPeriod;
    private LocatedTrade[] locatedTrades=[];private LocatedZone[] locatedZones=[];private int markerMapBuildCount;
    private readonly List<(Rect Bounds,TradeMarker Marker)> markerHitRegions=[];
    private IReadOnlyList<PriceBar>? hitBars;private IReadOnlyList<TradeMarker>? hitMarkers;private string? hitPeriod;private int hitVisible,hitOffset;private Size hitSize;
    /// <summary>Actual label bounds from the last rendered viewport, including aggregated trade badges.</summary>
    public IReadOnlyList<(Rect Bounds,TradeMarker Marker)> RenderedTradeMarkers=>markerHitRegions.AsReadOnly();
    private bool screeningReviewMode,screeningReviewFocused;private int screeningFocusLimit=500,screeningMaximumVisible=500;
    public int ViewOffset=>offset;
    public int VisibleBarCount=>visible;
    public int IndicatorCacheBuildCount=>indicatorCacheBuildCount;
    public int MarkerMapBuildCount=>markerMapBuildCount;
    public int RenderFrameCount {get;private set;}
    public bool ScreeningOverviewLocked=>screeningReviewMode&&!screeningReviewFocused;
    public bool ScreeningInteractionEnabled=>!screeningReviewMode||screeningReviewFocused;
    public int ScreeningMaximumVisibleBars=>screeningMaximumVisible;
    /// <summary>Diagnostic layout value: the latest real candle must be within one slot of the plot edge.</summary>
    public double RightGapSlots
    {
        get
        {
            var b=Bars;if(b==null||b.Count==0)return double.NaN;int end=Math.Max(1,b.Count-Math.Min(offset,Math.Max(0,b.Count-2))),start=Math.Max(0,end-visible);var count=end-start;var width=Math.Max(50,ActualWidth-88);var step=width/count;var latest=(end-1-start+.5)*step+6;return (width+6-latest)/step;
        }
    }
    private static SolidColorBrush FrozenBrush(Color color){var brush=new SolidColorBrush(color);brush.Freeze();return brush;}
    private static Pen FrozenPen(Brush brush,double thickness){var pen=new Pen(brush,thickness);pen.Freeze();return pen;}
    private static readonly SolidColorBrush Red=FrozenBrush(Color.FromRgb(240,68,82)),Blue=FrozenBrush(Color.FromRgb(49,130,246)),Muted=FrozenBrush(Color.FromRgb(139,149,161));
    private static readonly SolidColorBrush RedVolume=FrozenBrush(Color.FromArgb(65,240,68,82)),BlueVolume=FrozenBrush(Color.FromArgb(65,49,130,246)),Grid=FrozenBrush(Color.FromRgb(241,243,245));
    private static readonly SolidColorBrush CloudRed=FrozenBrush(Color.FromArgb(28,240,68,82)),CloudBlue=FrozenBrush(Color.FromArgb(28,49,130,246)),CloudRedLine=FrozenBrush(Color.FromArgb(130,240,68,82)),CloudBlueLine=FrozenBrush(Color.FromArgb(130,49,130,246)),Conversion=FrozenBrush(Color.FromRgb(231,130,36)),BaseLine=FrozenBrush(Color.FromRgb(104,86,206)),Lagging=FrozenBrush(Color.FromRgb(24,159,136));
    private static readonly SolidColorBrush MaFast=FrozenBrush(Color.FromRgb(255,161,50)),MaSlow=FrozenBrush(Color.FromRgb(142,94,239)),Ma60=FrozenBrush(Color.FromRgb(34,166,140)),Ma120=FrozenBrush(Color.FromRgb(228,93,131));
    private static readonly Pen RedPen=FrozenPen(Red,1),BluePen=FrozenPen(Blue,1),GridPen=FrozenPen(Grid,1);
    public CandleChart(){ClipToBounds=true;Focusable=true;MinHeight=180;MouseEnter+=(_,_)=>{if(screeningReviewMode&&ScreeningInteractionEnabled)Focus();};MouseMove+=(_,e)=>{if(!ScreeningInteractionEnabled){hover=null;ToolTip=null;Cursor=Cursors.Arrow;return;}hover=e.GetPosition(this);if(drag is {} p){PanFromGesture(hover.Value.X-p.X);ToolTip=null;Cursor=Cursors.SizeWE;return;}var i=HoverBarIndex(hover.Value.X);if(i!=tooltipIndex){tooltipIndex=i;ToolTip=i>=0&&Bars!=null?BarHelp(Bars[i]):null;}QueueInteractiveRender();};MouseLeave+=(_,_)=>{hover=null;tooltipIndex=-1;ToolTip=null;if(drag==null)Cursor=Cursors.Arrow;QueueInteractiveRender();};PreviewMouseWheel+=(_,e)=>{if(ScreeningInteractionEnabled)ZoomAt(e.GetPosition(this).X,e.Delta);e.Handled=true;};MouseLeftButtonDown+=(_,e)=>{Focus();drag=e.GetPosition(this);dragOffset=offset;Cursor=ScreeningInteractionEnabled?Cursors.SizeWE:Cursors.Arrow;CaptureMouse();e.Handled=true;};MouseLeftButtonUp+=(_,e)=>{var point=e.GetPosition(this);var wasClick=drag is {} start&&Math.Abs(start.X-point.X)<4&&Math.Abs(start.Y-point.Y)<4;drag=null;hover=point;Cursor=Cursors.Arrow;if(IsMouseCaptured)ReleaseMouseCapture();QueueInteractiveRender();if(wasClick)SelectMarkerAtPoint(point);e.Handled=true;};}
    /// <summary>Fits the complete interval; only a genuinely smaller focus window requires a right click before navigation.</summary>
    public void EnableScreeningReviewMode(int maximumFocusedBars=500)
    {
        screeningReviewMode=true;screeningFocusLimit=Math.Clamp(maximumFocusedBars,25,2000);ShowScreeningOverview();
    }
    public void DisableScreeningReviewMode(){screeningReviewMode=false;screeningReviewFocused=false;screeningMaximumVisible=Math.Max(25,Math.Min(2000,Bars?.Count??100));InvalidateVisual();}
    public void ToggleScreeningReviewView(double pointerX)
    {
        if(!screeningReviewMode)return;
        if(screeningReviewFocused){ShowScreeningOverview();return;}
        var total=Bars?.Count??0;if(total==0)return;var index=HoverBarIndex(pointerX);var next=ScreeningFocusCount(total,screeningFocusLimit);var start=Math.Clamp(index-next/2,0,Math.Max(0,total-next));
        visible=next;offset=Math.Clamp(total-(start+next),0,Math.Max(0,total-next));screeningMaximumVisible=next;screeningReviewFocused=true;hover=null;tooltipIndex=-1;ToolTip=null;Cursor=Cursors.Arrow;Focus();InvalidateVisual();
    }
    public static int ScreeningFocusCount(int total,int maximumFocusedBars=500)
    {
        if(total<=0)return 0;maximumFocusedBars=Math.Max(1,maximumFocusedBars);if(total<=25)return total;
        return Math.Min(total,Math.Min(maximumFocusedBars,Math.Max(25,(int)Math.Ceiling(total/4d))));
    }
    private void ShowScreeningOverview()
    {
        var total=Bars?.Count??0;visible=Math.Max(1,total);offset=0;screeningMaximumVisible=ScreeningFocusCount(total,screeningFocusLimit);screeningReviewFocused=total>0&&screeningMaximumVisible==total;drag=null;hover=null;tooltipIndex=-1;ToolTip=null;Cursor=Cursors.Arrow;if(IsMouseCaptured)ReleaseMouseCapture();InvalidateVisual();
    }
    private void QueueInteractiveRender()
    {
        if(interactiveRenderQueued)return;interactiveRenderQueued=true;
        _=Dispatcher.BeginInvoke(DispatcherPriority.Render,new Action(()=>{interactiveRenderQueued=false;InvalidateVisual();}));
    }
    /// <summary>Keeps the candle below the pointer stationary while the visible candle count changes.</summary>
    public void ZoomAt(double pointerX,int wheelDelta)
    {
        var total=Bars?.Count??0;if(total==0||wheelDelta==0||!ScreeningInteractionEnabled)return;
        var width=Math.Max(50,ActualWidth-88);var ratio=Math.Clamp((pointerX-6)/width,0,1);var minimum=Math.Min(screeningReviewMode&&screeningReviewFocused?5:25,total);var maximum=screeningReviewMode&&screeningReviewFocused?Math.Max(minimum,Math.Min(total,screeningMaximumVisible)):Math.Min(2000,Math.Max(25,total));var next=Math.Clamp(visible+(wheelDelta>0?-15:15),minimum,maximum);
        if(next==visible)return;offset=CalculateZoomOffset(total,visible,offset,next,ratio);visible=next;tooltipIndex=-1;QueueInteractiveRender();
    }
    public static int CalculateZoomOffset(int total,int oldVisible,int oldOffset,int newVisible,double pointerRatio)
    {
        if(total<=0)return 0;pointerRatio=Math.Clamp(pointerRatio,0,1);oldVisible=Math.Max(1,oldVisible);newVisible=Math.Max(1,newVisible);
        var oldEnd=Math.Max(1,total-Math.Min(oldOffset,Math.Max(0,total-2)));var oldStart=Math.Max(0,oldEnd-oldVisible);var oldCount=oldEnd-oldStart;
        var shown=Math.Min(newVisible,total);var anchor=oldStart+pointerRatio*Math.Max(0,oldCount-1);var start=(int)Math.Round(anchor-pointerRatio*Math.Max(0,shown-1));start=Math.Clamp(start,0,Math.Max(0,total-shown));
        return Math.Clamp(total-(start+shown),0,Math.Max(0,total-shown));
    }
    /// <summary>Direct manipulation: drag right reveals older bars, drag left reveals newer bars.</summary>
    public void PanFromGesture(double pointerDeltaX)
    {
        if(!ScreeningInteractionEnabled)return;
        var max=Math.Max(0,(Bars?.Count??0)-visible);
        var candleWidth=Math.Max(.25,(ActualWidth-90)/Math.Max(1,visible));
        offset=CalculatePanOffset(dragOffset,pointerDeltaX,candleWidth,max);
        QueueInteractiveRender();
    }
    public static int CalculatePanOffset(int startOffset,double pointerDeltaX,double candleWidth,int maxOffset)
    {
        const double clickDeadZone=4;
        if(Math.Abs(pointerDeltaX)<=clickDeadZone)return Math.Clamp(startOffset,0,maxOffset);
        // A wide candle must not require a full candle-width swipe before the first move.
        var gestureUnit=Math.Min(12,Math.Max(.25,candleWidth));
        var bars=(int)Math.Ceiling((Math.Abs(pointerDeltaX)-clickDeadZone)/gestureUnit);
        return Math.Clamp(startOffset+Math.Sign(pointerDeltaX)*Math.Max(1,bars),0,maxOffset);
    }
    /// <summary>Plot edge and right-side price labels snap to the nearest visible candle.</summary>
    public int HoverBarIndex(double x)
    {
        var b=Bars;if(b==null||b.Count==0||double.IsNaN(x))return -1;
        int end=Math.Max(1,b.Count-Math.Min(offset,Math.Max(0,b.Count-2))),start=Math.Max(0,end-visible);
        var step=Math.Max(50,ActualWidth-88)/(end-start);
        return Math.Clamp(start+(int)Math.Floor((Math.Max(0,x)-6)/step),start,end-1);
    }
    public void ResetView(){if(screeningReviewMode)ShowScreeningOverview();else{offset=0;InvalidateVisual();}}
    public void FocusTrade(DateTimeOffset time,bool buy)
    {
        var bars=Bars;if(bars==null||bars.Count==0)return;var marker=new TradeMarker("focus",bars[0].Time.Offset==TimeSpan.FromHours(9)?"KRX:FOCUS":"NASDAQ:FOCUS",time,buy,0,null,"focus");var index=TradeBarIndex(bars,marker,Period);if(index<0)return;offset=Math.Clamp(bars.Count-index-visible/2,0,Math.Max(0,bars.Count-visible));InvalidateVisual();
    }
    private string BarHelp(PriceBar b)=>$"{(b.IsComplete||ChartPeriods.Intraday(Period)?b.KoreaTime:b.Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd")+" 진행 중")} · 한국시간\n시가 {DisplayNumber.Price(b.Open)}  고가 {DisplayNumber.Price(b.High)}\n저가 {DisplayNumber.Price(b.Low)}  종가 {DisplayNumber.Price(b.Close)}\n거래량 {b.Volume:N0}";
    protected override void OnRender(DrawingContext dc)
    {
        RenderFrameCount++;
        markerHitRegions.Clear();hitBars=Bars;hitMarkers=TradeMarkers;hitPeriod=Period;hitVisible=visible;hitOffset=offset;hitSize=RenderSize;
        base.OnRender(dc);dc.DrawRectangle(Brushes.White,null,new Rect(RenderSize));var b=Bars;if(b==null||b.Count==0){var text=new FormattedText(EmptyMessage,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),14,Muted,VisualTreeHelper.GetDpi(this).PixelsPerDip){MaxTextWidth=Math.Max(50,ActualWidth-48)};dc.DrawText(text,new(24,90));return;}
        double width=Math.Max(50,ActualWidth-88),height=Math.Max(55,ActualHeight-130),top=34;int end=Math.Max(1,b.Count-Math.Min(offset,Math.Max(0,b.Count-2))),start=Math.Max(0,end-visible);double step=width/(end-start);decimal low=decimal.MaxValue,high=decimal.MinValue;for(var i=start;i<end;i++){var bar=b[i];if(bar.Low<low)low=bar.Low;if(bar.High>high)high=bar.High;}var pad=(high-low)*.07m;if(pad==0)pad=high*.01m;low-=pad;high+=pad;var indicators=IndicatorsFor(b);EnsureMarkerMap(b);var renderStride=Math.Max(1,(int)Math.Ceiling(1.5/step));var renderBars=RenderBars(b,start,end,renderStride);var maxVol=Math.Max(1,renderBars.Max(x=>x.Volume));
        double X(double i)=>(i-start+.5)*step+6;double Y(decimal v)=>top+(double)((high-v)/(high-low))*height;
        for(int n=0;n<=4;n++){var val=low+(high-low)*n/4;var y=Y(val);dc.DrawLine(GridPen,new(0,y),new(width+8,y));Text(dc,DisplayNumber.Price(val),width+15,y-8,Muted,11);}
        foreach(var mapped in locatedZones)
        {
            var zone=mapped.Zone;var first=mapped.First;var last=mapped.Last;
            if(first<0||last<0||last<start||first>=end)continue;first=Math.Max(first,start);last=Math.Min(last,end-1);
            var zoneTop=Y(Math.Min(high,zone.Upper));var zoneBottom=Y(Math.Max(low,zone.Lower));var fill=new SolidColorBrush(zone.UsedForEntry?Color.FromArgb(38,49,130,246):Color.FromArgb(20,139,149,161));var border=new Pen(new SolidColorBrush(zone.UsedForEntry?Color.FromArgb(150,49,130,246):Color.FromArgb(90,139,149,161)),zone.UsedForEntry?1.4:1);
            dc.DrawRectangle(fill,border,new Rect(Math.Max(6,X(first)-step/2),Math.Min(zoneTop,zoneBottom),Math.Max(2,X(last)-X(first)+step),Math.Max(2,Math.Abs(zoneBottom-zoneTop))));
        }
        if(Ichimoku)
        {
            // Draw displaced cloud only over real candle slots. Reserving 26 empty future slots
            // made every period look as if the newest candles were missing.
            var risingCloud=new StreamGeometry();var fallingCloud=new StreamGeometry();
            using(var rising=risingCloud.Open())using(var falling=fallingCloud.Open())for(int i=start;i<end-1;i+=renderStride)
            {var next=Math.Min(end-1,i+renderStride);var a=CloudAt(i);var z=CloudAt(next);if(a.A==null||a.B==null||z.A==null||z.B==null)continue;var c=a.A>a.B?rising:falling;c.BeginFigure(new(X(i),Y(a.A.Value)),true,true);c.LineTo(new(X(next),Y(z.A.Value)),true,false);c.LineTo(new(X(next),Y(z.B.Value)),true,false);c.LineTo(new(X(i),Y(a.B.Value)),true,false);}
            risingCloud.Freeze();fallingCloud.Freeze();dc.DrawGeometry(CloudRed,null,risingCloud);dc.DrawGeometry(CloudBlue,null,fallingCloud);
            DrawLine(i=>indicators.Mid9[i],Conversion,end);
            DrawLine(i=>indicators.Mid26[i],BaseLine,end);
            DrawLine(i=>CloudAt(i).A,CloudRedLine,end);
            DrawLine(i=>CloudAt(i).B,CloudBlueLine,end);
            DrawLine(i=>i+26<end?b[i+26].Close:null,Lagging,end-26);
        }
        foreach(var bar in renderBars){var rising=bar.Close>=bar.Open;var brush=rising?Red:Blue;double x=X(bar.Index),cw=Math.Max(1,step*renderStride*.58);dc.DrawLine(rising?RedPen:BluePen,new(x,Y(bar.High)),new(x,Y(bar.Low)));dc.DrawRectangle(brush,null,new(x-cw/2,Math.Min(Y(bar.Open),Y(bar.Close)),cw,Math.Max(1,Math.Abs(Y(bar.Open)-Y(bar.Close)))));dc.DrawRectangle(rising?RedVolume:BlueVolume,null,new(x-cw/2,top+height+55-(double)(bar.Volume/maxVol)*42,cw,(double)(bar.Volume/maxVol)*42));}
        if(MovingAverages){DrawLine(i=>indicators.Fast[i],MaFast,end);DrawLine(i=>indicators.Slow[i],MaSlow,end);DrawLine(i=>indicators.Sma60[i],Ma60,end);DrawLine(i=>indicators.Sma120[i],Ma120,end);}
        for(int i=start;i<end;i+=Math.Max(1,(end-start)/5))Text(dc,b[i].Time.ToOffset(TimeSpan.FromHours(9)).ToString(ChartPeriods.Intraday(Period)?"HH:mm:ss":"yy.MM.dd"),Math.Max(8,X(i)-17),top+height+64,Muted,11);
        Text(dc,"거래량",8,top+height+8,Muted,10);
        var located=locatedTrades.Where(x=>x.Index>=start&&x.Index<end).ToArray();
        // Bucket aggregation bounds the initial work at overview scale; measured text bounds
        // finish the job for long reasons, profit figures, edge-clamped and opposite-side labels.
        var seeds=step<15||located.Length>Math.Max(12,width/60)
            ?ChartMarkerAggregation.Cluster(located,x=>x.Trade.Buy,x=>x.Index,x=>x.Trade.Price,start,end,width).Select(g=>(IReadOnlyList<LocatedTrade>)g.Items)
            :located.Select(x=>(IReadOnlyList<LocatedTrade>)new[]{x});
        (string Label,double X,double Y,Brush Brush) LabelFor(IReadOnlyList<LocatedTrade> items)
        {
            var position=items.Average(x=>x.Index);var index=Math.Clamp((int)Math.Round(position),start,end-1);
            var buys=items.Count(x=>x.Trade.Buy);var sells=items.Count-buys;var buy=buys>=sells;
            var numbers=items.Select(x=>x.Trade.TradeNumber).Where(n=>n.HasValue).Select(n=>n!.Value).Distinct().Order().ToArray();
            var prefix=numbers.Length==0?"":numbers.Length==1?$"#{numbers[0]} ":$"#{numbers[0]}~#{numbers[^1]} ";
            var label=items.Count==1?prefix+(buy?"▲ ":"▼ ")+items[0].Trade.SideLabel+" "+items[0].Trade.Quantity.ToString("N0")+(items[0].Trade.DateOnly?"*":"")
                :prefix+(buys>0?$"▲ 매수 {buys:N0}건":"")+(buys>0&&sells>0?" · ":"")+(sells>0?$"▼ 매도 {sells:N0}건":"");
            if(sells>0)
            {
                if(items.Count>1)
                {
                    var exits=items.Where(x=>!x.Trade.Buy).Select(x=>x.Trade.SideLabel).ToArray();var reasons=exits.Distinct().ToArray();
                    label+=" · "+(reasons.Length==1?reasons[0]:string.Join(" · ",exits.GroupBy(text=>text.Contains("익절")?"익절":text.Contains("손절")?"손절":text.Contains("본전")?"본전":"기타 청산").Select(g=>$"{g.Key} {g.Count():N0}건")));
                }
                var sold=items.Where(x=>!x.Trade.Buy).ToArray();if(sold.All(x=>x.Trade.RealizedPnl.HasValue))label+=" · "+DisplayNumber.SignedPrice(sold.Sum(x=>x.Trade.RealizedPnl!.Value));
            }
            var price=items.Average(x=>x.Trade.Price??b[x.Index].Close);
            var y=items.All(x=>x.Trade.DateOnly)?top+height+20:Y(price)+(buy?12:-27);
            return(label,X(position),Math.Clamp(y,top,top+height+35),buy?Red:Blue);
        }
        foreach(var group in ChartMarkerAggregation.MergeOverlaps(seeds,items=>{var label=LabelFor(items);return BadgeBounds(label.Label,label.X,label.Y);}))
        {
            var label=LabelFor(group);var bounds=DrawBadge(dc,label.Label,label.X,label.Y,label.Brush);
            var position=group.Average(x=>x.Index);var representative=group.OrderBy(x=>Math.Abs(x.Index-position)).ThenBy(x=>x.Trade.Time).First().Trade;
            markerHitRegions.Add((bounds,representative));
        }
        if(EquityCurve is {Count:>0} curve)
        {
            var mapped=Enumerable.Range(start,end-start).Select(i=>(Index:i,Value:curve.LastOrDefault(p=>p.Time<=b[i].Time))).Where(p=>p.Value!=null).ToArray();
            if(mapped.Length>0)
            {
                var min=mapped.Min(p=>p.Value!.Equity);var max=mapped.Max(p=>p.Value!.Equity);var range=Math.Max(1,max-min);Point? last=null;
                foreach(var p in mapped){var point=new Point(X(p.Index),top+height*.9-(double)((p.Value!.Equity-min)/range)*height*.8);if(last is {} prior)dc.DrawLine(new Pen(Brushes.SeaGreen,1.6),prior,point);last=point;}
                Text(dc,$"평가금액 {mapped[^1].Value!.Equity:N0} · 별도 축 {min:N0}~{max:N0}",8,top+height+85,Brushes.SeaGreen,10);
            }
        }
        if(hover is {} h){int i=HoverBarIndex(h.X);var bar=b[i];dc.DrawLine(new Pen(Muted,.6){DashStyle=DashStyles.Dash},new(Math.Clamp(X(i),6,width),top),new(Math.Clamp(X(i),6,width),top+height+55));Text(dc,$"{(bar.IsComplete||ChartPeriods.Intraday(Period)?bar.KoreaTime:bar.Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd")+" 진행 중")} (한국)   시 {DisplayNumber.Price(bar.Open)}   고 {DisplayNumber.Price(bar.High)}   저 {DisplayNumber.Price(bar.Low)}   종 {DisplayNumber.Price(bar.Close)}   거래량 {bar.Volume:N0}",8,8,Brushes.DimGray,11);}
        else Text(dc,screeningReviewMode?(screeningReviewFocused?"휠 확대·축소 · 좌우 드래그 이동 · 우클릭하면 전체 기간으로 복귀":"전체 기간 잠금 · 원하는 봉에서 우클릭하면 확대 및 탐색 활성화"):"마우스 휠로 확대 · 어디서든 좌우로 밀어 이동 · 모든 시각은 한국시간",8,8,Muted,11);
        (decimal? A,decimal? B) CloudAt(int i){int k=i-26;if(k<0||k>=end||indicators.Mid9[k] is not {} mid9||indicators.Mid26[k] is not {} mid26||indicators.Mid52[k] is not {} mid52)return(null,null);return((mid9+mid26)/2,mid52);}
        void DrawLine(Func<int,decimal?> value,Brush brush,int stop){Point? prev=null;var pen=new Pen(brush,1.15);if(pen.CanFreeze)pen.Freeze();void PointAt(int i){var v=value(i);if(v==null){prev=null;return;}var p=new Point(X(i),Y(v.Value));if(prev.HasValue)dc.DrawLine(pen,prev.Value,p);prev=p;}var last=-1;for(int i=start;i<stop;i+=renderStride){PointAt(i);last=i;}if(stop>start&&last!=stop-1)PointAt(stop-1);}
    }
    private IndicatorCache IndicatorsFor(IReadOnlyList<PriceBar> bars)
    {
        if(indicatorCache is {} cached&&ReferenceEquals(cached.Source,bars)&&cached.FastPeriod==FastPeriod&&cached.SlowPeriod==SlowPeriod&&cached.WithIchimoku==Ichimoku)return cached;
        var prefix=new decimal[bars.Count+1];for(var i=0;i<bars.Count;i++)prefix[i+1]=prefix[i]+bars[i].Close;
        decimal?[] Sma(int requested)
        {
            var period=Math.Max(1,requested);var values=new decimal?[bars.Count];
            for(var i=period-1;i<bars.Count;i++)values[i]=(prefix[i+1]-prefix[i+1-period])/period;
            return values;
        }
        decimal?[] Mid(int period)
        {
            if(!Ichimoku)return [];
            var values=new decimal?[bars.Count];
            for(var i=period-1;i<bars.Count;i++)
            {
                var low=decimal.MaxValue;var high=decimal.MinValue;
                for(var j=i-period+1;j<=i;j++){if(bars[j].Low<low)low=bars[j].Low;if(bars[j].High>high)high=bars[j].High;}
                values[i]=(high+low)/2;
            }
            return values;
        }
        indicatorCache=new(bars,FastPeriod,SlowPeriod,Ichimoku,Sma(FastPeriod),Sma(SlowPeriod),Sma(60),Sma(120),Mid(9),Mid(26),Mid(52));indicatorCacheBuildCount++;return indicatorCache;
    }
    private sealed record IndicatorCache(IReadOnlyList<PriceBar> Source,int FastPeriod,int SlowPeriod,bool WithIchimoku,decimal?[] Fast,decimal?[] Slow,decimal?[] Sma60,decimal?[] Sma120,decimal?[] Mid9,decimal?[] Mid26,decimal?[] Mid52);
    private static RenderBar[] RenderBars(IReadOnlyList<PriceBar> bars,int start,int end,int stride)
    {
        var result=new List<RenderBar>((end-start+stride-1)/stride);
        for(var first=start;first<end;first+=stride)
        {
            var last=Math.Min(end,first+stride);var high=decimal.MinValue;var low=decimal.MaxValue;decimal volume=0;
            for(var i=first;i<last;i++){var bar=bars[i];if(bar.High>high)high=bar.High;if(bar.Low<low)low=bar.Low;volume+=bar.Volume;}
            result.Add(new((first+last-1)/2d,bars[first].Open,high,low,bars[last-1].Close,volume));
        }
        return result.ToArray();
    }
    private readonly record struct RenderBar(double Index,decimal Open,decimal High,decimal Low,decimal Close,decimal Volume);
    public static int TradeBarIndex(IReadOnlyList<PriceBar> bars,TradeMarker trade,string period)
    {
        if(bars.Count==0)return -1;
        var zone=TimeZoneInfo.FindSystemTimeZoneById(trade.StockKey.StartsWith("KRX:")?"Korea Standard Time":"Eastern Standard Time");
        DateTime Day(DateTimeOffset t)=>TimeZoneInfo.ConvertTime(t,zone).Date;
        DateTime Bucket(DateTime day)=>period=="월봉"?new(day.Year,day.Month,1):period=="주봉"?day.AddDays(-((int)day.DayOfWeek+6)%7):day;
        // Reports with only a KST date are placed as date annotations, not intraday fills.
        var day=trade.DateOnly?trade.Time.ToOffset(TimeSpan.FromHours(9)).Date:Day(trade.Time);
        var calendar=!ChartPeriods.Intraday(period)||trade.DateOnly;var minutes=ChartPeriods.Minutes(period);
        long Key(DateTimeOffset time)=>calendar?Bucket(trade.DateOnly?time.ToOffset(TimeSpan.FromHours(9)).Date:Day(time)).Ticks:minutes>0?ChartPeriods.MinuteBucket(new(time,1,1,1,1,0),minutes):time.UtcTicks;
        var target=Key(trade.Time);int left=0,right=bars.Count;
        while(left<right){var middle=left+(right-left)/2;if(Key(bars[middle].Time)<target)left=middle+1;else right=middle;}
        if(left>=bars.Count)return -1;
        return calendar||minutes>0?Key(bars[left].Time)==target?left:-1:Day(bars[left].Time)==day?left:-1;
    }
    private sealed record LocatedTrade(TradeMarker Trade,int Index);
    private sealed record LocatedZone(ScreeningSupportZone Zone,int First,int Last);
    /// <summary>Binary-search immutable source bars once; pointer and viewport changes reuse the mapping.</summary>
    private void EnsureMarkerMap(IReadOnlyList<PriceBar> bars)
    {
        if(ReferenceEquals(mappedBars,bars)&&ReferenceEquals(mappedMarkers,TradeMarkers)&&ReferenceEquals(mappedZones,SupportZones)&&mappedPeriod==Period)return;
        locatedTrades=(TradeMarkers??[]).Select(t=>new LocatedTrade(t,TradeBarIndex(bars,t,Period))).ToArray();
        var key=bars[0].Time.Offset==TimeSpan.FromHours(9)?"KRX:ZONE":"NASDAQ:ZONE";
        locatedZones=(SupportZones??[]).Select(z=>new LocatedZone(z,TradeBarIndex(bars,new("zone",key,z.From,true,0,z.Lower,"zone"),Period),TradeBarIndex(bars,new("zone",key,z.To,true,0,z.Upper,"zone"),Period))).ToArray();
        mappedBars=bars;mappedMarkers=TradeMarkers;mappedZones=SupportZones;mappedPeriod=Period;markerMapBuildCount++;
    }
    public TradeMarker? MarkerAtPoint(Point point)
    {
        var b=Bars;var markers=TradeMarkers;if(b==null||b.Count==0||markers==null||markers.Count==0)return null;
        // A badge can be centred between traded candles; its text can also extend over adjacent candles.
        // Never reuse its old screen coordinates after pan, zoom, resize or source replacement.
        if(ReferenceEquals(hitBars,b)&&ReferenceEquals(hitMarkers,markers)&&hitPeriod==Period&&hitVisible==visible&&hitOffset==offset&&hitSize==RenderSize)
        {
            var label=markerHitRegions.Where(hit=>hit.Bounds.Contains(point)).OrderBy(hit=>Math.Abs(hit.Bounds.Top+hit.Bounds.Height/2-point.Y)).Select(hit=>hit.Marker).FirstOrDefault();
            if(label!=null)return label;
        }
        EnsureMarkerMap(b);int end=Math.Max(1,b.Count-Math.Min(offset,Math.Max(0,b.Count-2))),start=Math.Max(0,end-visible);double width=Math.Max(50,ActualWidth-88),height=Math.Max(55,ActualHeight-130),top=34,step=width/(end-start);decimal low=decimal.MaxValue,high=decimal.MinValue;for(var i=start;i<end;i++){low=Math.Min(low,b[i].Low);high=Math.Max(high,b[i].High);}var pad=(high-low)*.07m;if(pad==0)pad=high*.01m;low-=pad;high+=pad;
        double Y(decimal v)=>top+(double)((high-v)/(high-low))*height;var clicked=HoverBarIndex(point.X);
        return locatedTrades.Where(x=>x.Index==clicked&&x.Index>=start&&x.Index<end).OrderBy(x=>Math.Abs((x.Trade.DateOnly?top+height+20:Y(x.Trade.Price??b[x.Index].Close))-point.Y)).Select(x=>x.Trade).FirstOrDefault();
    }
    /// <summary>Selects an actual fill represented by a label or by the candle underneath it.</summary>
    public bool SelectMarkerAtPoint(Point point)
    {if(MarkerAtPoint(point) is not {} marker)return false;TradeMarkerClicked?.Invoke(marker);return true;}
    private Rect BadgeBounds(string label,double centerX,double y)
    {
        var text=BadgeText(label,Muted);var x=Math.Clamp(centerX-text.Width/2,6,Math.Max(6,ActualWidth-94-text.Width));
        return new Rect(x-4,y-2,text.Width+8,text.Height+4);
    }
    private FormattedText BadgeText(string label,Brush brush)=>new(label,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),14,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);
    private Rect DrawBadge(DrawingContext dc,string label,double centerX,double y,Brush brush)
    {
        var text=BadgeText(label,brush);var rect=BadgeBounds(label,centerX,y);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225,255,255,255)),new Pen(brush,1),rect,4,4);dc.DrawText(text,new Point(rect.Left+4,rect.Top+2));
        return rect;
    }
    private Rect Text(DrawingContext dc,string text,double x,double y,Brush brush,double size,bool keepInside=false)
    {var formatted=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,new Typeface("Malgun Gothic"),size,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);if(keepInside)x=Math.Clamp(x,6,Math.Max(6,ActualWidth-88-formatted.Width));dc.DrawText(formatted,new(x,y));return new Rect(x,y,formatted.Width,formatted.Height);}
}
