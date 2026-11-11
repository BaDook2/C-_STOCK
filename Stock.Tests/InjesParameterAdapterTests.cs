using System.IO;
using Stock.App;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void InjesParameterAdapterChecks(string output)
    {
        Directory.CreateDirectory(output);using var store=new TradingStore(Path.Combine(output,"injes-wpf-editor.db"),true);
        var presets=new InjesPresetStore(store);presets.EnsureDefaults();
        foreach(var name in presets.Names)
        {
            var definition=presets.Get(name);var editor=new InjesEditorModel(definition);var field=new ParameterField(editor.CommonFields.Single(f=>f.Path=="commonParameters.MinPrice"));field.Value="1.2345";field.Apply();
            Check(editor.Apply(name,DateTimeOffset.UtcNow).CommonParameters.MinPrice==1.2345m,"WPF parameter adapter preserves Inje decimal precision: "+definition.StrategyId);
        }
    }
}
