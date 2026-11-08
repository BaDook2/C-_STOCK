using Avalonia.Controls;
namespace Stock.Desktop;
public partial class LiveChartPanel:UserControl
{public LiveChartPanel(){InitializeComponent();Chart.TradeSelected+=marker=>{if(DataContext is LiveChartViewModel model){model.Selected=model.Trades.FirstOrDefault(r=>r.Marker.Id==marker.Id);if(model.Selected!=null)Trades.ScrollIntoView(model.Selected,null);}};}}
