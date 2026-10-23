using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Stock.Core.Research;
namespace Stock.App;
public partial class MainWindow : Window
{
    public MainWindow() : this(null) { }
    public MainWindow(WorkspaceViewModel? viewModel)
    {
        InitializeComponent();
        var keys=new BrokerCredentialStore(System.IO.Path.Combine(MainViewModel.StateDirectory,"broker-keys"));
        DataContext=viewModel??new WorkspaceViewModel(message=>MessageBox.Show(this,message,"주문 확인",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes,alert:message=>MessageBox.Show(this,message,"계좌 연결",MessageBoxButton.OK,MessageBoxImage.Information),connectionPrompt:()=>{var dialog=new ConnectionDialog(keys,id=>DataContext is WorkspaceViewModel vm?vm.PrepareCredentialReset(id):Task.CompletedTask){Owner=this};return dialog.ShowDialog()==true?dialog.Credentials:null;},saveConnection:keys.Save,defaultConnectionLoader:()=>{try{return keys.Load(keys.DefaultBroker);}catch{return null;}});
        PriceChart.RangeRequested+=(date,start)=>{if(DataContext is WorkspaceViewModel vm&&vm.CanConfigure)vm.SetChartRange(date,start);};
        PriceChart.TradeMarkerClicked+=marker=>MessageBox.Show(this,$"{marker.Source}\n{(marker.TradeNumber is {} number?$"거래 #{number} · ":"")}{(marker.Buy?"매수":"매도")} · {marker.Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss}\n가격 {(marker.Price.HasValue?$"{marker.Price:N2}":"가격 미확인")} · 수량 {marker.Quantity:N0}","차트 매매 내역",MessageBoxButton.OK,MessageBoxImage.Information);
        Closing+=OnClosing;
    }
    private bool closingAfterStop;
    private async void OnClosing(object? sender,CancelEventArgs e)
    {
        if(DataContext is not WorkspaceViewModel vm)return;
        if(closingAfterStop){vm.Dispose();return;}
        if(vm.Busy){e.Cancel=true;MessageBox.Show(this,"작업 취소를 누르고 작업이 끝난 뒤 닫아주세요.");return;}
        if(vm.MultiSessionExists){e.Cancel=true;IsEnabled=false;try{await vm.StopMulti();}catch{MessageBox.Show(this,"중지 후 취소 상태를 증권사에서 확인하세요.");}finally{IsEnabled=true;closingAfterStop=true;Close();}}else vm.Dispose();
    }
    private async void ScreeningTradeClick(object sender,RoutedEventArgs e)
    {
        if(sender is not Button {DataContext:ScreeningRow row})return;
        if(DataContext is WorkspaceViewModel vm){e.Handled=true;vm.SelectedMatch=row;await vm.OpenScreeningStock(row.Stock);}
    }
    private async void ScreeningRowDoubleClick(object sender,System.Windows.Input.MouseButtonEventArgs e)
    {
        if(sender is not DataGrid grid||grid.SelectedItem is not ScreeningRow row||DataContext is not WorkspaceViewModel vm)return;
        if(e.OriginalSource is DependencyObject origin&&ItemsControl.ContainerFromElement(grid,origin) is not DataGridRow)return;
        e.Handled=true;vm.SelectedMatch=row;await vm.OpenScreeningStock(row.Stock);
    }
    private MultiTradeDialog? multiDialog;
    private void MultiClick(object sender,RoutedEventArgs e)
    {
        if(DataContext is not WorkspaceViewModel vm||!vm.CanOpenMulti)return;
        if(multiDialog!=null){multiDialog.Activate();return;}
        var context=(sender as FrameworkElement)?.Tag as string;bool? mock=context switch{"VirtualRealtime"=>true,"Live"=>false,_=>(bool?)null};
        vm.MultiTrading.PrepareLaunchMode(mock);vm.MultiTrading.Reload();vm.MultiTrading.ReloadWatchGroups(vm.WatchGroups);multiDialog=new MultiTradeDialog(vm.MultiTrading){Owner=this};
        multiDialog.Closed+=(_,_)=>multiDialog=null;multiDialog.Show();
    }
    private async void MultiStopClick(object sender,RoutedEventArgs e)
    {if(DataContext is WorkspaceViewModel vm)try{await vm.StopMulti();}catch{MessageBox.Show(this,"신규 주문은 중지했습니다. 증권사에서 미체결 취소를 확인하세요.");}}
    private void SearchKeyUp(object sender,KeyEventArgs e)
    {
        if(e.Key!=Key.Return && !(e.Key==Key.ImeProcessed&&e.ImeProcessedKey==Key.Return))return;
        ((TextBox)sender).GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if(DataContext is WorkspaceViewModel vm)vm.Actions["검색"].Execute(null);
        e.Handled=true;
    }
    private async void StockClick(object sender,MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=MouseButton.Left)return;
        if(ItemsControl.ContainerFromElement((ListBox)sender,e.OriginalSource as DependencyObject) is ListBoxItem {DataContext:StockItem stock} && DataContext is WorkspaceViewModel vm)
            await vm.OpenStock(stock);
    }
    private async void StockKeyUp(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Return && ((ListBox)sender).SelectedItem is StockItem stock && DataContext is WorkspaceViewModel vm){e.Handled=true;await vm.OpenStock(stock);}
    }
    private void LegacyHelpClick(object sender,RoutedEventArgs e)
    {if(((FrameworkElement)sender).DataContext is SettingField field)new ParameterHelpDialog(field.Metadata,field.Value){Owner=this}.ShowDialog();}
}
