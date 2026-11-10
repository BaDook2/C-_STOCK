using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ScottPlot;
using Stock.Core.Research;
using Avalonia.Threading;
using Avalonia.Interactivity;
namespace Stock.Desktop;

/// <summary>ScottPlot5 array series; price data is independent of execution markers and frozen support snapshots.</summary>
public partial class StockChart:UserControl
{
    public static readonly StyledProperty<PriceBar[]?> BarsProperty=AvaloniaProperty.Register<StockChart,PriceBar[]?>(nameof(Bars));
    public static readonly StyledProperty<TradeMarker[]?> MarkersProperty=AvaloniaProperty.Register<StockChart,TradeMarker[]?>(nameof(Markers));
    public static readonly StyledProperty<ChartSupportZone[]?> ZonesProperty=AvaloniaProperty.Register<StockChart,ChartSupportZone[]?>(nameof(Zones));
    public static readonly StyledProperty<int> RevisionProperty=AvaloniaProperty.Register<StockChart,int>(nameof(Revision));
    public static readonly StyledProperty<string> PeriodProperty=AvaloniaProperty.Register<StockChart,string>(nameof(Period),"일봉");
    public static readonly StyledProperty<bool> ShowMovingAveragesProperty=AvaloniaProperty.Register<StockChart,bool>(nameof(ShowMovingAverages),true);
    public static readonly StyledProperty<bool> ShowCloudProperty=AvaloniaProperty.Register<StockChart,bool>(nameof(ShowCloud));
    public static readonly StyledProperty<PriceBar[]?> DailyBarsProperty=AvaloniaProperty.Register<StockChart,PriceBar[]?>(nameof(DailyBars));
    public PriceBar[]? DailyBars {get=>GetValue(DailyBarsProperty);set=>SetValue(DailyBarsProperty,value);}
    public int RenderedCrashLines {get;private set;}
    public bool RenderedMa240 {get;private set;}
    public static readonly StyledProperty<PriceBar[]?> IndicatorBarsProperty=AvaloniaProperty.Register<StockChart,PriceBar[]?>(nameof(IndicatorBars));
    public static readonly StyledProperty<bool> OverviewNavigationProperty=AvaloniaProperty.Register<StockChart,bool>(nameof(OverviewNavigation));
    public static readonly StyledProperty<int> DisplayBarCountProperty=AvaloniaProperty.Register<StockChart,int>(nameof(DisplayBarCount));
    public int DisplayBarCount {get=>GetValue(DisplayBarCountProperty);set=>SetValue(DisplayBarCountProperty,value);}
    public PriceBar[]? Bars {get=>GetValue(BarsProperty);set=>SetValue(BarsProperty,value);}
    public TradeMarker[]? Markers {get=>GetValue(MarkersProperty);set=>SetValue(MarkersProperty,value);}
    public ChartSupportZone[]? Zones {get=>GetValue(ZonesProperty);set=>SetValue(ZonesProperty,value);}
    public int Revision {get=>GetValue(RevisionProperty);set=>SetValue(RevisionProperty,value);}
    public string Period {get=>GetValue(PeriodProperty);set=>SetValue(PeriodProperty,value);}
    public bool ShowMovingAverages {get=>GetValue(ShowMovingAveragesProperty);set=>SetValue(ShowMovingAveragesProperty,value);}
    public bool ShowCloud {get=>GetValue(ShowCloudProperty);set=>SetValue(ShowCloudProperty,value);}
    public PriceBar[]? IndicatorBars {get=>GetValue(IndicatorBarsProperty);set=>SetValue(IndicatorBarsProperty,value);}
    public bool OverviewNavigation {get=>GetValue(OverviewNavigationProperty);set=>SetValue(OverviewNavigationProperty,value);}
    public const int DetailBarCount=500;
    private readonly CandleViewport viewport=new();
    public bool DetailActive=>viewport.Enabled;
    public int VisibleBarCount=>viewport.Visible;
    public int ViewOffset=>viewport.Offset;
    public event Action<TradeMarker>? TradeSelected;
    private double[] times=[];
    private bool rendered;
    private int renderedRevision;
    private readonly List<IPlottable> annotations=[];
    private readonly List<(double X,decimal Y)> indicatorExtents=[];
    private readonly List<(Rect Bounds,TradeMarker Trade)> tradeLabelHits=[];
    private bool annotationQueued;
    private bool redrawQueued,resetView;
    public int RenderedCandles {get;private set;}
    public int RenderedMarkers {get;private set;}
    public int RenderedMarkerGroups {get;private set;}
    public StockChart()
    {
        InitializeComponent();ConfigureAppearance();DataContextChanged+=(_,_)=>{resetView=true;QueueRedraw();};ConfigureInteractions();PricePlot.SizeChanged+=(_,_)=>QueueAnnotations();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);if(change.Property==BarsProperty&&Bars is not {Length:>0}){TradeLayer?.Children.Clear();tradeLabelHits.Clear();}
        // Live updates may increase DisplayBarCount. Only an explicit Revision
        // or navigation-mode change should discard the user's pan/zoom state.
        if(change.Property==OverviewNavigationProperty)resetView=true;
        if(change.Property==BarsProperty||change.Property==MarkersProperty||change.Property==ZonesProperty||change.Property==RevisionProperty||change.Property==PeriodProperty||change.Property==ShowMovingAveragesProperty||change.Property==OverviewNavigationProperty||change.Property==ShowCloudProperty||change.Property==IndicatorBarsProperty||change.Property==DailyBarsProperty||change.Property==DisplayBarCountProperty)QueueRedraw();
    }
    private void QueueRedraw(){if(redrawQueued)return;redrawQueued=true;Dispatcher.UIThread.Post(()=>{redrawQueued=false;Redraw();},DispatcherPriority.Normal);}
    private static double X(DateTimeOffset time)=>time.ToOffset(TimeSpan.FromHours(9)).DateTime.ToOADate();
    private void Redraw()
    {
        if(PricePlot==null)return;var hasData=Bars is {Length:>0};PricePlot.IsVisible=VolumePlot.IsVisible=HoverInfo.IsVisible=VolumeCaption.IsVisible=hasData;EmptyNotice.IsVisible=!hasData;if(Bars is not {Length:>0} source){PricePlot.Plot.Clear();VolumePlot.Plot.Clear();annotations.Clear();times=[];HideGuide();RenderedCandles=RenderedMarkers=RenderedMarkerGroups=0;rendered=false;PricePlot.Refresh();VolumePlot.Refresh();return;}
        var plot=PricePlot.Plot;var preserve=rendered&&renderedRevision==Revision&&!resetView;plot.Clear();VolumePlot.Plot.Clear();
        annotations.Clear();
        times=source.Select(b=>X(b.Time)).ToArray();
        const double gap=1;var span=TimeSpan.FromDays(1);
        var candles=plot.Add.Candlestick(source.Select(b=>new OHLC((double)b.Open,(double)b.High,(double)b.Low,(double)b.Close,b.Time.ToOffset(TimeSpan.FromHours(9)).DateTime,span)).ToArray());
        candles.Sequential=true;candles.SymbolWidth=.58;candles.RisingColor=Color.FromHex("F04452");candles.FallingColor=Color.FromHex("3182F6");
        AddIndicators(source);
        RenderedCrashLines=0;
        foreach(var zone in (Zones??[]).Concat((Markers??[]).Where(m=>m.CrashJudgmentLow.HasValue).Select(m=>new ChartSupportZone(m.Time,m.Time,m.CrashJudgmentLow!.Value,m.CrashJudgmentLow.Value,true,"급락 판단 지점",true))).DistinctBy(z=>(z.CrashPoint,z.From,z.Lower)))
        {
            if(zone.CrashPoint){var line=plot.Add.HorizontalLine((double)zone.Lower);line.Color=Color.FromHex("9333EA");line.LineWidth=1.5f;RenderedCrashLines++;continue;}
            var box=plot.Add.Rectangle(TimeCoordinate(zone.From),TimeCoordinate(zone.To),(double)zone.Lower,(double)zone.Upper);box.FillColor=Colors.Blue.WithAlpha(zone.UsedForEntry?.13:.04);box.LineWidth=zone.UsedForEntry?1:0;box.LineColor=Colors.Blue;
        }
        VolumePlot.Plot.Add.Bars(source.Select((b,i)=>new ScottPlot.Bar{Position=i,Value=(double)b.Volume,Size=gap*.58,FillColor=(b.Close>=b.Open?Color.FromHex("F04452"):Color.FromHex("3182F6")).WithAlpha(.25),LineWidth=0}).ToArray());
        plot.Font.Automatic();VolumePlot.Plot.Font.Automatic();
        if(preserve)viewport.UpdateCount(source.Length);else viewport.Reset(source.Length,OverviewNavigation,DisplayBarCount);ApplyInputMode();
        ConfigureAppearance();ApplyViewport();rendered=true;resetView=false;renderedRevision=Revision;RenderedCandles=source.Length;
    }
    private void QueueAnnotations()
    {
        if(annotationQueued)return;annotationQueued=true;Dispatcher.UIThread.Post(()=>{annotationQueued=false;if(times.Length>0)ApplyViewport();},DispatcherPriority.Render);
    }
    private void UpdateAnnotations()
    {
        var plot=PricePlot.Plot;foreach(var annotation in annotations)plot.Remove(annotation);annotations.Clear();TradeLayer.Children.Clear();tradeLabelHits.Clear();var limits=plot.Axes.GetLimits();
        var density=Math.Max(200,PricePlot.Bounds.Width);var placements=new List<(ChartMarkerGroup Group,Border Badge,Rect Bounds)>();
        // Use measured text and free screen space, not an arbitrary four-row
        // offset. If the window cannot fit every caption, merge annotations
        // further; the original markers/executions are never removed.
        for(var attempt=0;attempt<12;attempt++)
        {
            placements.Clear();var groups=ChartMarkerLayout.Build(Markers??[],MarkerX,limits.Left,limits.Right,density);var fits=true;
            foreach(var group in groups)
            {
                var allBuy=group.Trades.All(m=>m.Buy);
                var brush=Avalonia.Media.Brush.Parse(group.ColorHex);
                var text=new TextBlock{Text=group.Caption,FontSize=14,Foreground=brush,TextWrapping=Avalonia.Media.TextWrapping.Wrap,MaxWidth=Math.Max(70,Math.Min(210,PricePlot.Bounds.Width-100))};
                var badge=new Border{Child=text,Tag=group,Background=Avalonia.Media.Brushes.White,BorderBrush=brush,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(4,2)};badge.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
                var size=badge.DesiredSize;var pixel=plot.GetPixel(new Coordinates(group.X,group.Price));var x=Math.Clamp(pixel.X/RenderScale-size.Width/2,6,Math.Max(6,PricePlot.Bounds.Width-82-size.Width));var preferred=Math.Clamp(pixel.Y/RenderScale+(allBuy?8:-size.Height-8),8,Math.Max(8,PricePlot.Bounds.Height-size.Height-8));
                Rect? free=null;
                for(var offset=0d;offset<=PricePlot.Bounds.Height&&free==null;offset+=4)
                    for(var direction=1;direction>=-1;direction-=2)
                    {
                        var y=preferred+offset*direction;if(y<8||y+size.Height>PricePlot.Bounds.Height-8)continue;
                        var candidate=new Rect(x,y,size.Width,size.Height);if(placements.All(p=>!p.Bounds.Inflate(2).Intersects(candidate))){free=candidate;break;}
                    }
                if(free==null){fits=false;break;}placements.Add((group,badge,free.Value));
            }
            if(fits||groups.Length==0)break;density*=.65;
        }
        foreach(var price in (Zones??[]).Where(z=>z.CrashPoint).Select(z=>z.Lower).Concat((Markers??[]).Where(m=>m.CrashJudgmentLow.HasValue).Select(m=>m.CrashJudgmentLow!.Value)).Distinct())
        {
            var label=new Border{Background=Avalonia.Media.Brushes.White,Padding=new Thickness(4,2),Child=new TextBlock{Text="급락 판단 지점",Foreground=Avalonia.Media.Brush.Parse("#9333EA"),FontSize=12}};
            var y=plot.GetPixel(new Coordinates(limits.Left,(double)price)).Y/RenderScale;Canvas.SetLeft(label,12);Canvas.SetTop(label,Math.Clamp(y-24,4,Math.Max(4,PricePlot.Bounds.Height-28)));TradeLayer.Children.Add(label);
        }
        RenderedMarkers=placements.Sum(p=>p.Group.Trades.Length);RenderedMarkerGroups=placements.Count;
        foreach(var (group,badge,bounds) in placements)
        {
            var allBuy=group.Trades.All(m=>m.Buy);var allSell=group.Trades.All(m=>!m.Buy);var color=Color.FromHex(group.ColorHex);
            annotations.Add(plot.Add.Marker(group.X,group.Price,allBuy?MarkerShape.FilledTriangleUp:allSell?MarkerShape.FilledTriangleDown:MarkerShape.FilledDiamond,11,color));
            Canvas.SetLeft(badge,bounds.X);Canvas.SetTop(badge,bounds.Y);TradeLayer.Children.Add(badge);tradeLabelHits.Add((bounds,group.Trades[0]));
        }
    }
    public void ResetView(){if(times.Length>0){viewport.Reset(times.Length,OverviewNavigation,DisplayBarCount);HideGuide();ApplyInputMode();QueueAnnotations();}}
    public void ZoomTo(DateTimeOffset from,DateTimeOffset to){if(times.Length>0&&DetailActive){viewport.Focus(Nearest(X(from)),Nearest(X(to)));QueueAnnotations();}}
    private void ApplyInputMode(){PricePlot.UserInputProcessor.Disable();VolumePlot.UserInputProcessor.Disable();HoverInfo.Text=DetailActive?(OverviewNavigation?"상세보기 · 휠 확대/축소 · 드래그 이동 · 우클릭으로 전체 기간":"마우스 휠로 확대 · 어디서든 좌우로 밀어 이동 · 모든 시각은 한국시간"):"전체 기간 잠금 · 원하는 봉에서 우클릭하면 확대 및 탐색 활성화";}
    private int Nearest(double value)
    {var index=Array.BinarySearch(times,value);if(index<0){index=~index;if(index==times.Length)index--;else if(index>0&&Math.Abs(times[index-1]-value)<Math.Abs(times[index]-value))index--;}return index;}
    private double MarkerX(TradeMarker marker)
    {
        if(Bars is not {Length:>0} bars)return double.NaN;
        var index=TradeCandleIndex(bars,marker,Period);return index<0?double.NaN:index;
    }
    /// <summary>Same market-calendar/minute buckets as WPF CandleChart.TradeBarIndex.
    /// Only the display coordinate changes; original execution precision is retained.</summary>
    public static int TradeCandleIndex(IReadOnlyList<PriceBar> bars,TradeMarker trade,string period)
    {
        if(bars.Count==0)return -1;
        var zone=trade.StockKey.StartsWith("KRX:")?KoreaZone:UsZone;
        DateTime Day(DateTimeOffset time)=>TimeZoneInfo.ConvertTime(time,zone).Date;
        DateTime Bucket(DateTime day)=>period=="월봉"?new(day.Year,day.Month,1):period=="주봉"?day.AddDays(-((int)day.DayOfWeek+6)%7):day;
        var calendar=!ChartPeriods.Intraday(period)||trade.DateOnly;var minutes=ChartPeriods.Minutes(period);
        long Key(DateTimeOffset time)=>calendar?Bucket(trade.DateOnly?time.ToOffset(TimeSpan.FromHours(9)).Date:Day(time)).Ticks:minutes>0?ChartPeriods.MinuteBucket(new(time,1,1,1,1,0),minutes):time.UtcTicks;
        var target=Key(trade.Time);var left=0;var right=bars.Count;
        while(left<right){var middle=left+(right-left)/2;if(Key(bars[middle].Time)<target)left=middle+1;else right=middle;}
        return left<bars.Count&&Key(bars[left].Time)==target?left:-1;
    }
    private static readonly TimeZoneInfo KoreaZone=TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
    private static readonly TimeZoneInfo UsZone=TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private void HoverAt(Point point)
    {
        ShowHover(point,false);
    }
    private void SelectAt(Point point)
    {
        var label=tradeLabelHits.FirstOrDefault(h=>h.Bounds.Contains(point));if(label.Trade!=null){TradeSelected?.Invoke(label.Trade);return;}
        if(Bars is not {Length:>0} bars||Markers is not {Length:>0})return;
        var index=IndexAt(point);var marker=Markers.Where(m=>MarkerX(m)==index).OrderBy(m=>Math.Abs((double)(m.Price??bars[index].Close)-CoordinatesAt(point).Y)).FirstOrDefault();
        if(marker!=null)TradeSelected?.Invoke(marker);
    }
}
