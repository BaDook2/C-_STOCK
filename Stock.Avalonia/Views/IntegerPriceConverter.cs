using System.Globalization;
using Avalonia.Data.Converters;
using Stock.Core.Research;
namespace Stock.Desktop;
public sealed class IntegerPriceConverter:IValueConverter
{
    public object Convert(object? value,Type targetType,object? parameter,CultureInfo culture)=>value==null?"—":DisplayNumber.Price(System.Convert.ToDecimal(value,CultureInfo.InvariantCulture));
    public object ConvertBack(object? value,Type targetType,object? parameter,CultureInfo culture)=>throw new NotSupportedException();
}
