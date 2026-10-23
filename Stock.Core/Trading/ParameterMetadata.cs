using System.Globalization;
using System.Reflection;
namespace Stock.Core.Trading;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ParameterAttribute(string name,string description,string increased,string decreased,string unit,double min=0,double max=1e12):Attribute
{
    public string Name=>name;public string Description=>description;public string Increased=>increased;public string Decreased=>decreased;public string Unit=>unit;public double Min=>min;public double Max=>max;
}
public enum ParameterUsageScope { Backtest, CommonStrategy, Online }
public enum ParameterInputKind { Text, Integer, Decimal, IntegerList, DecimalList }
public sealed record ParameterMetadata(string Key,string DisplayName,string ShortDescription,string DetailedDescription,string EffectWhenIncreased,string EffectWhenDecreased,string Unit,double? Min,double? Max,string Category="",string Group="",ParameterUsageScope? UsageScope=null,string UsageDescription="",ParameterInputKind InputKind=ParameterInputKind.Text);

/// <summary>파라미터 정의의 메타데이터를 Tooltip·도움말·검증에서 공유합니다.</summary>
public static class ParameterCatalog
{
    public static ParameterMetadata For(PropertyInfo property)
    {
        var a=property.GetCustomAttribute<ParameterAttribute>();
        if(a!=null)
        {
            var metadata=new ParameterMetadata(property.Name,a.Name,a.Description,a.Description,a.Increased,a.Decreased,a.Unit,IsNumeric(property.PropertyType)?a.Min:null,IsNumeric(property.PropertyType)?a.Max:null,GroupName(property.DeclaringType),property.DeclaringType?.Name.Contains("Standard")==true?"Standard":"Advanced",InputKind:property.PropertyType==typeof(int)?ParameterInputKind.Integer:IsNumeric(property.PropertyType)?ParameterInputKind.Decimal:ParameterInputKind.Text);
            return property.DeclaringType==typeof(Research.Oversold.OversoldConfig)?Research.Oversold.OversoldParameterMetadata.Describe(metadata):metadata with {DetailedDescription=a.Description+" 설정을 저장한 값은 백테스트와 모의 엔진에서 동일하게 사용합니다."};
        }
        var legacy=property.GetCustomAttribute<SettingAttribute>()??throw new InvalidOperationException("파라미터 메타데이터가 없습니다.");
        return new(property.Name,legacy.Label,legacy.Label+"에 사용하는 기존 전략 값입니다.","기존 전략의 "+legacy.Label+" 조건을 설정합니다. 새 Standard 설정 사용 시 같은 의미의 항목은 변환된 Advanced 값이 우선합니다.","이 항목의 상한·기간·기준값을 늘립니다. 전략별 조건 충족 여부는 해당 항목의 의미에 따릅니다.","이 항목의 상한·기간·기준값을 줄입니다.","기존 엔진 단위",IsNumeric(property.PropertyType)?legacy.Min:null,IsNumeric(property.PropertyType)?legacy.Max:null);
    }
    private static string GroupName(Type? type)
    {
        var name=type?.Name??"";
        foreach(var section in new[]{"Universe","Attention","PriceAction","Microstructure","Tick","Farmer","Portfolio"})if(name.StartsWith(section))return section;
        return name.Contains("Risk")?"Risk":name.Contains("Execution")?"Execution":name.Contains("Recurring")?"정기매수":"Strategy";
    }
    public static bool IsNumeric(Type t)=>t==typeof(int)||t==typeof(decimal)||t==typeof(double);
    public static IEnumerable<PropertyInfo> Properties(object target)=>target.GetType().GetProperties().Where(p=>p.GetCustomAttribute<ParameterAttribute>()!=null);
}
public static class StrategyParameterValidation
{
    public static void Object(object value)
    {
        foreach(var p in ParameterCatalog.Properties(value))
        {
            var meta=ParameterCatalog.For(p);var x=p.GetValue(value);
            if(ParameterCatalog.IsNumeric(p.PropertyType)){var n=Convert.ToDouble(x,CultureInfo.InvariantCulture);if(!double.IsFinite(n)||n<meta.Min||n>meta.Max)throw new InvalidOperationException($"{meta.DisplayName}: {meta.Min:#,##0.########}~{meta.Max:#,##0.########} {meta.Unit} 범위로 입력하세요.");}
            if(p.PropertyType.IsEnum&&(x==null||!Enum.IsDefined(p.PropertyType,x)))throw new InvalidOperationException(meta.DisplayName+": 선택값을 확인하세요.");
        }
    }
    public static void Validate(AdvancedStrategyConfig s)
    {
        if(!Enum.IsDefined(s.StrategyType))throw new InvalidOperationException("전략을 선택하세요.");
        foreach(var o in new object[]{s.Common,s.Scalping,s.Box,s.Rebound,s.Risk,s.Execution})Object(o);
        foreach(var time in new[]{s.Common.TradingStart,s.Common.TradingEnd})if(!TimeOnly.TryParseExact(time,"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))throw new InvalidOperationException("매매 가능 시간은 한국시간 HH:mm 형식으로 입력하세요.");
        if(s.Rebound.MinimumCrashPercent>s.Rebound.MaximumCrashPercent)throw new InvalidOperationException("최소 급락률은 최대 급락률보다 클 수 없습니다.");
        if(s.Scalping.MomentumWindowSeconds>=s.Scalping.VolumeLookbackSeconds)throw new InvalidOperationException("거래량 비교기간은 상승속도 계산기간보다 길어야 합니다.");
        if(s.Scalping.ExitScoreThreshold>=s.Scalping.EntryScoreThreshold)throw new InvalidOperationException("청산 점수는 진입 점수보다 낮아야 합니다.");
    }
}
