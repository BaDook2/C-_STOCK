using System.IO;
using System.Text;
using System.Text.Json;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record OversoldPresetEnvelope(int SchemaVersion,string StrategyId,string Preset,DateTimeOffset ExportedAt,OversoldConfig Configuration);
public static class OversoldPresetExport
{
    private static readonly JsonSerializerOptions Options=new(Serialization.Json){WriteIndented=true,PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static string SafeName(string? preset,DateOnly day)
    {
        var invalid=Path.GetInvalidFileNameChars().ToHashSet();
        var clean=new string((preset??"").Trim().Where(ch=>!invalid.Contains(ch)&&!char.IsControl(ch)).ToArray()).Trim(' ','.');
        if(clean.Length>60)clean=clean[..60].Trim(' ','.');
        if(clean.Length==0||new[]{"CON","PRN","AUX","NUL","COM1","LPT1"}.Contains(clean,StringComparer.OrdinalIgnoreCase))clean="낙주-전략";
        return clean+"-"+day.ToString("yyyyMMdd")+".json";
    }
    public static OversoldPresetEnvelope Snapshot(string? preset,OversoldConfig config,DateTimeOffset at)
    {
        var clone=config.Clone();clone.Validate();return new(1,"krx-oversold-rebound",preset?.Trim()??"",at.ToOffset(TimeSpan.FromHours(9)),clone);
    }
    public static string Json(OversoldPresetEnvelope envelope)=>JsonSerializer.Serialize(envelope,Options);
    public static OversoldPresetEnvelope Parse(string json)
    {
        var result=JsonSerializer.Deserialize<OversoldPresetEnvelope>(json,Options)??throw new InvalidOperationException("전략 JSON이 비어 있습니다.");
        if(result.SchemaVersion!=1||result.StrategyId!="krx-oversold-rebound")throw new InvalidOperationException("지원하지 않는 전략 JSON입니다.");
        result.Configuration.Validate();return result;
    }
    public static OversoldPresetEnvelope Read(string file)
    {
        if(string.IsNullOrWhiteSpace(file)||!string.Equals(Path.GetExtension(file),".json",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("전략 JSON 파일을 선택하세요.");
        return Parse(File.ReadAllText(file,Encoding.UTF8));
    }
    public static bool Write(string? file,OversoldPresetEnvelope envelope)
    {
        if(string.IsNullOrWhiteSpace(file))return false;
        if(!string.Equals(Path.GetExtension(file),".json",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("JSON 파일로 저장하세요.");
        File.WriteAllText(file,Json(envelope),new UTF8Encoding(false));return true;
    }
}
