using Avalonia.Controls;
namespace Stock.Desktop;
public partial class DayTradingDialog:Window
{
    public DayTradingDialog()=>InitializeComponent();
    public DayTradingDialog(DayTradingViewModel model,int page=0):this(){DataContext=model;model.DisplayedPage=page;if(model.Latest is {} result)model.ShowResult(result);}
}
