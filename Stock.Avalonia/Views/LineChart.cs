using Avalonia;
using Avalonia.Controls;
using ScottPlot.Avalonia;
using Stock.Core.Research;
namespace Stock.Desktop;
/// <summary>Actual irregular time/price points only, never converted into synthetic OHLC candles.</summary>
public sealed class LineChart:UserControl
{
    public static readonly StyledProperty<IndexPoint[]?> PointsProperty=AvaloniaProperty.Register<LineChart,IndexPoint[]?>(nameof(Points));
    public IndexPoint[]? Points {get=>GetValue(PointsProperty);set=>SetValue(PointsProperty,value);}
    private readonly AvaPlot chart=new();
    public LineChart(){Content=chart;MinHeight=80;chart.Plot.HideAxesAndGrid();chart.Plot.FigureBackground.Color=ScottPlot.Color.FromHex("F7F8FA");chart.Plot.DataBackground.Color=ScottPlot.Color.FromHex("F7F8FA");}
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);if(change.Property!=PointsProperty)return;chart.Plot.Clear();if(Points is not {Length:>1} points){chart.Refresh();return;}
        var sorted=points.OrderBy(p=>p.Time).ToArray();var line=chart.Plot.Add.SignalXY(sorted.Select(p=>p.Time.ToOffset(TimeSpan.FromHours(9)).DateTime.ToOADate()).ToArray(),sorted.Select(p=>(double)p.Value).ToArray());line.Color=ScottPlot.Color.FromHex("3182F6");line.LineWidth=2;chart.Plot.Axes.AutoScale();chart.Refresh();
    }
}
