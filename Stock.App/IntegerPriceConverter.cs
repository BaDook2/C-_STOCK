using System.Globalization;
using System.Windows.Data;
using Stock.Core.Research;
namespace Stock.App;
public sealed class IntegerPriceConverter : IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is decimal n?DisplayNumber.Price(n):value;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>throw new NotSupportedException();
}
