using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.ComponentModel;
using ScottPlot.WPF;
using Stock.Core.Research;
namespace Stock.App;

public sealed class RawTickChartView:UserControl
{
    public WpfPlot Chart {get;}=new();
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap},hover=new(),detail=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12)};
    private readonly DispatcherTimer debounce=new(){Interval=TimeSpan.FromMilliseconds(140)},live=new(){Interval=TimeSpan.FromSeconds(3)};
    private readonly TextBlock loading=new(){Text="구간 조회 중…",Foreground=Brushes.SteelBlue,HorizontalAlignment=HorizontalAlignment.Right,IsHitTestVisible=false};
    private readonly ComboBox sources=new(){Width=150,Margin=new Thickness(0,0,8,0)};
    private RawTickChartModel? model;private long origin;private bool internalAxes,axisPending;private double[] xs=[];private RawTick[] rows=[];private ScottPlot.Plottables.Crosshair? cross;
    private readonly List<(ScottPlot.Coordinates Point,TickFillMarker Marker)> markers=[];
    public RawTickChartView()
    {
        var root=new DockPanel{Margin=new Thickness(14),Background=Brushes.White};var bar=new WrapPanel();
        bar.Children.Add(sources);sources.SelectionChanged+=async(_,_)=>{if(model!=null&&sources.SelectedItem is string selected)await model.SwitchSource(selected);};
        Button Button(string title,Func<Task> fn){var b=new Button{Content=title,Margin=new Thickness(0,0,8,0)};b.Click+=async(_,_)=>{if(model!=null)await fn();};return b;}
        bar.Children.Add(Button("이전 구간",()=>model!.Move(false)));bar.Children.Add(Button("다음 구간",()=>model!.Move(true)));bar.Children.Add(Button("최신 / 초기화",()=>model!.Reset()));bar.Children.Add(Button("이 거래 확대",()=>model!.ZoomTrade()));
        var follow=new CheckBox{Content="최신 체결 따라가기",VerticalAlignment=VerticalAlignment.Center};follow.SetBinding(CheckBox.IsCheckedProperty,new System.Windows.Data.Binding(nameof(RawTickChartModel.FollowLatest)){Mode=System.Windows.Data.BindingMode.TwoWay});bar.Children.Add(follow);
        DockPanel.SetDock(bar,Dock.Top);root.Children.Add(bar);DockPanel.SetDock(status,Dock.Bottom);root.Children.Add(status);DockPanel.SetDock(hover,Dock.Bottom);root.Children.Add(hover);
        var side=new StackPanel{Width=270};side.Children.Add(new TextBlock{Text="체결 상세 · 한국시간",FontSize=18,FontWeight=FontWeights.Bold,Margin=new Thickness(12)});side.Children.Add(detail);side.Children.Add(new TextBlock{Text="부분 체결은 FIFO로 나누어 표시합니다. 같은 위치의 마커는 클릭할 때 선택 목록을 엽니다. 날짜만 있는 체결은 정확한 틱 마커로 사용하지 않습니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12),Foreground=Brushes.Gray});DockPanel.SetDock(side,Dock.Right);root.Children.Add(side);
        var grid=new Grid();grid.Children.Add(Chart);grid.Children.Add(loading);root.Children.Add(grid);Content=root;
        Chart.Plot.Axes.Bottom.TickGenerator=new ScottPlot.TickGenerators.NumericAutomatic{LabelFormatter=x=>FormatTime(x)};
        Chart.Plot.Axes.Left.TickGenerator=new ScottPlot.TickGenerators.NumericAutomatic{LabelFormatter=y=>y.ToString("#,##0.########")};
        Chart.Plot.RenderManager.AxisLimitsChanged+=(_,_)=>{if(!internalAxes){axisPending=true;debounce.Stop();debounce.Start();}};
        Chart.PreviewMouseDown+=(_,_)=>{if(model!=null)model.FollowLatest=false;};Chart.PreviewMouseWheel+=(_,_)=>{if(model!=null)model.FollowLatest=false;QueueMarkerRender();};Chart.MouseMove+=Mouse;Chart.PreviewMouseLeftButtonUp+=(_,e)=>{Click(Chart,e);QueueMarkerRender();};
        debounce.Tick+=async(_,_)=>{debounce.Stop();if(axisPending){axisPending=false;await Navigate();}};
        live.Tick+=async(_,_)=>{if(IsVisible&&model is {FollowLatest:true,Busy:false})await model.Reset();};
        DataContextChanged+=async(_,_)=>{if(model!=null){model.Updated-=Render;model.PropertyChanged-=Changed;}model=DataContext as RawTickChartModel;origin=0;rows=[];Chart.Plot.Clear();Chart.Refresh();var current=model;if(current!=null){debounce.Interval=TimeSpan.FromMilliseconds(current.Options.DebounceMilliseconds);current.Updated+=Render;current.PropertyChanged+=Changed;await current.LoadSources();if(ReferenceEquals(model,current))await current.Reset();}UpdateText();};
        Loaded+=(_,_)=>live.Start();Unloaded+=(_,_)=>{live.Stop();debounce.Stop();};
    }
    private string FormatTime(double x){try{return new DateTimeOffset(origin+(long)(x*TimeSpan.TicksPerSecond),TimeSpan.Zero).ToOffset(TimeSpan.FromHours(9)).ToString("MM.dd HH:mm:ss.fff");}catch{return "";}}
    private void Changed(object? sender,PropertyChangedEventArgs args){if(args.PropertyName==nameof(RawTickChartModel.Sources)){sources.ItemsSource=model?.Sources;sources.SelectedItem=model?.Source;}UpdateText();}
    private void UpdateText(){status.Text=model?.Status??"종목을 먼저 선택하세요.";detail.Text=model?.TradeSummary??"";loading.Visibility=model?.Busy==true?Visibility.Visible:Visibility.Collapsed;}
    private void Render(bool reset)
    {
        if(model==null)return;var previous=Chart.Plot.Axes.GetLimits();rows=model.Rows;Chart.Plot.Clear();markers.Clear();cross=null;internalAxes=true;
        try
        {
            if(rows.Length==0){Chart.Refresh();return;}origin=model.Origin;xs=model.XValues;var ys=model.YValues;
            var signal=Chart.Plot.Add.SignalXY(xs,ys);signal.Color=ScottPlot.Colors.DodgerBlue;signal.LineWidth=1;
            var viewLeft=reset||previous.Right<=previous.Left?xs[0]:Math.Max(xs[0],previous.Left);var viewRight=reset||previous.Right<=previous.Left?Math.Max(xs[^1],xs[0]+.000001):Math.Min(xs[^1],previous.Right);
            var located=model.Markers.Where(m=>m.Time.UtcTicks>=rows[0].Key.UtcTicks&&m.Time.UtcTicks<=rows[^1].Key.UtcTicks).Select(m=>(Marker:m,X:(m.Time.UtcTicks-origin)/(double)TimeSpan.TicksPerSecond)).ToArray();
            foreach(var cluster in ChartMarkerAggregation.Cluster(located,x=>x.Marker.Buy,x=>x.X,x=>x.Marker.Price,viewLeft,Math.Max(viewRight,viewLeft+.000001),Math.Max(100,Chart.ActualWidth-80)))
            {
                var point=new ScottPlot.Coordinates(cluster.Position,(double)(cluster.Price??cluster.Items[0].Marker.Price));var p=Chart.Plot.Add.Marker(point);p.Shape=cluster.Buy?ScottPlot.MarkerShape.FilledTriangleUp:ScottPlot.MarkerShape.FilledTriangleDown;p.Color=cluster.Buy?ScottPlot.Colors.Red:ScottPlot.Colors.Blue;p.Size=cluster.Count>1?12:9;
                if(cluster.Count>1){var label=Chart.Plot.Add.Text((cluster.Buy?"▲ 매수 ":"▼ 매도 ")+cluster.Count.ToString("N0")+"건",point.X,point.Y);label.LabelFontColor=cluster.Buy?ScottPlot.Colors.Red:ScottPlot.Colors.Blue;label.LabelFontSize=14;label.LabelOffsetY=cluster.Buy?14:-18;label.LabelAlignment=ScottPlot.Alignment.MiddleCenter;}
                foreach(var item in cluster.Items)markers.Add((point,item.Marker));
            }
            cross=Chart.Plot.Add.Crosshair(xs[0],ys[0]);cross.IsVisible=false;
            if(reset||previous.Right<xs[0]||previous.Left>xs[^1]){Chart.Plot.Axes.AutoScaleY();Chart.Plot.Axes.SetLimitsX(xs[0],Math.Max(xs[^1],xs[0]+.000001));}
            else {var width=Math.Min(previous.Right-previous.Left,Math.Max(.000001,xs[^1]-xs[0]));var left=Math.Clamp(previous.Left,xs[0],Math.Max(xs[0],xs[^1]-width));Chart.Plot.Axes.SetLimitsX(left,left+width);Chart.Plot.Axes.AutoScaleY();}
            Chart.Plot.RenderManager.DisableAxisLimitsChangedEventOnNextRender=true;Chart.Refresh();UpdateText();
        }finally{internalAxes=false;}
    }
    private void QueueMarkerRender()=>Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>{if(model!=null&&rows.Length>0)Render(false);}));
    private async Task Navigate()
    {
        if(model==null||model.Busy||rows.Length==0||xs.Length==0)return;var lim=Chart.Plot.Axes.GetLimits();var span=Math.Max(.000001,xs[^1]-xs[0]);var margin=span*model.Options.PreloadThresholdPercent/100;
        if(lim.Left<=xs[0]||lim.Right>=xs[^1]){model.FollowLatest=false;await model.Move(lim.Right>=xs[^1]);}
        else if(lim.Left<xs[0]+margin)await model.Preload(false);else if(lim.Right>xs[^1]-margin)await model.Preload(true);
    }
    private void Mouse(object sender,MouseEventArgs e)
    {
        if(rows.Length==0||cross==null)return;var point=Chart.Plot.GetCoordinates(Chart.GetPlotPixelPosition(e));int index=Array.BinarySearch(xs,point.X);if(index<0){index=~index;if(index>0&&(index==xs.Length||point.X-xs[index-1]<=xs[index]-point.X))index--;}index=Math.Clamp(index,0,rows.Length-1);if(index<0)return;
        var tick=rows[index];cross.Position=new(xs[index],(double)tick.Price);cross.IsVisible=true;hover.Text=$"{tick.Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss.fffffff} · {tick.Price:#,##0.########} · 체결량 {tick.Size:#,##0.########} · 순서 {tick.Sequence:N0} / 행 {tick.Key.RowId:N0}";Chart.Refresh();
    }
    private void Click(object sender,MouseButtonEventArgs e)=>SelectAtPixel(Chart.GetPlotPixelPosition(e));
    public void SelectAtPixel(ScottPlot.Pixel pixel)
    {
        if(model==null)return;var near=markers.Where(m=>{var p=Chart.Plot.GetPixel(m.Point);return Math.Abs(p.X-pixel.X)<9&&Math.Abs(p.Y-pixel.Y)<9;}).ToArray();
        if(near.Length==1)model.Select(near[0].Marker);else if(near.Length>1){var menu=new ContextMenu();foreach(var item in near){var row=new MenuItem{Header=(item.Marker.Buy?"매수 ":"매도 ")+item.Marker.Detail.Id};row.Click+=(_,_)=>model.Select(item.Marker);menu.Items.Add(row);}menu.IsOpen=true;}
    }
}
