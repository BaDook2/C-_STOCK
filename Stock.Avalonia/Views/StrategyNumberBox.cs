using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
namespace Stock.Desktop;
public readonly record struct DecimalEdit(string Text,int Caret);
public static class DecimalEditing
{
    /// <summary>Integer input is normal insertion. Only digits already to the right of the decimal are overwritten.</summary>
    public static DecimalEdit? Fraction(string text,int caret,string input,int scale)
    {
        var dot=text.IndexOf('.');if(dot<0||caret<=dot||input.Length!=1||!char.IsAsciiDigit(input[0]))return null;
        if(caret>dot+scale)return new(text,Math.Min(caret,text.Length));var padded=text.PadRight(dot+scale+1,'0');return new(padded[..caret]+input+padded[(caret+1)..],caret+1);
    }
    public static string Format(string text,int scale)=>decimal.TryParse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value)?value.ToString("F"+scale,CultureInfo.InvariantCulture):text;
}
public sealed class StrategyNumberBox(int scale):TextBox
{
    protected override Type StyleKeyOverride=>typeof(TextBox);
    protected override void OnTextInput(TextInputEventArgs e)
    {
        if(SelectionStart==SelectionEnd&&Text is {} current&&e.Text is {} input&&DecimalEditing.Fraction(current,CaretIndex,input,scale) is {} edit){Text=edit.Text;CaretIndex=edit.Caret;e.Handled=true;return;}base.OnTextInput(e);
    }
    protected override void OnLostFocus(FocusChangedEventArgs e){if(Text is {} text)Text=DecimalEditing.Format(text,scale);base.OnLostFocus(e);}
}
