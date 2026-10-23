using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Stock.Core.Trading;
namespace Stock.App;

/// <summary>WPF event adapter for the shared editor; focus/blur never write a rounded source value.</summary>
public static class FixedDecimalInput
{
    public static readonly DependencyProperty KindProperty=DependencyProperty.RegisterAttached("Kind",typeof(ParameterInputKind),typeof(FixedDecimalInput),new PropertyMetadata(ParameterInputKind.Text,Changed));
    public static ParameterInputKind GetKind(DependencyObject o)=>(ParameterInputKind)o.GetValue(KindProperty);
    public static void SetKind(DependencyObject o,ParameterInputKind v)=>o.SetValue(KindProperty,v);
    public static readonly DependencyProperty ErrorProperty=DependencyProperty.RegisterAttached("Error",typeof(string),typeof(FixedDecimalInput),new PropertyMetadata(""));
    public static string GetError(DependencyObject o)=>(string)o.GetValue(ErrorProperty);
    private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e)
    {
        if(o is not TextBox box)return;
        box.PreviewTextInput-=TextInput;box.PreviewKeyDown-=KeyDown;DataObject.RemovePastingHandler(box,Pasting);CommandManager.RemovePreviewExecutedHandler(box,Executed);
        if(!FixedDecimalEdit.Enabled((ParameterInputKind)e.NewValue))return;
        InputMethod.SetIsInputMethodEnabled(box,false);
        box.PreviewTextInput+=TextInput;box.PreviewKeyDown+=KeyDown;DataObject.AddPastingHandler(box,Pasting);CommandManager.AddPreviewExecutedHandler(box,Executed);
        NumericDisplay.SetPercent(box,false);
    }
    private static void Apply(TextBox box,DecimalEditResult result)
    {
        box.SetValue(ErrorProperty,result.Error);if(!result.Accepted)return;
        if(result.Commit){box.SetCurrentValue(TextBox.TextProperty,result.Text);box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();}
        box.Select(Math.Clamp(result.Caret,0,box.Text.Length),0);
    }
    private static void TextInput(object sender,TextCompositionEventArgs e)
    {var box=(TextBox)sender;if(box.IsReadOnly)return;Apply(box,FixedDecimalEdit.Type(box.Text,box.SelectionStart,box.SelectionLength,e.Text,GetKind(box)));e.Handled=true;}
    private static void KeyDown(object sender,KeyEventArgs e)
    {
        var box=(TextBox)sender;if(box.IsReadOnly)return;
        if(e.Key is Key.Back or Key.Delete){Apply(box,FixedDecimalEdit.Delete(box.Text,box.SelectionStart,box.SelectionLength,e.Key==Key.Back,GetKind(box)));e.Handled=true;}
        if(e.Key==Key.Space){box.SetValue(ErrorProperty,"숫자 입력에는 공백을 사용할 수 없습니다.");e.Handled=true;}
    }
    private static void Pasting(object sender,DataObjectPastingEventArgs e)
    {
        var box=(TextBox)sender;e.CancelCommand();if(box.IsReadOnly)return;
        var text=e.DataObject.GetData(DataFormats.UnicodeText) as string??e.DataObject.GetData(DataFormats.Text) as string;
        Apply(box,text==null?new(false,box.Text,box.CaretIndex,false,"숫자 텍스트만 붙여넣을 수 있습니다."):FixedDecimalEdit.Paste(box.Text,box.SelectionStart,box.SelectionLength,text,GetKind(box)));
    }
    private static void Executed(object sender,ExecutedRoutedEventArgs e)
    {
        if(e.Command!=ApplicationCommands.Cut)return;var box=(TextBox)sender;e.Handled=true;if(box.IsReadOnly||box.SelectionLength==0)return;
        try{Clipboard.SetText(box.SelectedText);Apply(box,FixedDecimalEdit.Delete(box.Text,box.SelectionStart,box.SelectionLength,false,GetKind(box)));}catch(System.Runtime.InteropServices.ExternalException){box.SetValue(ErrorProperty,"클립보드를 사용할 수 없어 잘라내지 않았습니다.");}
    }
}
