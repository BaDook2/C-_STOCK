using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stock.Core.Trading;
namespace Stock.App;

public sealed class ParameterHelpDialog:Window
{
    public ParameterHelpDialog(ParameterMetadata m,string current)
    {
        if(m.Unit.Contains('%')&&decimal.TryParse(current,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var percent))current=percent.ToString("N2",System.Globalization.CultureInfo.InvariantCulture)+" (표시 반올림 · 편집값 보존)";
        Title=m.DisplayName;Width=510;MaxHeight=740;SizeToContent=SizeToContent.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brushes.White;
        var panel=new StackPanel{Margin=new Thickness(26)};Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        void Text(string value,int size=13,bool bold=false){panel.Children.Add(new TextBlock{Text=value,FontSize=size,FontWeight=bold?FontWeights.Bold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)});}
        Text(m.DisplayName,23,true);Text(m.ShortDescription);if(m.UsageScope.HasValue){Text("사용범위 · "+m.Category,14,true);Text(m.UsageDescription);}Text("전략에서의 역할",14,true);Text(m.DetailedDescription);Text("값을 높이면 / 켜면",14,true);Text(m.EffectWhenIncreased);Text("값을 낮추면 / 끄면",14,true);Text(m.EffectWhenDecreased);Text($"단위: {m.Unit}\n범위: {(m.Min.HasValue?$"{m.Min:#,##0.########} ~ {m.Max:#,##0.########}":"선택 / 시간 형식")}\n현재값: {current}");
        var close=new Button{Content="닫기",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,MinWidth=90};close.Click+=(_,_)=>Close();panel.Children.Add(close);
    }
}
