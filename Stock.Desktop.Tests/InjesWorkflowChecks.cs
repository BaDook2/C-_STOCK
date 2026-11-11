using System.Threading.Channels;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;
using Stock.Desktop;

internal static partial class Program
{
    private sealed class UiTickFeed:IMarketDataProvider
    {
        private readonly Channel<MarketEvent> events=Channel.CreateUnbounded<MarketEvent>();
        public string Description=>"SYNTHETIC UI ticks, no broker orders";
        public void Send(MarketEvent value)=>events.Writer.TryWrite(value);
        public IAsyncEnumerable<MarketEvent> Stream(CancellationToken token)=>events.Reader.ReadAllAsync(token);
        public ValueTask DisposeAsync(){events.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
    private static void InjesWorkflowChecks()
    {
        var epoch=DateTimeOffset.Parse("2026-09-25T13:30:00Z");long clockTicks=epoch.UtcTicks;
        DateTimeOffset Clock()=>new(Interlocked.Read(ref clockTicks),TimeSpan.Zero);
        var feed=new UiTickFeed();var broker=new ReadOnlyBroker(epoch);var market=new MarketFixture();
        using var services=new AppServices(Path.Combine(output,"native-workflow",Guid.NewGuid().ToString("N")),market,brokerFactory:(_,_)=>broker,clock:Clock,credentials:MemoryCredentials(),liveFeedFactory:(_,_,_,_)=>feed);
        using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};window.Show();
        model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.ChartBars.Length>0);
        Check(model.SelectedStock?.Symbol=="005930","general Korean stock search and chart remain available after strategy replacement");
        model.ToggleWatch.Execute(null);Check(model.WatchGroups[0].Stocks.Any(s=>s.Symbol=="005930"),"existing watchlist registration remains independent of Inje US strategy restrictions");
        model.Region="해외";model.Query="애플";model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SelectedStock?.Exchange==Exchange.NASDAQ);
        Check(model.SelectedStock?.Symbol=="AAPL","US stock search remains available with the same chart layout");
        model.Research.From=epoch.Date;model.Research.To=epoch.Date;model.Research.Run.Execute(null);Pump(()=>!model.Research.Busy);
        Check(model.Research.Result==null&&model.Research.Status.Contains("MissingData"),"daily-only backtest reports missing original tick data rather than executing an old strategy");
        var definition=InjesStrategyDefinition.Create("quote-scalping",epoch);definition.StrategyName="Inje native UI quote fixture";definition.InitialCash=1000;
        definition.CommonParameters.OrderBudget=20;definition.CommonParameters.MaxSpreadPercent=3;definition.CommonParameters.StopLossPercent=5;definition.CommonParameters.TakeProfitPercent=10;
        var quote=definition.StrategyParameters.Deserialize<QuoteScalpingParameters>(InjesStrategyJson.Options)!;quote.VolumeLookbackSeconds=1;quote.MinTradesPerSecond=1;quote.MinSharesPerSecond=1;quote.MinQuoteChangesPerSecond=0;
        definition.StrategyParameters=JsonSerializer.SerializeToElement(quote,InjesStrategyJson.Options);definition.RefreshEffective();
        var file=Path.Combine(output,"native-ui-strategy.json");File.WriteAllText(file,InjesStrategyJson.Export(definition));model.Strategies.FileDialogs=new FixtureStrategyFiles(file);model.Strategies.Import.Execute(null);Pump(()=>model.Strategies.Selected==definition.StrategyName);
        Check(model.Strategies.Current().Injes!.StrategyId=="quote-scalping","existing JSON import selects native strategy without a new settings window");
        model.Strategies.Name="Inje native renamed";model.Strategies.Rename.Execute(null);Pump(()=>model.Strategies.Selected=="Inje native renamed");
        model.Strategies.Export.Execute(null);Pump(()=>Stock.Core.Strategies.StrategyProfileJson.Import(File.ReadAllText(file)).Name=="Inje native renamed");
        Check(Stock.Core.Strategies.StrategyProfileJson.Import(File.ReadAllText(file)).InjeStrategies.Single(x=>x.StrategyId==model.Strategies.InjeId).CommonParameters.OrderBudget==20,"existing rename/export preserves the native strategy and common budget");
        model.Accounts.AppKey="DUMMY-UI-KEY";model.Accounts.Secret="DUMMY-UI-SECRET";model.Accounts.Connect.Execute(null);Pump(()=>model.Online);
        var stock=new StockItem("AAPL","합성 애플 틱 fixture",Exchange.NASDAQ,"AAPL");var instrument=new Instrument(stock.Symbol,stock.Exchange);
        var config=model.Strategies.Current();var request=new RealtimeRequest([stock],definition.StrategyName,config,new(1,BarUnit.Day),new(),false,1000,10);
        model.VirtualExecution.AddCurrent.Execute(null);Check(model.VirtualExecution.Items.Count==1,"existing virtual target picker accepts the searched US stock");model.VirtualExecution.Items[0].Strategy=model.Strategies.Selected;model.VirtualExecution.Items[0].Budget="20";model.VirtualExecution.TotalBudget="1000";model.VirtualExecution.DailyLossLimit="10";model.OpenVirtualExecution.Execute(null);Pump(()=>window.OwnedWindows.OfType<VirtualExecutionDialog>().Any());model.VirtualExecution.Start.Execute(null);Pump(()=>!model.Realtime.Busy&&model.Realtime.Running);var popup=window.OwnedWindows.OfType<VirtualExecutionDialog>().Single();popup.Close();Check(!model.VirtualExecution.Editable&&model.Realtime.Running,"closing the existing virtual popup preserves the application-owned native session");
        Check(model.Realtime.Running&&model.Realtime.Automatic,"existing realtime host starts one explicitly selected native paper strategy");
        Check(model.Realtime.Status.Contains("MissingData"),"realtime header explicitly reports unavailable source data before the first original event");
        var session=UsSessionClock.Bounds(epoch,UsMarketPhase.Regular,true) with{AllowedOrderKinds=[OrderKind.Limit,OrderKind.Market]};
        void Send(int n,decimal price=2.01m,bool isQuote=false,decimal? bid=null,decimal? ask=null,decimal size=100)
        {
            var at=epoch.AddMilliseconds(n*250);Interlocked.Exchange(ref clockTicks,at.UtcTicks);
            var data=new MarketEvent(instrument,isQuote?EventKind.Quote:EventKind.Trade,at,at,"SYNTHETIC_UI",n)
            {TradePrice=isQuote?null:price,TradeSize=isQuote?null:size,Bid=isQuote?bid??1.99m:null,Ask=isQuote?ask??2.03m:null,BidSize=isQuote?1000:null,AskSize=isQuote?400:null,TradingSession=session,AvailableAt=at,
                StrategyReference=new(at){PreviousClose=2,SessionOpen=2,TickSize=.01m,DailyTradingValue=1000000,AverageDailyTradingValue=500000,Halted=false,FundamentalRisk=FundamentalRiskState.Cleared}};
            feed.Send(data);
        }
        for(int n=0;n<=6;n++)Send(n,isQuote:n%2==0,bid:n%4==0?1.99m:2m,ask:n%4==0?2.03m:2.04m);
        Pump(()=>services.Store.States("inje-live:").Any(x=>Serialization.Unpack<InjesSessionJournal>(x.Json).Ledger.Orders.Length==1));
        Send(7,1.98m,size:20);Pump(()=>services.Store.States("inje-live:").Any(x=>Serialization.Unpack<InjesSessionJournal>(x.Json).Ledger.Positions.Any(p=>p.Quantity==2)));
        model.Realtime.Refresh();Check(model.Realtime.Rows.Count==1&&broker.SubmitCalls==0,"native realtime rows show confirmed paper holdings without any broker submission");
        Send(8,isQuote:true,bid:2,ask:2.03m);Pump(()=>services.Store.States("inje-live:").Any(x=>Serialization.Unpack<InjesSessionJournal>(x.Json).Ledger.Orders.Any(o=>!o.Buy)));
        Send(9,2.04m);Pump(()=>services.Store.States("inje-live:").Any(x=>Serialization.Unpack<InjesSessionJournal>(x.Json).Ledger.Positions.All(p=>p.Quantity==0)));
        model.Realtime.Refresh();Check(services.Store.States("trade-marker:inje:").Count==2,"actual simulated buy/sell fills create the existing chart marker records");
        model.Realtime.Selected=model.Realtime.Rows[0];model.Realtime.OpenChart.Execute(null);Pump(()=>window.OwnedWindows.Any(w=>w.Content is LiveChartPanel));var chartModel=(LiveChartViewModel)((LiveChartPanel)window.OwnedWindows.Single(w=>w.Content is LiveChartPanel).Content!).DataContext!;chartModel.Selected=chartModel.Trades.First();Check(chartModel.Detail.Contains("quote-scalping")&&chartModel.Selected.Price.Contains("."),"native chart fill details show the frozen strategy evidence and precise USD price");AvaloniaHeadlessPlatform.ForceRenderTimerTick();Dispatcher.UIThread.RunJobs();var nativeChart=window.OwnedWindows.Single(w=>w.Content is LiveChartPanel).GetVisualDescendants().OfType<StockChart>().Single();Check(nativeChart.Currency=="USD"&&nativeChart.FindControl<ScottPlot.Avalonia.AvaPlot>("PricePlot")!.Plot.Axes.Right.TickGenerator.Ticks.Any(t=>t.Label.Contains(".")),"US penny chart labels retain fractional prices instead of flooring every tick to dollars");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
        var actual=model.ActualTrading.Start(request);Pump(()=>actual.IsCompleted);Check(actual.IsFaulted&&broker.SubmitCalls==0,"native real-account strategy start remains explicitly locked");
        model.Realtime.StopAll.Execute(null);window.Close();
    }
}
