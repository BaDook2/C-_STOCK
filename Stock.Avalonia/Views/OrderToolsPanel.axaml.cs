using Avalonia.Controls;
using Avalonia.Interactivity;
namespace Stock.Desktop;
public partial class OrderToolsPanel:UserControl
{
 public OrderToolsPanel()=>InitializeComponent();
 private void OpenResearchResult(object? sender,RoutedEventArgs e)
 {if(DataContext is MainViewModel{Research.Result:{} result}&&TopLevel.GetTopLevel(this) is Window owner)new Window{Title=result.Title,Width=1250,Height=900,Content=new ResearchResultPanel{DataContext=result},WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show(owner);}
}
