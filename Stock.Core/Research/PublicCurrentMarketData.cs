using System.Globalization;
using System.Text.Json;
using System.Collections.Concurrent;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Core.Research;

public sealed partial class PublicMarketData:IPublicCurrentBatchData,IFastCurrentMarketData,IFastPublicCurrentBatchData
{
    public static readonly TimeSpan ForegroundPollInterval=TimeSpan.FromSeconds(1);
    private sealed class CurrentQuoteSlot
    {
        public readonly SemaphoreSlim Gate=new(1,1);
        public DateTimeOffset NextRead;
        public CurrentMarketSnapshot? Snapshot;
        public Exception? Failure;
    }
    private readonly ConcurrentDictionary<string,CurrentQuoteSlot> currentQuotes=[];
    public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)=>CurrentCore(stock,token,false);
    public Task<CurrentMarketSnapshot> CurrentFast(StockItem stock,CancellationToken token)=>CurrentCore(stock,token,true);
    private async Task<CurrentMarketSnapshot> CurrentCore(StockItem stock,CancellationToken token,bool fast)
    {
        var slot=currentQuotes.GetOrAdd(stock.Key+(fast?"|foreground":""),_=>new());
        await slot.Gate.WaitAsync(token);
        try
        {
            if(DateTimeOffset.UtcNow<slot.NextRead)
            {
                if(slot.Failure!=null)throw new InvalidOperationException("공개 현재가 조회 재시도 대기",slot.Failure);
                if(slot.Snapshot==null)throw new InvalidOperationException("공개 현재가 캐시가 없습니다.");
                var remaining=slot.NextRead-DateTimeOffset.UtcNow;
                if(remaining>TimeSpan.FromMilliseconds(50))return slot.Snapshot;
                if(remaining>TimeSpan.Zero)await Task.Delay(remaining,token);
            }
            var started=DateTimeOffset.UtcNow;
            try
            {
                var snapshot=await ReadCurrent(stock,token);
                if(fast&&snapshot.Quote.State=="OPEN")snapshot=snapshot with{PollInterval=ForegroundPollInterval};
                slot.Snapshot=snapshot;slot.Failure=null;slot.NextRead=started+snapshot.PollInterval;return snapshot;
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
            catch(Exception ex){slot.Failure=ex;slot.NextRead=DateTimeOffset.UtcNow.AddSeconds(30);throw;}
        }
        finally{slot.Gate.Release();}
    }
    private async Task<CurrentMarketSnapshot> ReadCurrent(StockItem stock,CancellationToken token)
    {
        var kind=stock.Exchange==Exchange.KRX?"domestic":"worldstock";
        var code=stock.Exchange==Exchange.KRX?stock.Symbol:stock.ProviderCode;
        var root=await Json($"https://polling.finance.naver.com/api/realtime/{kind}/stock/{Uri.EscapeDataString(code)}",token);
        return ParseRealtimeStock(stock,root,DateTimeOffset.UtcNow);
    }
    /// <summary>여러 국내 종목을 한 요청으로 읽어 공개 실시간 스크리닝의 요청 수를 제한한다.</summary>
    public async Task<CurrentMarketSnapshot[]> CurrentBatch(IReadOnlyList<StockItem> stocks,CancellationToken token)
    {
        if(stocks.Count is <1 or >20||stocks.Any(s=>s.Exchange!=Exchange.KRX)||stocks.Select(s=>s.Key).Distinct().Count()!=stocks.Count)
            throw new InvalidOperationException("공개 시세 일괄 조회는 중복 없는 국내 종목 1~20개를 사용하세요.");
        var codes=string.Join(',',stocks.Select(s=>Uri.EscapeDataString(s.Symbol)));
        var root=await Json("https://polling.finance.naver.com/api/realtime/domestic/stock/"+codes,token);
        var received=DateTimeOffset.UtcNow;
        var snapshots=new List<CurrentMarketSnapshot>();
        foreach(var stock in stocks)
            try{snapshots.Add(ParseRealtimeStock(stock,root,received));}
            catch(InvalidOperationException){ /* A missing symbol must not hide the other quotes in this response. */ }
        if(snapshots.Count==0)throw new InvalidOperationException("공개 현재가 응답에 요청한 종목이 없습니다.");
        return snapshots.ToArray();
    }
    /// <summary>Visible real-time screening polls at most one 20-symbol request per second; failures retain the public HTTP cooldown.</summary>
    public async Task<CurrentMarketSnapshot[]> CurrentBatchFast(IReadOnlyList<StockItem> stocks,CancellationToken token)
        =>(await CurrentBatch(stocks,token)).Select(s=>s.Quote.State=="OPEN"?s with{PollInterval=ForegroundPollInterval}:s).ToArray();
    private static decimal? Number(string value)=>decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:null;
    /// <summary>StockViewer와 같은 공개 폴링 응답. 서버 관측과 거래소 체결시각을 구분한다.</summary>
    public static CurrentMarketSnapshot ParseRealtimeStock(StockItem stock,JsonElement root,DateTimeOffset received)
    {
        if(!root.TryGetProperty("datas",out var data)||data.ValueKind!=JsonValueKind.Array)throw new InvalidOperationException("공개 현재가 응답 형식을 확인할 수 없습니다.");
        var domestic=stock.Exchange==Exchange.KRX;
        var row=data.EnumerateArray().FirstOrDefault(r=>Text(r,domestic?"itemCode":"reutersCode")== (domestic?stock.Symbol:stock.ProviderCode));
        if(row.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException(stock.Name+": 공개 현재가가 없습니다.");
        decimal? Value(string raw,string formatted)=>Number(Text(row,raw))??Number(Text(row,formatted));
        var price=Value("closePriceRaw","closePrice")??0;
        if(price<=0)throw new InvalidOperationException(stock.Name+": 유효한 공개 현재가가 없습니다.");
        var change=Value("compareToPreviousClosePriceRaw","compareToPreviousClosePrice")??0;
        var direction=row.TryGetProperty("compareToPreviousPrice",out var comparison)?Text(comparison,"code"):"";
        change=direction switch{"4" or "5"=>-Math.Abs(change),"1" or "2"=>Math.Abs(change),"3"=>0,_=>change};
        var previous=price-change;
        var observed=DateTime.TryParseExact(Text(root,"time"),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var serverTime)
            ?new DateTimeOffset(serverTime,TimeSpan.FromHours(9)):received;
        DateTimeOffset? traded=DateTimeOffset.TryParse(Text(row,"localTradedAt"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var tradeTime)?tradeTime:null;
        var source="네이버 "+(domestic?"KRX":"미국")+" 공개 시세 · 지연 가능";
        if(!domestic&&row.TryGetProperty("stockExchangeType",out var exchange)&&Number(Text(exchange,"delayTime")) is >0 and var delay)
            source+=$" · {delay:N0}분 지연";
        var state=Text(row,"marketStatus");
        var volume=Value("accumulatedTradingVolumeRaw","accumulatedTradingVolume");
        if(!domestic&&row.TryGetProperty("overMarketPriceInfo",out var extended)&&extended.ValueKind==JsonValueKind.Object
            &&Text(extended,"tradingSessionType") is "PRE_MARKET" or "AFTER_MARKET"
            &&DateTimeOffset.TryParse(Text(extended,"localTradedAt"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var extendedAt)
            &&extendedAt>traded&&Number(Text(extended,"overPrice")) is >0 and var extendedPrice)
        {
            var session=Text(extended,"tradingSessionType");
            var baseClose=price;
            if(baseClose>0)
            {
                price=extendedPrice;change=price-baseClose;previous=baseClose;traded=extendedAt;
                state=string.IsNullOrWhiteSpace(Text(extended,"overMarketStatus"))?state:Text(extended,"overMarketStatus");source+=session=="PRE_MARKET"?" · 프리마켓":" · 애프터마켓";
                volume=Number(Text(extended,"accumulatedTradingVolumeRaw"))??volume;
            }
        }
        var interval=Number(Text(root,"pollingInterval"))??7000;
        if(interval<=0||interval>86_400_000)throw new InvalidOperationException("공개 시세 갱신 간격을 확인할 수 없습니다.");
        var time=domestic?observed:traded??observed;
        return new(stock,new(stock.Name,price,change,previous>0?(price/previous-1)*100:0,time,state),received,
            Value("openPriceRaw","openPrice"),Value("highPriceRaw","highPrice"),Value("lowPriceRaw","lowPrice"),volume,
            Number(Text(row,"accumulatedTradingValueRaw")),source,TimeSpan.FromMilliseconds((double)Math.Max(1000,interval)),ExchangeTimestamp:!domestic&&traded.HasValue);
    }
    public static CurrentMarketSnapshot ParseCurrentPoll(StockItem stock,JsonElement root,DateTimeOffset received)
    {
        var result=root.GetProperty("result");var row=result.GetProperty("areas").EnumerateArray().SelectMany(a=>a.GetProperty("datas").EnumerateArray()).First(r=>Text(r,"cd")==stock.Symbol);
        decimal N(string k)=>Number(Text(row,k))??throw new InvalidOperationException("공개 시세 필드 없음: "+k);
        var price=N("nv");var previous=N("pcv");if(previous<=0)throw new InvalidOperationException("전일 종가 없음");
        // Server observation time, not last execution time. Ignore NXT subtree to avoid mixing venues.
        var observed=DateTimeOffset.FromUnixTimeMilliseconds(result.GetProperty("time").GetInt64());
        var quote=new MarketQuote(Text(row,"nm"),price,price-previous,(price/previous-1)*100,observed,Text(row,"ms"));
        var interval=Number(Text(result,"pollingInterval"))??7000;
        var requested=TimeSpan.FromMilliseconds((double)Math.Max(1000,interval));
        var cadence=requested;
        return new(stock,quote,received,N("ov"),N("hv"),N("lv"),N("aq"),N("aa"),"네이버 KRX 공개 시세 · 지연 가능",cadence,ExchangeTimestamp:false);
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
