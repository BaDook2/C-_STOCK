using Microsoft.Data.Sqlite;
using Stock.Core.Trading;
namespace Stock.App;

public static class ChartTradeHistory
{
    public static IReadOnlyList<(string Key,string Json)> Read(Func<IReadOnlyList<(string Key,string Json)>> read,out bool unavailable)
    {
        try{var rows=read();unavailable=false;return rows;}
        catch(SqliteException){unavailable=true;return [];}
        catch(StorageException){unavailable=true;return [];}
    }
}
