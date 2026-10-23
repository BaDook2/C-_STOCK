using Stock.Core.Trading;
namespace Stock.Core.Research;
/// <summary>Historical or streaming provider → original SQLite events. No chart aggregation or tick cap.</summary>
public static class RawTickIngestion
{
    public static async Task<int> Import(TradingStore store,IMarketDataProvider provider,CancellationToken token)
    {
        var pending=new List<MarketEvent>(512);int count=0;
        try{await foreach(var e in provider.Stream(token).WithCancellation(token)){e.Validate();pending.Add(e);if(pending.Count==512){var batch=pending.ToArray();pending.Clear();count+=await Task.Run(()=>store.InsertEvents(batch),CancellationToken.None);}}}
        finally{if(pending.Count>0)count+=await Task.Run(()=>store.InsertEvents(pending),CancellationToken.None);}
        return count;
    }
}
