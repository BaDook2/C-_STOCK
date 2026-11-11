using System.Reflection;
using System.Text.Json;
using Stock.Core.Strategies.Injes;

internal static partial class Program
{
    private static void ValidateManifest(string path)
    {
        var full=Path.GetFullPath(path);var root=Directory.GetParent(Path.GetDirectoryName(full)!)!.FullName;using var manifest=JsonDocument.Parse(File.ReadAllText(full));var listed=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in manifest.RootElement.EnumerateArray())
        {
            var relative=row.GetProperty("Path").GetString()!;if(!listed.Add(relative)||row.GetProperty("HashMode").GetString()!="Utf8Lf")throw new InvalidOperationException("Duplicate/unsupported manifest path");
            var source=File.ReadAllText(Path.Combine(root,relative)).Replace("\r\n","\n");var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)));
            if(hash!=row.GetProperty("Sha256").GetString())throw new InvalidOperationException("Core source changed: "+relative);
        }
        var actual=Directory.GetFiles(Path.Combine(root,"Stock.Core"),"*",SearchOption.AllDirectories).Where(p=>Path.GetExtension(p) is ".cs" or ".csproj").Where(p=>!Path.GetRelativePath(root,p).Split(Path.DirectorySeparatorChar).Any(s=>s is "obj" or "bin")).Select(p=>Path.GetRelativePath(root,p).Replace(Path.DirectorySeparatorChar,'/')).ToHashSet(StringComparer.Ordinal);
        Check(actual.SetEquals(listed),"Core manifest covers every current source/project and excludes retired paths ("+listed.Count+" files)");
    }
    private static void ValidateGuide(string path)
    {
        var blocks=System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path),"```json\\s*([\\s\\S]*?)```",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var texts=blocks.Select(b=>b.Groups[1].Value).ToArray();
        var definitions=texts.Where(t=>JsonDocument.Parse(t).RootElement.TryGetProperty("familyId",out _)).Select(InjesStrategyJson.Import).ToArray();
        var profiles=texts.Where(t=>JsonDocument.Parse(t).RootElement.TryGetProperty("kind",out _)).Select(global::Stock.Core.Strategies.StrategyProfileJson.Import).ToArray();
        Check(profiles.Length==10&&profiles.All(p=>p.InjeStrategies.Length==4&&p.Bin.StrategyId=="bins-strategy-v1"),"single guide includes one default and nine strict complete sample profiles");
        Check(profiles.Skip(1).Count(p=>p.SelectedSystem==global::Stock.Core.Strategies.StrategySystem.Inje)==4&&profiles.Skip(1).Count(p=>p.SelectedSystem==global::Stock.Core.Strategies.StrategySystem.Bin)==5,"sample guide covers all four Inje algorithms and five Bin entry patterns");
        Check(definitions.Length==5&&definitions.Select(d=>d.StrategyId).Distinct().Count()==4,"single guide contains five valid complete imports for all four strategies");
        Check(definitions.Last().StrategyMode==StrategyMode.Standard&&definitions.Last().EffectiveAdvancedParameters.GetProperty("minRVOL").GetDecimal()==7,"guide Standard example maps strength4 to actual RVOL7 while preserving Advanced");
    }
    private static void WriteCatalog(string path)
    {
        var at=DateTimeOffset.Parse("2026-11-11T00:00:00+09:00");
        object[] Fields(object value)=>InjesParameterValidation.Properties(value).Select(p=>
        {var a=p.GetCustomAttribute<InjeParameterAttribute>()!;return(object)new{Key=JsonNamingPolicy.CamelCase.ConvertName(p.Name),a.Label,a.Description,a.Unit,Type=p.PropertyType.Name,Default=p.GetValue(value),a.Min,a.Max,a.Strictness};}).ToArray();
        var rows=StrategyRegistry.Injes.All.Select(f=>
        {var d=InjesStrategyDefinition.Create(f.StrategyId,at);return new{f.StrategyId,f.Name,Definition=JsonSerializer.Deserialize<JsonElement>(InjesStrategyJson.Export(d)),Common=Fields(d.CommonParameters),Execution=Fields(d.ExecutionPolicy),Advanced=Fields(f.Defaults()),f.StandardRules};}).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,JsonSerializer.Serialize(rows,InjesStrategyJson.Options));
    }
}
