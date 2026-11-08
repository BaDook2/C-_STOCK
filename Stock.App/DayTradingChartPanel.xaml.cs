using System.Windows.Controls;
using Stock.Core.Trading;
using Stock.Core.Research.DayTrading;
namespace Stock.App;
public partial class DayTradingChartPanel:UserControl
{
    public DayTradingChartPanel()
    {
        InitializeComponent();PriceChart.TradeMarkerClicked+=marker=>
        {if(DataContext is DayTradingViewModel model&&model.VisibleFills.FirstOrDefault(f=>DayTradingViewModel.FillKey(f.Fill)==marker.Id) is {} row){Fills.SelectedItem=row;Fills.ScrollIntoView(row);model.SelectFill(row.Fill);}};
    }
    private void FillSelected(object sender,SelectionChangedEventArgs e)
    {if(DataContext is DayTradingViewModel model&&Fills.SelectedItem is IntradayFillRow row)model.SelectFill(row.Fill);}
}
