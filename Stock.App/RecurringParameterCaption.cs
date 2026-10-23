using System.Windows;
using System.Windows.Controls;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
/// <summary>정기매수의 Label·Tooltip·Popup도 설정 모델의 동일 Metadata를 사용합니다.</summary>
public sealed class RecurringParameterCaption:StackPanel
{
    public static readonly DependencyProperty CurrentValueProperty=DependencyProperty.Register(nameof(CurrentValue),typeof(string),typeof(RecurringParameterCaption),new PropertyMetadata(""));
    public string CurrentValue {get=>(string)GetValue(CurrentValueProperty);set=>SetValue(CurrentValueProperty,value);}
    public string Parameter {get;set;}="";
    public RecurringParameterCaption(){Orientation=Orientation.Horizontal;VerticalAlignment=VerticalAlignment.Center;Loaded+=(_,_)=>Build();}
    private void Build()
    {
        if(Children.Count>0)return;var property=typeof(RecurringPurchaseConfig).GetProperty(Parameter);if(property==null)return;var metadata=ParameterCatalog.For(property);
        Children.Add(new TextBlock{Text=metadata.DisplayName,VerticalAlignment=VerticalAlignment.Center,FontSize=14});
        var button=new Button{Content="?",ToolTip=metadata.ShortDescription,Width=20,Height=20,Padding=new Thickness(0),Margin=new Thickness(4,0,0,0)};button.Click+=(_,_)=>{var dialog=new ParameterHelpDialog(metadata,CurrentValue){Owner=Window.GetWindow(this)};MinimumTextSize.SetValue(dialog,14);dialog.ShowDialog();};Children.Add(button);
    }
}
