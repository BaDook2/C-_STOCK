using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Stock.Core.Research.DayTrading;
namespace Stock.Desktop;

/// <summary>Instantiate only the active editor. Hidden empty ComboBoxes must never write null into numeric parameter text.</summary>
public sealed class SettingInput:ContentControl
{
    public SettingInput(){DataContextChanged+=(_,_)=>BuildEditor();}
    private void BuildEditor()
    {
        if(DataContext is not (LegacyParameter or DayTradingParameter)){Content=null;return;}
        var flags=DataContext switch{LegacyParameter p=>(p.IsBool,p.IsChoice),DayTradingParameter p=>(p.IsBool,p.IsChoice),_=>(false,false)};
        Control editor;
        if(flags.Item1)
        {
            var check=new CheckBox{Content="사용"};check.Bind(CheckBox.IsCheckedProperty,new Binding("BoolValue"){Mode=BindingMode.TwoWay});editor=check;
        }
        else if(flags.Item2)
        {
            var combo=new ComboBox{HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Stretch,DisplayMemberBinding=new Binding("Label"),SelectedValueBinding=new Binding("Value")};combo.Bind(ItemsControl.ItemsSourceProperty,new Binding("Choices"));combo.Bind(SelectingItemsControl.SelectedValueProperty,new Binding("Text"){Mode=BindingMode.TwoWay});editor=combo;
        }
        else
        {
            TextBox box=DataContext is LegacyParameter{IsDecimal:true} number?new StrategyNumberBox(number.Scale):new TextBox();box.Bind(TextBox.TextProperty,new Binding("Text"){Mode=BindingMode.TwoWay});editor=box;
        }
        editor.Height=32;editor.Margin=new(0);if(editor is Avalonia.Controls.Primitives.TemplatedControl input)input.Padding=new(8,4);editor.DataContext=DataContext;Content=editor;
    }
}
