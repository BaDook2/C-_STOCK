using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace Stock.App;

/// <summary>Local reading-size floor, including templates and detached tooltips/popups.</summary>
public static class MinimumTextSize
{
    public static readonly DependencyProperty ValueProperty=DependencyProperty.RegisterAttached("Value",typeof(double),typeof(MinimumTextSize),new PropertyMetadata(0d,Changed));
    public static double GetValue(DependencyObject o)=>(double)o.GetValue(ValueProperty);
    public static void SetValue(DependencyObject o,double v)=>o.SetValue(ValueProperty,v);
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)
    {
        if(o is not FrameworkElement root||e.NewValue is not double min||min<=0)return;
        foreach(var type in new[]{typeof(TextBlock),typeof(Button),typeof(TextBox),typeof(ComboBox),typeof(ComboBoxItem),typeof(TabItem),typeof(CheckBox),typeof(RadioButton),typeof(DatePicker),typeof(DatePickerTextBox),typeof(DataGrid),typeof(ToolTip)})
        {var style=new Style(type,root.TryFindResource(type) as Style);style.Setters.Add(new Setter(type==typeof(TextBlock)?TextBlock.FontSizeProperty:Control.FontSizeProperty,min));root.Resources[type]=style;}
        var connected=new HashSet<FrameworkElement>();
        void Apply(DependencyObject node)
        {
            if(node is TextBlock t&&t.FontSize<min)t.SetCurrentValue(TextBlock.FontSizeProperty,min);
            if(node is Control c&&c.FontSize<min)c.SetCurrentValue(Control.FontSizeProperty,min);
            if(node is FrameworkElement f)
            {
                if(f.ToolTip is string text){var tip=new ToolTip{Content=text,FontSize=min};SetValue(tip,min);f.SetCurrentValue(FrameworkElement.ToolTipProperty,tip);}
                else if(f.ToolTip is ToolTip tip&&GetValue(tip)<min)SetValue(tip,min);
                if(connected.Add(f))
                {
                    if(f is ComboBox combo)combo.DropDownOpened+=(_,_)=>{if(combo.Template.FindName("PART_Popup",combo) is Popup {Child:FrameworkElement child})SetValue(child,min);};
                    if(f is DatePicker picker)picker.CalendarOpened+=(_,_)=>{if(picker.Template.FindName("PART_Popup",picker) is Popup {Child:FrameworkElement child})SetValue(child,min);};
                }
            }
            if(node is Visual)for(var i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Apply(VisualTreeHelper.GetChild(node,i));
        }
        EventHandler update=(_,_)=>Apply(root);root.Loaded+=(_,_)=>{Apply(root);root.LayoutUpdated+=update;};root.Unloaded+=(_,_)=>root.LayoutUpdated-=update;if(root.IsLoaded){Apply(root);root.LayoutUpdated+=update;}
    }
}
