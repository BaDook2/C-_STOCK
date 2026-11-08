using Avalonia.Controls;
using Avalonia;
namespace Stock.Desktop;
public partial class ResearchResultPanel:UserControl
{
    public static readonly StyledProperty<bool> OverviewNavigationProperty=AvaloniaProperty.Register<ResearchResultPanel,bool>(nameof(OverviewNavigation));
    public bool OverviewNavigation {get=>GetValue(OverviewNavigationProperty);set=>SetValue(OverviewNavigationProperty,value);}
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change){base.OnPropertyChanged(change);if(change.Property==OverviewNavigationProperty&&Chart!=null)Chart.OverviewNavigation=OverviewNavigation;}
    public ResearchResultPanel(){InitializeComponent();Chart.TradeSelected+=marker=>{if(DataContext is ResearchResultViewModel model){model.SelectMarker(marker);if(model.Selected!=null)Trades.ScrollIntoView(model.Selected,null);}};Trades.SelectionChanged+=(_,_)=>{if(DataContext is ResearchResultViewModel{Selected:{} selected} model){var from=selected.Fill.ExecutionTime.AddDays(-3);var to=selected.Fill.ExecutionTime.AddDays(3);if(Stock.Core.Research.ChartPeriods.Minutes(model.Period)>0){from=selected.Fill.ExecutionTime.AddHours(-2);to=selected.Fill.ExecutionTime.AddHours(2);}Chart.ZoomTo(from,to);}};}
}
