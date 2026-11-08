using Avalonia.Controls;
using Avalonia.Input;
namespace Stock.Desktop;
public partial class HistoryPanel:UserControl
{
 public HistoryPanel()=>InitializeComponent();
 private void OpenRecord(object? sender,TappedEventArgs e) { if(DataContext is MainViewModel model && model.SelectedHistory!=null)model.OpenHistoryCommand.Execute(null); }
}
