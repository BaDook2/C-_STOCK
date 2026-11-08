using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Stock.Core;
namespace Stock.Desktop;
public sealed class BrokerLogoConverter:IValueConverter
{
 private static readonly Dictionary<Brokerage,Bitmap> logos=[];
 public object? Convert(object? value,Type targetType,object? parameter,CultureInfo culture)
 {if(value is not Brokerage broker)return null;if(!logos.TryGetValue(broker,out var bitmap)){using var stream=AssetLoader.Open(new Uri($"avares://Stock.Avalonia/Assets/Brokers/{broker}.png"));bitmap=new Bitmap(stream);logos[broker]=bitmap;}return bitmap;}
 public object ConvertBack(object? value,Type targetType,object? parameter,CultureInfo culture)=>throw new NotSupportedException();
}
