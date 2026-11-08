using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void StorageDiagnosticsTests()
    {
        var path=Path.Combine(Dir,"storage-health.db");
        using(var store=new TradingStore(path))store.State("probe",new{Value=7});
        var healthy=StorageDiagnostics.Inspect(path);
        Check(healthy.Healthy&&healthy.Rows["state"]==1&&healthy.Engine.Length>0&&healthy.DatabasePath==Path.GetFullPath(path),"storage inspection uses the explicit absolute path and checks full integrity plus JSON");
        Check(File.Exists(healthy.PhysicalDatabasePath)&&StoragePaths.PhysicalPath(path)==healthy.PhysicalDatabasePath,"storage inspection records the actual opened file path instead of hiding AppData redirection");
        Throws<ArgumentException>(()=>StorageDiagnostics.Inspect("relative.db"),"diagnostics refuses an ambiguous relative database path");
        using(var store=new TradingStore(path))
        {
            var replacement=Path.Combine(Dir,"replacement.db");using(var other=new TradingStore(replacement))other.State("probe",new{Value=9});
            var previous=path+".original";File.Move(path,previous);File.Copy(replacement,path);
            Check(store.State<JsonElement>("probe").GetProperty("Value").GetInt32()==9,"completed operations release every handle and the next read cannot reuse a cached connection to the previous file");
        }
        using(var raw=new SqliteConnection("Data Source="+path+";Pooling=False"))
        {raw.Open();using var command=raw.CreateCommand();command.CommandText="UPDATE state SET json='{' WHERE key='probe'";command.ExecuteNonQuery();}
        var invalid=StorageDiagnostics.Inspect(path);
        Check(!invalid.Healthy&&invalid.Integrity.Single()=="ok"&&invalid.JsonIssues.Single().Table=="state","valid SQLite structure and invalid application JSON are reported separately");
        var broken=Path.Combine(Dir,"storage-corrupt.db");using(var store=new TradingStore(broken))store.State("fixture-private-key",new{Secret="never-log-this-private-value"});
        long page;using(var raw=new SqliteConnection("Data Source="+broken+";Pooling=False")){raw.Open();using var c=raw.CreateCommand();c.CommandText="SELECT rootpage FROM sqlite_schema WHERE name='state'";page=(long)c.ExecuteScalar()!;}
        using(var file=new FileStream(broken,FileMode.Open,FileAccess.Write)){file.Position=(page-1)*4096;file.WriteByte(255);}
        var damaged=StorageDiagnostics.Inspect(broken);
        Check(!damaged.Healthy&&damaged.Error!=null,"a genuinely corrupted state B-tree fails read-only integrity checks");
        StorageException? failure=null;
        try{using var store=new TradingStore(broken);store.States("");}catch(StorageException ex){failure=ex;}
        Check(failure?.InnerException is SqliteException {SqliteErrorCode:11}&&File.Exists(failure.ReportPath),"real SQLite error 11 preserves the native cause and writes independent-connection evidence");
        var evidence=File.ReadAllText(failure!.ReportPath!);
        Check(!evidence.Contains("never-log-this-private-value")&&!evidence.Contains("fixture-private-key")&&evidence.Contains("ExtendedCode"),"DB failure evidence omits row values and keys but retains the extended error code");
        Check(!StorageDiagnostics.Inspect(broken).Healthy,"failed operations do not silently erase or recreate corrupted user data");
        using(var disposed=new TradingStore(Path.Combine(Dir,"disposed-store.db"))){disposed.Dispose();Throws<ObjectDisposedException>(()=>disposed.State("x",1),"disposed store cannot reopen itself from a late background callback");}
    }
}
