using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public sealed record InjesParameterChoice(string Value,string Label);
public sealed class InjesEditorOptions
{
    [InjeParameter("설정 모드","Standard는6개 강도를 실제 수치로 변환합니다. Advanced는 직접 입력한 전용 값을 사용합니다.")]
    public StrategyMode StrategyMode {get;set;}
    [InjeParameter("초기 가상 현금","리플레이·가상 계좌의 USD 현금입니다. 실계좌 자금과 구분합니다.","USD",.01)]
    public decimal InitialCash {get;set;}=10000;
}
public sealed class StandardParameterValue
{
    public int Level {get;set;}=3;
}
/// <summary>UI-neutral editor field for the existing WPF/Avalonia cards. Decimal prices retain full precision.</summary>
public sealed class InjesParameterField:INotifyPropertyChanged,IDataErrorInfo
{
    private readonly object target;private readonly PropertyInfo property;private readonly Action? immediate;
    private string text;
    public ParameterMetadata Metadata {get;}
    public string Path {get;}
    public string Label=>Metadata.DisplayName;public string Help=>Metadata.DetailedDescription+"\n"+Metadata.Unit;
    public string Text {get=>text;set
    {
        if(value==null&&(IsChoice||IsBool))return;
        value??="";if(text==value)return;var before=property.GetValue(target);text=value;Notify();Notify(nameof(Value));Notify(nameof(InputText));Notify(nameof(BoolValue));Notify(nameof(Error));
        if(immediate!=null&&Error==""&&!Equals(before,Parse())){Apply();immediate();}
    }}
    public string Value {get=>Text;set=>Text=value;}public string InputText {get=>Text;set=>Text=value;}
    public bool IsBool=>property.PropertyType==typeof(bool);public bool IsChoice=>property.PropertyType.IsEnum;public bool IsEnum=>IsChoice;
    public bool IsText=>!IsBool&&!IsChoice;public bool IsList=>property.PropertyType==typeof(string);
    public bool HasTimeframe=>false;public bool ShowRange=>false;public string Range=>"";public string InputTooltip=>Help;
    public double EditorWidth=>150;public bool BoolValue {get=>bool.TryParse(text,out var b)&&b;set=>Text=value.ToString();}
    public InjesParameterChoice[] Choices=>IsChoice?Enum.GetNames(property.PropertyType).Select(v=>new InjesParameterChoice(v,v switch{"Standard"=>"Standard · 간편","Advanced"=>"Advanced · 상세","PreviousClose"=>"전일 종가","SessionOpen"=>"당일 세션 시가","ObservedSessionHigh"=>"관측 세션 고점",_=>v})).ToArray():[];
    public string Error {get{try{_ = Parse();return "";}catch(Exception e)when(e is FormatException or OverflowException or ArgumentException or InvalidOperationException){return Label+": 값·범위·단위를 확인하세요.";}}}
    public string this[string name]=>Error;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([System.Runtime.CompilerServices.CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    public InjesParameterField(object target,PropertyInfo property,string path,ParameterMetadata? metadata=null,Action? immediate=null)
    {
        this.target=target;this.property=property;Path=path;this.immediate=immediate;
        if(metadata!=null)Metadata=metadata;
        else{var a=property.GetCustomAttribute<InjeParameterAttribute>()!;var t=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;Metadata=new(path,a.Label,a.Description,a.Description,"","",a.Unit,t==typeof(int)||t==typeof(decimal)?(double)a.Min:null,t==typeof(int)||t==typeof(decimal)?(double)a.Max:null,InputKind:t==typeof(int)?ParameterInputKind.Integer:t==typeof(decimal)?ParameterInputKind.Decimal:ParameterInputKind.Text);}
        text=Convert.ToString(property.GetValue(target),CultureInfo.InvariantCulture)??"";
    }
    private object? Parse()
    {
        var type=Nullable.GetUnderlyingType(property.PropertyType)??property.PropertyType;
        if(Nullable.GetUnderlyingType(property.PropertyType)!=null&&string.IsNullOrWhiteSpace(text))return null;
        if(type==typeof(string))return text;if(type==typeof(bool))return bool.Parse(text);
        if(type.IsEnum){var v=Enum.Parse(type,text,false);return Enum.IsDefined(type,v)?v:throw new FormatException();}
        var number=decimal.Parse(text,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
        if(Metadata.Min is {} min&&(number<(decimal)min||number>(decimal)Metadata.Max!.Value))throw new FormatException();
        if(type==typeof(int))return number==decimal.Truncate(number)?checked((int)number):throw new FormatException();return number;
    }
    public void Apply(){if(Error!="")throw new InvalidOperationException(Error);property.SetValue(target,Parse());}
}
public sealed class InjesEditorModel
{
    private InjesStrategyDefinition definition;private readonly object advanced;
    private readonly Dictionary<string,StandardParameterValue> standard=[];
    public InjesEditorOptions Options {get;}
    public InjesParameterField[] CommonFields {get;private set;}=[];
    public InjesParameterField[] ExecutionFields {get;private set;}=[];
    public InjesParameterField[] StrategyFields {get;private set;}=[];
    public InjesParameterField[] ModeFields {get;private set;}=[];
    public event Action? ModeChanged;
    public InjesEditorModel(InjesStrategyDefinition definition,decimal? initialCash=null)
    {
        this.definition=definition.Snapshot();advanced=definition.StrategyParameters.Deserialize(StrategyRegistry.Injes.Get(definition.StrategyId).ParametersType,InjesStrategyJson.Options)!;
        Options=new(){StrategyMode=definition.StrategyMode,InitialCash=initialCash??definition.InitialCash};foreach(var (key,value) in definition.StandardParameters)standard[key]=new(){Level=value};
        CommonFields=Fields(this.definition.CommonParameters,"commonParameters");ExecutionFields=Fields(this.definition.ExecutionPolicy,"executionPolicy").Concat(Fields(Options,"options").Where(f=>f.Path.EndsWith("InitialCash"))).ToArray();
        ModeFields=[new(Options,typeof(InjesEditorOptions).GetProperty(nameof(InjesEditorOptions.StrategyMode))!,"strategyMode",immediate:()=>{foreach(var f in StrategyFields)f.Apply();BuildStrategyFields();ModeChanged?.Invoke();})];BuildStrategyFields();
    }
    private static InjesParameterField[] Fields(object target,string path)=>InjesParameterValidation.Properties(target).Select(p=>new InjesParameterField(target,p,path+"."+p.Name)).ToArray();
    private void BuildStrategyFields()
    {
        if(Options.StrategyMode==StrategyMode.Advanced){StrategyFields=Fields(advanced,"strategyParameters");return;}
        StrategyFields=StrategyRegistry.Injes.Get(definition.StrategyId).StandardRules.Select(rule=>new InjesParameterField(standard[rule.Key],typeof(StandardParameterValue).GetProperty(nameof(StandardParameterValue.Level))!,"standardParameters."+rule.Key,new(rule.Key,rule.Label,"1~5 강도를 명시적 실제 수치로 변환합니다.",string.Join(" / ",rule.Values.Select((v,i)=>$"{i+1} → {v}")),"","","강도",1,5,InputKind:ParameterInputKind.Integer))).ToArray();
    }
    public InjesStrategyDefinition Apply(string name,DateTimeOffset at)
    {
        foreach(var f in CommonFields.Concat(ExecutionFields).Concat(StrategyFields))f.Apply();
        definition.StrategyName=name;definition.StrategyMode=Options.StrategyMode;definition.InitialCash=Options.InitialCash;definition.UpdatedAt=at;
        definition.StrategyParameters=JsonSerializer.SerializeToElement(advanced,advanced.GetType(),InjesStrategyJson.Options);definition.StandardParameters=standard.ToDictionary(x=>x.Key,x=>x.Value.Level);
        definition.RefreshEffective();definition.Validate();return definition.Snapshot();
    }
}
