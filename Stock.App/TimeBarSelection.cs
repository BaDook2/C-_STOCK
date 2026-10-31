using Stock.Core.Research.Oversold;

namespace Stock.App;

public sealed record TimeBarUnitOption(string Value,string Label);

internal static class TimeBarSelection
{
    private static readonly string[] MinuteValues=Enumerable.Range(1,59).Select(x=>x.ToString()).ToArray();
    private static readonly string[] HourValues=Enumerable.Range(1,12).Select(x=>x.ToString()).ToArray();
    public static TimeBarUnitOption[] Options {get;}=[new("분","분봉"),new("시","시간봉")];
    public static string NormalizeUnit(string? unit)=>unit=="시"?"시":"분";
    public static string[] Values(string? unit)=>NormalizeUnit(unit)=="시"?HourValues:MinuteValues;
    public static string NormalizeValue(string? value,string? unit)
    {
        var max=NormalizeUnit(unit)=="시"?12:59;
        return int.TryParse(value,out var parsed)?Math.Clamp(parsed,1,max).ToString():"1";
    }
    public static BarUnit Unit(string? unit)=>NormalizeUnit(unit)=="시"?BarUnit.Hour:BarUnit.Minute;
    public static BarSpecification Parse(string? value,string? unit)
    {
        var normalized=NormalizeUnit(unit);var specification=BarSpecification.Parse(value??"",Unit(normalized));var max=normalized=="시"?12:59;
        if(specification.Value>max)throw new InvalidOperationException($"{(normalized=="시"?"시간봉":"분봉")} 숫자는 1~{max}로 선택하세요.");
        return specification;
    }
}
