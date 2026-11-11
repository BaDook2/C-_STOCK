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
        var definition=Stock.Core.Strategies.Injes.InjesStrategyDefinition.Create("momentum-pullback",now);var request=new RealtimeRequest([new("AAPL","애플",Exchange.NASDAQ,"AAPL")],definition.StrategyName,Stock.Core.Strategies.Injes.InjesPresetStore.Wrap(definition),new(1,BarUnit.Day),new(),false,1000,100);
        var locked=model.ActualTrading.Start(request);Pump(()=>locked.IsCompleted);Check(locked.IsFaulted&&broker.SubmitCalls==0,"native actual strategy startup remains locked before account authorization");
        account.EnableOrders.Execute(null);Pump(()=>account.Authorized);var afterAuthorization=model.ActualTrading.Start(request);Pump(()=>afterAuthorization.IsCompleted);
        Check(afterAuthorization.IsFaulted&&!feed.Started&&!model.ActualTrading.Running&&broker.SubmitCalls==0,"manual account authorization cannot activate native automatic strategy orders");
        Check(!account.Session!.AutomationClaimed,"rejected native startup never claims or leaves an automatic account session");window.Close();
    }

    private sealed class IdleLiveFeed:IMarketDataProvider
    {
        public bool Started,Disposed;public string Description=>"SYNTHETIC IDLE LIVE FEED";
        public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation]CancellationToken cancellationToken){Started=true;await Task.Delay(Timeout.Infinite,cancellationToken);yield break;}
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
}
