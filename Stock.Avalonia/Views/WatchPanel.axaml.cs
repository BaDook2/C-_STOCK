using Avalonia.Controls;
namespace Stock.Desktop;
public partial class WatchPanel:UserControl
{
 public WatchPanel()=>InitializeComponent();
 private void RenameGroup(object? sender,Avalonia.Interactivity.RoutedEventArgs e)
 {
  if(sender is not Button{DataContext:WatchGroupViewModel group}||DataContext is not MainViewModel model||TopLevel.GetTopLevel(this) is not Window owner)return;
  model.SelectedWatchGroup=group;var input=new TextBox{Text=group.Name};var ok=new Button{Content="이름 변경"};var dialog=new Window{Title="그룹명 편집",Width=380,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new StackPanel{Margin=new(24),Spacing=16,Children={new TextBlock{Text="그룹 이름"},input,ok}}};ok.Click+=(_,_)=>{model.GroupDraft=input.Text??"";model.RenameWatchGroup.Execute(null);if(group.Name==model.GroupDraft)dialog.Close();};dialog.Show(owner);
 }
}