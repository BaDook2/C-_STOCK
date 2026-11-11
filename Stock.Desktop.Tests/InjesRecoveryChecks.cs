using System.Text.Json;
using Avalonia.Controls;
using Stock.Core.Research;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;
using Stock.Desktop;

internal static partial class Program
{
    private static void InjesRecoveryChecks()
    {
        var epoch=DateTimeOffset.Parse("2026-09-25T13:30:00Z");long clockTicks=epoch.AddMilliseconds(1750).UtcTicks;DateTimeOffset Clock()=>new(Interlocked.Read(ref clockTicks),TimeSpan.Zero);
        var instrument=new Instrument("AAPL",Exchange.NASDAQ);var feed=new UiTickFeed();var broker=new ReadOnlyBroker(epoch);
        using var services=new AppServices(Path.Combine(output,"native-recovery",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>broker,clock:Clock,credentials:MemoryCredentials(),liveFeedFactory:(_,_,_,_)=>feed);
        using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};window.Show();
        var d=InjesStrategyDefinition.Create("quote-scalping",epoch);d.StrategyName="Inje held recovery fixture";d.InitialCash=1000;d.CommonParameters.OrderBudget=20;d.CommonParameters.MaxSpreadPercent=3;d.CommonParameters.StopLossPercent=5;d.CommonParameters.TakeProfitPercent=10;
        var p=d.StrategyParameters.Deserialize<QuoteScalpingParameters>(InjesStrategyJson.Options)!;p.VolumeLookbackSeconds=1;p.MinTradesPerSecond=p.MinSharesPerSecond=1;p.MinQuoteChangesPerSecond=0;d.StrategyParameters=JsonSerializer.SerializeToElement(p,InjesStrategyJson.Options);d.RefreshEffective();
        var sessionInfo=UsSessionClock.Bounds(epoch,UsMarketPhase.Regular,true) with{AllowedOrderKinds=[OrderKind.Limit,OrderKind.Market]};
        MarketEvent Event(int n,bool quote=false,decimal price=2.01m,decimal bid=1.99m,decimal ask=2.03m,decimal size=100)
        {
            var at=epoch.AddMilliseconds(n*250);return new(instrument,quote?EventKind.Quote:EventKind.Trade,at,at,"UI_RECOVERY",n)
            {TradePrice=quote?null:price,TradeSize=quote?null:size,Bid=quote?bid:null,Ask=quote?ask:null,BidSize=quote?1000:null,AskSize=quote?400:null,AvailableAt=at,TradingSession=sessionInfo,
                StrategyReference=new(at){PreviousClose=2,SessionOpen=2,TickSize=.01m,DailyTradingValue=1000000,Halted=false,FundamentalRisk=FundamentalRiskState.Cleared}};
        }
        var paper=new InjesPaperExecution(d.InitialCash,d.ExecutionPolicy);var session=new InjesSession(d,[instrument],paper);session.SetEntriesEnabled(true,epoch.AddTicks(-1));
        for(int n=0;n<=6;n++){var e=Event(n,n%2==0,bid:n%4==0?1.99m:2m,ask:n%4==0?2.03m:2.04m);session.OnMarket(InjesResearchBridge.Observation(e),e.ReceivedTime);}
        var fillEvent=Event(7,price:1.98m,size:20);session.OnMarket(InjesResearchBridge.Observation(fillEvent),fillEvent.ReceivedTime);
        Check(paper.Positions[instrument.Key].Quantity==2,"recovery fixture contains confirmed partial holdings, not a pending signal");
        var journal=new InjesSessionJournal("ui-recovery",Clock(),d,[instrument],paper.Broker.Capture(),session.Signals,"isolated saved holding",session.Capture());services.Store.State("inje-live:"+journal.Id,journal);
        Check(model.Realtime.RestoreSaved.CanExecute(null),"existing restore action is enabled for a saved unsettled native account");
        model.Realtime.RestoreSaved.Execute(null);Pump(()=>!model.Realtime.Busy&&model.Realtime.RecoveredPlan!=null);
        Check(model.VirtualExecution.Items.Count==1&&model.VirtualExecution.Items[0].Stock.Key==instrument.Key&&!model.Realtime.Automatic&&!model.VirtualExecution.Editable,"native restore populates the existing target grid and keeps new entry OFF");
        Check(services.Store.States("trade-marker:inje:ui-recovery:").Count==journal.Ledger.Fills.Length,"restore rebuilds missing chart markers only from confirmed journal fills");
        Check(model.Realtime.ResumeSaved.CanExecute(null)&&!model.Realtime.RestoreSaved.CanExecute(null),"restored account offers protection resume and cannot be restored over its own holdings");
        model.Accounts.AppKey="DUMMY-RECOVERY";model.Accounts.Secret="DUMMY-RECOVERY";model.Accounts.Connect.Execute(null);Pump(()=>model.Online);
        model.Realtime.ResumeSaved.Execute(null);Pump(()=>model.Realtime.Running&&!model.Realtime.Busy);
        void Send(MarketEvent e){Interlocked.Exchange(ref clockTicks,e.ReceivedTime.UtcTicks);feed.Send(e);}
        Send(Event(8,true,bid:1.8m,ask:1.82m));Send(Event(9,price:1.8m));
        Pump(()=>services.Store.State<InjesSessionJournal>("inje-live:ui-recovery")!.Ledger.Orders.Any(o=>!o.Buy));Send(Event(10,true,bid:1.79m,ask:1.81m));
        Pump(()=>services.Store.State<InjesSessionJournal>("inje-live:ui-recovery")!.Ledger.Positions.All(p=>p.Quantity==0));
        Check(!model.Realtime.Automatic&&broker.SubmitCalls==0&&services.Store.State<InjesSessionJournal>("inje-live:ui-recovery")!.Ledger.Orders.Count(o=>o.Buy)==1,"existing resume action protects confirmed holdings without a second buy or broker submission");
        model.Realtime.Stop.Execute(null);Pump(()=>!model.Realtime.Running);Check(model.VirtualExecution.Editable,"stopping a flat native session releases the existing target editor");window.Close();
    }
}
