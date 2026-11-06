using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Stock.Core.Trading;
public sealed class TradingStore : IDisposable
{
    private readonly object gate=new();
    private readonly string connectionString;
    private bool disposed;
    public string Path { get; }
    public TradingStore(string path)
    {
        Path=System.IO.Path.GetFullPath(path); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        connectionString=new SqliteConnectionStringBuilder{DataSource=Path,Mode=SqliteOpenMode.ReadWriteCreate,DefaultTimeout=10,Pooling=false}.ToString();
        // Durable rollback journal. A normal crash alone does not imply SQLite file corruption.
        // Each operation owns a fresh, non-pooled connection; no live handle survives a repair/reopen.
        Exec("PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA busy_timeout=10000; CREATE TABLE IF NOT EXISTS presets(name TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE TABLE IF NOT EXISTS settings_history(id INTEGER PRIMARY KEY,at TEXT,name TEXT,old_json TEXT,new_json TEXT); CREATE TABLE IF NOT EXISTS events(id INTEGER PRIMARY KEY,market_key TEXT,source TEXT,at INTEGER,kind TEXT,digest TEXT UNIQUE,json TEXT); CREATE INDEX IF NOT EXISTS ix_events ON events(market_key,source,at); CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY,at TEXT,mode TEXT,settings TEXT,data_hash TEXT,result TEXT); CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY,run_id TEXT,at TEXT,category TEXT,json TEXT); CREATE INDEX IF NOT EXISTS ix_audit ON audit(run_id,id); ");
        Exec("CREATE TABLE IF NOT EXISTS state(key TEXT PRIMARY KEY,json TEXT NOT NULL); CREATE INDEX IF NOT EXISTS ix_events_raw_tick ON events(market_key,source,kind,at,id);");
        if(Access(db=>{using var migration=db.CreateCommand();migration.CommandText="PRAGMA user_version";return Convert.ToInt32(migration.ExecuteScalar());})<2)Exec("PRAGMA user_version=2");
    }
    private T Access<T>(Func<SqliteConnection,T> action,[System.Runtime.CompilerServices.CallerMemberName]string operation="")
    {
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            try{using var db=new SqliteConnection(connectionString);db.Open();using(var setup=db.CreateCommand()){setup.CommandText="PRAGMA synchronous=FULL; PRAGMA busy_timeout=10000";setup.ExecuteNonQuery();}return action(db);}
            catch(SqliteException ex)
            {
                var evidence=StorageDiagnostics.RecordFailure(Path,operation,ex);
                throw new StorageException($"저장소 작업에 실패했습니다 (SQLite {ex.SqliteErrorCode}/{ex.SqliteExtendedErrorCode}). 원본은 초기화하지 않았습니다. 진단: {evidence??"진단 파일 작성 실패"}",evidence,ex);
            }
        }
    }
    private void Exec(string sql, params (string,object?)[] args)
    { Access(db=>{using var c=db.CreateCommand();c.CommandText=sql; foreach(var (k,v) in args)c.Parameters.AddWithValue(k,v??DBNull.Value);return c.ExecuteNonQuery();},"SQL: "+sql[..Math.Min(sql.Length,80)]); }
    public IReadOnlyList<string> Presets() =>Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT name FROM presets ORDER BY name";using var r=c.ExecuteReader();var x=new List<string>();while(r.Read())x.Add(r.GetString(0));return x;});
    public TradingSettings Load(string name)
    =>Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT json FROM presets WHERE name=$n";c.Parameters.AddWithValue("$n",name);var raw=c.ExecuteScalar() as string;var s=raw==null?new TradingSettings():Serialization.Unpack<TradingSettings>(raw);s.Validate();return s;});
    public void Save(string name,TradingSettings settings)
    {
        settings.Validate();if(string.IsNullOrWhiteSpace(name)||name.Length>80)throw new InvalidOperationException("프리셋 이름은 1~80자입니다.");
        Access(db=>
        {
            using var tx=db.BeginTransaction();using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="SELECT json FROM presets WHERE name=$n";c.Parameters.AddWithValue("$n",name);var old=c.ExecuteScalar() as string;var json=Serialization.Pack(settings);
            using var write=db.CreateCommand();write.Transaction=tx;write.CommandText="INSERT INTO settings_history(at,name,old_json,new_json) VALUES($at,$n,$old,$new); INSERT INTO presets(name,json) VALUES($n,$new) ON CONFLICT(name) DO UPDATE SET json=excluded.json";
            write.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));write.Parameters.AddWithValue("$n",name);write.Parameters.AddWithValue("$old",(object?)old??DBNull.Value);write.Parameters.AddWithValue("$new",json);write.ExecuteNonQuery();tx.Commit();return 0;
        });
    }
    public int InsertEvents(IEnumerable<MarketEvent> events)
    {
        return Access(db=>
        {
            using var tx=db.BeginTransaction();using var command=db.CreateCommand();command.Transaction=tx;
            command.CommandText="INSERT OR IGNORE INTO events(market_key,source,at,kind,digest,json) VALUES($key,$source,$at,$kind,$hash,$json)";
            foreach(var name in new[]{"$key","$source","$at","$kind","$hash","$json"})command.Parameters.AddWithValue(name,name=="$at"?0L:"");
            command.Prepare();int count=0;
            foreach(var e in events){e.Validate();var json=Serialization.Pack(e);command.Parameters["$key"].Value=e.Key;command.Parameters["$source"].Value=e.Source;command.Parameters["$at"].Value=e.ExchangeTime.UtcTicks;command.Parameters["$kind"].Value=e.Kind.ToString();command.Parameters["$hash"].Value=Hash(json);command.Parameters["$json"].Value=json;count+=command.ExecuteNonQuery();}
            tx.Commit();return count;
        });
    }
    public IReadOnlyList<MarketEvent> ReadEvents(string instrument,string source,DateTimeOffset from,DateTimeOffset to,int limit=int.MaxValue)
    {
        return Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT json FROM events WHERE market_key=$key AND source=$source AND at >= $from AND at <= $to ORDER BY at,id LIMIT $limit";c.Parameters.AddWithValue("$key",instrument);c.Parameters.AddWithValue("$source",source);c.Parameters.AddWithValue("$from",from.UtcTicks);c.Parameters.AddWithValue("$to",to.UtcTicks);c.Parameters.AddWithValue("$limit",(long)limit+1);using var r=c.ExecuteReader();var x=new List<MarketEvent>();while(r.Read())x.Add(Serialization.Unpack<MarketEvent>(r.GetString(0)));if(x.Count>limit)throw new InvalidOperationException($"명시한 조회 한도 {limit:N0}건을 초과했습니다.");return x;});
    }
    public string BeginRun(string mode,TradingSettings settings,string dataHash="")
    {var id=Guid.NewGuid().ToString("N");lock(gate)Exec("INSERT INTO runs(id,at,mode,settings,data_hash) VALUES($id,$at,$mode,$s,$h)",("$id",id),("$at",DateTimeOffset.UtcNow.ToString("O")),("$mode",mode),("$s",Serialization.Pack(settings)),("$h",dataHash));return id;}
    /// <summary>All original events in database order, independent of any chart cap. Enumerate off the UI thread.</summary>
    public IEnumerable<MarketEvent> EnumerateEvents(string instrument,string source,DateTimeOffset from,DateTimeOffset to,CancellationToken token)
    {
        using var read=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Path,Mode=SqliteOpenMode.ReadOnly,DefaultTimeout=10,Pooling=false}.ToString());read.Open();
        using var command=read.CreateCommand();command.CommandText="SELECT json FROM events WHERE market_key=$key AND source=$source AND at >= $from AND at <= $to ORDER BY at,id";
        command.Parameters.AddWithValue("$key",instrument);command.Parameters.AddWithValue("$source",source);command.Parameters.AddWithValue("$from",from.UtcTicks);command.Parameters.AddWithValue("$to",to.UtcTicks);
        using var cancel=token.Register(command.Cancel);using var rows=command.ExecuteReader();while(rows.Read()){token.ThrowIfCancellationRequested();yield return Serialization.Unpack<MarketEvent>(rows.GetString(0));}
    }
    public void RunHash(string id,string hash){lock(gate)Exec("UPDATE runs SET data_hash=$h WHERE id=$id",("$h",hash),("$id",id));}
    public void FinishRun(string id,object result) {lock(gate)Exec("UPDATE runs SET result=$r WHERE id=$id",("$r",Serialization.Pack(result)),("$id",id));}
    public void Audit(string run,string category,object data,DateTimeOffset time)
    {lock(gate)Exec("INSERT INTO audit(run_id,at,category,json) VALUES($r,$t,$c,$j)",("$r",run),("$t",time.ToString("O")),("$c",category),("$j",Serialization.Pack(data)));}
    public IReadOnlyList<string> RecentAudit(int limit=300)
    =>Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT at || ' ' || category || ' ' || json FROM audit ORDER BY id DESC LIMIT $n";c.Parameters.AddWithValue("$n",limit);using var r=c.ExecuteReader();var a=new List<string>();while(r.Read())a.Add(r.GetString(0));return a;});
    public IReadOnlyList<string> History()
    =>Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT at || ' / ' || name || char(10) || COALESCE(old_json,'초기값') || char(10) || '→ ' || new_json FROM settings_history ORDER BY id DESC LIMIT 50";using var r=c.ExecuteReader();var a=new List<string>();while(r.Read())a.Add(r.GetString(0));return a;});
    public IReadOnlyList<string> Datasets()
    =>Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT market_key || ' / ' || source || ' / ' || COUNT(*) || ' events' FROM events GROUP BY market_key,source";using var r=c.ExecuteReader();var a=new List<string>();while(r.Read())a.Add(r.GetString(0));return a;});
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public T? State<T>(string key)=>Access(db=>{using var c=db.CreateCommand();c.CommandText="SELECT json FROM state WHERE key=$k";c.Parameters.AddWithValue("$k",key);var v=c.ExecuteScalar() as string;return v==null?default:Serialization.Unpack<T>(v);});
    public void State<T>(string key,T value) {lock(gate)Exec("INSERT INTO state(key,json) VALUES($k,$j) ON CONFLICT(key) DO UPDATE SET json=excluded.json",("$k",key),("$j",Serialization.Pack(value)));}
    public bool DeleteState(string key)
    =>Access(db=>{using var c=db.CreateCommand();c.CommandText="DELETE FROM state WHERE key=$k";c.Parameters.AddWithValue("$k",key);return c.ExecuteNonQuery()>0;});
    public IReadOnlyList<(string Key,string Json)> States(string prefix)
    {
        return Access(db=>
        {
            using var c=db.CreateCommand();
            // ASCII application prefixes can use the primary-key BINARY range directly.
            // This is an index optimization, not a substitute for an integrity check.
            if(prefix.Length>0&&prefix.All(ch=>ch<127))
            {
                c.CommandText="SELECT key,json FROM state WHERE key >= $from COLLATE BINARY AND key < $to COLLATE BINARY ORDER BY key";
                c.Parameters.AddWithValue("$from",prefix);c.Parameters.AddWithValue("$to",prefix[..^1]+(char)(prefix[^1]+1));
            }
            else c.CommandText="SELECT key,json FROM state ORDER BY rowid DESC";
            using var r=c.ExecuteReader();var rows=new List<(string,string)>();
            while(r.Read())
            {
                var key=r.GetString(0);
                if(key.StartsWith(prefix,StringComparison.Ordinal))rows.Add((key,r.GetString(1)));
            }
            return rows;
        });
    }
    public void Dispose(){lock(gate)disposed=true;}
}
public static class MarketCsv
{
    // Scalar fields remain human-readable; complete nullable metadata/L2 are carried in lossless JSON base64.
    public const string Header="version,exchange,symbol,currency,source,kind,exchange_time,received_time,trade_price,trade_size,cumulative_volume,bid,ask,bid_size,ask_size,event_base64";
    public static void Export(string path,IEnumerable<MarketEvent> events)
    {
        using var w=new StreamWriter(path,false,new UTF8Encoding(true));w.WriteLine(Header);
        foreach(var e in events) w.WriteLine(string.Join(',',"1",e.Instrument.Exchange,e.Instrument.Symbol,e.Instrument.Currency,e.Source,e.Kind,e.ExchangeTime.ToString("O"),e.ReceivedTime.ToString("O"),N(e.TradePrice),N(e.TradeSize),N(e.CumulativeVolume),N(e.Bid),N(e.Ask),N(e.BidSize),N(e.AskSize),Convert.ToBase64String(Encoding.UTF8.GetBytes(Serialization.Pack(e)))));
    }
    private static string N(decimal? x)=>x.HasValue?Serialization.Number(x.Value):"";
    public static IReadOnlyList<MarketEvent> Import(string path)
    {
        using var r=new StreamReader(path,Encoding.UTF8);if(r.ReadLine()!=Header)throw new InvalidOperationException("지원 CSV v1 헤더가 아닙니다. 샘플 내보내기 형식을 사용하세요.");var rows=new List<MarketEvent>();string? line;
        while((line=r.ReadLine())!=null)
        {
            if(rows.Count>=1000000)throw new InvalidOperationException("CSV는 100만 이벤트 이하로 나누세요.");var f=line.Split(',');if(f.Length!=16||f[0]!="1")throw new InvalidOperationException("CSV 열 개수/버전 오류");
            decimal? Number(int index)=>f[index]==""?null:decimal.Parse(f[index],System.Globalization.CultureInfo.InvariantCulture);
            MarketEvent e;
            if(f[15]=="")e=new(Instrument.Parse(f[1]+":"+f[2]),Enum.Parse<EventKind>(f[5]),DateTimeOffset.Parse(f[6]),DateTimeOffset.Parse(f[7]),f[4],rows.Count){TradePrice=Number(8),TradeSize=Number(9),CumulativeVolume=Number(10),Bid=Number(11),Ask=Number(12),BidSize=Number(13),AskSize=Number(14),TimeQuality="CSV 명시 시각"};
            else
            {
                e=Serialization.Unpack<MarketEvent>(Encoding.UTF8.GetString(Convert.FromBase64String(f[15])));
                if(e.Key!=$"{f[1]}:{f[2]}"||e.Instrument.Currency!=f[3]||e.Source!=f[4]||e.Kind.ToString()!=f[5]||e.ExchangeTime.ToString("O")!=f[6]||e.ReceivedTime.ToString("O")!=f[7]||N(e.TradePrice)!=f[8]||N(e.TradeSize)!=f[9]||N(e.CumulativeVolume)!=f[10]||N(e.Bid)!=f[11]||N(e.Ask)!=f[12]||N(e.BidSize)!=f[13]||N(e.AskSize)!=f[14])throw new InvalidOperationException("CSV 표시열과 원본 이벤트가 일치하지 않습니다.");
            }
            e.Validate();if(e.Instrument.Currency!=f[3])throw new InvalidOperationException("CSV 시장/통화 불일치");rows.Add(e);
        }
        return rows;
    }
}
