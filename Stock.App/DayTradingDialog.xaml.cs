using System.Windows;
namespace Stock.App;
public partial class DayTradingDialog:Window
{
    public DayTradingDialog(DayTradingViewModel model,int page=0){InitializeComponent();DataContext=model;model.DisplayedPage=page;Pages.SelectedIndex=page;}
    private void PageChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {if(e.OriginalSource==Pages&&DataContext is DayTradingViewModel model){model.DisplayedPage=Pages.SelectedIndex;if(Pages.SelectedIndex==2)model.RefreshLive();else if(Pages.SelectedIndex==1&&model.Latest is {} result)model.ShowResult(result);}}
}
