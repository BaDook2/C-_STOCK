using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Globalization;

namespace Stock.Core.Trading;
public sealed class DataQualityGate(DataSettings settings,ExecutionSettings execution)
{
    private readonly Dictionary<string,MarketEvent> last=[];
    private readonly Dictionary<string,decimal> cumulative=[];
    private readonly Dictionary<string,DateTimeOffset> latest=[];
    public int Duplicates { get; private set; }
    public int OutOfOrder { get; private set; }
    public int Gaps { get; private set; }
    public int Stale { get; private set; }
    public bool Accept(MarketEvent e,bool live,out string reason)
    {
        reason="정상";e.Validate();var key=e.Key+":"+e.Kind;
        if(latest.TryGetValue(e.Key,out var latestTime)&&e.ExchangeTime<latestTime){OutOfOrder++;reason="체결/호가 간 역순";return false;}
        if(live&&(e.ReceivedTime-e.ExchangeTime).Duration().TotalSeconds>execution.StaleSeconds){Stale++;reason="stale/시각 불확실";return false;}
        if(last.TryGetValue(key,out var p))
        {
            if(e.ExchangeTime<p.ExchangeTime){OutOfOrder++;reason="역순 이벤트";return false;}
            if(e.Sequence==p.Sequence&&e.Source==p.Source&&e.ExchangeTime==p.ExchangeTime&&e.TradePrice==p.TradePrice&&e.TradeSize==p.TradeSize&&e.CumulativeVolume==p.CumulativeVolume&&e.Bid==p.Bid&&e.Ask==p.Ask&&e.BidSize==p.BidSize&&e.AskSize==p.AskSize&&Serialization.Pack(e.Bids)==Serialization.Pack(p.Bids)&&Serialization.Pack(e.Asks)==Serialization.Pack(p.Asks)){Duplicates++;reason="중복 이벤트";return false;}
            if((e.ExchangeTime-p.ExchangeTime).TotalSeconds>settings.GapSeconds){Gaps++;reason="수신 간격 큼: 비거래/누락 구분 불가";}
        }
        if(e.Kind==EventKind.Trade&&e.CumulativeVolume is decimal cv)
        {
            if(cumulative.TryGetValue(e.Key,out var prev)&&last.TryGetValue(key,out var prior)&&e.ExchangeTime.UtcDateTime.Date==prior.ExchangeTime.UtcDateTime.Date)
            {if(cv==prev){Duplicates++;reason="누적거래량 중복";return false;}if(cv<prev){OutOfOrder++;reason="누적거래량 감소/세션 재설정 확인 필요";return false;}if(cv-prev>(e.TradeSize??0)){Gaps++;reason="누적거래량 차이: 누락 의심 (틱량으로 합산하지 않음)";}}
            cumulative[e.Key]=cv;
        }
        last[key]=e;latest[e.Key]=e.ExchangeTime;return true;
    }
}
public interface IMarketDataProvider : IAsyncDisposable
{
    string Description { get; }
    IAsyncEnumerable<MarketEvent> Stream(CancellationToken cancellationToken);
}
public sealed class HistoricalMarketDataProvider(IReadOnlyList<MarketEvent> events,int delayMs=0) : IMarketDataProvider
{
    public string Description=>"저장 이벤트 과거 재생";
    public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation]CancellationToken cancellationToken)
    {foreach(var e in events.OrderBy(x=>x.ExchangeTime)){cancellationToken.ThrowIfCancellationRequested();if(delayMs>0)await Task.Delay(delayMs,cancellationToken);yield return e;}}
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
public sealed class DemoMarketDataProvider(Instrument instrument,int delayMs) : IMarketDataProvider
{
    public string Description=>"명시적 합성 데모 · 실제 시세 아님";
    public static IReadOnlyList<MarketEvent> Sample(Instrument i,int seconds=1800)
    {
        var data=new List<MarketEvent>();var start=DateTimeOffset.Parse("2026-09-25T13:30:00Z");decimal cum=0;var scale=i.Currency=="KRW"?10000m:1m;var high=0m;var low=decimal.MaxValue;
        for(int n=0;n<seconds;n++)
        {
            var t=start.AddSeconds(n);var wave=(decimal)Math.Sin(n*Math.PI/30)*.04m;
            var price=decimal.Round((2m+wave-(n>=700&&n<800?(n-700)*.003m:n>=800&&n<900?.3m-(n-800)*.003m:0))*scale,i.Currency=="KRW"?0:4);
            var tick=i.Tick(price);price=decimal.Round(price/tick)*tick;var bid=price-tick;var ask=price+tick;var size=n%300>240?200:20;cum+=size;high=Math.Max(high,price);low=Math.Min(low,price);
            data.Add(new(i,EventKind.Quote,t,t,"SAMPLE",2*n){Bid=bid,Ask=ask,BidSize=500,AskSize=300,Bids=Enumerable.Range(0,10).Select(k=>new BookLevel(bid-k*tick,500)).ToArray(),Asks=Enumerable.Range(0,10).Select(k=>new BookLevel(ask+k*tick,300)).ToArray()});
            data.Add(new(i,EventKind.Trade,t.AddMilliseconds(10),t.AddMilliseconds(10),"SAMPLE",2*n+1){LastPrice=price,TradePrice=price,TradeSize=size,CumulativeVolume=cum,Open=2*scale,High=high,Low=low,PreviousClose=2*scale,MarketCap=100000000*scale,AverageDailyVolume=1000000,AverageDailyTurnover=2000000*scale});
        }
        return data;
    }
    public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation]CancellationToken cancellationToken)
    {var start=DateTimeOffset.UtcNow;foreach(var e in Sample(instrument,86400)){await Task.Delay(delayMs,cancellationToken);var time=start+(e.ExchangeTime-DateTimeOffset.Parse("2026-09-25T13:30:00Z"));yield return e with {ExchangeTime=time,ReceivedTime=time,Source="DEMO"};}}
    public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
}
public static class KiwoomTickParser
{
    public static IReadOnlyList<MarketEvent> Parse(string json,DateTimeOffset received,ref long sequence)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;var result=new List<MarketEvent>();if(Text(root,"trnm")!="REAL")return result;
        foreach(var row in root.GetProperty("data").EnumerateArray())
        {
            var type=Text(row,"type");if(type is not("0B" or "0D" or "FE" or "FT"))continue;var us=type is "FE" or "FT";
            var exchange=us?Text(row,"stexTp") switch {"ND"=>Exchange.NASDAQ,"NY"=>Exchange.NYSE,"NA"=>Exchange.AMEX,_=>throw new InvalidOperationException("미국 거래소 코드 누락")}:Exchange.KRX;
            var instrument=new Instrument(Text(row,"item"),exchange);var v=row.GetProperty("values");var trade=type is "0B" or "FE";
            var time=Text(v,trade?"20":"21");var date=Text(v,"22");
            // 20/21: provider time; the official table does not specify a quote date. Keep inference explicit.
            var kst=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(received,"Korea Standard Time");
            if(date.Length!=8)date=kst.ToString("yyyyMMdd");
            if(!DateTime.TryParseExact(date+time,"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var dt))throw new InvalidOperationException("거래소 시각 파싱 실패");
            var stamp=new DateTimeOffset(dt,TimeSpan.FromHours(9));if(stamp-received>TimeSpan.FromHours(12))stamp=stamp.AddDays(-1);if(received-stamp>TimeSpan.FromHours(12))stamp=stamp.AddDays(1);
            decimal? Abs(string id){var n=Number(v,id);return n.HasValue?Math.Abs(n.Value):null;}
            var e=new MarketEvent(instrument,trade?EventKind.Trade:EventKind.Quote,stamp,received,"KIWOOM",Interlocked.Increment(ref sequence)){TimeQuality=us?"공급자 20/21 KST 해석; 원문 시각 "+time+" / 현지 "+Text(v,"51020"):"KST 날짜 추정",LastPrice=Abs("10"),TradePrice=trade?Abs("10"):null,TradeSize=trade?Abs("15"):null,CumulativeVolume=Abs("13"),Open=Abs("16"),High=Abs("17"),Low=Abs("18"),PreviousClose=Abs("10")-Number(v,"11"),MarketCap=!us?Abs("311")*100000000:null};
            if(!trade)
            {var bids=new List<BookLevel>();var asks=new List<BookLevel>();for(int j=0;j<10;j++){var bp=Abs((51+j).ToString());var bs=Abs((71+j).ToString());var ap=Abs((41+j).ToString());var az=Abs((61+j).ToString());if(bp>0&&bs>=0)bids.Add(new(bp.Value,bs!.Value));if(ap>0&&az>=0)asks.Add(new(ap.Value,az!.Value));}e=e with {Bid=Abs("51"),Ask=Abs("41"),BidSize=Abs("71"),AskSize=Abs("61"),Bids=bids,Asks=asks};}
            else e=e with {Bid=Abs("28"),Ask=Abs("27")};
            e.Validate();result.Add(e);
        }
        return result;
    }
    internal static string Text(JsonElement e,string name)=>e.TryGetProperty(name,out var v)?v.ToString().Trim():"";
    internal static decimal? Number(JsonElement e,string name)=>decimal.TryParse(Text(e,name),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n:null;
}
public interface IWireSocket : IAsyncDisposable
{Task Connect(Uri uri,CancellationToken token);Task Send(string data,CancellationToken token);Task<string> Receive(CancellationToken token);}
public sealed class WireSocket : IWireSocket
{
    private readonly ClientWebSocket socket=new();
    public Task Connect(Uri uri,CancellationToken token)=>socket.ConnectAsync(uri,token);
    public Task Send(string data,CancellationToken token)=>socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(data)),WebSocketMessageType.Text,true,token);
    public async Task<string> Receive(CancellationToken token)
    {using var memory=new MemoryStream();var buffer=new byte[16384];WebSocketReceiveResult r;do{r=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);if(r.MessageType==WebSocketMessageType.Close)throw new IOException("WS closed");memory.Write(buffer,0,r.Count);if(memory.Length>2_000_000)throw new IOException("WS frame limit");}while(!r.EndOfMessage);return Encoding.UTF8.GetString(memory.ToArray());}
    public ValueTask DisposeAsync(){socket.Dispose();return ValueTask.CompletedTask;}
}
public sealed class KiwoomMarketDataProvider(Func<Task<string>> tokenProvider,IReadOnlyList<Instrument> instruments,DataSettings settings,Action<string> status,Func<IWireSocket>? factory=null,bool live=false) : IMarketDataProvider
{
    public string Description=>(live?"키움 실전":"키움 모의")+" 서버 실시간 · 구독 종목만";
    private readonly CancellationTokenSource stop=new();
    private long sequence;
    public static string Registration(IEnumerable<Instrument> instruments,bool us,bool remove=false)=>Serialization.Pack(new{trnm=remove?"REMOVE":"REG",grp_no="1",refresh="0",data=new[]{new{item=us?(object)instruments.Select(i=>new{jmcode=i.Symbol,stex_tp=i.Code}).ToArray():instruments.Select(i=>i.Symbol).ToArray(),type=us?new[]{"FE","FT"}:new[]{"0B","0D"}}}});
    public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation]CancellationToken cancellationToken)
    {
        if(instruments.Count==0||instruments.Count>settings.SubscriptionLimit||instruments.Count>20)throw new InvalidOperationException("구독 종목 한도 초과");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,stop.Token);
        var channel=Channel.CreateBounded<MarketEvent>(new BoundedChannelOptions(settings.BufferCapacity){FullMode=BoundedChannelFullMode.Wait,SingleReader=true});
        async Task Pump(bool us)
        {
            var items=instruments.Where(i=>(i.Exchange!=Exchange.KRX)==us).ToArray();if(items.Length==0)return;int attempt=0;
            while(!linked.IsCancellationRequested)
            {
                await using var socket=factory?.Invoke()??new WireSocket();
                try
                {
                    var auth=await tokenProvider();await socket.Connect(new Uri((live?"wss://api.kiwoom.com:10000/api/":"wss://mockapi.kiwoom.com:10000/api/")+(us?"us":"dostk")+"/websocket"),linked.Token);
                    await socket.Send(Serialization.Pack(new{trnm="LOGIN",token=auth}),linked.Token);
                    using(var login=JsonDocument.Parse(await socket.Receive(linked.Token)))if(KiwoomTickParser.Text(login.RootElement,"return_code")!="0")throw new InvalidOperationException("WS 인증 실패");
                    await socket.Send(Registration(items,us),linked.Token);attempt=0;status((us?"미국":"국내")+" WebSocket 연결·등록 요청");var connected=DateTimeOffset.UtcNow;
                    while(!linked.IsCancellationRequested)
                    {
                        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(linked.Token);timeout.CancelAfter(TimeSpan.FromSeconds(90));var json=await socket.Receive(timeout.Token);using var message=JsonDocument.Parse(json);var kind=KiwoomTickParser.Text(message.RootElement,"trnm");
                        if(kind=="PING"){await socket.Send(json,linked.Token);continue;}
                        if(kind is "REG" or "REMOVE"){if(KiwoomTickParser.Text(message.RootElement,"return_code")!="0")throw new InvalidOperationException("실시간 등록/해지 거부");status("구독 등록 확인");continue;}
                        foreach(var e in KiwoomTickParser.Parse(json,DateTimeOffset.UtcNow,ref sequence))if(!channel.Writer.TryWrite(e)){status("버퍼 초과: 데이터 손실, 수신 중지");throw new BufferOverflowException();}
                        if(DateTimeOffset.UtcNow-connected>TimeSpan.FromHours(12))throw new IOException("주기적 인증 확인");
                    }
                }
                catch(BufferOverflowException){linked.Cancel();throw;}
                catch(OperationCanceledException) when(linked.IsCancellationRequested){}
                catch(Exception){status("WebSocket 단절/오류: 누락 구간 존재, 자동매매 중지 후 재접속");attempt++;await Task.Delay(TimeSpan.FromSeconds(Math.Min(settings.ReconnectSeconds,Math.Pow(2,Math.Min(attempt,6)))),linked.Token);}
                finally {try{using var end=new CancellationTokenSource(1000);await socket.Send(Registration(items,us,true),end.Token);}catch{/* Socket disposal also releases registration. */}}
            }
        }
        async Task Produce(){try{await Task.WhenAll(Pump(false),Pump(true));channel.Writer.TryComplete();}catch(Exception ex){channel.Writer.TryComplete(ex);}}
        var producer=Produce();
        try{await foreach(var e in channel.Reader.ReadAllAsync(cancellationToken))yield return e;}
        finally{linked.Cancel();try{await producer;}catch(OperationCanceledException){}}
    }
    public ValueTask DisposeAsync(){stop.Cancel();stop.Dispose();return ValueTask.CompletedTask;}
    private sealed class BufferOverflowException:Exception;
}
