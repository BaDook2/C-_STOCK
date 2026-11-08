using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core.Research;
using Stock.Desktop;
internal static partial class Program
{
 private static bool Paint(IBrush? brush,string color)=>brush is ISolidColorBrush b&&b.Color==Color.Parse(color);
 private static void MoveTo(Window window,Control control,double x=.5)
 {var point=control.TranslatePoint(new(control.Bounds.Width*x,control.Bounds.Height/2),window)!.Value;window.MouseMove(point);Dispatcher.UIThread.RunJobs();}
 private static void ClickAt(Window window,Control control,double x)
 {var point=control.TranslatePoint(new(control.Bounds.Width*x,control.Bounds.Height/2),window)!.Value;window.MouseDown(point,MouseButton.Left);window.MouseUp(point,MouseButton.Left);Dispatcher.UIThread.RunJobs();}
 private static void InteractiveStateChecks()
 {
  var calls=0;var primary=new Button{Content="실제 상태 검사",Width=220,Height=40,Command=new AsyncCommand(()=>{calls++;return Task.CompletedTask;})};
  var secondary=new Button{Content="보조 버튼",Width=220,Height=40,Classes={"secondary"}};
  var disabled=new Button{Content="사용 불가",Width=220,Height=40,IsEnabled=false};
  var combo=new ComboBox{ItemsSource=new[]{"첫 번째","두 번째","세 번째"},SelectedIndex=0,Width=220};
  var expander=new Expander{Header="모형 계좌 · 체결 비용",Content=new TextBox{Text="실제 조건 입력",Height=32},Width=700};
  var check=new CheckBox{Content="전체 대상·탈락 사유 보기",Width=300,Margin=new(0)};
  var grid=new DataGrid{Height=170,ItemsSource=new[]{new StateRow("2","B"),new StateRow("1","A")},AutoGenerateColumns=false};
  grid.Columns.Add(new DataGridTextColumn{Header="코드",Binding=new Binding("Code"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});grid.Columns.Add(new DataGridTextColumn{Header="종목 이름",Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
  var scroll=new ScrollViewer{Height=80,Content=new StackPanel{Children={new Border{Height=1500,Background=Brushes.White}}},VerticalScrollBarVisibility=ScrollBarVisibility.Visible};
  var panel=new StackPanel{Margin=new(24),Spacing=12,Children={primary,secondary,disabled,combo,expander,check,grid,scroll}};var window=new Window{Content=panel,Width=850,Height=1000};window.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
  Border Shell(Button b)=>b.GetVisualDescendants().OfType<Border>().Single(p=>p.Name=="ButtonShell");
  ContentPresenter Presenter(Button b)=>b.GetVisualDescendants().OfType<ContentPresenter>().Single(p=>p.Name=="PART_ContentPresenter");
  MoveTo(window,primary);Check(Shell(primary).Opacity==.88&&(Presenter(primary).Background==null||Presenter(primary).Background is ISolidColorBrush {Color.A:0}),"button hover changes the rounded shell without Fluent painting a gray rectangle inside its label");
  var press=primary.TranslatePoint(new(primary.Bounds.Width/2,primary.Bounds.Height/2),window)!.Value;window.MouseDown(press,MouseButton.Left);Dispatcher.UIThread.RunJobs();Check(primary.IsPressed&&Shell(primary).Opacity==.76,"primary pressed state matches the WPF shell opacity without scaling content");window.MouseUp(press,MouseButton.Left);Dispatcher.UIThread.RunJobs();Check(calls==1&&!primary.IsPressed,"actual pointer release invokes the button once and restores its state");
  MoveTo(window,secondary);Check(Paint(Shell(secondary).Background,"#EDF1F5")&&Shell(secondary).Opacity==1,"secondary hover uses the WPF surface color without a second opacity tint");
  Check(Paint(Shell(disabled).Background,"#ECEFF2")&&(Presenter(disabled).Background==null||Presenter(disabled).Background is ISolidColorBrush {Color.A:0}),"disabled button paints one rounded disabled surface with no inner label rectangle");
  ClickAt(window,combo,.5);Check(combo.IsDropDownOpen,"clicking the real combo shell opens its popup");
  var popup=combo.GetVisualDescendants().OfType<Popup>().Single();var items=popup.Child!.GetVisualDescendants().OfType<ComboBoxItem>().ToArray();Check(items.Length==3,"custom dropdown generates every selectable item container");
  var chosen=items[0];var chosenRow=chosen.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="StockComboItemRow");var mark=chosen.GetVisualDescendants().OfType<Path>().Single(p=>p.Name=="StockComboCheck");Check(mark.Opacity==1&&!Paint(chosenRow.Background,"#3182F6"),"selected combo item has a blue check instead of the default blue rectangle");
  // Popup is a separate top level: send its actual pointer input through that root.
  var popupRoot=(WindowBase)TopLevel.GetTopLevel(items[1])!;var secondPoint=items[1].TranslatePoint(new(30,items[1].Bounds.Height/2),popupRoot)!.Value;popupRoot.MouseMove(secondPoint);Dispatcher.UIThread.RunJobs();Check(Paint(items[1].GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="StockComboItemRow").Background,"#F2F4F6"),"dropdown option hover uses the same light surface as WPF");popupRoot.MouseDown(secondPoint,MouseButton.Left);popupRoot.MouseUp(secondPoint,MouseButton.Left);Dispatcher.UIThread.RunJobs();Check(combo.SelectedIndex==1&&!combo.IsDropDownOpen,"mouse selection closes the dropdown and updates the actual selection");
  var header=expander.GetVisualDescendants().OfType<ToggleButton>().Single(b=>b.Name=="PART_ToggleButton");foreach(var offset in new[]{2d,8d,14d,40d}){expander.IsExpanded=false;Dispatcher.UIThread.RunJobs();ClickAt(window,header,offset/header.Bounds.Width);Check(expander.IsExpanded,"expander arrow/header hit area toggles at horizontal offset "+offset);}
  foreach(var fraction in new[]{.02,.30,.95}){check.IsChecked=false;Dispatcher.UIThread.RunJobs();ClickAt(window,check,fraction);Check(check.IsChecked==true,"checkbox toggles across its box, label and blank hit region at "+fraction);}
  var headers=grid.GetVisualDescendants().OfType<DataGridColumnHeader>().Where(h=>h.Content?.ToString() is "코드" or "종목 이름").ToArray();Check(headers.All(h=>{var text=h.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text==h.Content?.ToString());var x=text.TranslatePoint(new(text.Bounds.Width/2,0),h)!.Value.X;return Math.Abs(x-h.Bounds.Width/2)<2;}),"every grid header is centered in the full column including the sort icon area");
  var rows=grid.GetVisualDescendants().OfType<DataGridRow>().ToArray();ClickAt(window,rows[0],.6);Dispatcher.UIThread.RunJobs();var selectedRow=grid.GetVisualDescendants().OfType<DataGridRow>().First(r=>r.IsSelected);var fill=selectedRow.GetVisualDescendants().OfType<Rectangle>().Single(r=>r.Name=="BackgroundRectangle");Check(Paint(fill.Fill,"#0078D7")&&fill.Opacity==1,"active table selection matches the actual WPF screen RGB 0,120,215");primary.Focus();Dispatcher.UIThread.RunJobs();Check(Paint(fill.Fill,"#F0F0F0")&&fill.Opacity==1,"unfocused table selection matches the actual WPF screen RGB 240,240,240");
  var scrollbar=scroll.GetVisualDescendants().OfType<ScrollBar>().Single(b=>b.Orientation==Avalonia.Layout.Orientation.Vertical);Check(!scrollbar.GetVisualDescendants().OfType<RepeatButton>().Any(b=>b.Name is "PART_LineUpButton" or "PART_LineDownButton"),"slim scrollbar contains no system arrow buttons");var thumb=scrollbar.GetVisualDescendants().OfType<Thumb>().Single();var start=thumb.TranslatePoint(new(thumb.Bounds.Width/2,thumb.Bounds.Height/2),window)!.Value;window.MouseDown(start,MouseButton.Left);window.MouseMove(start+new Vector(0,35));window.MouseUp(start+new Vector(0,35),MouseButton.Left);Dispatcher.UIThread.RunJobs();Check(scroll.Offset.Y>0,"custom scrollbar thumb still scrolls the content with actual pointer dragging");
  combo.IsEditable=true;Dispatcher.UIThread.RunJobs();var editor=combo.GetVisualDescendants().OfType<TextBox>().Single(t=>t.Name=="PART_EditableTextBox");editor.Focus();editor.Text="편집한 전략 이름";Dispatcher.UIThread.RunJobs();var editorBorder=editor.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="PART_BorderElement");Check(editorBorder.BorderThickness==new Thickness(0)&&combo.Text=="편집한 전략 이름","editable strategy combo keeps text and one outer border when focused (border="+editorBorder.BorderThickness+", text="+combo.Text+")");
  window.Close();SectorCatalogChecks();
 }
 private sealed record StateRow(string Code,string Name);
 private sealed class SectorMarket:IPublicMarketData
 {
  private readonly MarketFixture source=new();internal int Calls;
  public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>source.Search(name,region,token);
  public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token){Calls++;return Task.FromResult<IReadOnlyList<StockItem>>([new("005930","삼성전자",Stock.Core.Trading.Exchange.KRX,"005930","반도체"){Board="KOSPI"},new("000660","분야 비교",Stock.Core.Trading.Exchange.KRX,"000660","금융"){Board="KOSPI"}]);}
  public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>source.Describe(stock,token);
  public Task<BarDataset> History(StockItem stock,CancellationToken token)=>source.History(stock,token);
 }
 private static void SectorCatalogChecks()
 {
  var source=new SectorMarket();using var services=new AppServices(System.IO.Path.Combine(output,"sector-isolated",Guid.NewGuid().ToString("N")),source);using var model=new MainViewModel(services);var screening=model.Screening;screening.LoadCatalog.Execute(null);Pump(()=>!screening.Busy);Check(screening.Sectors.SequenceEqual(new[]{"전체","금융","반도체"}),"sector combo is populated from the existing public universe metadata");screening.Sector="반도체";screening.ShowAll=true;screening.Start.Execute(null);Pump(()=>!screening.Busy);Check(source.Calls==1&&screening.Snapshot is {Results.Length:1}&&screening.Snapshot.Targets[0].Symbol=="005930","sector screening filters the frozen target list and reuses the in-memory catalog");screening.LoadCatalog.Execute(null);Pump(()=>!screening.Busy);Check(source.Calls==2,"explicit list/sector refresh performs a new provider request rather than silently reusing the cache");
 }
}
