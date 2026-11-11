using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
namespace Stock.Desktop;

/// <summary>Instantiate only the active editor. Hidden empty ComboBoxes must never write null into numeric parameter text.</summary>
public sealed class SettingInput:ContentControl
{
    public SettingInput(){DataContextChanged+=(_,_)=>BuildEditor();}
    private void BuildEditor()
    {
        if(DataContext is not LegacyParameter){Content=null;return;}
        var parameter=(LegacyParameter)DataContext;var flags=(parameter.IsBool,parameter.IsChoice);
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
            TextBox box=new TextBox();box.Bind(TextBox.TextProperty,new Binding("Text"){Mode=BindingMode.TwoWay});editor=box;
        }
        editor.IsEnabled=!parameter.ReadOnly;editor.Height=32;editor.Margin=new(0);if(editor is Avalonia.Controls.Primitives.TemplatedControl input)input.Padding=new(8,4);editor.DataContext=DataContext;
        if(parameter.HasTimeUnit)
        {
            var unit=new ComboBox{Width=100,Height=32,Margin=new(4,0,0,0),DisplayMemberBinding=new Binding("Label"),SelectedValueBinding=new Binding("Value"),DataContext=DataContext};
            unit.Bind(ItemsControl.ItemsSourceProperty,new Binding("UnitChoices"));unit.Bind(SelectingItemsControl.SelectedValueProperty,new Binding("Unit"){Mode=BindingMode.TwoWay});
            var grid=new Grid{ColumnDefinitions=new("*,Auto")};Grid.SetColumn(unit,1);grid.Children.Add(editor);grid.Children.Add(unit);Content=grid;
        }
        else Content=editor;
    }
}
