using Avalonia.Controls;
namespace Stock.Desktop;
public partial class DayTradingDialog:Window
{
    public DayTradingDialog(){InitializeComponent();}
    public DayTradingDialog(DayTradingViewModel model,int page=0):this(){DataContext=model;model.DisplayedPage=page;Pages.SelectedIndex=page;}
    private void PageChanged(object? sender,SelectionChangedEventArgs e)
    {if(e.Source==Pages&&DataContext is DayTradingViewModel model){model.DisplayedPage=Pages.SelectedIndex;if(Pages.SelectedIndex==2)model.RefreshLive();else if(Pages.SelectedIndex==1&&model.Latest is {} result)model.ShowResult(result);}}
}
