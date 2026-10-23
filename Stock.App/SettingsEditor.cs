using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Stock.Core.Trading;
namespace Stock.App;
public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
public sealed class SettingField : Observable,IDataErrorInfo
{
    private readonly object target;
    private readonly PropertyInfo property;
    private string value;
    private readonly SettingAttribute attribute;
    private readonly decimal scale=1;
    private readonly string labelSuffix="";
    public string GroupLabel {get;set;}="";
    public SettingField(object target,PropertyInfo property,bool integerDisplay=false){this.target=target;this.property=property;attribute=property.GetCustomAttribute<SettingAttribute>()!;value=NumericDisplay.Format(property.GetValue(target));if(integerDisplay&&property.PropertyType==typeof(decimal)){var n=(decimal)property.GetValue(target)!;if(property.Name is "FeePercent" or "SlippagePercent"){scale=100;labelSuffix=" · bp (100bp = 1%)";}else if(property.Name is "WeakVolumeRatio" or "WeakTradesRatio"){scale=100;labelSuffix=" · %";}else{while(n*scale!=decimal.Truncate(n*scale)&&scale<100000000)scale*=10;if(scale>1)labelSuffix=$" · 1/{scale:0} 단위";}value=(n*scale).ToString("#,##0",CultureInfo.InvariantCulture);}}
    public ParameterMetadata Metadata=>ParameterCatalog.For(property);
    public string Label=>(scale==100&&property.Name is "FeePercent" or "SlippagePercent"?attribute.Label.Replace("(%)",""):attribute.Label)+labelSuffix;
    public bool IsBool=>property.PropertyType==typeof(bool);
    public bool IsEnum=>property.PropertyType.IsEnum;
    public bool IsText=>!IsBool&&!IsEnum;
    public string[] Choices=>IsEnum?Enum.GetNames(property.PropertyType):[];
    public string Value {get=>value;set{this.value=value;Changed();Changed(nameof(Error));}}
    public bool BoolValue {get=>bool.TryParse(value,out var b)&&b;set{this.value=value.ToString();Changed();}}
    public string Hint=>IsText&&property.PropertyType!=typeof(string)?$"범위 {attribute.Min:#,##0.########} ~ {attribute.Max:#,##0.########}":"";
    public string Error {get{try{_ = Parse();return "";}catch(Exception){return Label+": 값 또는 범위를 확인하세요.";}}}
    public string this[string name]=>name==nameof(Value)?Error:"";
    private object Parse()
    {var t=property.PropertyType;if(t==typeof(string))return value;if(t==typeof(bool))return bool.Parse(value);if(t.IsEnum){var e=Enum.Parse(t,value);if(!Enum.IsDefined(t,e))throw new FormatException();return e;}var parsed=t==typeof(decimal)?decimal.Parse(value,CultureInfo.InvariantCulture)/scale:Convert.ChangeType(value.Replace(",",""),t,CultureInfo.InvariantCulture)!;var n=Convert.ToDouble(parsed);if(n<attribute.Min||n>attribute.Max)throw new FormatException();return parsed;}
    public void Apply(){if(Error!="")throw new InvalidOperationException(Error);property.SetValue(target,Parse());}
}
public sealed record SettingGroup(string Name,IReadOnlyList<SettingField> Fields);
public sealed class FilterEditor(FilterRule rule)
{
    public string Name=>rule.Name;
    public string Boundary=>rule.Minimum?"최소":"최대";
    public bool Enabled {get;set;}=rule.Enabled;
    public string Value {get;set;}=NumericDisplay.Format(rule.Value);
    public void Apply(){if(!decimal.TryParse(Value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)||n<0)throw new InvalidOperationException(Name+" 필터 숫자 오류");rule.Value=n;rule.Enabled=Enabled;}
}
