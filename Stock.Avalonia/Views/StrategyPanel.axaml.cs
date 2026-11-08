using Avalonia.Controls;
namespace Stock.Desktop;
public partial class StrategyPanel:UserControl
{
 public StrategyPanel()=>InitializeComponent();
 private void HelpClick(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
 {if(sender is Button{DataContext:LegacyParameter field}&&TopLevel.GetTopLevel(this) is Window owner){var ok=new Button{Content="확인",HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};var dialog=new Window{Title=field.Metadata.DisplayName,Width=520,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new StackPanel{Margin=new(24),Spacing=20,Children={new TextBlock{Text=field.Help,TextWrapping=Avalonia.Media.TextWrapping.Wrap},ok}}};ok.Click+=(_,_)=>dialog.Close();dialog.Show(owner);}}
}
