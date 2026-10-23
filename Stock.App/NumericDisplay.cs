using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace Stock.App;
public static class NumericDisplay
{
    public static string Format(object? value)=>value switch {decimal d=>d.ToString("#,##0.############################",CultureInfo.InvariantCulture),int i=>i.ToString("N0",CultureInfo.InvariantCulture),long l=>l.ToString("N0",CultureInfo.InvariantCulture),double d=>d.ToString("#,##0.########",CultureInfo.InvariantCulture),_=>Convert.ToString(value,CultureInfo.InvariantCulture)??""};
    public static readonly DependencyProperty GroupedProperty=DependencyProperty.RegisterAttached("Grouped",typeof(bool),typeof(NumericDisplay),new PropertyMetadata(false,Changed));
    public static bool GetGrouped(DependencyObject o)=>(bool)o.GetValue(GroupedProperty);
    public static void SetGrouped(DependencyObject o,bool v)=>o.SetValue(GroupedProperty,v);
    public static readonly DependencyProperty PercentProperty=DependencyProperty.RegisterAttached("Percent",typeof(bool),typeof(NumericDisplay),new PropertyMetadata(false));
    public static bool GetPercent(DependencyObject o)=>(bool)o.GetValue(PercentProperty);
    public static void SetPercent(DependencyObject o,bool v)=>o.SetValue(PercentProperty,v);
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)
    {if(o is TextBox box&&(bool)e.NewValue){box.Loaded+=FormatBox;box.LostKeyboardFocus+=FormatBox;box.Loaded+=DetectPercent;box.DataContextChanged+=(_,_)=>DetectPercent(box,new RoutedEventArgs());}}
    private static void DetectPercent(object sender,RoutedEventArgs e)
    {
        var box=(TextBox)sender;var path=box.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path?.Path;
        SetPercent(box,!FixedDecimalEdit.Enabled(FixedDecimalInput.GetKind(box))&&(path=="RecurringFee"||path=="Value"&&(box.DataContext is ParameterField p&&!p.IsList&&p.Metadata.Unit.Contains('%')||box.DataContext is SettingField s&&s.Label.Contains('%')&&!s.Label.Contains("bp"))));
    }
    private static void FormatBox(object sender,RoutedEventArgs e)
    {
        var box=(TextBox)sender;if(!GetGrouped(box)||box.IsKeyboardFocusWithin||FixedDecimalEdit.Enabled(FixedDecimalInput.GetKind(box))||box.DataContext is ParameterField {IsList:true})return;var binding=box.GetBindingExpression(TextBox.TextProperty);var path=binding?.ParentBinding.Path?.Path;
        bool numeric=path is "Capital" or "Quantity" or "LiveOrderLimitText" or "RecurringAmount" or "RecurringInitial" or "RecurringFee"||(path is "Value" or "InputText")&&(box.DataContext is ParameterField p&&p.Metadata.Min!=null||box.DataContext is SettingField s&&s.Metadata.Min!=null||box.DataContext is FilterEditor);
        if(numeric&&decimal.TryParse(box.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)){box.SetCurrentValue(TextBox.TextProperty,Format(n));binding?.UpdateSource();}
    }
}
/// <summary>Presentation only: never writes a rounded percent back to the source binding.</summary>
public sealed class PercentPresentation:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>decimal.TryParse(value?.ToString(),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n.ToString("N2",CultureInfo.InvariantCulture):value??"";
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
