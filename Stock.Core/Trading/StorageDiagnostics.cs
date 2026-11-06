using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;

namespace Stock.Core.Trading;

public sealed record StorageJsonIssue(string Table,long RowId,string Problem);
public sealed class StorageInspection
{
    public DateTimeOffset At {get;init;}=DateTimeOffset.UtcNow;
    public string DatabasePath {get;init;}="";
    public long Bytes {get;set;}
    public string Engine {get;set;}="";
    public string SourceId {get;set;}="";
    public string JournalMode {get;set;}="";
    public List<string> Integrity {get;}=[];
    public Dictionary<string,long> Rows {get;}=[];
    public List<StorageJsonIssue> JsonIssues {get;}=[];
    public string? Error {get;set;}
    public bool Healthy=>Error==null&&Integrity.Count==1&&Integrity[0]=="ok"&&JsonIssues.Count==0;
}

public sealed class StorageException(string message,string? reportPath,Exception? inner=null):InvalidOperationException(message,inner)
{public string? ReportPath {get;}=reportPath;}

/// <summary>Read-only integrity/JSON checks and private failure evidence. Never repairs or replaces a database.</summary>
public static class StorageDiagnostics
{
    public static StorageInspection Inspect(string path,bool deep=true)
    {
        if(!System.IO.Path.IsPathFullyQualified(path))throw new ArgumentException("DB 진단에는 절대 경로가 필요합니다.",nameof(path));
        var report=new StorageInspection{DatabasePath=System.IO.Path.GetFullPath(path)};
        try
        {
            report.Bytes=new FileInfo(path).Length;
            using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=10}.ToString());db.Open();
            using var tx=db.BeginTransaction(deferred:true);
            object? Scalar(string sql){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText=sql;return c.ExecuteScalar();}
            report.Engine=Scalar("SELECT sqlite_version()")?.ToString()??"";report.SourceId=Scalar("SELECT sqlite_source_id()")?.ToString()??"";report.JournalMode=Scalar("PRAGMA journal_mode")?.ToString()??"";
            using(var check=db.CreateCommand()){check.Transaction=tx;check.CommandText="PRAGMA integrity_check";using var rows=check.ExecuteReader();while(rows.Read())report.Integrity.Add(rows.GetString(0));}
            foreach(var (table,columns) in new[]{("state",new[]{"json"}),("presets",new[]{"json"}),("events",new[]{"json"}),("audit",new[]{"json"}),("runs",new[]{"settings","result"}),("settings_history",new[]{"old_json","new_json"})})
            {
                if(Convert.ToInt64(Scalar($"SELECT count(*) FROM sqlite_schema WHERE type='table' AND name='{table}'"))==0)continue;
                report.Rows[table]=Convert.ToInt64(Scalar($"SELECT count(*) FROM {table}"));
                if(!deep)continue;
                foreach(var column in columns)
                {
                    using var cmd=db.CreateCommand();cmd.Transaction=tx;cmd.CommandText=$"SELECT rowid,CAST({column} AS BLOB) FROM {table} NOT INDEXED";using var rows=cmd.ExecuteReader();
                    while(rows.Read())
                    {
                        if(rows.IsDBNull(1))continue;var bytes=(byte[])rows[1];
                        try{new UTF8Encoding(false,true).GetString(bytes);using var json=JsonDocument.Parse(bytes);}
                        catch(DecoderFallbackException){report.JsonIssues.Add(new(table,rows.GetInt64(0),column+": invalid UTF-8"));}
                        catch(JsonException){report.JsonIssues.Add(new(table,rows.GetInt64(0),column+": invalid JSON"));}
                    }
                }
            }
        }
        catch(SqliteException ex){report.Error=$"SQLite {ex.SqliteErrorCode}/{ex.SqliteExtendedErrorCode}: {ex.Message}";}
        catch(IOException ex){report.Error=ex.GetType().Name;}
        catch(UnauthorizedAccessException){report.Error="Access denied";}
        return report;
    }

    public static string? RecordFailure(string path,string operation,SqliteException error)
    {
        try
        {
            var folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!,"storage-diagnostics");Directory.CreateDirectory(folder);
            var destination=System.IO.Path.Combine(folder,$"failure-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.json");
            // Do not log SQL parameter values, account data, JSON payloads, or key names.
            var evidence=new{Operation=operation,Code=error.SqliteErrorCode,ExtendedCode=error.SqliteExtendedErrorCode,FailureStack=error.StackTrace,ProcessId=Environment.ProcessId,ProcessPath=Environment.ProcessPath,Runtime=Environment.Version.ToString(),IndependentConnection=Inspect(path,false)};
            File.WriteAllText(destination,JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));return destination;
        }
        catch{return null;} // Preserve the original DB failure even if its evidence cannot be written.
    }
}
