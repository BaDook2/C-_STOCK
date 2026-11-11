using Stock.Core.Strategies.Injes;
using Stock.Desktop;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void InjesPresetChecks()
    {
        using var store=new TradingStore(System.IO.Path.Combine(output,"injes-presets-"+System.Guid.NewGuid().ToString("N")+".db"),true);
        var editor=new StrategyEditor(store);
        Check(editor.Names.Count==1&&editor.InjeChoices.Length==4,"one profile retains four Inje algorithms and Bin fields");
        foreach(var name in editor.Names){editor.Selected=name;Check(editor.Current().Injes?.FamilyId=="inje","Inje preset contains no strategy D: "+name);}
    }
}
