using System.Text.Json;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static async Task CurrentDailyTests()
    {
        var c=DailyConfig();c.MinimumSupportCount=1;c.MovingAveragePeriods="1";c.UsePivotSupport=c.UseApproximateVolumeZone=false;
        PriceBar[] prices=[DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,81,high:110),DailyBar(4,84,high:110)];
        var bars=prices.Select((b,i)=>new StrategyBar(b,b.Time,b.Time,i,null,"FIXTURE")).ToArray();var engine=new OversoldScreeningEngine();
        var r=engine.EvaluateCurrent(DailyStock,bars,c,new(),DailyTime(4,16));
        Check(r.Candidate&&!r.Evaluation!.Enter&&r.Status.Contains("자료 미확인"),"current screening works without any facts JSON but cannot authorize entry");
        Check(r.Evaluation!.Conditions.Single(x=>x.Condition=="Crash").Outcome==ConditionOutcome.Passed&&r.Evaluation.Conditions.Single(x=>x.Condition=="RSI").Outcome==ConditionOutcome.Disabled,"price and disabled conditions evaluated independently of missing metadata");
        Check(new[]{"상품 제외","정지·관리 제외","거래대금","FundamentalRisk"}.All(name=>r.Evaluation.Conditions.Single(x=>x.Condition==name).Outcome==ConditionOutcome.MissingData),"no stock type turnover or manual risk fabricated from OHLCV");
        Check(!engine.Evaluate(DailyStock,bars,c,new(),DailyTime(4,16)).Candidate,"strict historical screening still requires verified sessions");
        Check(DailyRun(prices,c,new()).Trades.Count==0,"backtest does not inherit research-only metadata policy");
        c.RequireReboundConfirmation=true;c.ReboundPercent=3;
        r=engine.EvaluateCurrent(DailyStock,bars,c,new(),DailyTime(4,16));
        Check(r.Candidate&&r.Evaluation!.ReboundLow==80&&r.Evaluation.TriggerDate==DailyDates.Of(prices[2]),"technical rebound warmup survives missing past status and exact turnover");
        var prefix=engine.EvaluateCurrent(DailyStock,bars,c,new(),DailyTime(2,16));
        Check(!prefix.Candidate&&prefix.Evaluation!.ReboundLow==80,"future rebound does not affect earlier research prefix");
        var status=DailyFacts(5).StatusRecords[^1] with{KnownAt=DailyTime(4,16),Suspended=true};
        r=engine.EvaluateCurrent(DailyStock,bars,c,new(),DailyTime(4,16),status);
        Check(!r.Candidate&&r.Evaluation!.Conditions.Single(x=>x.Condition=="정지·관리 제외").Outcome==ConditionOutcome.Failed,"known current halt excludes technical candidates");
        r=engine.EvaluateCurrent(DailyStock,bars,c,new(),DailyTime(4,16),currentRisk:true);
        Check(!r.Candidate,"explicit manual current risk excludes while unreviewed remains unknown");
        status=status with{Suspended=false,KnownAt=DailyTime(5)};
        r=engine.EvaluateCurrent(DailyStock,bars,c,new(),DailyTime(4,16),status);
        Check(r.Evaluation!.Conditions.Single(x=>x.Condition=="상품 제외").Outcome==ConditionOutcome.MissingData,"future current status is not leaked backwards");
        var intraday=DailyTime(4,10);var quote=new CurrentMarketSnapshot(DailyStock,new("검사",85,4,4.94m,intraday,"OPEN"),intraday,81,86,79,1234,10000,"FIXTURE",TimeSpan.FromSeconds(7));
        var forming=prices.Take(4).Append(prices[4] with{IsComplete=false}).ToArray();var merged=DailyQuoteMerger.Merge(forming,quote,intraday);
        Check(merged[^1] is {Open:81,High:86,Low:79,Close:85,Volume:1234,IsComplete:false}&&merged[^1].Time==prices[^1].Time,"snapshot updates true OHLC cumulative volume and preserves daily axis identity");
        Check(new BarDataset(DailyStock,merged,"TEST",intraday).CompletedBars.Length==4,"forming quote excluded from completed research candles");
        Check(DailyQuoteMerger.Merge(merged,quote,intraday).SequenceEqual(merged),"unchanged snapshot does not invent price movement or double-count volume");
        Check(DailyQuoteMerger.Merge(merged,quote with{Quote=quote.Quote with{State="CLOSE"}},DailyTime(4,15,31))[^1].IsComplete==false,"close state alone never confirms unfinished daily bar");
        Check(DailyQuoteMerger.Merge(prices,quote with{Quote=quote.Quote with{State="CLOSE"}},DailyTime(4,16)).SequenceEqual(prices),"official completed history cannot be overwritten by late cached quote");
        Check(DailyQuoteMerger.Merge(prices,quote,DailyTime(5,10)).SequenceEqual(prices),"prior-day or holiday quote does not create today's candle");
        Check(DailyQuoteMerger.Merge(prices.Take(4).ToArray(),quote with{Volume=null},intraday).Length==4,"missing cumulative volume prevents fabricated daily candle");
        var next=quote with{Quote=quote.Quote with{TradeTime=DailyTime(5,10)},ReceivedAt=DailyTime(5,10)};
        Check(DailyQuoteMerger.Merge(prices,next,DailyTime(5,10)).Length==6,"new trading date appends exactly one forming bar");
        Check(DailyQuoteMerger.Date(DateTimeOffset.Parse("2026-10-06T01:00:00+09:00"),Exchange.NASDAQ)==new DateOnly(2026,10,5),"US trading date uses exchange zone rather than Korea midnight");
        var json="""{"result":{"pollingInterval":7000,"time":1736038800000,"areas":[{"datas":[{"cd":"005930","nm":"검사","nv":85,"pcv":100,"ms":"OPEN","ov":90,"hv":92,"lv":80,"aq":1234,"aa":105678,"nxtOverMarketPriceInfo":{"overPrice":"999"}}]}]}}""";
        using var doc=JsonDocument.Parse(json);var parsed=PublicMarketData.ParseCurrentPoll(DailyStock,doc.RootElement,intraday);
        Check(parsed.PollInterval==TimeSpan.FromSeconds(7)&&parsed.Quote.Price==85&&parsed.TradingValue==105678&&!parsed.ExchangeTimestamp,"public snapshot honors server polling interval and exact notional without NXT mixing");
        using var basic=JsonDocument.Parse("""{"itemCode":"005930","stockEndType":"stock","stockExchangeType":{"name":"KOSPI"},"tradeStopType":{"name":"TRADING"}}""");
        var security=PublicMarketData.ParseCurrentSecurity(DailyStock,basic.RootElement,intraday);
        Check(security.Board==HistoricalBoard.Kospi&&security.Suspended==false&&security.Kind==HistoricalSecurityKind.Unknown&&security.Administrative==null,"public generic stock and missing warning never imply common non-admin security");
        var active=0;var maximum=0;var calls=0;
        using(var parallelProvider=new PublicMarketData(new Handler(async request=>
        {
            var current=Interlocked.Increment(ref active);Interlocked.Increment(ref calls);maximum=Math.Max(maximum,current);
            try{await Task.Delay(80);var code=request.RequestUri!.Query.Contains("000660")?"000660":"005930";var body=$"{{\"result\":{{\"pollingInterval\":7000,\"time\":1736038800000,\"areas\":[{{\"datas\":[{{\"cd\":\"{code}\",\"nm\":\"검사\",\"nv\":85,\"pcv\":100,\"ms\":\"OPEN\",\"ov\":90,\"hv\":92,\"lv\":80,\"aq\":1234,\"aa\":105678}}]}}]}}}}";return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent(body,System.Text.Encoding.UTF8,"application/json")};}
            finally{Interlocked.Decrement(ref active);}
        })))
        {
            var other=DailyStock with{Symbol="000660",Name="SK하이닉스",ProviderCode="000660"};var watch=System.Diagnostics.Stopwatch.StartNew();await Task.WhenAll(parallelProvider.Current(DailyStock,CancellationToken.None),parallelProvider.Current(other,CancellationToken.None));watch.Stop();await parallelProvider.Current(DailyStock,CancellationToken.None);
            Check(maximum==2&&calls==2&&watch.Elapsed<TimeSpan.FromMilliseconds(500),"public current quotes use per-symbol cadence without a global seven-second queue");
        }
        await QuotePumpTests(quote);
    }

    private sealed class ManualQuoteClock:TimeProvider
    {
        private DateTimeOffset now=DateTimeOffset.Parse("2026-10-06T00:00:00Z");
        private readonly List<ManualTimer> timers=[];
        public override DateTimeOffset GetUtcNow()=>now;
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
        {var t=new ManualTimer(this,callback,state);lock(timers)timers.Add(t);t.Change(dueTime,period);return t;}
        public int Pending {get{lock(timers)return timers.Count(t=>t.Due!=null);}}
        public void Advance(TimeSpan amount){now+=amount;ManualTimer[] copy;lock(timers)copy=timers.ToArray();foreach(var t in copy)if(t.Due<=now){t.Due=null;t.Fire();}}
        private sealed class ManualTimer(ManualQuoteClock owner,TimerCallback callback,object? state):ITimer
        {
            public DateTimeOffset? Due;
            public bool Change(TimeSpan due,TimeSpan period){Due=due==Timeout.InfiniteTimeSpan?null:owner.now+due;return true;}
            public void Fire()=>callback(state);
            public void Dispose()=>Due=null;
            public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
        }
    }
    private sealed class QuoteFixture(CurrentMarketSnapshot value):ICurrentMarketData
    {
        public int Calls,Active,MaxActive;public bool Fail,Block;public TaskCompletionSource<CurrentMarketSnapshot> Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)
        {Calls++;Active++;MaxActive=Math.Max(MaxActive,Active);try{if(Block)return await Pending.Task;if(Fail)throw new InvalidOperationException("fixture rate failure");return value with{Stock=stock};}finally{Active--;}}
        public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
    }
    private static async Task AwaitQuote(Func<bool> predicate)
    {for(int i=0;i<500&&!predicate();i++)await Task.Delay(2);if(!predicate())throw new Exception("quote test timed out");}
    private static async Task QuotePumpTests(CurrentMarketSnapshot quote)
    {
        var clock=new ManualQuoteClock();var source=new QuoteFixture(quote);int updates=0,errors=0;
        using var pump=new SelectedQuotePump(source,clock);pump.Select(DailyStock,_=>updates++,_=>errors++);await AwaitQuote(()=>clock.Pending==1);
        pump.Select(DailyStock,_=>updates++,_=>errors++);Check(source.Calls==1&&updates==1,"reselecting same stock keeps a single polling loop");
        clock.Advance(TimeSpan.FromSeconds(6));Check(source.Calls==1,"1-second UI cadence cannot bypass provider's seven-second cadence");
        clock.Advance(TimeSpan.FromSeconds(1));await AwaitQuote(()=>source.Calls==2&&clock.Pending==1);Check(updates==2&&source.MaxActive==1,"next allowed poll has no overlapping request");
        source.Fail=true;clock.Advance(TimeSpan.FromSeconds(7));await AwaitQuote(()=>errors==1&&clock.Pending==1);clock.Advance(TimeSpan.FromSeconds(7));Check(source.Calls==3,"failed provider backs off instead of continuing normal polling");
        pump.Stop();clock.Advance(TimeSpan.FromMinutes(5));Check(source.Calls==3,"stopped loop never restarts on clock advance");
        source=new QuoteFixture(quote){Block=true};using var changing=new SelectedQuotePump(source,clock);var seen=new List<string>();
        changing.Select(DailyStock,s=>seen.Add(s.Stock.Key),_=>{});await AwaitQuote(()=>source.Calls==1);
        var other=DailyStock with{Symbol="000660"};changing.Select(other,s=>seen.Add(s.Stock.Key),_=>{});source.Block=false;source.Pending.SetResult(quote);
        await AwaitQuote(()=>seen.Count==1);Check(seen.Single()==other.Key&&source.MaxActive==1,"late cancelled symbol response ignored before new symbol delivery");
        changing.Stop();source=new QuoteFixture(quote with{Quote=quote.Quote with{State="CLOSE"}});using var closed=new SelectedQuotePump(source,clock);
        closed.Select(DailyStock,_=>{},_=>{});await AwaitQuote(()=>clock.Pending==1);clock.Advance(TimeSpan.FromSeconds(29));Check(source.Calls==1,"closed and holiday market uses slower polling without synthesizing ticks");
        clock.Advance(TimeSpan.FromSeconds(1));await AwaitQuote(()=>source.Calls==2);closed.Stop();
    }
}
