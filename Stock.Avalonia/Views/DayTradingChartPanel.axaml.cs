using Avalonia.Controls;
using Stock.Core.Research.DayTrading;
namespace Stock.Desktop;
public partial class DayTradingChartPanel:UserControl
{
    public DayTradingChartPanel(){InitializeComponent();Chart.TradeSelected+=marker=>{if(DataContext is DayTradingViewModel model&&model.VisibleFills.FirstOrDefault(row=>DayTradingViewModel.FillKey(row.Fill)==marker.Id) is {} selected){Fills.SelectedItem=selected;Fills.ScrollIntoView(selected,null);model.SelectFill(selected.Fill);}};}
    private void FillSelected(object? sender,SelectionChangedEventArgs e){if(DataContext is DayTradingViewModel model&&Fills.SelectedItem is IntradayFillRow row)model.SelectFill(row.Fill);}
}
