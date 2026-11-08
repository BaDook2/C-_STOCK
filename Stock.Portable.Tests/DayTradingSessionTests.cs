using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Stock.Core.Research.DayTrading;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class ControlledDayFeed:IMarketDataProvider
    {
        public readonly Channel<MarketEvent> Input=Channel.CreateUnbounded<MarketEvent>();
        public bool Disposed;public string Description=>"Controlled fixture only";
        public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation]CancellationToken cancellationToken)
        {await foreach(var e in Input.Reader.ReadAllAsync(cancellationToken))yield return e;}
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private static void DayTradingSessionChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var instrument=new Instrument("005930",Exchange.KRX);
        var definition=StrategyDTestConfig();var editor=new StrategyDEditorModel(definition);
        var fee=editor.Parameters.Single(p=>p.Path=="Configuration.CommissionPercent");
        Check(fee.Text=="0.015"&&editor.Apply("보존").Configuration.CommissionPercent==.015m,"D editor preserves the exact 0.015 fee before and after untouched save");
        fee.Text="0.0175";var modified=editor.Apply("편집");
        Check(modified.Configuration.CommissionPercent==.0175m&&definition.Configuration.CommissionPercent==.015m,"D editing applies atomically to a private snapshot without changing its source");
        editor.Parameters.Single(p=>p.Path=="DayTrading.SimpleTimeframe.Unit").Text=((int)TimeframeUnit.Second).ToString();
        Check(editor.Apply("초 기준").DayTrading!.SimpleTimeframe.Unit==TimeframeUnit.Second,"framework-independent editor handles immutable timeframe records through a validated snapshot");
        fee.Text="wrong";Throws<InvalidOperationException>(()=>editor.Apply("오류"),"D editor rejects invalid numeric input before any save or execution");
        var feed=new ControlledDayFeed();var service=new DayTradingSessionService();long nowTicks=open.UtcTicks;
        var costs=new ExecutionSettings{UseSpread=false,FeePercent=definition.Configuration.CommissionPercent,TaxPercent=0,SlippagePercent=0,LatencyMs=0,ParticipationPercent=100,AllowPartial=true};
        service.Start(feed,[new(instrument,definition,StrategyDSeed(open),"D")],200000,costs,definition.DayTrading!.Risk,2,()=>new DateTimeOffset(Interlocked.Read(ref nowTicks),TimeSpan.Zero));
        Throws<InvalidOperationException>(()=>service.Start(new ControlledDayFeed(),[new(instrument,definition,StrategyDSeed(open),"D")],200000,costs,definition.DayTrading.Risk,2),"application service prevents duplicate sessions independently from popup lifetime");
        decimal[] prices=[10100,9900,9920,9950];
        for(int n=0;n<prices.Length;n++){var time=open.AddSeconds(n);Interlocked.Exchange(ref nowTicks,time.UtcTicks);feed.Input.Writer.TryWrite(new(instrument,EventKind.Trade,time,time,"TEST",n+1){TradePrice=prices[n],TradeSize=1000,CumulativeVolume=(n+1)*1000});if(!SpinWait.SpinUntil(()=>service.ProcessedEvents>=n+1,TimeSpan.FromSeconds(3)))throw new TimeoutException("fixture input timeout");}
        if(!SpinWait.SpinUntil(()=>service.Signals.Length==1,TimeSpan.FromSeconds(5)))throw new TimeoutException("D background fixture signal timeout");
        Check(service.Snapshot() is {EntriesEnabled:false}&&service.Fills.Length==0,"background watch reports a valid signal with automatic entries off by default");
        service.SetEntriesEnabled(true,open.AddSeconds(4));Interlocked.Exchange(ref nowTicks,open.AddSeconds(5).UtcTicks);
        feed.Input.Writer.TryWrite(new(instrument,EventKind.Trade,open.AddSeconds(5),open.AddSeconds(5),"TEST",5){TradePrice=9960,TradeSize=1000,CumulativeVolume=5000});
        SpinWait.SpinUntil(()=>service.Snapshot()?.RecentEvents.Any(a=>a.Time>=open.AddSeconds(5))==true,100);
        Check(service.Fills.Length==0,"enabling background auto entries never retroactively buys an already-observed signal");
        service.SetEntriesEnabled(false,open.AddSeconds(6));Check(service.IsRunning,"turning off new entries keeps the application feed and protective management running");
        service.EmergencyStop(open.AddSeconds(6));Check(service.Snapshot() is {EmergencyStopped:true,EntriesEnabled:false},"central emergency immediately gates D order generation");
        service.StopAsync().GetAwaiter().GetResult();Check(!service.IsRunning&&feed.Disposed,"explicit application shutdown cancels bounded stream, independent clock and disposes its feed");
        service.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
