using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Injes;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void StrategyValueChecks()
    {
        using var store=new TradingStore(Path.Combine(output,"json-values-"+Guid.NewGuid().ToString("N")+".db"),true);var editor=new StrategyEditor(store);int changedInje=0,changedBin=0;
        string Different(LegacyParameter f)
        {
            if(f.IsBool)return(!f.BoolValue).ToString();if(f.IsChoice)return f.Choices.First(c=>c.Value!=f.Text).Value;
            if(f.Path.EndsWith("SupportSources"))return "MA,Pivot";if(f.Path.EndsWith("MovingAveragePeriods"))return "60,120,240";
            if(f.Path=="strategyName"||f.Path.EndsWith(".source"))return f.Text+" test";
            if(f.Path.EndsWith(".knownAt"))return DateTimeOffset.Parse(f.Text,CultureInfo.InvariantCulture).AddSeconds(1).ToString("O");
            if(f.Path.EndsWith(".from"))return DateOnly.Parse(f.Text,CultureInfo.InvariantCulture).AddDays(1).ToString("yyyy-MM-dd");
            if(f.Path.EndsWith(".until"))return DateOnly.Parse(f.Text,CultureInfo.InvariantCulture).AddDays(2).ToString("yyyy-MM-dd");
            var old=decimal.TryParse(f.Text,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:(decimal)(f.Metadata.Min??0);
            var step=f.Metadata.InputKind==ParameterInputKind.Integer||f.HasTimeUnit&&f.Path.EndsWith("Bar")?1m:.013m;
            // Frame editors use integer values, while elapsed durations may be fractional.
            if(f.HasTimeUnit&&f.UnitChoices.Any(c=>c.Value=="TradingDay"))step=1;
            var value=old+step;if(f.Metadata.Max is {} max&&value>(decimal)max)value=old-step;
            return value.ToString("0.####################",CultureInfo.InvariantCulture);
        }
        JsonNode? Read(JsonNode node,string path)
        {foreach(var part in path.Split('.'))node=node is JsonArray a?a[int.Parse(part)]!:node.AsObject().Single(p=>p.Key.Equals(part,StringComparison.OrdinalIgnoreCase)).Value!;return node;}
        foreach(var id in editor.InjeChoices.Select(c=>c.Value))
        {
            editor.InjeId=id;
            foreach(var f in editor.InjeGroups.SelectMany(g=>g.Fields).Where(f=>f.Path!="strategyMode").ToArray())
            {
                var original=f.Text;var next=Different(f);f.Text=next;var d=editor.CurrentDefinition();var packed=InjesStrategyJson.Export(d);var round=InjesStrategyJson.Import(packed);var path=f.Path=="options.InitialCash"?"initialCash":f.Path;
                var a=Read(JsonNode.Parse(InjesStrategyJson.Export(round))!,path)?.ToString();
                Check(f.IsBool?bool.Parse(a!)==bool.Parse(next):f.IsText&&decimal.TryParse(next,out var number)?decimal.Parse(a!,CultureInfo.InvariantCulture)==number:a==next,"edited Inje value survives JSON: "+id+"."+path);changedInje++;f.Text=original;
            }
            editor.CurrentDefinition();
            editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="strategyMode").Text="Standard";
            foreach(var f in editor.InjeGroups.Last().Fields)
            {
                var original=f.Text;f.Text="4";var d=InjesStrategyJson.Import(InjesStrategyJson.Export(editor.CurrentDefinition()));
                Check(d.StandardParameters[f.Path.Split('.')[1]]==4,"edited Inje Standard strength survives JSON: "+id+"."+f.Path);changedInje++;f.Text=original;
            }
            editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="strategyMode").Text="Advanced";
        }
        var seed=new BinsEditorModel(new(){Enabled=false});seed.AddFee("krx");seed.AddFee("us");var baseBin=seed.Apply(false);var all=new BinsEditorModel(baseBin).Fields;
        foreach(var field in all.Where(f=>!f.ReadOnly&&(!f.IsChoice||f.Choices.Length>1)&&!f.Path.StartsWith("entries.patternPriority.")))
        {
            var model=new BinsEditorModel(baseBin);var f=model.Fields.Single(x=>x.Path==field.Path);var view=new LegacyParameter(f);var next=Different(view);view.Text=next;
            var round=BinsJson.Import(BinsJson.Export(model.Apply(false)));var tree=JsonNode.Parse(BinsJson.Export(round))!;
            var path=f.HasTimeUnit?f.Path+".value":f.Path;
            if(path.StartsWith("market.enabledExchanges."))Check(!round.Market.EnabledExchanges.Select(x=>x.ToString()).Contains(path.Split('.')[^1]),"edited Bin exchange checkbox survives JSON: "+path);
            else {var actual=Read(tree,path)?.ToString();Check(path.EndsWith(".knownAt")?DateTimeOffset.Parse(actual!,CultureInfo.InvariantCulture)==DateTimeOffset.Parse(next,CultureInfo.InvariantCulture):f.IsBool?bool.Parse(actual!)==bool.Parse(next):f.IsText&&decimal.TryParse(next,out var number)?decimal.Parse(actual!,CultureInfo.InvariantCulture)==number:actual==next,"edited Bin value survives JSON: "+path);}changedBin++;
        }
        var priority=new BinsEditorModel(baseBin);var one=priority.Fields.Single(f=>f.Path=="entries.patternPriority.0");var two=priority.Fields.Single(f=>f.Path=="entries.patternPriority.1");(one.Text,two.Text)=(two.Text,one.Text);var permuted=BinsJson.Import(BinsJson.Export(priority.Apply(false)));Check(permuted.Entries.PatternPriority[0]==baseBin.Entries.PatternPriority[1]&&permuted.Entries.PatternPriority[1]==baseBin.Entries.PatternPriority[0],"edited pattern priority permutation survives JSON");
        editor.UseBin=true;editor.AddUsdFee.Execute(null);editor.BinGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="regime.confirmBars").Text="3";editor.BinCashUsd="12345.67";editor.BinCashKrw="7654321";editor.Save.Execute(null);
        var file=Path.Combine(output,"actual-profile-export.json");editor.FileDialogs=new FixtureStrategyFiles(file);editor.Export.Execute(null);Pump(()=>File.Exists(file));var exported=StrategyProfileJson.Import(File.ReadAllText(file));
        Check(exported.Bin.Regime.ConfirmBars==3&&exported.BinInitialCashUsd==12345.67m&&exported.BinInitialCashKrw==7654321,"actual save/download retains changed nested Bin fields and both currency balances");
        editor.Import.Execute(null);Pump(()=>editor.Names.Count==2);var loaded=new StrategyEditor(store);var after=loaded.CurrentProfile();
        Check(after.SelectedSystem==StrategySystem.Bin&&after.Bin.Regime.ConfirmBars==3&&after.BinInitialCashUsd==12345.67m&&after.InjeStrategies.Length==4,"file import and editor restart retain both systems and active selection");
        var expectedNode=JsonSerializer.SerializeToNode(exported,Stock.Core.Strategies.Bins.BinsJson.Options)!;var actualNode=JsonSerializer.SerializeToNode(after,Stock.Core.Strategies.Bins.BinsJson.Options)!;actualNode["name"]=expectedNode["name"]!.DeepClone();
        foreach(var node in actualNode["injeStrategies"]!.AsArray()){var match=expectedNode["injeStrategies"]!.AsArray().Single(n=>n!["strategyId"]!.ToString()==node!["strategyId"]!.ToString());node!["updatedAt"]=match!["updatedAt"]!.DeepClone();}
        Check(JsonNode.DeepEquals(expectedNode,actualNode),"all profile leaves, including inactive and internal policy values, survive file export/import/restart");
        var root=JsonNode.Parse(File.ReadAllText(file))!;var before=loaded.Names.Count;foreach(var mutate in new Action<JsonNode>[]
        {
            n=>n["bin"]!["entries"]!["downtrendRebound"]!.AsObject().Remove("lowStabilization"),
            n=>n["bin"]!["quality"]!["maxTradeAge"]!["unit"]="Month",
            n=>n["injeStrategies"]![0]!["strategyParameters"]!["unexpected"]=1,
            n=>n["selectedSystem"]="Both",
            n=>n["bin"]!["enabled"]=false,
            n=>n["bin"]!["regime"]!["confirmBars"]="three"
        })
        {var bad=root.DeepClone();mutate(bad);bool rejected=false;try{loaded.ImportText(bad.ToJsonString());}catch(Exception e)when(e is InvalidOperationException or JsonException){rejected=true;}Check(rejected&&loaded.Names.Count==before,"invalid profile cannot overwrite saved settings: "+bad["selectedSystem"]);}
        Check(changedInje>130&&changedBin>140,"parameter audit exercises changed values across every editable scalar group (Inje "+changedInje+", Bin "+changedBin+")");
    }
}
