using System.Windows;
using System.Windows.Controls;
namespace Stock.App;
public partial class OversoldParameterPanel:UserControl
{
    public OversoldParameterPanel()=>InitializeComponent();
    private void HelpClick(object sender,RoutedEventArgs e)
    {if(((FrameworkElement)sender).DataContext is ParameterField field)new ParameterHelpDialog(field.Metadata,field.Value){Owner=Window.GetWindow(this)}.ShowDialog();}
}
