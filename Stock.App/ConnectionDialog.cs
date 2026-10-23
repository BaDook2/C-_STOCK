using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Stock.Core;
namespace Stock.App;

/// <summary>선택한 증권사 한 곳의 실전 연결 정보를 입력합니다. 인증·저장은 ViewModel이 수행합니다.</summary>
public sealed class ConnectionDialog : Window
{
    private readonly BrokerCredentialStore? store;
    private readonly SecretKeyInput key=new(),secret=new();
    private readonly CheckBox showKeys=new(){Content="키 표시",ToolTip="입력한 키를 표시합니다. 키 복사·잘라내기는 지원하지 않습니다."};
    private readonly CredentialResetService? reset;
    private readonly Button resetSelected=new(){Content="API 키 초기화",HorizontalAlignment=HorizontalAlignment.Right},resetAll=new(){Content="모든 API키 초기화",HorizontalAlignment=HorizontalAlignment.Right};
    private readonly TextBox account=new(),product=new(){Text="01"},mac=new();
    private readonly TextBlock notice=new(){TextWrapping=TextWrapping.Wrap},saved=new();
    private readonly StackPanel fields=new(),accountFields=new(),productFields=new(),macFields=new();
    private readonly CheckBox useDefault=new(){Content="온라인 모드 기본 계좌로 사용"};
    private readonly Button connect=new(){Content="선택한 계좌 연결",IsDefault=true};
    private readonly Dictionary<Brokerage,RadioButton> choices=[];
    private Brokerage selected=Brokerage.Kiwoom;
    public BrokerConnection? Credentials {get;private set;}
    public ConnectionDialog(BrokerCredentialStore? store=null,Func<Brokerage?,Task>? beforeReset=null)
    {
        this.store=store;reset=store==null?null:new(store,beforeReset??(_=>Task.CompletedTask));Title="실제 계좌 키 설정";Width=690;Height=790;MinHeight=660;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brushes.White;FontFamily=new("Malgun Gothic");
        var panel=new StackPanel{Margin=new Thickness(28)};var root=new DockPanel{Background=Brushes.White};Content=root;
        var footer=new StackPanel{Margin=new Thickness(28,8,28,20)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(useDefault);footer.Children.Add(resetSelected);connect.Margin=new Thickness(0,8,0,0);footer.Children.Add(connect);root.Children.Add(new ScrollViewer{Content=panel,Background=Brushes.White,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var heading=new DockPanel();DockPanel.SetDock(resetAll,Dock.Right);heading.Children.Add(resetAll);heading.Children.Add(new TextBlock{Text="어느 계좌를 연결할까요?",FontSize=25,FontWeight=FontWeights.Bold});panel.Children.Add(heading);
        panel.Children.Add(new TextBlock{Text="지금 선택한 한 계좌로만 주문해요. 다른 저장 계좌로는 주문하지 않아요.",Foreground=Brushes.SlateGray,TextWrapping=TextWrapping.Wrap,Margin=new(0,8,0,18)});
        var grid=new UniformGrid{Columns=2,Margin=new(-5,0,-5,16)};panel.Children.Add(grid);
        foreach(var info in BrokerageInfo.All.Where(b=>b.Id is not (Brokerage.Mirae or Brokerage.Samsung or Brokerage.Shinhan)))
        {
            var tile=new StackPanel{Margin=new Thickness(6)};
            tile.Children.Add(new Image{Source=new BitmapImage(new Uri($"pack://application:,,,/Stock;component/Assets/Brokers/{info.Id}.{(info.Id is Brokerage.Meritz or Brokerage.Kiwoom or Brokerage.KoreaInvestment?"png":"ico")}")),Height=34,Width=90,Stretch=Stretch.Uniform,Margin=new(0,4,0,9)});
            tile.Children.Add(new TextBlock{Text=info.Name,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center});
            tile.Children.Add(new TextBlock{Text=info.Available?"실전 API 연결":"연동 준비 필요",FontSize=10,Foreground=Brushes.SlateGray,HorizontalAlignment=HorizontalAlignment.Center,Margin=new(0,5,0,0)});
            var button=new RadioButton{Content=tile,GroupName="Brokerage",Style=TileStyle(),ToolTip=info.Notice,Tag=info.Id};
            button.Checked+=(_,_)=>SelectBroker(info);choices.Add(info.Id,button);grid.Children.Add(button);
        }
        notice.Margin=new(0,0,0,10);panel.Children.Add(notice);saved.Foreground=Brushes.SlateGray;saved.FontSize=11;panel.Children.Add(saved);panel.Children.Add(fields);
        AddField(fields,"App Key",key);AddField(fields,"Secret Key",secret);fields.Children.Add(showKeys);showKeys.Checked+=(_,_)=>{key.Reveal(true);secret.Reveal(true);};showKeys.Unchecked+=(_,_)=>{key.Reveal(false);secret.Reveal(false);};
        AddField(accountFields,"계좌번호 (한국투자: 앞 8자리 / 토스: 전체 번호, 단일 계좌는 생략 가능)",account);AddField(productFields,"계좌 상품코드 2자리",product);accountFields.Children.Add(productFields);fields.Children.Add(accountFields);
        AddField(macFields,"접속 장치 MAC 주소 (12자리)",mac);fields.Children.Add(macFields);
        mac.ToolTip="API 요청에 사용할 네트워크 장치의 실제 MAC 주소입니다. 기본값은 현재 연결된 장치입니다.";
        key.ToolTip="증권사에서 발급받은 실전 App Key";secret.ToolTip="증권사에서 발급받은 실전 Secret Key";
        fields.Children.Add(new TextBlock{Text="연결 성공 후 이 Windows 계정에 암호화해 저장합니다.\n한국투자·메리츠: 국내주식 연결 / 실시간 틱: 키움 연결 지원",Foreground=Brushes.SlateGray,FontSize=11,Margin=new(0,5,0,16)});
        connect.Click+=(_,_)=>Submit();
        resetSelected.Click+=async(_,_)=>await ResetKeys(selected);resetAll.Click+=async(_,_)=>await ResetKeys(null);resetSelected.IsEnabled=resetAll.IsEnabled=reset!=null;
        Closed+=(_,_)=>{showKeys.IsChecked=false;key.Clear();secret.Clear();account.Clear();};
        choices[store?.DefaultBroker??Brokerage.Kiwoom].IsChecked=true;
    }
    private void SelectBroker(BrokerageInfo info)
    {
        selected=info.Id;showKeys.IsChecked=false;key.Clear();secret.Clear();account.Clear();product.Text="01";mac.Clear();notice.Text=info.Notice;saved.Text="";
        fields.Visibility=info.Available?Visibility.Visible:Visibility.Collapsed;connect.Visibility=useDefault.Visibility=fields.Visibility;accountFields.Visibility=selected is Brokerage.KoreaInvestment or Brokerage.Toss?Visibility.Visible:Visibility.Collapsed;macFields.Visibility=selected==Brokerage.Meritz?Visibility.Visible:Visibility.Collapsed;
        productFields.Visibility=selected==Brokerage.KoreaInvestment?Visibility.Visible:Visibility.Collapsed;
        useDefault.IsChecked=selected==(store?.DefaultBroker??Brokerage.Kiwoom);
        if(!info.Available)return;
        try
        {
            var c=store?.Load(selected);
            if(c!=null){key.Value=c.AppKey;secret.Value=c.Secret;account.Text=c.Account;product.Text=c.Product;mac.Text=c.MacAddress;saved.Text="저장된 키를 불러왔어요. 연결할 때 실전 계좌를 다시 확인합니다.";}
            else if(selected==Brokerage.Meritz)mac.Text=NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback&&n.GetPhysicalAddress().GetAddressBytes().Length==6)?.GetPhysicalAddress().ToString()??"";
        }catch{saved.Text="저장된 키를 읽지 못했습니다. 다시 입력하면 연결 성공 후 교체합니다.";}
    }
    private async Task ResetKeys(Brokerage? broker)
    {
        if(reset==null)return;
        IsEnabled=false;
        try
        {
            if(!await reset.Reset(broker,message=>MessageBox.Show(this,message,"API 키 초기화",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)==MessageBoxResult.Yes))return;
            Credentials=null;showKeys.IsChecked=false;key.Clear();secret.Clear();account.Clear();product.Text="01";mac.Clear();saved.Text="API 키를 초기화했습니다. 다시 입력하고 연결하세요.";
        }
        catch{saved.Text="초기화를 완료하지 못했습니다. 연결 상태와 키 저장소 접근 권한을 확인하세요.";}
        finally{IsEnabled=true;}
    }
    private void Submit()
    {
        try{var c=new BrokerConnection{Brokerage=selected,AppKey=key.Value.Trim(),Secret=secret.Value.Trim(),Account=account.Text.Trim(),Product=product.Text.Trim(),MacAddress=mac.Text.Replace(":","").Replace("-","").Trim(),UseAsDefault=useDefault.IsChecked==true};c.Validate();Credentials=c;DialogResult=true;}
        catch(InvalidOperationException ex){MessageBox.Show(this,ex.Message,"계좌 설정",MessageBoxButton.OK,MessageBoxImage.Information);}
    }
    private static void AddField(Panel panel,string label,FrameworkElement input)
    {panel.Children.Add(new TextBlock{Text=label,FontSize=12,Margin=new(0,12,0,5)});if(input is Control control){control.Padding=new(9);control.BorderBrush=new SolidColorBrush(Color.FromRgb(229,232,235));control.Background=new SolidColorBrush(Color.FromRgb(249,250,251));}panel.Children.Add(input);}
    private static Style TileStyle()
    {
        var style=new Style(typeof(RadioButton));style.Setters.Add(new Setter(MarginProperty,new Thickness(5)));style.Setters.Add(new Setter(CursorProperty,System.Windows.Input.Cursors.Hand));
        var template=new ControlTemplate(typeof(RadioButton));var border=new FrameworkElementFactory(typeof(Border));border.Name="Tile";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(14));border.SetValue(Border.BorderThicknessProperty,new Thickness(2));border.SetValue(Border.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(229,232,235)));border.SetValue(Border.BackgroundProperty,Brushes.White);border.SetValue(Border.PaddingProperty,new Thickness(8));
        var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.ContentSourceProperty,"Content");border.AppendChild(content);template.VisualTree=border;
        var selected=new Trigger{Property=ToggleButton.IsCheckedProperty,Value=true};selected.Setters.Add(new Setter(Border.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(49,130,246)),"Tile"));selected.Setters.Add(new Setter(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(239,246,255)),"Tile"));template.Triggers.Add(selected);
        var focus=new Trigger{Property=IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,Brushes.SlateBlue,"Tile"));template.Triggers.Add(focus);
        style.Setters.Add(new Setter(TemplateProperty,template));return style;
    }
}
