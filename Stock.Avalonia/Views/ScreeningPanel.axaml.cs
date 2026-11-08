using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
namespace Stock.Desktop;
public partial class ScreeningPanel:UserControl
{
    public ScreeningPanel(){InitializeComponent();}
    private void StockNameClick(object? sender,RoutedEventArgs e){if(DataContext is ScreeningWorkspace model&&sender is Control{DataContext:ScreeningDisplayRow row}){model.Selected=row;model.OpenResult.Execute(null);e.Handled=true;}}
    private void ResultDoubleTapped(object? sender,TappedEventArgs e){if(DataContext is ScreeningWorkspace model&&model.Selected!=null)model.OpenResult.Execute(null);}
    private void FunnelClick(object? sender,RoutedEventArgs e)
    {
        if(DataContext is not ScreeningWorkspace model||TopLevel.GetTopLevel(this) is not Window owner)return;
        var grid=new DataGrid{ItemsSource=model.Funnel,AutoGenerateColumns=false,Margin=new(20)};
        foreach(var (title,path) in new[]{("조건","Condition"),("통과","Passed"),("탈락","Failed"),("미평가","NotEvaluated"),("자료부족","MissingData"),("사용안함","Disabled")})grid.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(path),Width=path=="Condition"?new DataGridLength(2,DataGridLengthUnitType.Star):new DataGridLength(1,DataGridLengthUnitType.Star)});
        var stocks=new DataGrid{ItemsSource=model.Rows,AutoGenerateColumns=false};stocks.Bind(DataGrid.SelectedItemProperty,new Binding(nameof(ScreeningWorkspace.Selected)){Mode=BindingMode.TwoWay});
        foreach(var (title,path) in new[]{("코드","Code"),("종목","Name"),("현재가","Price"),("상태","State")})stocks.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(path),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        stocks.DoubleTapped+=(_,_)=>model.OpenResult.Execute(null);
        var conditions=new DataGrid{AutoGenerateColumns=false};conditions.Bind(ItemsControl.ItemsSourceProperty,new Binding(nameof(ScreeningWorkspace.SelectedConditions)));
        foreach(var (title,path) in new[]{("조건","Condition"),("결과","Outcome"),("계산값","Value"),("기준값","Threshold"),("사유","Reason")})conditions.Columns.Add(new DataGridTextColumn{Header=title,Binding=new Binding(path),Width=new DataGridLength(path=="Reason"?2:1,DataGridLengthUnitType.Star)});
        var detail=new Grid{RowDefinitions=new RowDefinitions("*,*"),Margin=new(16)};detail.Children.Add(stocks);Grid.SetRow(conditions,1);detail.Children.Add(conditions);
        var tabs=new TabControl{DataContext=model,Items={new TabItem{Header="단계별 집계",Content=grid},new TabItem{Header="종목별 조건 · 체결 차트",Content=detail}}};
        new Window{Title="스크리닝 결과 · "+model.SnapshotLabel,Width=1150,Height=780,Content=tabs,WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show(owner);
    }
}
