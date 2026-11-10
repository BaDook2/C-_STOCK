using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed record LegacyParameterGroup(string Name,LegacyParameter[] Fields);
public sealed record LegacyParameterCell(LegacyParameter? Field) { public bool Present=>Field!=null; }
public sealed record LegacyParameterRow(LegacyParameterCell[] Cells);
public sealed record LegacyLayoutGroup(string Name,LegacyParameterRow[] Rows);
public sealed record LegacyUsageGroup(string Name,string Description,string Status,int Count,LegacyLayoutGroup[] Groups) { public ParameterUsageScope Scope {get;init;}
    public string Heading=>$"{Name} · {Count}개"; }
public sealed record ParameterChoice(string Value,string Label);
public sealed class LegacyParameter:Bindable
{
    private readonly PropertyInfo property;
    private readonly object target;
    private string text;
    public ParameterMetadata Metadata {get;}
    private readonly OversoldConfig? configuration;
    public bool HasTimeframe=>configuration!=null&&(Metadata.Unit=="봉"||Metadata.Key=="MovingAveragePeriods");
    public ParameterChoice[] TimeframeChoices=>Metadata.Key=="MovingAveragePeriods"?[new("TradingDay","일봉"),new("Week","주봉"),new("Month","월봉")]:[new("Minute","분"),new("Hour","시간"),new("TradingDay","일봉"),new("Week","주봉"),new("Month","월봉")];
    public string Timeframe {get=>configuration?.ParameterTimeframes.GetValueOrDefault(Metadata.Key)?.Unit.ToString()??"TradingDay";set{if(configuration!=null){var unit=Enum.Parse<TimeframeUnit>(value);if(configuration.ParameterTimeframes.TryGetValue(Metadata.Key,out var existing)&&existing.Unit==unit)return;configuration.ParameterTimeframes[Metadata.Key]=new(1,unit);Changed();}}}

    public string Text {get=>text;set{if(Set(ref text,value))Changed(nameof(BoolValue));}}
    public bool BoolValue {get=>bool.TryParse(text,out var v)&&v;set=>Text=value.ToString();}
    public bool IsBool=>property.PropertyType==typeof(bool);
    public bool IsChoice=>property.PropertyType.IsEnum;
    public bool IsText=>!IsBool&&!IsChoice;
    public bool IsDecimal=>property.PropertyType==typeof(decimal);
    public int Scale=>Metadata.Key=="CommissionPercent"?Math.Max(3,text.Contains('.')?text.Length-text.IndexOf('.')-1:0):Math.Max(2,text.Contains('.')?text.Length-text.IndexOf('.')-1:0);
    public string Unit=>IsBool?"":Metadata.Unit;
    public string Hint=>IsBool?"":Metadata.Min is {} min?$"{Unit} {min:N0} ~ {Metadata.Max:N0}":Unit;
    public string Help=>Metadata.ShortDescription+"\n"+Metadata.DetailedDescription+(Metadata.Min is {} min?$"\n허용 {min} ~ {Metadata.Max}":"");
    public ParameterChoice[] Choices=>IsChoice?Enum.GetNames(property.PropertyType).Select(n=>new ParameterChoice(n,n switch{"ConservativeHighLow"=>"고저가 감지 · 손절 우선","CloseOnly"=>"확정 종가만","AfterEntry"=>"첫 체결 후","AfterTakeProfit1"=>"1차 익절 후",_=>n})).ToArray():[];
    public LegacyParameter(object target,PropertyInfo property)
    {this.target=target;configuration=target as OversoldConfig;this.property=property;Metadata=ParameterCatalog.For(property);text=Convert.ToString(property.GetValue(target),CultureInfo.InvariantCulture)??"";if(IsDecimal)text=decimal.Parse(text,CultureInfo.InvariantCulture).ToString("0.############################",CultureInfo.InvariantCulture);}
    public void Apply()
    {
        try
        {
            object value=property.PropertyType==typeof(string)?Text:IsBool?bool.Parse(Text):IsChoice?Enum.Parse(property.PropertyType,Text):
                property.PropertyType==typeof(int)?(object)int.Parse(Text,NumberStyles.Integer,CultureInfo.InvariantCulture):decimal.Parse(Text,NumberStyles.Number,CultureInfo.InvariantCulture);
            if(value is decimal number&&Metadata.Min is {} min&&(number<(decimal)min||number>(decimal)Metadata.Max!))throw new FormatException();property.SetValue(target,value);
        }
        catch(Exception ex) when(ex is FormatException or ArgumentException or OverflowException){throw new InvalidOperationException(Metadata.DisplayName+"의 값과 단위를 확인하세요.",ex);}
    }
}
/// <summary>61 legacy parameters use the original schema and metadata; saved snapshots are separate from in-progress edits.</summary>
public sealed class StrategyEditor:Bindable
{
    private readonly TradingStore store;
    private OversoldConfig config=new();
    private string selected="",name="기본",message="";
    public IStrategyFileDialogs? FileDialogs {get;set;}
    public event Action<string>? Notice;
    public ObservableCollection<string> Names {get;}=[];
    public ObservableCollection<LegacyParameterGroup> Groups {get;}=[];
    public ObservableCollection<LegacyUsageGroup> UsageGroups {get;}=[];
    public IEnumerable<LegacyUsageGroup> BacktestUsageGroups=>UsageGroups.Where(g=>g.Scope==ParameterUsageScope.Backtest);
    public IEnumerable<LegacyUsageGroup> StrategyUsageGroups=>UsageGroups.Where(g=>g.Scope!=ParameterUsageScope.Backtest);
    private StrategyDEditorModel crashEditor=null!;
    public ObservableCollection<DayTradingParameterGroup> CrashGroups {get;}=[];
    public bool UseCrashStrategy {get=>config.UseCrashStrategy;set{config.UseCrashStrategy=value;Changed();}}

    public AsyncCommand Save {get;}
    public AsyncCommand Add {get;}
    public AsyncCommand Rename {get;}
    public AsyncCommand Restore {get;}
    public AsyncCommand Import {get;}
    public AsyncCommand Export {get;}
    public AsyncCommand Defaults {get;}
    public string Message {get=>message;private set=>Set(ref message,value);}
    public string Name {get=>name;set=>Set(ref name,value);}
    public string Selected {get=>selected;set{if(value==selected||string.IsNullOrEmpty(value))return;Load(value);}}
    public StrategyEditor(TradingStore store)
    {
        this.store=store;
        foreach(var preset in store.State<string[]>("oversold-presets-v1")??["기본"])Names.Add(preset);
        foreach(var pair in StrategyDiagnosticPreset.BacktestVerificationPresets().Prepend(new(StrategyDiagnosticPreset.Name,StrategyDiagnosticPreset.Configuration())))
        {if(!Names.Contains(pair.Key)){Names.Add(pair.Key);store.State("oversold-preset:"+pair.Key,pair.Value);}}
        if(!Names.Contains("기본"))Names.Insert(0,"기본");if(store.State<OversoldConfig>("oversold-preset:기본")==null)store.State("oversold-preset:기본",config);
        Load(Names.First());
        Save=new(()=>Run(()=>{ValidateName(Name);if(Name!=Selected)throw new InvalidOperationException("이름 변경 버튼을 사용하세요.");Persist(Name,Current());return Task.CompletedTask;}));
        Add=new(()=>Run(()=>{var basis=Name==Selected?"새 전략":Name;var unique=basis;for(int n=2;Names.Contains(unique);n++)unique=basis+" "+n;Persist(unique,Current());return Task.CompletedTask;}));
        Rename=new(()=>Run(()=>{ValidateName(Name);if(Name==Selected)return Task.CompletedTask;if(Names.Contains(Name))throw new InvalidOperationException("같은 전략 이름이 있습니다.");var old=Selected;Persist(Name,Current());Names.Remove(old);store.DeleteState("oversold-preset:"+old);store.State("oversold-presets-v1",Names.ToArray());return Task.CompletedTask;}));
        Restore=new(()=>Run(()=>{Load(Selected);return Task.CompletedTask;}));
        Defaults=new(()=>Run(()=>{Load(Selected,new OversoldConfig());return Task.CompletedTask;}));
        Import=new(()=>Run(async()=>{var path=await RequireFiles().Import();if(path==null)return;var value=StrategyDefinitionJson.Import(File.ReadAllText(path));if(value.IsStrategyD){var shared=value.Configuration.Clone();shared.UseCrashStrategy=true;shared.CrashStrategy=value;value=new(1,StrategyDefinition.LegacyId,value.Preset,value.ExportedAt,shared);}var basis=string.IsNullOrWhiteSpace(value.Preset)?"가져온 전략":value.Preset;var unique=basis;for(int n=2;Names.Contains(unique);n++)unique=basis+" "+n;Persist(unique,value.Configuration);}));
        Export=new(()=>Run(async()=>{var current=Current();var file=await RequireFiles().Export("전략-"+DateTime.Today.ToString("yyyyMMdd")+".json");if(file!=null)File.WriteAllText(file,StrategyDefinitionJson.Export(new(1,StrategyDefinition.LegacyId,Name,DateTimeOffset.UtcNow,current)));}));
    }
    private IStrategyFileDialogs RequireFiles()=>FileDialogs??throw new InvalidOperationException("파일 대화상자가 연결되지 않았습니다.");
    private async Task Run(Func<Task> action){try{await action();Message="설정을 반영했습니다.";}catch(Exception ex) when(ex is not OutOfMemoryException){Message=ex.Message;Notice?.Invoke(ex.Message);}}
    public OversoldConfig Current(){foreach(var field in Groups.SelectMany(g=>g.Fields)){field.Apply();if(field.HasTimeframe&&!config.ParameterTimeframes.ContainsKey(field.Metadata.Key))field.Timeframe="TradingDay";}var d=crashEditor.Apply(Name);
        // Capital and execution costs have one source in the enclosing strategy.
        d.Configuration.InitialCapital=config.InitialCapital;d.Configuration.PlannedInvestment=config.PlannedInvestment;
        d.Configuration.CommissionPercent=config.CommissionPercent;d.Configuration.TaxPercent=config.TaxPercent;d.Configuration.SlippagePercent=config.SlippagePercent;
        config.CrashStrategy=d;config.Validate();return config.Clone();}
    public OversoldConfig Registered(string name){var value=(store.State<OversoldConfig>("oversold-preset:"+name)??throw new InvalidOperationException("등록 전략이 없습니다: "+name)).Clone();value.Validate();return value;}
    private void Load(string value,OversoldConfig? replacement=null)
    {
        config=(replacement??store.State<OversoldConfig>("oversold-preset:"+value)??throw new InvalidOperationException("저장한 전략이 없습니다.")).Clone();config.Validate();selected=name=value;Groups.Clear();
        var fields=ParameterCatalog.Properties(config).Select(p=>new LegacyParameter(config,p)).ToDictionary(f=>f.Metadata.Key);
        foreach(var group in OversoldParameterMetadata.Rows.GroupBy(r=>r.Group))Groups.Add(new(group.Key,group.SelectMany(r=>r.Keys).Where(k=>k!=null).Select(k=>fields[k!]).ToArray()));
        var definition=config.CrashStrategy??StrategyDDefaults.Create(DateTimeOffset.UtcNow);
        crashEditor=new(definition);CrashGroups.Clear();
        var omitted=new HashSet<string>{"Configuration.PlannedInvestment","Configuration.CommissionPercent","Configuration.TaxPercent","Configuration.SlippagePercent"};
        foreach(var group in crashEditor.Parameters.Where(p=>!omitted.Contains(p.Path)&&!p.Path.EndsWith(".Unit")).GroupBy(p=>p.Group))CrashGroups.Add(new(group.Key,group.ToArray()));
        Changed(nameof(UseCrashStrategy));
        UsageGroups.Clear();
        foreach(var section in OversoldParameterMetadata.Sections)
        {
            var rows=OversoldParameterMetadata.Rows.Where(r=>r.Scope==section.Scope).ToArray();if(rows.Length==0)continue;
            var groups=rows.GroupBy(r=>r.Group).Select(g=>new LegacyLayoutGroup(g.Key,g.Select(r=>new LegacyParameterRow(r.Keys.Select(k=>new LegacyParameterCell(k==null?null:fields[k])).ToArray())).ToArray())).ToArray();
            UsageGroups.Add(new(section.Name,section.Description,section.Status,rows.Sum(r=>r.Keys.Count(k=>k!=null)),groups){Scope=section.Scope});
        }
        Changed(nameof(BacktestUsageGroups));Changed(nameof(StrategyUsageGroups));Changed(nameof(Name));Changed(nameof(Selected));
    }
    private void Persist(string key,OversoldConfig value){ValidateName(key);value.Validate();store.State("oversold-preset:"+key,value);if(!Names.Contains(key))Names.Add(key);store.State("oversold-presets-v1",Names.ToArray());Load(key);}
    private static void ValidateName(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>80||value.Any(char.IsControl))throw new InvalidOperationException("전략 이름은1~80자로 입력하세요.");}
}
