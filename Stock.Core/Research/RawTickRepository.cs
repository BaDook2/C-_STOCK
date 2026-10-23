using Microsoft.Data.Sqlite;
using System.Text.Json;
using Stock.Core.Trading;
namespace Stock.Core.Research;

public readonly record struct TickKey(long UtcTicks,long RowId):IComparable<TickKey>
{public int CompareTo(TickKey other){var time=UtcTicks.CompareTo(other.UtcTicks);return time!=0?time:RowId.CompareTo(other.RowId);}}
public readonly record struct RawTick(TickKey Key,long Sequence,decimal Price,decimal Size)
{public DateTimeOffset Time=>new(Key.UtcTicks,TimeSpan.Zero);}
public sealed record TickExtent(TickKey First,TickKey Last,long Count);
public sealed record TickPage(RawTick[] Ticks,bool More);

/// <summary>Read-only chart projection of the original event table; keys preserve all equal-time executions.</summary>
public sealed class RawTickRepository(string path)
{
    private SqliteConnection Open(){var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,DefaultTimeout=10}.ToString());db.Open();return db;}
    public Task<string[]> Sources(string symbol,CancellationToken token)=>Task.Run(()=>{using var db=Open();using var c=db.CreateCommand();c.CommandText="SELECT DISTINCT source FROM events WHERE market_key=$k AND kind='Trade' ORDER BY source";c.Parameters.AddWithValue("$k",symbol);using var r=c.ExecuteReader();var sources=new List<string>();while(r.Read()){token.ThrowIfCancellationRequested();sources.Add(r.GetString(0));}return sources.ToArray();},token);
    public Task<TickExtent?> Extent(string symbol,string source,CancellationToken token)=>Task.Run(()=>
    {
        using var db=Open();using var c=db.CreateCommand();c.CommandText="SELECT at,id FROM events WHERE market_key=$k AND source=$s AND kind='Trade' ORDER BY at,id LIMIT 1";Args(c,symbol,source);TickKey first,last;
        using(var r=c.ExecuteReader()){if(!r.Read())return null;first=new(r.GetInt64(0),r.GetInt64(1));}token.ThrowIfCancellationRequested();
        c.CommandText="SELECT at,id FROM events WHERE market_key=$k AND source=$s AND kind='Trade' ORDER BY at DESC,id DESC LIMIT 1";using(var r=c.ExecuteReader()){r.Read();last=new(r.GetInt64(0),r.GetInt64(1));}
        c.CommandText="SELECT COUNT(*) FROM events WHERE market_key=$k AND source=$s AND kind='Trade'";return new TickExtent(first,last,(long)c.ExecuteScalar()!);
    },token);
    public Task<TickPage> Range(string symbol,string source,long from,long to,int limit,CancellationToken token)=>Query(symbol,source,"at >= $a AND at <= $b",from,to,0,limit,false,token);
    public Task<TickPage> After(string symbol,string source,TickKey key,int limit,CancellationToken token)=>Query(symbol,source,"(at,id)>($a,$id)",key.UtcTicks,0,key.RowId,limit,false,token);
    public Task<TickPage> Before(string symbol,string source,TickKey key,int limit,CancellationToken token)=>Query(symbol,source,"(at,id)<($a,$id)",key.UtcTicks,0,key.RowId,limit,true,token);
    private Task<TickPage> Query(string symbol,string source,string predicate,long a,long b,long id,int limit,bool reverse,CancellationToken token)=>Task.Run(()=>
    {
        if(limit<1)throw new ArgumentOutOfRangeException(nameof(limit));token.ThrowIfCancellationRequested();using var db=Open();using var c=db.CreateCommand();
        c.CommandText=$"SELECT id,at,json FROM events WHERE market_key=$k AND source=$s AND kind='Trade' AND {predicate} ORDER BY at {(reverse?"DESC":"ASC")},id {(reverse?"DESC":"ASC")} LIMIT $n";
        Args(c,symbol,source);c.Parameters.AddWithValue("$a",a);c.Parameters.AddWithValue("$b",b);c.Parameters.AddWithValue("$id",id);c.Parameters.AddWithValue("$n",(long)limit+1);
        using var cancel=token.Register(c.Cancel);using var reader=c.ExecuteReader();var rows=new List<RawTick>();while(reader.Read()){token.ThrowIfCancellationRequested();using var json=JsonDocument.Parse(reader.GetString(2));var root=json.RootElement;rows.Add(new(new(reader.GetInt64(1),reader.GetInt64(0)),root.GetProperty("Sequence").GetInt64(),root.GetProperty("TradePrice").GetDecimal(),root.TryGetProperty("TradeSize",out var v)&&v.ValueKind==JsonValueKind.Number?v.GetDecimal():0));}
        bool more=rows.Count>limit;if(more)rows.RemoveAt(rows.Count-1);if(reverse)rows.Reverse();return new TickPage(rows.ToArray(),more);
    },token);
    private static void Args(SqliteCommand c,string key,string source){c.Parameters.AddWithValue("$k",key);c.Parameters.AddWithValue("$s",source);}
}
