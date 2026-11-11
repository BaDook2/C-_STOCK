using System.Text.Json;
using System.Text.Json.Serialization;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies.Bins;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

namespace Stock.Core.Strategies;

public enum StrategySystem { None, Inje, Bin }
/// <summary>One saved UI profile retains BOTH systems' fields and selects at most one system for execution.</summary>
public sealed record StrategyProfile
{
    public int SchemaVersion {get;init;}=1;
    public string Kind {get;init;}="stock-strategy-profile";
    public string Name {get;init;}="기본 설정";
    public StrategySystem SelectedSystem {get;init;}=StrategySystem.Inje;
    public string SelectedInjeId {get;init;}="momentum-pullback";
    public InjesStrategyDefinition[] InjeStrategies {get;init;}=[];
    public BinsStrategyConfig Bin {get;init;}=new(){Enabled=false};
    public decimal BinInitialCashUsd {get;init;}=10000;
    public decimal BinInitialCashKrw {get;init;}=10000000;
    public static StrategyProfile Create(string name="기본 설정")=>new(){Name=name,InjeStrategies=StrategyRegistry.Injes.All.Select(f=>InjesStrategyDefinition.Create(f.StrategyId,DateTimeOffset.UtcNow)).ToArray()};
    public void Validate()
    {
        if(SchemaVersion!=1||Kind!="stock-strategy-profile"||string.IsNullOrWhiteSpace(Name)||Name.Length>80||Name.Any(char.IsControl)||!Enum.IsDefined(SelectedSystem)||InjeStrategies==null||Bin==null||InjeStrategies.Length!=StrategyRegistry.Injes.All.Length||InjeStrategies.Select(d=>d.StrategyId).Distinct().Count()!=InjeStrategies.Length||!InjeStrategies.Any(d=>d.StrategyId==SelectedInjeId))throw new InvalidOperationException("두 전략의 설정 프로필 이름·선택·구성을 확인하세요.");
        foreach(var d in InjeStrategies)d.Validate();Bin.Validate();
        if(Bin.Enabled!=(SelectedSystem==StrategySystem.Bin))throw new InvalidOperationException("Bin 사용 체크와 실행 설정이 일치하지 않습니다.");
        if(BinInitialCashUsd<=0||BinInitialCashKrw<=0||BinInitialCashUsd>1000000000000000m||BinInitialCashKrw>1000000000000000m)throw new InvalidOperationException("통화별 가상 초기 현금은 양수여야 합니다.");
    }
    public StrategyProfile Snapshot()=>StrategyProfileJson.Import(StrategyProfileJson.Export(this));
    public OversoldConfig ExecutionConfiguration()
    {
        Validate();return SelectedSystem switch
        {
            StrategySystem.Inje=>InjesPresetStore.Wrap(InjeStrategies.Single(d=>d.StrategyId==SelectedInjeId)),
            StrategySystem.Bin=>new(){Bins=Bin.Snapshot(),BinsInitialCashUsd=BinInitialCashUsd,BinsInitialCashKrw=BinInitialCashKrw,InitialCapital=BinInitialCashUsd,PlannedInvestment=Bin.Market.CountryProfiles.US.PlannedInvestment??0},
            _=>throw new InvalidOperationException("전략 설정에서 Inje’s Strategy 또는 Bin’s Strategy의 사용을 체크하세요.")
        };
    }
}
public static class StrategyProfileJson
{
    public static string Export(StrategyProfile value){value.Validate();return JsonSerializer.Serialize(value,BinsJson.Options);}
    public static StrategyProfile Import(string json)
    {
        using var doc=JsonDocument.Parse(json,new(){MaxDepth=64});Unique(doc.RootElement);
        string[] keys=["schemaVersion","kind","name","selectedSystem","selectedInjeId","injeStrategies","bin","binInitialCashUsd","binInitialCashKrw"];
        if(doc.RootElement.ValueKind!=JsonValueKind.Object||doc.RootElement.EnumerateObject().Any(p=>!keys.Contains(p.Name))||keys.Any(k=>!doc.RootElement.TryGetProperty(k,out var v)||v.ValueKind==JsonValueKind.Null))throw new InvalidOperationException("전체 전략 프로필의 필수 항목과 이름을 확인하세요.");
        foreach(var node in doc.RootElement.GetProperty("injeStrategies").EnumerateArray())InjesStrategyJson.Import(node.GetRawText());BinsJson.Import(doc.RootElement.GetProperty("bin").GetRawText());
        var result=JsonSerializer.Deserialize<StrategyProfile>(json,BinsJson.Options)??throw new InvalidOperationException("빈 전략 프로필입니다.");result.Validate();return result;
    }
    private static void Unique(JsonElement node)
    {if(node.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var p in node.EnumerateObject()){if(!names.Add(p.Name))throw new InvalidOperationException("중복 설정 항목: "+p.Name);Unique(p.Value);}}else if(node.ValueKind==JsonValueKind.Array)foreach(var v in node.EnumerateArray())Unique(v);}
}
public sealed class StrategyProfileStore(TradingStore store)
{
    public const string Prefix="strategy-profile:";
    public string[] Names=>store.State<string[]>("strategy-profile-names-v1")??[];
    public void EnsureDefault()
    {
        if(Names.Length>0)return;
        var old=new InjesPresetStore(store);old.EnsureDefaults();var defaults=StrategyProfile.Create();
        var definitions=old.Names.Select(old.Get).ToArray();
        defaults=defaults with{InjeStrategies=defaults.InjeStrategies.Select(d=>definitions.Where(x=>x.StrategyId==d.StrategyId).OrderByDescending(x=>x.UpdatedAt).FirstOrDefault()??d).ToArray()};Save(defaults);
        var defaultNames=StrategyRegistry.Injes.All.Select(f=>InjesStrategyDefinition.Create(f.StrategyId,DateTimeOffset.UtcNow).StrategyName).ToHashSet();
        foreach(var d in definitions.Where(d=>!defaultNames.Contains(d.StrategyName)))Save(defaults with{Name=d.StrategyName,SelectedInjeId=d.StrategyId,InjeStrategies=defaults.InjeStrategies.Select(x=>x.StrategyId==d.StrategyId?d:x).ToArray()});
    }
    public StrategyProfile Get(string name)=>store.State<StrategyProfile>(Prefix+name)?.Snapshot()??throw new InvalidOperationException("저장된 전략 설정이 없습니다: "+name);
    public void Save(StrategyProfile value)
    {var p=value.Snapshot();var names=Names.ToList();if(!names.Contains(p.Name))names.Add(p.Name);store.StatesAtomic((Prefix+p.Name,p),("strategy-profile-names-v1",names.ToArray()));}
    public void Rename(string oldName,StrategyProfile value)
    {if(Names.Contains(value.Name))throw new InvalidOperationException("같은 이름의 설정이 있습니다.");store.StatesAtomic((Prefix+value.Name,value.Snapshot()),("strategy-profile-names-v1",Names.Select(n=>n==oldName?value.Name:n).ToArray()));store.DeleteState(Prefix+oldName);}
}
