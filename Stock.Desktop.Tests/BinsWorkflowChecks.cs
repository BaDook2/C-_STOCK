using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void BinsWorkflowChecks()
    {
        using var store=new TradingStore(Path.Combine(output,"bin-dispatch-"+Guid.NewGuid().ToString("N")+".db"),true);var editor=new StrategyEditor(store){UseBin=true};
        var config=editor.Current();var stock=new StockItem("AAPL","Synthetic Bin dispatch",Exchange.NASDAQ,"AAPL");var at=DateTimeOffset.Parse("2026-10-05T13:30:00Z");var date=new DateOnly(2026,10,5);
        store.InsertEvents([new(new(stock.Symbol,stock.Exchange),EventKind.Trade,at,at,"SYNTHETIC_DISPATCH",1){TradePrice=100,TradeSize=100},new(new(stock.Symbol,stock.Exchange),EventKind.Trade,at.AddSeconds(1),at.AddSeconds(1),"SYNTHETIC_DISPATCH",2){TradePrice=100,TradeSize=100}]);
        var dataset=new BarDataset(stock,[],"SYNTHETIC_DISPLAY",at);var service=new OversoldBacktestService(store);var task=service.RunAsync(new(dataset,config,date,date,new()),CancellationToken.None);Pump(()=>task.IsCompleted);var result=task.GetAwaiter().GetResult();
        Check(result.BinsResult!=null&&result.InjesResult==null&&result.BinsResult.Orders.Length==0,"desktop backtest service dispatches selected Bin and preserves missing-data no-order result");
        var screen=BinsResearchBridge.Screen(stock,config,BinsResearchBridge.Read(store,stock,date,date),CancellationToken.None);Check(screen.BinsEvaluation?.Outcomes[0]==ConditionOutcome.MissingData&&!screen.Candidate,"desktop stored events reach Bin screening with missing session reported");
        editor.UseInje=true;var old=editor.Current();old.Bins=config.Bins;bool blocked=false;try{old.Validate();}catch(InvalidOperationException){blocked=true;}Check(blocked,"no desktop execution may contain both systems");
        using var services=new AppServices(Path.Combine(output,"bin-host-"+Guid.NewGuid().ToString("N")),new MarketFixture(),credentials:MemoryCredentials());var history=new MarketHistoryService(services,()=>null);var host=new RealtimeScreeningHost(services,history,()=>null);
        blocked=false;try{host.Start(new([stock],"Bin",config,new(1,Stock.Core.Research.Oversold.BarUnit.Day),new(),false,10000,100),CancellationToken.None).GetAwaiter().GetResult();}catch(InvalidOperationException ex){blocked=ex.Message.Contains("복원");}Check(blocked&&!host.Active,"unconnected Bin realtime paper starts are explicitly blocked before feed access");host.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
