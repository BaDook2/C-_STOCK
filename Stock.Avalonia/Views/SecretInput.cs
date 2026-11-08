using Avalonia.Controls;
namespace Stock.Desktop;
/// <summary>Revealing is visual only. Copy/cut cannot put a broker secret onto the global clipboard.</summary>
public sealed class SecretInput:TextBox
{
 protected override Type StyleKeyOverride=>typeof(TextBox);
 public SecretInput(){CopyingToClipboard+=(_,args)=>args.Handled=true;CuttingToClipboard+=(_,args)=>args.Handled=true;}
}
