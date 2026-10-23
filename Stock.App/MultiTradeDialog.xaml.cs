using System.Windows;
using System.Windows.Input;
namespace Stock.App;
public partial class MultiTradeDialog:Window
{
    public MultiTradeDialog(MultiTradeViewModel model)
    {
        InitializeComponent();DataContext=model;RowsGrid.BeginningEdit+=(_,e)=>{if(!model.CanEdit)e.Cancel=true;};
        void Notify(string message)=>MessageBox.Show(this,message,"다량 전략 실행 안내",MessageBoxButton.OK,MessageBoxImage.Information);
        model.NotificationRequested+=Notify;Closed+=(_,_)=>{model.NotificationRequested-=Notify;DataContext=null;};
    }
    private void SearchKey(object sender,KeyEventArgs e){if(e.Key==Key.Return||(e.Key==Key.ImeProcessed&&e.ImeProcessedKey==Key.Return)){((MultiTradeViewModel)DataContext).Actions["검색"].Execute(null);e.Handled=true;}}
}
