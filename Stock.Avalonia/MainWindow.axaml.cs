using Avalonia.Controls;
using Avalonia.Layout;
namespace Stock.Desktop;
public partial class MainWindow:Window
{
    private MainViewModel? hooked;
    private DayTradingDialog? dayDialog;
    private AccountDialog? accountDialog;
    private VirtualExecutionDialog? virtualDialog;
    private VirtualExecutionDialog? actualDialog;
    private bool exitConfirmed,exitQuestion;
    public MainWindow()
    {
        InitializeComponent();DataContextChanged+=(_,_)=>
        {if(hooked!=null){hooked.NotificationRequested-=ShowNotice;hooked.ResultPopupRequested-=ShowResult;hooked.DayTradingPopupRequested-=ShowDayTrading;hooked.RecurringPopupRequested-=ShowRecurring;hooked.AccountPopupRequested-=ShowAccount;hooked.LiveChartPopupRequested-=ShowLive;hooked.BrowserPopupRequested-=ShowBrowser;hooked.ScreeningHistoryRequested-=ShowScreenHistory;hooked.VirtualPopupRequested-=ShowVirtual;}hooked=DataContext as MainViewModel;if(hooked!=null){hooked.NotificationRequested+=ShowNotice;hooked.ResultPopupRequested+=ShowResult;hooked.DayTradingPopupRequested+=ShowDayTrading;hooked.RecurringPopupRequested+=ShowRecurring;hooked.AccountPopupRequested+=ShowAccount;hooked.LiveChartPopupRequested+=ShowLive;hooked.BrowserPopupRequested+=ShowBrowser;hooked.ScreeningHistoryRequested+=ShowScreenHistory;hooked.VirtualPopupRequested+=ShowVirtual;hooked.Strategies.FileDialogs??=new StrategyFileDialogs(()=>this);}};
        MainPriceChart.TradeSelected+=marker=>ShowNotice($"거래 #{marker.TradeNumber} · {marker.SideLabel}\n{marker.Time.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss}\n가격 {(marker.Price is {} price?Stock.Core.Research.DisplayNumber.Price(price):"—")} · 수량 {marker.Quantity:N0}\n{marker.ProfitText}\n{marker.Reason}");
        DataContextChanged+=(_,_)=>{if(DataContext is MainViewModel model)model.Accounts.Confirm=ShowConfirmation;};
        MainPriceChart.RangeRequested+=(time,from)=>{if(hooked==null)return;if(from)hooked.Research.From=time.ToOffset(TimeSpan.FromHours(9)).Date;else hooked.Research.To=time.ToOffset(TimeSpan.FromHours(9)).Date;};
        Closing+=ConfirmProtectedExit;

        Closed+=(_,_)=>{if(hooked!=null){hooked.NotificationRequested-=ShowNotice;hooked.ResultPopupRequested-=ShowResult;hooked.DayTradingPopupRequested-=ShowDayTrading;hooked.RecurringPopupRequested-=ShowRecurring;hooked.AccountPopupRequested-=ShowAccount;hooked.LiveChartPopupRequested-=ShowLive;hooked.BrowserPopupRequested-=ShowBrowser;hooked.ScreeningHistoryRequested-=ShowScreenHistory;hooked.VirtualPopupRequested-=ShowVirtual;hooked.Dispose();}};
    }
    private void HistoryDoubleTapped(object? sender,Avalonia.Input.TappedEventArgs e){if(hooked?.SelectedHistory!=null)hooked.OpenHistoryCommand.Execute(null);}
    // RadioButton checks itself before Click. Display verified connection state,
    // rather than accepting a two-way optimistic selection before authentication.
    private void ChooseOnlineMode(object? sender,Avalonia.Interactivity.RoutedEventArgs e){hooked?.Accounts.OnlineMode.Execute(null);SyncModeSelection();}
    private void ChooseOfflineMode(object? sender,Avalonia.Interactivity.RoutedEventArgs e){hooked?.Accounts.Offline.Execute(null);SyncModeSelection();}
    private void SyncModeSelection(){if(hooked==null)return;OfflineModeButton.SetCurrentValue(RadioButton.IsCheckedProperty,!hooked.Online);OnlineModeButton.SetCurrentValue(RadioButton.IsCheckedProperty,hooked.Online);}
    private void ShowResult(ResearchResultViewModel model){var dialog=new Window{Title=model.Title,Width=1250,Height=900,Content=new ResearchResultPanel{DataContext=model},WindowStartupLocation=WindowStartupLocation.CenterOwner};dialog.Show(this);}
    private void ShowDayTrading(DayTradingViewModel model,int page){if(dayDialog!=null){dayDialog.Activate();return;}dayDialog=new(model,page);dayDialog.Closed+=(_,_)=>dayDialog=null;dayDialog.Show(this);}
    private void ShowVirtual(VirtualExecutionWorkspace model){if(model.IsActual){if(actualDialog!=null){actualDialog.Activate();return;}actualDialog=new(){DataContext=model};actualDialog.Closed+=(_,_)=>actualDialog=null;actualDialog.Show(this);return;}if(virtualDialog!=null){virtualDialog.Activate();return;}virtualDialog=new(){DataContext=model};virtualDialog.Closed+=(_,_)=>virtualDialog=null;virtualDialog.Show(this);}
    private void ShowBrowser(ResearchBrowserViewModel model){var dialog=new Window{Title="스크리닝 당시 설정 · 전략 백테스트",Width=1280,Height=950,Content=new ResearchBrowserPanel{DataContext=model},WindowStartupLocation=WindowStartupLocation.CenterOwner};dialog.Closed+=(_,_)=>model.Dispose();dialog.Show(this);}
    private async void ConfirmProtectedExit(object? sender,WindowClosingEventArgs args)
    {
        if(exitConfirmed||hooked==null||!hooked.DayTrading.HasUnsettled&&!hooked.Realtime.HasProtectedPositions&&!hooked.ActualTrading.HasProtectedPositions)return;args.Cancel=true;if(exitQuestion)return;exitQuestion=true;
        try
        {
            var yes=new Button{Content="자동 감시 중지 후 종료"};var no=new Button{Content="돌아가기",Classes={"secondary"}};var panel=new StackPanel{Margin=new(24),Spacing=16};panel.Children.Add(new TextBlock{Text="실제 또는 가상 전략의 보유분/미체결이 남아 있습니다. 종료하면 자동 감시가 중지되며 보유분을 임의청산하지 않습니다. 실제 미체결의 취소·체결 상태는 증권사에서 확인하세요. 가상 계좌는 다음 실행에서 정지 상태로 복원할 수 있습니다.",TextWrapping=Avalonia.Media.TextWrapping.Wrap});panel.Children.Add(yes);panel.Children.Add(no);var question=new Window{Title="종료 전 실행 상태 확인",Width=510,SizeToContent=SizeToContent.Height,Content=panel,WindowStartupLocation=WindowStartupLocation.CenterOwner};yes.Click+=(_,_)=>question.Close(true);no.Click+=(_,_)=>question.Close(false);if(await question.ShowDialog<bool>(this)){exitConfirmed=true;Close();}
        }
        finally{exitQuestion=false;}
    }
    private void ShowScreenHistory(ScreeningWorkspace model)=>new Window{Title="보관된 스크리닝 결과",Width=1280,Height=900,DataContext=hooked,Content=new ScreeningPanel{DataContext=model},WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show(this);
    private void ShowLive(LiveChartViewModel model)=>new Window{Title=model.Title,Width=1250,Height=900,Content=new LiveChartPanel{DataContext=model},WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show(this);
    private void ShowRecurring(RecurringResultViewModel model)=>new Window{Title=model.Title,Width=1250,Height=920,Content=new RecurringResultPanel{DataContext=model},WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show(this);
    private void ShowAccount(AccountWorkspace model){if(accountDialog!=null){accountDialog.Activate();return;}accountDialog=new(){DataContext=model};accountDialog.Closed+=(_,_)=>accountDialog=null;model.Confirm=ShowConfirmation;accountDialog.Show(this);}
    private async Task<bool> ShowConfirmation(string text)
    {
        if(!IsVisible)return false;
        var yes=new Button{Content="확인"};var no=new Button{Content="취소",Classes={"secondary"}};var panel=new StackPanel{Margin=new(24),Spacing=18};panel.Children.Add(new TextBlock{Text=text,TextWrapping=Avalonia.Media.TextWrapping.Wrap});var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8};buttons.Children.Add(no);buttons.Children.Add(yes);panel.Children.Add(buttons);
        var dialog=new Window{Title="작업 확인",Width=520,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};yes.Click+=(_,_)=>dialog.Close(true);no.Click+=(_,_)=>dialog.Close(false);return await dialog.ShowDialog<bool>(OwnedWindows.LastOrDefault(w=>w.IsVisible&&w.IsActive)??this);
    }
    private async void ShowNotice(string text)
    {
        if(!IsVisible)return;
        var ok=new Button{Content="확인",HorizontalAlignment=HorizontalAlignment.Right};var panel=new StackPanel{Margin=new(24),Spacing=20};panel.Children.Add(new TextBlock{Text=text,TextWrapping=Avalonia.Media.TextWrapping.Wrap});panel.Children.Add(ok);
        var dialog=new Window{Title="안내",Width=480,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel};ok.Click+=(_,_)=>dialog.Close();await dialog.ShowDialog(this);
    }
}
