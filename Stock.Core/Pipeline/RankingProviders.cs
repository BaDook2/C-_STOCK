using System.Globalization;
using System.Text.Json;
using Stock.Core.Trading;
namespace Stock.Core.Pipeline;
/// <summary>시점별 외부 관측 자료. null은 미제공이며 실제 값처럼 대체하지 않습니다.</summary>
public sealed record SymbolObservation
{
    public string Symbol {get;init;}="";
    public DateTimeOffset At {get;init;}
    public bool IsWatchlist {get;init;}
    public bool IsOTC {get;init;}
    public int? SearchRank {get;init;}
    public int? PreviousRank {get;init;}
    public DateTimeOffset? PreviousRankAt {get;init;}
    public decimal? MarketCap {get;init;}
    public decimal? DayTurnover {get;init;}
    public decimal? DayVolume {get;init;}
    public decimal? Average20DayVolume {get;init;}
    public decimal? SameTime5MinAverageVolume {get;init;}
    public decimal? Vwap {get;init;}
    public decimal? PreviousLow {get;init;}
    public decimal? Resistance {get;init;}
    public decimal? RecentHigh {get;init;}
    public int RecentHighDays {get;init;}=20;
    public string Source {get;init;}="Manual";
}
public interface ISearchRankingProvider
{
    RankingSource Source {get;}
    Task<IReadOnlyList<SymbolObservation>> ReadAsync(DateTimeOffset asOf,CancellationToken token);
}
public interface ITrendingSymbolProvider { Task<IReadOnlyList<string>> SymbolsAsync(DateTimeOffset asOf,CancellationToken token); }
public class ManualInputProvider(IReadOnlyList<SymbolObservation> rows):ISearchRankingProvider,ITrendingSymbolProvider
{
    public virtual RankingSource Source=>RankingSource.Manual;
    public Task<IReadOnlyList<SymbolObservation>> ReadAsync(DateTimeOffset asOf,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult<IReadOnlyList<SymbolObservation>>(rows.Where(r=>r.At<=asOf).GroupBy(r=>r.Symbol).Select(g=>g.MaxBy(r=>r.At)!).ToArray());}
    public async Task<IReadOnlyList<string>> SymbolsAsync(DateTimeOffset asOf,CancellationToken token)=>(await ReadAsync(asOf,token)).Select(r=>r.Symbol).Distinct().ToArray();
}
public sealed class CsvImportProvider(IReadOnlyList<SymbolObservation> rows):ManualInputProvider(rows)
{
    public override RankingSource Source=>RankingSource.CSV;
    public static IReadOnlyList<SymbolObservation> Parse(string text)
    {
        var lines=text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);if(lines.Length<2)throw new InvalidOperationException("CSV 헤더와 자료 행을 입력하세요.");
        var headers=Split(lines[0]);var properties=typeof(SymbolObservation).GetProperties().ToDictionary(p=>p.Name,StringComparer.OrdinalIgnoreCase);
        if(!headers.Contains("Symbol",StringComparer.OrdinalIgnoreCase)||!headers.Contains("At",StringComparer.OrdinalIgnoreCase)||headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=headers.Length||headers.Any(h=>!properties.ContainsKey(h)))throw new InvalidOperationException("CSV에는 Symbol, At 열이 필요하며 중복/알 수 없는 열은 사용할 수 없습니다.");
        var result=new List<SymbolObservation>();
        for(int i=1;i<lines.Length;i++)try
        {
            var cells=Split(lines[i]);if(cells.Length!=headers.Length)throw new FormatException();var row=new SymbolObservation{Source="CSV"};
            for(int n=0;n<headers.Length;n++){var p=properties[headers[n]];var value=cells[n];if(value=="")continue;var type=Nullable.GetUnderlyingType(p.PropertyType)??p.PropertyType;object item;if(type==typeof(string))item=value;else if(type==typeof(DateTimeOffset))item=ParseTime(value);else if(type==typeof(bool))item=bool.Parse(value);else if(type==typeof(int))item=int.Parse(value,NumberStyles.Number,CultureInfo.InvariantCulture);else item=decimal.Parse(value,NumberStyles.Number,CultureInfo.InvariantCulture);p.SetValue(row,item);}
            Validate(row);result.Add(row);
        }catch(Exception ex) when(ex is FormatException or OverflowException or ArgumentException or InvalidOperationException){throw new InvalidOperationException($"CSV {i+1:N0}행: 종목·시각·숫자 형식을 확인하세요.");}
        return result;
    }
    public static DateTimeOffset ParseTime(string value)
    {if(!System.Text.RegularExpressions.Regex.IsMatch(value,@"(Z|[+-]\d{2}:\d{2})$"))throw new FormatException();return DateTimeOffset.Parse(value,CultureInfo.InvariantCulture);}
    public static void Validate(SymbolObservation row)
    {Instrument.Parse(row.Symbol);if(row.At==default||row.SearchRank is <=0||row.PreviousRank is <=0||row.PreviousRankAt>=row.At||row.RecentHighDays<=0||new[]{row.MarketCap,row.DayTurnover,row.DayVolume,row.Average20DayVolume,row.SameTime5MinAverageVolume,row.Vwap,row.PreviousLow,row.Resistance,row.RecentHigh}.Any(n=>n<0))throw new InvalidOperationException("자료의 시각·순위·금액 범위를 확인하세요.");}
    private static string[] Split(string line)
    {var fields=new List<string>();var b=new System.Text.StringBuilder();bool quoted=false;for(int i=0;i<line.Length;i++){var c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){b.Append('"');i++;}else quoted=!quoted;}else if(c==','&&!quoted){fields.Add(b.ToString().Trim());b.Clear();}else b.Append(c);}if(quoted)throw new FormatException("CSV 따옴표를 확인하세요.");fields.Add(b.ToString().Trim());return fields.ToArray();}
}
/// <summary>사용자가 명시적으로 선택하는 검증용 자료만 제공합니다. 실제 순위 API 대용으로 자동 선택하지 않습니다.</summary>
public sealed class MockRankingProvider(IReadOnlyList<SymbolObservation> rows):ManualInputProvider(rows){public override RankingSource Source=>RankingSource.Mock;}
public sealed class UnavailableRankingProvider(RankingSource source):ISearchRankingProvider
{
    public RankingSource Source=>source;
    public Task<IReadOnlyList<SymbolObservation>> ReadAsync(DateTimeOffset asOf,CancellationToken token)=>throw new InvalidOperationException(source+" 검색순위 API는 연결되지 않았습니다. Manual / CSV / Mock을 선택하세요.");
}
public static class RankingProviderFactory
{
    public static ISearchRankingProvider Create(RankingSource source,IReadOnlyList<SymbolObservation> rows)=>source switch {RankingSource.Manual=>new ManualInputProvider(rows),RankingSource.CSV=>new CsvImportProvider(rows),RankingSource.Mock=>new MockRankingProvider(rows),_=>new UnavailableRankingProvider(source)};
}
