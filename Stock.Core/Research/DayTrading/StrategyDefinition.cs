using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

/// <summary>Exact schema-1 legacy payload or schema-2 strategy D; unknown algorithms never fall back.</summary>
public sealed record StrategyDefinition(int SchemaVersion,string StrategyId,string Preset,DateTimeOffset ExportedAt,OversoldConfig Configuration,DayTradingPolicy? DayTrading=null)
{
    public const string LegacyId="krx-oversold-rebound";
    public const string StrategyDId="strategy-d-intraday-v1";
    [System.Text.Json.Serialization.JsonIgnore] public bool IsStrategyD=>StrategyId==StrategyDId;
    public void Validate()
    {
        if(Configuration==null)throw new InvalidOperationException("전략 설정이 비어 있습니다.");
        if(SchemaVersion==1&&StrategyId==LegacyId&&DayTrading==null){Configuration.Validate();return;}
        if(SchemaVersion!=2||!IsStrategyD||DayTrading==null)throw new InvalidOperationException("지원하지 않는 전략 버전/ID입니다.");
        if(string.IsNullOrWhiteSpace(Preset)||Preset.Length>80||Preset.Any(char.IsControl))throw new InvalidOperationException("전략 이름은 1~80자로 입력하세요.");
        DayTrading.Validate(Configuration);
    }
    public StrategyDefinition Snapshot(){Validate();return Serialization.Unpack<StrategyDefinition>(Serialization.Pack(this));}
}
public static class StrategyDefinitionJson
{
    private static readonly JsonSerializerOptions Options=new(Serialization.Json){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull};
    private static readonly JsonSerializerOptions ImportOptions=CreateImportOptions();
    private static JsonSerializerOptions CreateImportOptions()
    {
        var result=new JsonSerializerOptions(Options){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
        // Keep numeric exports backward compatible; accept English enum names on import too.
        result.Converters.Add(new JsonStringEnumConverter());return result;
    }
    public static string Export(StrategyDefinition definition){definition.Validate();return JsonSerializer.Serialize(definition,Options);}
    public static StrategyDefinition Import(string json)
    {
        try
        {
            using var document=JsonDocument.Parse(json);
            if(document.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("전략 JSON 객체가 필요합니다.");
            ValidateUniqueProperties(document.RootElement,"$");
            var properties=document.RootElement.EnumerateObject().ToArray();
            foreach(var key in new[]{"schemaVersion","strategyId","configuration"})
                if(!properties.Any(p=>string.Equals(p.Name,key,StringComparison.OrdinalIgnoreCase)&&p.Value.ValueKind!=JsonValueKind.Null))throw new InvalidOperationException("전략 JSON 필수 항목이 없습니다: "+key);
            var definition=JsonSerializer.Deserialize<StrategyDefinition>(json,ImportOptions)??throw new InvalidOperationException("빈 전략 JSON입니다.");
            NormalizeTimeframeKeys(definition.Configuration);
            definition.Validate();return definition.Snapshot();
        }
        catch(JsonException ex){throw new InvalidOperationException("전략 JSON 항목 이름·형식·값을 확인하세요: "+(ex.Path??"$"),ex);}
    }
    private static void ValidateUniqueProperties(JsonElement value,string path)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var property in value.EnumerateObject())
            {
                if(!names.Add(property.Name))throw new InvalidOperationException("중복된 전략 JSON 항목입니다: "+path+"."+property.Name);
                ValidateUniqueProperties(property.Value,path+"."+property.Name);
            }
        }
        else if(value.ValueKind==JsonValueKind.Array){var i=0;foreach(var child in value.EnumerateArray())ValidateUniqueProperties(child,path+"["+(i++)+"]");}
    }
    private static void NormalizeTimeframeKeys(OversoldConfig? configuration)
    {
        if(configuration==null)return;
        if(configuration.ParameterTimeframes!=null)
        {
            var keys=ParameterCatalog.Properties(configuration).Where(p=>ParameterCatalog.For(p).Unit=="봉"||p.Name==nameof(OversoldConfig.MovingAveragePeriods)).Select(p=>p.Name).ToDictionary(k=>k,k=>k,StringComparer.OrdinalIgnoreCase);
            var normalized=new Dictionary<string,TimeframeSpec>();
            foreach(var (key,frame) in configuration.ParameterTimeframes)
            {
                if(!keys.TryGetValue(key,out var canonical))throw new InvalidOperationException("알 수 없는 전략 봉 항목입니다: parameterTimeframes."+key);
                normalized.Add(canonical,frame);
            }
            configuration.ParameterTimeframes=normalized;
        }
        if(configuration.CrashStrategy!=null)NormalizeTimeframeKeys(configuration.CrashStrategy.Configuration);
    }
}
public static class StrategyDDefaults
{
    /// <summary>Unverified experimental preset; never selected as the user's active or live default.</summary>
    public static StrategyDefinition Create(DateTimeOffset at)=>new(2,StrategyDefinition.StrategyDId,"전략 D · 실험",at,new OversoldConfig
    {
        UseRsiFilter=false,UseVolumeFilter=false,MovingAveragePeriods="240",UsePivotSupport=false,
        RequireReboundConfirmation=true,ReboundPercent=.5m,MaxEntries=1,EntryRatios="100",
        UseEmergencyStop=true,EmergencyStopLossPercent=-1,TakeProfit1Percent=1,TakeProfit2Percent=2,TrailingStopPercent=.7m,
        SupportDistancePercent=1,ClusterChangeTolerancePercent=.5m
    },new DayTradingPolicy());
}
