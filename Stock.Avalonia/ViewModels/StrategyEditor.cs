using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Core.Strategies.Injes;
using Stock.Core.Strategies.Bins;
using Stock.Core.Strategies;
using System.Text.Json;
using ParameterMetadata=Stock.Core.Trading.ParameterMetadata;
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
    private readonly InjesParameterField? inje;
    private readonly BinsParameterField? bin;
    private readonly object target;
    private string text;
    public ParameterMetadata Metadata {get;}
    public string Label=>Metadata.DisplayName;
    public string Path=>inje?.Path??bin?.Path??Metadata.Key;
    public bool HasTimeUnit=>bin?.HasTimeUnit??false;
    public bool ReadOnly=>bin?.ReadOnly??false;
    public ParameterChoice[] UnitChoices=>bin?.UnitChoices.Select(c=>new ParameterChoice(c.Value,c.Label)).ToArray()??[];
    private readonly OversoldConfig? configuration;
    public bool HasTimeframe=>configuration!=null&&(Metadata.Unit=="봉"||Metadata.Key=="MovingAveragePeriods");
    public ParameterChoice[] TimeframeChoices=>Metadata.Key=="MovingAveragePeriods"?[new("TradingDay","일봉"),new("Week","주봉"),new("Month","월봉")]:[new("Minute","분"),new("Hour","시간"),new("TradingDay","일봉"),new("Week","주봉"),new("Month","월봉")];
    public string Timeframe {get=>configuration?.ParameterTimeframes.GetValueOrDefault(Metadata.Key)?.Unit.ToString()??"TradingDay";set{if(configuration!=null){var unit=Enum.Parse<TimeframeUnit>(value);if(configuration.ParameterTimeframes.TryGetValue(Metadata.Key,out var existing)&&existing.Unit==unit)return;configuration.ParameterTimeframes[Metadata.Key]=new(1,unit);Changed();}}}

    public string Text {get=>inje?.Text??bin?.Text??text;set{if(value==null&&(IsChoice||IsBool))return;value??="";if(inje!=null)inje.Text=value;if(bin!=null)bin.Text=value;if(Set(ref text,value))Changed(nameof(BoolValue));}}
    public bool BoolValue {get=>bool.TryParse(text,out var v)&&v;set=>Text=value.ToString();}
    public bool IsBool=>inje?.IsBool??bin?.IsBool??property.PropertyType==typeof(bool);
    public bool IsChoice=>inje?.IsChoice??bin?.IsChoice??property.PropertyType.IsEnum;
    public bool IsText=>!IsBool&&!IsChoice;
    public bool IsDecimal=>property.PropertyType==typeof(decimal);
    public int Scale=>Metadata.Key=="CommissionPercent"?Math.Max(3,text.Contains('.')?text.Length-text.IndexOf('.')-1:0):Math.Max(2,text.Contains('.')?text.Length-text.IndexOf('.')-1:0);
    public string Unit {get=>bin?.Unit??(IsBool?"":Metadata.Unit);set{if(bin!=null){bin.Unit=value;Changed();}}}
    public string Hint=>IsBool?"":Metadata.Min is {} min?$"{Unit} {min:N0} ~ {Metadata.Max:N0}":Unit;
    public string Help=>bin?.Help??string.Join("\n",new[]{Metadata.ShortDescription,Metadata.DetailedDescription,Metadata.Min is {} min?$"허용 {min} ~ {Metadata.Max}":""}.Where(s=>!string.IsNullOrWhiteSpace(s)).Select(s=>s.Trim()).Distinct(StringComparer.Ordinal));
    public ParameterChoice[] Choices=>bin!=null?bin.Choices.Select(c=>new ParameterChoice(c.Value,c.Label)).ToArray():inje!=null?inje.Choices.Select(c=>new ParameterChoice(c.Value,c.Label)).ToArray():IsChoice?Enum.GetNames(property.PropertyType).Select(n=>new ParameterChoice(n,n switch{"ConservativeHighLow"=>"고저가 감지 · 손절 우선","CloseOnly"=>"확정 종가만","AfterEntry"=>"첫 체결 후","AfterTakeProfit1"=>"1차 익절 후",_=>n})).ToArray():[];
    public LegacyParameter(object target,PropertyInfo property)
    {this.target=target;configuration=target as OversoldConfig;this.property=property;Metadata=ParameterCatalog.For(property);text=Convert.ToString(property.GetValue(target),CultureInfo.InvariantCulture)??"";if(IsDecimal)text=decimal.Parse(text,CultureInfo.InvariantCulture).ToString("0.############################",CultureInfo.InvariantCulture);}
    public LegacyParameter(InjesParameterField field)
    {inje=field;target=field;property=typeof(InjesParameterField).GetProperty(nameof(InjesParameterField.Text))!;Metadata=field.Metadata;text=field.Text;}
    public LegacyParameter(BinsParameterField field)
    {bin=field;target=field;property=typeof(BinsParameterField).GetProperty(nameof(BinsParameterField.Text))!;Metadata=field.Metadata;text=field.Text;}
    public void Apply()
    {
        if(bin!=null){bin.Apply();return;}
        if(inje!=null){inje.Apply();return;}
        try
        {
            object value=property.PropertyType==typeof(string)?Text:IsBool?bool.Parse(Text):IsChoice?Enum.Parse(property.PropertyType,Text):
                property.PropertyType==typeof(int)?(object)int.Parse(Text,NumberStyles.Integer,CultureInfo.InvariantCulture):decimal.Parse(Text,NumberStyles.Number,CultureInfo.InvariantCulture);
            if(value is decimal number&&Metadata.Min is {} min&&(number<(decimal)min||number>(decimal)Metadata.Max!))throw new FormatException();property.SetValue(target,value);
        }
        catch(Exception ex) when(ex is FormatException or ArgumentException or OverflowException){throw new InvalidOperationException(Metadata.DisplayName+"의 값과 단위를 확인하세요.",ex);}
    }
}
/// <summary>Two independent systems in one saved profile. Only the selected system is sent to execution.</summary>
public sealed class StrategyEditor:Bindable
{
    private readonly TradingStore store;private readonly StrategyProfileStore presets;
    private StrategyProfile profile=null!;private InjesEditorModel editor=null!;private BinsEditorModel binEditor=null!;
    private string selected="",name="",message="",injeId="",cashUsd="",cashKrw="";private StrategySystem system;
    public IStrategyFileDialogs? FileDialogs {get;set;} public event Action<string>? Notice;
    public ObservableCollection<string> Names {get;}=[];
    public ObservableCollection<LegacyParameterGroup> InjeGroups {get;}=[];
    public ObservableCollection<LegacyParameterGroup> BinGroups {get;}=[];
    // Compatibility aliases for existing native field consumers; no old groups appear at the top level.
    public IEnumerable<LegacyParameterGroup> Groups=>InjeGroups.Where(g=>g.Name is "백테스트·체결 가정" or "공통 전략 조건");
    public IEnumerable<LegacyParameterGroup> CrashGroups=>InjeGroups.Where(g=>g.Name is not ("백테스트·체결 가정" or "공통 전략 조건"));
    public ParameterChoice[] InjeChoices=>StrategyRegistry.Injes.All.Select(f=>new ParameterChoice(f.StrategyId,f.Name)).ToArray();
    public string InjeId {get=>injeId;set{if(string.IsNullOrEmpty(value)||value==injeId)return;try{CaptureInje();injeId=value;BuildInje();Changed();Changed(nameof(StrategyHeading));Revision++;Changed(nameof(Revision));}catch(Exception ex){Message=ex.Message;Notice?.Invoke(ex.Message);Changed();}}}
    public string StrategyHeading=>"Inje's Strategy · "+StrategyRegistry.Injes.Get(injeId).Name;
    public bool UseInje {get=>system==StrategySystem.Inje;set=>Choose(StrategySystem.Inje,value);}
    public bool UseBin {get=>system==StrategySystem.Bin;set=>Choose(StrategySystem.Bin,value);}
    public int Revision {get;private set;}
    public string BinCashUsd {get=>cashUsd;set=>Set(ref cashUsd,value);}
    public string BinCashKrw {get=>cashKrw;set=>Set(ref cashKrw,value);}
    public ObservableCollection<BinFeeAction> FeeRows {get;}=[];
    public AsyncCommand AddKrwFee {get;} public AsyncCommand AddUsdFee {get;}
    public AsyncCommand Save {get;} public AsyncCommand Add {get;} public AsyncCommand Rename {get;} public AsyncCommand Restore {get;} public AsyncCommand Import {get;} public AsyncCommand Export {get;} public AsyncCommand Defaults {get;}
    public string Message {get=>message;private set=>Set(ref message,value);}
    public string Name {get=>name;set=>Set(ref name,value);}
    public string Selected {get=>selected;set{if(value==selected||string.IsNullOrEmpty(value))return;Load(value);}}
    public StrategyEditor(TradingStore store)
    {
        this.store=store;presets=new(store);presets.EnsureDefault();foreach(var n in presets.Names)Names.Add(n);var savedSelection=store.State<string>("strategy-profile-selected-v1");Load(savedSelection!=null&&Names.Contains(savedSelection)?savedSelection:Names.First());
        Save=new(()=>Run(()=>{if(Name!=Selected)throw new InvalidOperationException("이름 변경 버튼을 사용하세요.");Persist(CurrentProfile());return Task.CompletedTask;}));
        Add=new(()=>Run(()=>{Persist(CurrentProfile() with{Name=Unique(Name==Selected?"새 전략 설정":Name)});return Task.CompletedTask;}));
        Rename=new(()=>Run(()=>{var p=CurrentProfile();if(Name==Selected)return Task.CompletedTask;presets.Rename(Selected,p);Names[Names.IndexOf(Selected)]=Name;Load(Name);return Task.CompletedTask;}));
        Restore=new(()=>Run(()=>{Load(Selected);return Task.CompletedTask;}));
        Defaults=new(()=>Run(()=>{Load(Selected,StrategyProfile.Create(Selected));return Task.CompletedTask;}));
        Import=new(()=>Run(async()=>{var path=await RequireFiles().Import();if(path!=null)ImportText(File.ReadAllText(path));}));
        Export=new(()=>Run(async()=>{var json=StrategyProfileJson.Export(CurrentProfile());var path=await RequireFiles().Export("Stock-Strategies-"+DateTime.Today.ToString("yyyyMMdd")+".json");if(path!=null)File.WriteAllText(path,json);}));
        AddKrwFee=new(()=>Run(()=>{binEditor.AddFee("krx");return Task.CompletedTask;}));AddUsdFee=new(()=>Run(()=>{binEditor.AddFee("us");return Task.CompletedTask;}));
    }
    private void Choose(StrategySystem target,bool enabled)
    {var next=enabled?target:system==target?StrategySystem.None:system;if(next==system)return;system=next;Changed(nameof(UseInje));Changed(nameof(UseBin));Revision++;Changed(nameof(Revision));Message=system==StrategySystem.None?"사용할 전략을 체크하세요. 두 전략이 모두 꺼져 있으면 새 실행을 시작하지 않습니다.":(UseInje?"Inje":"Bin")+" 설정을 선택했습니다. 저장하면 다음 실행에 반영됩니다.";}
    private string Unique(string basis){var value=basis;for(int n=2;Names.Contains(value);n++)value=basis+" "+n;return value;}
    private IStrategyFileDialogs RequireFiles()=>FileDialogs??throw new InvalidOperationException("파일 대화상자가 연결되지 않았습니다.");
    private async Task Run(Func<Task> action){try{await action();Message="설정을 반영했습니다. 사용 체크는 한 전략만 선택합니다.";}catch(Exception ex)when(ex is not OutOfMemoryException){Message=ex.Message;Notice?.Invoke(ex.Message);}}
    private void CaptureInje()
    {var current=editor.Apply(profile.InjeStrategies.Single(d=>d.StrategyId==injeId).StrategyName,DateTimeOffset.UtcNow);profile=profile with{InjeStrategies=profile.InjeStrategies.Select(d=>d.StrategyId==injeId?current:d).ToArray()};}
    public InjesStrategyDefinition CurrentDefinition(){CaptureInje();return profile.InjeStrategies.Single(d=>d.StrategyId==injeId).Snapshot();}
    public StrategyProfile CurrentProfile()
    {CaptureInje();var p=profile with{Name=Name,SelectedSystem=system,SelectedInjeId=injeId,Bin=binEditor.Apply(UseBin),BinInitialCashUsd=ParseCash(BinCashUsd),BinInitialCashKrw=ParseCash(BinCashKrw)};p.Validate();return p.Snapshot();}
    private static decimal ParseCash(string text)=>decimal.TryParse(text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)&&n>0?n:throw new InvalidOperationException("통화별 가상 초기 현금은 양수로 입력하세요.");
    public OversoldConfig Current()=>CurrentProfile().ExecutionConfiguration();
    public OversoldConfig ForExecution(string value)=>value==Selected?Current():Registered(value);
    public OversoldConfig Registered(string value)=>Names.Contains(value)?presets.Get(value).ExecutionConfiguration():InjesPresetStore.Wrap(new InjesPresetStore(store).Get(value));
    public void ImportText(string json)
    {
        using var document=JsonDocument.Parse(json);var root=document.RootElement;StrategyProfile p;
        if(root.TryGetProperty("kind",out var kind)&&kind.GetString()=="stock-strategy-profile")p=StrategyProfileJson.Import(json);
        else if(root.TryGetProperty("familyId",out var family)&&family.GetString()=="inje")
        {var d=InjesStrategyJson.Import(json);p=CurrentProfile() with{Name=d.StrategyName,SelectedSystem=StrategySystem.Inje,SelectedInjeId=d.StrategyId,InjeStrategies=CurrentProfile().InjeStrategies.Select(x=>x.StrategyId==d.StrategyId?d:x).ToArray(),Bin=binEditor.Apply(false)};}
        else {var b=BinsJson.Import(json);p=CurrentProfile() with{Name="Bin 설정",SelectedSystem=b.Enabled?StrategySystem.Bin:StrategySystem.None,Bin=b};}
        Persist(p with{Name=Unique(p.Name)});
    }
    private void Load(string value,StrategyProfile? replacement=null)
    {
        profile=(replacement??presets.Get(value)).Snapshot();store.State("strategy-profile-selected-v1",value);selected=name=value;system=profile.SelectedSystem;injeId=profile.SelectedInjeId;cashUsd=profile.BinInitialCashUsd.ToString("0.####################",CultureInfo.InvariantCulture);cashKrw=profile.BinInitialCashKrw.ToString("0.####################",CultureInfo.InvariantCulture);
        BuildInje();binEditor=new(profile.Bin);binEditor.GroupsChanged+=BuildBin;BuildBin();Revision++;
        foreach(var property in new[]{nameof(Name),nameof(Selected),nameof(StrategyHeading),nameof(InjeId),nameof(UseInje),nameof(UseBin),nameof(BinCashUsd),nameof(BinCashKrw),nameof(Revision)})Changed(property);
        Message="한 번에 하나의 전략을 사용합니다. 다른 전략의 입력값도 함께 저장합니다. 원본 시세·당시 확인 자료가 없으면 진입을 보류합니다.";
    }
    private void BuildInje(){editor=new(profile.InjeStrategies.Single(d=>d.StrategyId==injeId));editor.ModeChanged+=BuildInjeGroups;BuildInjeGroups();}
    private void BuildInjeGroups()
    {
        InjeGroups.Clear();void Add(string title,InjesParameterField[] fields)=>InjeGroups.Add(new(title,fields.Select(f=>new LegacyParameter(f)).ToArray()));
        Add("백테스트·체결 가정",editor.ExecutionFields);Add("공통 전략 조건",editor.CommonFields);Add("Standard / Advanced",editor.ModeFields);Add(editor.Options.StrategyMode==StrategyMode.Standard?"Standard · 대표 강도":"Advanced · 전략 전용",editor.StrategyFields);
    }
    private void BuildBin()
    {
        BinGroups.Clear();foreach(var g in binEditor.Groups)BinGroups.Add(new(g.Name,g.Fields.Select(f=>new LegacyParameter(f)).ToArray()));FeeRows.Clear();
        foreach(var country in new[]{"krx","us"}){var n=binEditor.Groups.Count(g=>g.Name.StartsWith(country=="krx"?"KRW 비용표":"USD 비용표"));for(int i=0;i<n;i++){var index=i;FeeRows.Add(new((country=="krx"?"KRW":"USD")+" 비용표 "+(i+1),new(()=>Run(()=>{binEditor.RemoveFee(country,index);return Task.CompletedTask;}))));}}
    }
    private void Persist(StrategyProfile p){presets.Save(p);if(!Names.Contains(p.Name))Names.Add(p.Name);Load(p.Name);}
}
public sealed record BinFeeAction(string Name,AsyncCommand Remove);
