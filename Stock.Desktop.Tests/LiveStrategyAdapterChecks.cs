using System.Runtime.CompilerServices;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void LiveStrategyAdapterChecks()
    {
        var now=new DateTimeOffset(2026,11,6,10,30,0,TimeSpan.FromHours(9));var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930");var broker=new OrderBroker(now);var feed=new IdleLiveFeed();
        using var services=new AppServices(Path.Combine(output,"live-adapter",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>broker,clock:()=>now,credentials:MemoryCredentials(),liveFeedFactory:(_,_,_,_)=>feed);using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};window.Show();var account=model.Accounts;account.Confirm=_=>Task.FromResult(true);account.AppKey="DUMMY";account.Secret="DUMMY";account.Connect.Execute(null);Pump(()=>!account.Busy&&account.Online);
        var days=new[]{new DateOnly(2026,11,4),new DateOnly(2026,11,5),new DateOnly(2026,11,6)};var sessions=days.Select(d=>ResearchMarketSession.For(d,Exchange.KRX)).ToArray();var facts=new HistoricalFacts{Source="SYNTHETIC VERIFIED FIXTURE",MarketSessions=sessions,StatusRecords=sessions.Select(s=>new DailySecurityStatus(stock.Symbol,s.Date,s.Open,HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false)).ToArray(),RiskRecords=sessions.Select(s=>new FundamentalRiskRecord(stock.Symbol,s.Date,false,s.Open)).ToArray()};
        var config=StrategyDiagnosticPreset.BacktestVerificationPresets()[StrategyDiagnosticPreset.HoldOneDayName].Clone();config.UsePriceOnlyBacktest=false;config.UseEmergencyStop=true;config.PlannedInvestment=100000;
        var bars=sessions.Take(2).Select((s,n)=>new StrategyBar(new(s.Close,10000,11000,9000,10000,10000),s.Open,s.Close,n+1,100_000_000,"SYNTHETIC VERIFIED FIXTURE")).ToArray();model.ActualTrading.WarmupJson=Serialization.Pack(new Dictionary<string,StrategyBar[]>{{stock.Key,bars}});
        var request=new RealtimeRequest([stock],"실제 adapter 검증",config,new(1,BarUnit.Day),facts,false,200000,50000);
        var locked=model.ActualTrading.Start(request);Pump(()=>locked.IsCompleted);Check(locked.IsFaulted&&broker.SubmitCalls==0,"live strategy adapter refuses startup until the user explicitly enables the account");
        account.EnableOrders.Execute(null);Pump(()=>account.Authorized);var publicAssumption=model.ActualTrading.Start(request with{PaperAssumptions=true});Pump(()=>publicAssumption.IsCompleted);Check(publicAssumption.IsFaulted,"real strategy never accepts public-paper security/risk assumptions");
        var missing=model.ActualTrading.Start(request with{Facts=new HistoricalFacts()});Pump(()=>missing.IsCompleted);Check(missing.IsFaulted&&broker.SubmitCalls==0,"missing current market/security/risk facts block actual execution without fabricated defaults");
        var begin=model.ActualTrading.Start(request);Pump(()=>begin.IsCompleted);begin.GetAwaiter().GetResult();Pump(()=>model.ActualTrading.Running&&feed.Started);Check(model.ActualTrading.Running&&account.Session!.AutomationClaimed&&broker.SubmitCalls==0,"same LiveStrategyBatch claims the fake account and waits for new actual-feed events without historical orders");
        model.OpenActualExecution.Execute(null);Pump(()=>window.OwnedWindows.OfType<VirtualExecutionDialog>().Any(d=>d.Title=="실제 계좌 전략 실행"));var popup=window.OwnedWindows.OfType<VirtualExecutionDialog>().Single();popup.Close();Check(model.ActualTrading.Running&&model.ActualExecution.IsActual&&!model.ActualExecution.Editable,"actual popup is a shared target editor; closing it preserves the application-owned batch and frozen settings");
        var duplicate=model.ActualTrading.Start(request);Pump(()=>duplicate.IsCompleted);Check(duplicate.IsFaulted,"already-running actual account cannot start a second duplicate batch");
        var finish=model.ActualTrading.Stop();Pump(()=>finish.IsCompleted);finish.GetAwaiter().GetResult();Check(!model.ActualTrading.Running&&!account.Session!.AutomationClaimed&&feed.Disposed&&broker.SubmitCalls==0,"full stop cancels the fake subscription, reconciles and releases account ownership without liquidation");window.Close();
    }
    private sealed class IdleLiveFeed:IMarketDataProvider
    {
        public bool Started,Disposed;public string Description=>"SYNTHETIC IDLE LIVE FEED";
        public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation]CancellationToken cancellationToken){Started=true;await Task.Delay(Timeout.Infinite,cancellationToken);yield break;}
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
}
