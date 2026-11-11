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
        public int Failures;
    }
    private readonly ConcurrentDictionary<string,CurrentQuoteSlot> currentQuotes=[];
    public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)=>CurrentCore(stock,token);
    public Task<CurrentMarketSnapshot> CurrentFast(StockItem stock,CancellationToken token)=>CurrentCore(stock,token);
    private async Task<CurrentMarketSnapshot> CurrentCore(StockItem stock,CancellationToken token)
    {
        var slot=currentQuotes.GetOrAdd(stock.Key,_=>new());
        await slot.Gate.WaitAsync(token);
        try
        {
            if(clock.GetUtcNow()<slot.NextRead)
            {
                if(slot.Failure!=null)throw new PublicQuoteRetryException("공개 현재가 조회 재시도 대기",slot.NextRead-clock.GetUtcNow(),slot.Failure);
                if(slot.Snapshot==null)throw new InvalidOperationException("공개 현재가 캐시가 없습니다.");
                var remaining=slot.NextRead-clock.GetUtcNow();
                if(remaining>TimeSpan.FromMilliseconds(50))return slot.Snapshot;
                if(remaining>TimeSpan.Zero)await Task.Delay(remaining,token);
            }
            var started=clock.GetUtcNow();
            try
            {
                var snapshot=await ReadCurrent(stock,token,slot.Snapshot);
                snapshot=snapshot with{PollInterval=ForegroundPollInterval};
                slot.Snapshot=snapshot;slot.Failure=null;slot.Failures=0;slot.NextRead=started+snapshot.PollInterval;return snapshot;
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
            catch(Exception ex)when(ex is not OutOfMemoryException)
            {slot.Failure=ex;slot.Failures=Math.Min(slot.Failures+1,5);var delay=TimeSpan.FromSeconds(Math.Min(10,Math.Pow(2,slot.Failures-1)));if(ex is PublicQuoteRetryException limited&&limited.RetryAfter>delay)delay=limited.RetryAfter;slot.NextRead=clock.GetUtcNow()+delay;throw;}
        }
        finally{slot.Gate.Release();}
    }
    private async Task<CurrentMarketSnapshot> ReadCurrent(StockItem stock,CancellationToken token,CurrentMarketSnapshot? previous)
    {
        async Task<(CurrentMarketSnapshot? Snapshot,Exception? Error)> Primary()
        {
            try
            {
                var kind=stock.Exchange==Exchange.KRX?"domestic":"worldstock";var code=stock.Exchange==Exchange.KRX?stock.Symbol:stock.ProviderCode;
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(5));
                var root=await Json($"https://polling.finance.naver.com/api/realtime/{kind}/stock/{Uri.EscapeDataString(code)}",timeout.Token);
                return(ParseRealtimeStock(stock,root,clock.GetUtcNow()),null);
            }
            catch(Exception e)when(!token.IsCancellationRequested&&e is not OutOfMemoryException){return(null,e);}
        }
        async Task<(DayMarketBatch? Batch,bool Failed)> Daytime()
        {
            if(stock.Exchange==Exchange.KRX||dayMarket==null)return(null,false);
            try{dayMarket.Register(stock);return(await dayMarket.GetQuotesAsync([stock.Key],token),false);}
            catch(Exception e)when(!token.IsCancellationRequested&&e is HttpRequestException or IOException or JsonException or OperationCanceledException){return(null,true);}
        }
        var primary=Primary();var daytime=Daytime();await Task.WhenAll(primary,daytime);
        var (quote,error)=await primary;var (day,failed)=await daytime;
        return MergeCurrent(stock,quote,day,previous,failed,clock.GetUtcNow())??throw error??new InvalidOperationException(stock.Name+": 유효한 공개 현재가가 없습니다.");
    }
    /// <summary>StockViewer selection: newest actual venue price wins; source failures never manufacture a full close.</summary>
    public static CurrentMarketSnapshot? MergeCurrent(StockItem stock,CurrentMarketSnapshot? quote,DayMarketBatch? day,CurrentMarketSnapshot? previous,bool dayFailed,DateTimeOffset received)
    {
        if(day?.Quotes.TryGetValue(stock.Key,out var daytime)==true&&(quote?.ExchangeTimestamp!=true||daytime.Quote.TradeTime>=quote.Quote.TradeTime))quote=daytime;
        if(day?.OpenCodes.Contains(stock.Key)==true)
        {quote??=previous;if(quote!=null)quote=quote with{Quote=quote.Quote with{State="OPEN"}};}
        var uncertain=(dayFailed||day?.UnknownCodes.Contains(stock.Key)==true)&&quote?.Quote.State!="OPEN";
        if(quote==null)return null;
        var state=uncertain?"UNKNOWN":quote.Quote.State;
        // Keep last daytime/extended trade through closure or older provider replies, but use current session status.
        if(previous is {ExchangeTimestamp:true}&&quote.ExchangeTimestamp&&previous.Quote.TradeTime>quote.Quote.TradeTime)quote=previous;
        return quote with{Quote=quote.Quote with{State=state},ReceivedAt=received,PollInterval=ForegroundPollInterval,Source=quote.Source+(uncertain?" · 데이마켓 확인 지연 · 재시도 중":"")};
    }
    /// <summary>여러 국내 종목을 한 요청으로 읽어 공개 실시간 스크리닝의 요청 수를 제한한다.</summary>
    public async Task<CurrentMarketSnapshot[]> CurrentBatch(IReadOnlyList<StockItem> stocks,CancellationToken token)
    {
        if(stocks.Count is <1 or >20||stocks.Any(s=>s.Exchange!=Exchange.KRX)||stocks.Select(s=>s.Key).Distinct().Count()!=stocks.Count)
            throw new InvalidOperationException("공개 시세 일괄 조회는 중복 없는 국내 종목 1~20개를 사용하세요.");
        var codes=string.Join(',',stocks.Select(s=>Uri.EscapeDataString(s.Symbol)));
        var root=await Json("https://polling.finance.naver.com/api/realtime/domestic/stock/"+codes,token);
        var received=clock.GetUtcNow();
        var snapshots=new List<CurrentMarketSnapshot>();
        foreach(var stock in stocks)
            try{snapshots.Add(ParseRealtimeStock(stock,root,received));}
            catch(InvalidOperationException){ /* A missing symbol must not hide the other quotes in this response. */ }
        if(snapshots.Count==0)throw new InvalidOperationException("공개 현재가 응답에 요청한 종목이 없습니다.");
        return snapshots.ToArray();
    }
    /// <summary>Visible real-time screening polls at most one 20-symbol request per second; failures retain the public HTTP cooldown.</summary>
    public async Task<CurrentMarketSnapshot[]> CurrentBatchFast(IReadOnlyList<StockItem> stocks,CancellationToken token)
        =>(await CurrentBatch(stocks,token)).Select(s=>s with{PollInterval=ForegroundPollInterval}).ToArray();
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
        var regularPrice=price;var regularPrevious=price-change;var previous=regularPrevious;
        var observed=DateTime.TryParseExact(Text(root,"time"),"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var serverTime)
            ?new DateTimeOffset(serverTime,TimeSpan.FromHours(9)):received;
        DateTimeOffset? traded=DateTimeOffset.TryParse(Text(row,"localTradedAt"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var tradeTime)?tradeTime:null;
        var source="네이버 공개 시세 · "+(domestic?"KRX":"미국");
        if(row.TryGetProperty("currencyType",out var currency)&&currency.ValueKind==JsonValueKind.Object&&Text(currency,"code")!=stock.Currency)throw new InvalidOperationException("공개 현재가 통화가 일치하지 않습니다.");
        if(row.TryGetProperty("stockExchangeType",out var exchange)&&Number(Text(exchange,"delayTime")) is >0 and var delay)source+=$" · {delay:N0}분 지연";
        if(domestic&&Text(row,"marketSessionType")=="afterMarket")source+=" · 애프터마켓";
        var state=Text(row,"marketStatus");bool open=state=="OPEN",closed=state is "CLOSE" or "CLOSED";
        var volume=Value("accumulatedTradingVolumeRaw","accumulatedTradingVolume");var extendedSelected=false;
        foreach(var field in new[]{"overMarketPriceInfo","nxtOverMarketPriceInfo"})
        {
            if(!row.TryGetProperty(field,out var extended)||extended.ValueKind!=JsonValueKind.Object)continue;
            var session=Text(extended,"tradingSessionType");if(session is not("PRE_MARKET" or "REGULAR_MARKET" or "AFTER_MARKET")||!domestic&&session=="REGULAR_MARKET")continue;
            var status=Text(extended,"overMarketStatus");open|=status=="OPEN";closed|=status is "CLOSE" or "CLOSED";
            if(!DateTimeOffset.TryParse(Text(extended,"localTradedAt"),CultureInfo.InvariantCulture,DateTimeStyles.None,out var at)||traded.HasValue&&at<traded||at==traded&&(status!="OPEN"||Text(row,"marketStatus")=="OPEN")||Number(Text(extended,"overPrice")) is not >0)continue;
            var value=Number(Text(extended,"overPrice"))!.Value;
            decimal basis;
            if(domestic)
            {
                var ratio=Number(Text(extended,"fluctuationsRatio"));if(ratio==null)continue;
                var sign=extended.TryGetProperty("compareToPreviousPrice",out var d)?Text(d,"code"):"";
                ratio=sign switch{"4" or "5"=>-Math.Abs(ratio.Value),"1" or "2"=>Math.Abs(ratio.Value),"3"=>0,_=>ratio};
                if(1+ratio/100<=0)continue;basis=value/(1+ratio.Value/100);
            }
            else basis=session=="PRE_MARKET"?regularPrice:regularPrevious;
            if(basis<=0)continue;
            price=value;previous=basis;traded=at;extendedSelected=true;
            var label=session switch{"PRE_MARKET"=>"프리마켓","AFTER_MARKET"=>"애프터마켓",_=>"메인마켓"};
            source=domestic?"네이버 공개 시세 · NXT · "+label:source+" · "+label;
            volume=Number(Text(extended,"accumulatedTradingVolumeRaw"));
        }
        state=open?"OPEN":closed?"CLOSE":state;
        var interval=ReadPollingInterval(root);
        var time=traded??observed;
        return new(stock,new(stock.Name,price,price-previous,previous>0?(price/previous-1)*100:0,time,state),received,
            extendedSelected?null:Value("openPriceRaw","openPrice"),extendedSelected?null:Value("highPriceRaw","highPrice"),extendedSelected?null:Value("lowPriceRaw","lowPrice"),volume,
            extendedSelected?null:Number(Text(row,"accumulatedTradingValueRaw")),source+(extendedSelected?" · OHLCV 미제공":""),interval,ExchangeTimestamp:traded.HasValue);
    }
    public static TimeSpan ReadPollingInterval(JsonElement root)
    {
        var ms=Number(Text(root,"pollingInterval"));
        return ms is >0 and <=86400000?TimeSpan.FromMilliseconds((double)Math.Max(1000,ms.Value)):ForegroundPollInterval;
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
        return ParseCurrentSecurity(stock,root,clock.GetUtcNow());
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
