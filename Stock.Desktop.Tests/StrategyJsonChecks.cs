using System.Text.Json.Nodes;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Bins;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void StrategyJsonChecks()
    {
        using var store=new TradingStore(Path.Combine(output,"strategy-json-"+Guid.NewGuid().ToString("N")+".db"),true);var editor=new StrategyEditor(store);
        foreach(var id in editor.InjeChoices.Select(c=>c.Value)){editor.InjeId=id;var d=editor.CurrentDefinition();Check(InjesStrategyJson.Export(InjesStrategyJson.Import(InjesStrategyJson.Export(d)))==InjesStrategyJson.Export(d),"Inje native JSON retains "+id);}
        editor.UseBin=true;editor.AddUsdFee.Execute(null);editor.BinGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="market.countryProfiles.us.plannedInvestment").Text="1234.56";
        var p=editor.CurrentProfile();var json=StrategyProfileJson.Export(p);Check(StrategyProfileJson.Export(StrategyProfileJson.Import(json))==json,"complete profile preserves both systems, four algorithms, disabled values, fees and selection");
        var before=editor.Names.Count;editor.ImportText(json);Check(editor.Names.Count==before+1&&editor.UseBin&&editor.Current().Bins!.Market.CountryProfiles.US.PlannedInvestment==1234.56m,"actual import registers an entire profile under a unique name");
        Check(new StrategyEditor(store).Selected==editor.Selected&&new StrategyEditor(store).UseBin,"restart restores the selected custom profile and strategy system");
        editor.ImportText(InjesStrategyJson.Export(p.InjeStrategies[0]));Check(editor.UseInje&&!editor.UseBin&&editor.CurrentProfile().Bin.Market.CountryProfiles.US.PlannedInvestment==1234.56m,"raw Inje JSON import preserves Bin draft");
        editor.ImportText(BinsJson.Export(p.Bin with{Enabled=false}));Check(!editor.UseBin&&!editor.UseInje,"raw disabled Bin JSON is retained without silently enabling another system");
        var unchanged=editor.Names.Count;bool invalid=false;try{editor.ImportText(json.Replace("\"kind\": \"stock-strategy-profile\"","\"kind\": \"stock-strategy-profile\",\"extra\":1"));}catch(InvalidOperationException){invalid=true;}Check(invalid&&editor.Names.Count==unchanged,"unknown import key cannot mutate saved profiles");
        var model=new BinsEditorModel(p.Bin);var paths=model.Fields.Select(f=>f.Path).ToHashSet();var leaves=new List<string>();void Walk(JsonNode? node,string path){if(node is JsonObject o)foreach(var x in o)Walk(x.Value,path==""?x.Key:path+"."+x.Key);else if(node is JsonArray a)for(int i=0;i<a.Count;i++)Walk(a[i],path+"."+i);else leaves.Add(path);}Walk(JsonNode.Parse(BinsJson.Export(p.Bin)),"");
        foreach(var leaf in leaves.Where(l=>l is not("schemaVersion" or "strategyId" or "enabled")&&!l.StartsWith("market.enabledExchanges.")))Check(paths.Contains(leaf)||paths.Contains(leaf.EndsWith(".value")?leaf[..^6]:leaf.EndsWith(".unit")?leaf[..^5]:""),"Bin JSON field is represented in UI: "+leaf);
        var bad=model.Fields.Single(f=>f.Path=="regime.minimumBars");bad.Text="1.5";bool rejected=false;try{model.Apply(true);}catch(InvalidOperationException){rejected=true;}Check(rejected,"fractional count is rejected without changing a running config");
        File.WriteAllText(Path.Combine(output,"strategy-profile-example.json"),json);
        File.WriteAllText(Path.Combine(output,"strategy-profile-default.json"),StrategyProfileJson.Export(StrategyProfile.Create()));
    }
}
internal sealed class FixtureStrategyFiles(string file):IStrategyFileDialogs
{
    public Task<string?> Import()=>Task.FromResult<string?>(file);
    public Task<string?> Export(string name)=>Task.FromResult<string?>(file);
}
