using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public sealed record StandardRule(string Key,string Label,string AdvancedKey,decimal[] Values)
{
    public decimal Map(int value)=>value is >=1 and <=5?Values[value-1]:throw new InvalidOperationException(Label+": 강도는1~5입니다.");
}
public interface IStrategyFactory
{
    string StrategyId {get;} string Name {get;} Type ParametersType {get;}
    object Defaults(); StandardRule[] StandardRules {get;}
    object Resolve(InjesStrategyDefinition definition);
    IStrategyRuntime Create(InjesStrategyDefinition definition);
    int[] RequiredWindows(object effectiveParameters);
}
public sealed class StrategyFactory<T>(string id,string name,StandardRule[] rules,
    Func<T,IStrategyRuntime> create,Func<T,int[]> windows):IStrategyFactory where T:class,new()
{
    public string StrategyId=>id; public string Name=>name; public Type ParametersType=>typeof(T);
    public StandardRule[] StandardRules=>rules;
    public object Defaults()=>new T();
    public object Resolve(InjesStrategyDefinition definition)
    {
        var value=definition.StrategyParameters.Deserialize<T>(InjesStrategyJson.Options)??throw new InvalidOperationException("전략 전용 파라미터가 없습니다.");
        InjesParameterValidation.Validate(value);
        if(definition.StrategyMode==StrategyMode.Standard)
        {
            if(definition.StandardParameters.Count!=rules.Length||definition.StandardParameters.Keys.Any(k=>!rules.Any(r=>r.Key==k)))throw new InvalidOperationException("Standard 파라미터명이 일치하지 않습니다.");
            foreach(var rule in rules)
            {var property=typeof(T).GetProperty(rule.AdvancedKey)!;var number=rule.Map(definition.StandardParameters[rule.Key]);property.SetValue(value,property.PropertyType==typeof(int)?(object)checked((int)number):number);}
        }
        InjesParameterValidation.Validate(value);return value;
    }
    public IStrategyRuntime Create(InjesStrategyDefinition definition)=>create((T)Resolve(definition));
    public int[] RequiredWindows(object p)=>windows((T)p);
}

/// <summary>Family-scoped plugin registry. Adding a strategy does not change session dispatch or other strategies.</summary>
public sealed class StrategyRegistry
{
    private readonly Dictionary<string,IStrategyFactory> factories=new(StringComparer.Ordinal);
    public string FamilyId {get;}
    public StrategyRegistry(string familyId){if(string.IsNullOrWhiteSpace(familyId))throw new ArgumentException(nameof(familyId));FamilyId=familyId;}
    public void Register(IStrategyFactory factory){if(!factories.TryAdd(factory.StrategyId,factory))throw new InvalidOperationException("중복 전략 ID: "+factory.StrategyId);}
    public IStrategyFactory Get(string id)=>factories.GetValueOrDefault(id)??throw new InvalidOperationException("지원하지 않는 "+FamilyId+" 전략 ID: "+id);
    public IStrategyFactory[] All=>factories.Values.ToArray();
    public static StrategyRegistry Injes {get;}=Create();
    private static StrategyRegistry Create()
    {
        var r=new StrategyRegistry(InjesStrategyDefinition.InjesFamily);
        r.Register(new StrategyFactory<MomentumPullbackParameters>("momentum-pullback","급등주 눌림목 매매",
        [new("dayGain","당일 상승 강도","MinDayGainPercent",[5,7,10,15,20]),new("momentum","단기 상승 강도","MinMomentumPercent",[1,2,3,4,5]),new("volume","거래량 강도","MinRVOL",[2,3,5,7,10]),new("pullback","눌림 강도","MinPullbackPercent",[1,2,3,4,5]),new("stability","안정 대기","LowStabilitySeconds",[20,40,60,90,120]),new("reacceleration","재유입 강도","ReaccelerationVolumeRatio",[1,1.25m,1.5m,2,3])],p=>new MomentumPullbackRuntime(p),p=>[10,40,p.MomentumWindowSeconds]));
        r.Register(new StrategyFactory<QuoteScalpingParameters>("quote-scalping","호가 스프레드 틱떼기",
        [new("trades","체결 빈도","MinTradesPerSecond",[.5m,1,2,5,10]),new("shares","체결 주수","MinSharesPerSecond",[25,50,100,250,500]),new("quotes","호가 변경 빈도","MinQuoteChangesPerSecond",[.1m,.5m,1,2,5]),new("imbalance","잔량 불균형","MinOrderBookImbalance",[0,.1m,.2m,.3m,.5m]),new("spread","최소 스프레드","MinSpreadTicks",[1,1,2,3,4]),new("decay","유동성 청산","VolumeDecayRatio",[.1m,.2m,.3m,.4m,.5m])],p=>new QuoteScalpingRuntime(p),p=>[p.VolumeLookbackSeconds]));
        r.Register(new StrategyFactory<CrashReboundParameters>("crash-rebound","폭락주 초단기 반등",
        [new("crash","급락 강도","MinCrashPercent",[20,35,50,60,70]),new("velocity","급락 속도","MinCrashVelocity",[0,0,0,.01m,.02m]),new("volume","급락 거래량","CrashVolumeRatio",[1,1.5m,2,3,5]),new("stability","안정 대기","LowStabilitySeconds",[20,40,60,90,120]),new("rebound","반등 강도","MinReboundPercent",[.2m,.3m,.5m,.75m,1]),new("chase","추격 상한","MaxChasePercent",[2,1.5m,1,.75m,.5m])],p=>new CrashReboundRuntime(p),p=>[p.CrashLookbackSeconds,p.CrashLookbackSeconds*2]));
        r.Register(new StrategyFactory<SupportZoneParameters>("support-zone","저유동성 지지구간 매매",
        [new("cluster","군집 거리","SupportClusterRangePercent",[10,7,5,3,1]),new("count","독립 근거 수","MinimumSupportCount",[1,2,2,3,4]),new("distance","접근 거리","SupportDistancePercent",[5,4,3,2,1]),new("rebound","반등 강도","ReboundPercent",[1,2,3,4,5]),new("liquidity","거래대금 상한","MaxAverageDailyTradingValue",[5000000,3000000,2000000,1000000,500000]),new("break","지지 손절폭","SupportBreakPercent",[5,4,3,2,1])],p=>new SupportZoneRuntime(p),p=>[10]));
        return r;
    }
}

public sealed class InjesStrategyDefinition
{
    public const string InjesFamily="inje";
    public int SchemaVersion {get;set;}=1;
    public string FamilyId {get;set;}=InjesFamily;
    public string StrategyId {get;set;}="momentum-pullback";
    public string StrategyName {get;set;}="Inje's Strategy · 급등주 눌림목 매매";
    public decimal InitialCash {get;set;}=10000;
    public StrategyMode StrategyMode {get;set;}=StrategyMode.Advanced;
    public InjesCommonParameters CommonParameters {get;set;}=new();
    public JsonElement StrategyParameters {get;set;}
    public Dictionary<string,int> StandardParameters {get;set;}=[];
    public JsonElement EffectiveAdvancedParameters {get;set;}
    public InjesExecutionPolicy ExecutionPolicy {get;set;}=new();
    public DateTimeOffset CreatedAt {get;set;}
    public DateTimeOffset UpdatedAt {get;set;}
    public static InjesStrategyDefinition Create(string id,DateTimeOffset at)
    {
        var f=StrategyRegistry.Injes.Get(id);var d=new InjesStrategyDefinition{StrategyId=id,StrategyName="Inje's Strategy · "+f.Name,StrategyParameters=JsonSerializer.SerializeToElement(f.Defaults(),f.ParametersType,InjesStrategyJson.Options),StandardParameters=f.StandardRules.ToDictionary(r=>r.Key,_=>3),CreatedAt=at,UpdatedAt=at};
        if(id=="crash-rebound"){d.CommonParameters.TakeProfitPercent=5;d.CommonParameters.StopLossPercent=2.5m;}
        // Quote/support exits are experimental profiles, not performance-validated values.
        if(id=="quote-scalping"){d.CommonParameters.TakeProfitPercent=.5m;d.CommonParameters.StopLossPercent=.5m;d.CommonParameters.MaxHoldingSeconds=30;}
        if(id=="support-zone"){d.CommonParameters.TakeProfitPercent=3;d.CommonParameters.StopLossPercent=3;d.CommonParameters.MaxHoldingSeconds=300;d.CommonParameters.MinDailyTradingValue=10000;}
        d.RefreshEffective();return d;
    }
    public void RefreshEffective()=>EffectiveAdvancedParameters=JsonSerializer.SerializeToElement(StrategyRegistry.Injes.Get(StrategyId).Resolve(this),InjesStrategyJson.Options);
    public void Validate()
    {
        if(SchemaVersion!=1||FamilyId!=InjesFamily||!Enum.IsDefined(StrategyMode)||string.IsNullOrWhiteSpace(StrategyName)||StrategyName.Length>80||StrategyName.Any(char.IsControl)||CreatedAt==default||UpdatedAt<CreatedAt||CommonParameters==null||ExecutionPolicy==null||StandardParameters==null)throw new InvalidOperationException("Inje 전략의 버전·체계·이름·모드·시각을 확인하세요.");
        if(InitialCash<=0||InitialCash>1000000000000m)throw new InvalidOperationException("초기 가상 현금은 양의 USD 값이어야 합니다.");
        InjesParameterValidation.Validate(CommonParameters);InjesParameterValidation.Validate(ExecutionPolicy);
        var f=StrategyRegistry.Injes.Get(StrategyId);var effective=JsonSerializer.SerializeToElement(f.Resolve(this),InjesStrategyJson.Options);
        if(EffectiveAdvancedParameters.ValueKind!=JsonValueKind.Object||!JsonElement.DeepEquals(effective,EffectiveAdvancedParameters))throw new InvalidOperationException("EffectiveAdvancedParameters가 실제 실행 값과 다릅니다.");
        if(StandardParameters.Count!=f.StandardRules.Length||StandardParameters.Keys.Any(k=>!f.StandardRules.Any(r=>r.Key==k))||StandardParameters.Values.Any(n=>n is <1 or >5))throw new InvalidOperationException("Standard 설정 항목과 강도를 확인하세요.");
    }
    public InjesStrategyDefinition Snapshot(){Validate();return JsonSerializer.Deserialize<InjesStrategyDefinition>(JsonSerializer.Serialize(this,InjesStrategyJson.Options),InjesStrategyJson.Options)!;}
    public ParameterDefinition[] Metadata()
    {
        var f=StrategyRegistry.Injes.Get(StrategyId);var target=f.Resolve(this);
        return InjesParameterValidation.Properties(target).Select(p=>
        {var a=p.GetCustomAttribute<InjeParameterAttribute>()!;var rule=f.StandardRules.FirstOrDefault(r=>r.AdvancedKey==p.Name);return new ParameterDefinition(p.Name,a.Label,a.Description,a.Unit,p.PropertyType.Name,p.GetValue(f.Defaults()),p.PropertyType==typeof(decimal)||p.PropertyType==typeof(int)?a.Min:null,p.PropertyType==typeof(decimal)||p.PropertyType==typeof(int)?a.Max:null,a.Strictness,rule?.Values.Select((v,i)=>(v,i)).ToDictionary(x=>x.i+1,x=>x.v),"형식·범위 및 전략별 교차검증; 알려지지 않은 값 사용 금지");}).ToArray();
    }
}
public static class InjesStrategyJson
{
    public static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter(allowIntegerValues:false)}};
    public static string Export(InjesStrategyDefinition value){value.Validate();return JsonSerializer.Serialize(value,Options);}
    public static InjesStrategyDefinition Import(string json)
    {
        try
        {
            using var doc=JsonDocument.Parse(json,new(){MaxDepth=32});Unique(doc.RootElement);
            if(doc.RootElement.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Inje 전략 JSON 객체가 필요합니다.");
            var required=new[]{"schemaVersion","familyId","strategyId","strategyName","initialCash","strategyMode","commonParameters","strategyParameters","standardParameters","effectiveAdvancedParameters","executionPolicy","createdAt","updatedAt"};
            foreach(var key in required)if(!doc.RootElement.EnumerateObject().Any(p=>string.Equals(p.Name,key,StringComparison.OrdinalIgnoreCase)&&p.Value.ValueKind!=JsonValueKind.Null))throw new InvalidOperationException("필수 전략 항목이 없습니다: "+key);
            var d=JsonSerializer.Deserialize<InjesStrategyDefinition>(json,Options)??throw new InvalidOperationException("빈 전략입니다.");
            RequireAll(doc.RootElement.EnumerateObject().First(p=>string.Equals(p.Name,"commonParameters",StringComparison.OrdinalIgnoreCase)).Value,typeof(InjesCommonParameters));
            RequireAll(doc.RootElement.EnumerateObject().First(p=>string.Equals(p.Name,"executionPolicy",StringComparison.OrdinalIgnoreCase)).Value,typeof(InjesExecutionPolicy));
            // The selected parameter type alone is deserialized: fields from other strategies are errors.
            RequireAll(d.StrategyParameters,StrategyRegistry.Injes.Get(d.StrategyId).ParametersType);
            d.Validate();return d.Snapshot();
        }
        catch(JsonException e){throw new InvalidOperationException("Inje 전략 JSON 이름·형식·선택값을 확인하세요: "+e.Path,e);}
    }
    private static void RequireAll(JsonElement value,Type type)
    {foreach(var p in type.GetProperties())if(!value.EnumerateObject().Any(x=>string.Equals(x.Name,p.Name,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("필수 파라미터가 없습니다: "+p.Name);}
    private static void Unique(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object){var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in value.EnumerateObject()){if(!keys.Add(p.Name))throw new InvalidOperationException("중복 JSON 키: "+p.Name);Unique(p.Value);}}
        else if(value.ValueKind==JsonValueKind.Array)foreach(var v in value.EnumerateArray())Unique(v);
    }
}
