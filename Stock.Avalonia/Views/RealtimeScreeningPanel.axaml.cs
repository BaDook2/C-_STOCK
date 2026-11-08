using Avalonia.Controls;
using Avalonia.Input;
namespace Stock.Desktop;
public partial class RealtimeScreeningPanel:UserControl
{public RealtimeScreeningPanel(){InitializeComponent();}private void OpenDoubleTapped(object? sender,TappedEventArgs args){if(DataContext is RealtimeScreeningWorkspace model)model.OpenChart.Execute(null);}}
