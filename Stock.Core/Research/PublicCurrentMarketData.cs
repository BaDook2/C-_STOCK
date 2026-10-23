using System.Globalization;
using System.Text.Json;
using System.Collections.Concurrent;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Core.Research;

public sealed partial class PublicMarketData
{
    private sealed class CurrentQuoteSlot
    {
        public readonly SemaphoreSlim Gate=new(1,1);
        public DateTimeOffset NextRead;
        public CurrentMarketSnapshot? Snapshot;
    }
    private readonly ConcurrentDictionary<string,CurrentQuoteSlot> currentQuotes=[];
    public async Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)
    {
        var slot=currentQuotes.GetOrAdd(stock.Key,_=>new());
        await slot.Gate.WaitAsync(token);
        try
        {
            if(slot.Snapshot!=null&&DateTimeOffset.UtcNow<slot.NextRead)return slot.Snapshot;
            try{var snapshot=await ReadCurrent(stock,token);slot.Snapshot=snapshot;slot.NextRead=DateTimeOffset.UtcNow+snapshot.PollInterval;return snapshot;}
            catch{slot.NextRead=DateTimeOffset.UtcNow.AddSeconds(30);throw;}
        }
        finally{slot.Gate.Release();}
    }
    private async Task<CurrentMarketSnapshot> ReadCurrent(StockItem stock,CancellationToken token)
    {
        if(stock.Exchange==Exchange.KRX)
            return ParseCurrentPoll(stock,await Json("https://polling.finance.naver.com/api/realtime?query="+Uri.EscapeDataString("SERVICE_ITEM:"+stock.Symbol),token),DateTimeOffset.UtcNow);
        var root=await Json($"https://api.stock.naver.com/stock/{Uri.EscapeDataString(stock.ProviderCode)}/basic",token);
        var quote=ParseQuote(root);
        var extra=await Json($"https://api.stock.naver.com/stock/{Uri.EscapeDataString(stock.ProviderCode)}/integration",token);
        decimal? Info(string code)
        {
            if(!extra.TryGetProperty("totalInfos",out var values)||values.ValueKind!=JsonValueKind.Array)return null;
            var text=values.EnumerateArray().FirstOrDefault(v=>Text(v,"code")==code);
            return text.ValueKind==JsonValueKind.Object?Number(Text(text,"value")):null;
        }
        return new(stock,quote,DateTimeOffset.UtcNow,Info("openPrice"),Info("highPrice"),Info("lowPrice"),Info("accumulatedTradingVolume"),null,"네이버 해외 공개 시세 · 지연 가능",TimeSpan.FromSeconds(7));
    }
    private static decimal? Number(string value)=>decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:null;
    public static CurrentMarketSnapshot ParseCurrentPoll(StockItem stock,JsonElement root,DateTimeOffset received)
    {
        var result=root.GetProperty("result");var row=result.GetProperty("areas").EnumerateArray().SelectMany(a=>a.GetProperty("datas").EnumerateArray()).First(r=>Text(r,"cd")==stock.Symbol);
        decimal N(string k)=>Number(Text(row,k))??throw new InvalidOperationException("공개 시세 필드 없음: "+k);
        var price=N("nv");var previous=N("pcv");if(previous<=0)throw new InvalidOperationException("전일 종가 없음");
        // Server observation time, not last execution time. Ignore NXT subtree to avoid mixing venues.
        var observed=DateTimeOffset.FromUnixTimeMilliseconds(result.GetProperty("time").GetInt64());
        var quote=new MarketQuote(Text(row,"nm"),price,price-previous,(price/previous-1)*100,observed,Text(row,"ms"));
        var interval=Number(Text(result,"pollingInterval"))??7000;
        return new(stock,quote,received,N("ov"),N("hv"),N("lv"),N("aq"),N("aa"),"네이버 KRX 공개 시세 · 지연 가능",TimeSpan.FromMilliseconds((double)Math.Max(1000,interval)),ExchangeTimestamp:false);
    }
    public async Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)
    {
        if(stock.Exchange!=Exchange.KRX)return null;
        var root=await Json($"https://m.stock.naver.com/api/stock/{Uri.EscapeDataString(stock.Symbol)}/basic",token);
        return ParseCurrentSecurity(stock,root,DateTimeOffset.UtcNow);
    }
    public static DailySecurityStatus ParseCurrentSecurity(StockItem stock,JsonElement root,DateTimeOffset now)
    {
        if(Text(root,"itemCode")!=stock.Symbol)throw new InvalidOperationException("현재 종목 상태 응답 불일치");
        var board=root.TryGetProperty("stockExchangeType",out var exchange)?Text(exchange,"name") switch{"KOSPI"=>HistoricalBoard.Kospi,"KOSDAQ"=>HistoricalBoard.Kosdaq,_=>HistoricalBoard.Unknown}:HistoricalBoard.Unknown;
        // 'stock' includes preferred/SPAC securities; absence of a warning is not proof of normal status.
        var kind=Text(root,"stockEndType") switch{"etf"=>HistoricalSecurityKind.Etf,"etn"=>HistoricalSecurityKind.Etn,_=>HistoricalSecurityKind.Unknown};
        bool? suspended=root.TryGetProperty("tradeStopType",out var stop)?Text(stop,"name") switch{"TRADING"=>false,"HALT" or "SUSPENDED" or "STOP"=>true,_=>null}:null;
        return new(stock.Symbol,DailyQuoteMerger.Date(now,stock.Exchange),now,board,kind,suspended,null);
    }
}
