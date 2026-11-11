using Avalonia;
using Avalonia.Controls;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;
using Avalonia.VisualTree;
internal static partial class Program
{
    private static void OnlineDesktopChecks()
    {
        var now=new DateTimeOffset(2026,11,6,10,30,0,TimeSpan.FromHours(9));var broker=new ReadOnlyBroker(now);var created=0;
        using var services=new AppServices(Path.Combine(output,"online-isolated",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>{created++;return broker;},clock:()=>now,credentials:MemoryCredentials());using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};window.Show();
        model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SelectedStock!=null);model.OpenAccounts.Execute(null);Pump(()=>window.OwnedWindows.OfType<AccountDialog>().Any());var dialog=window.OwnedWindows.OfType<AccountDialog>().Single();
        dialog.Close();var onlineToggle=window.GetVisualDescendants().OfType<RadioButton>().Single(b=>b.Content?.ToString()=="온라인");ClickAt(window,onlineToggle,.5);Pump(()=>window.OwnedWindows.OfType<AccountDialog>().Any());dialog=window.OwnedWindows.OfType<AccountDialog>().Single();Check(!model.Online&&onlineToggle.IsChecked!=true,"requesting online mode without authentication never leaves a false online radio selection");dialog.Close();Check(!model.Online&&!model.ChartCustomAvailable,"closing the unauthenticated account dialog preserves the true offline state");model.OpenAccounts.Execute(null);Pump(()=>window.OwnedWindows.OfType<AccountDialog>().Any());dialog=window.OwnedWindows.OfType<AccountDialog>().Single();
        Check(model.Accounts.Brokers.Length==4&&model.Accounts.SecretMask!='\0',"Avalonia account dialog exposes only four supported brokers and masks input by default");
        Pump(()=>!model.Accounts.Busy);model.Accounts.AppKey="DUMMY-KEY-NOT-A-REAL-KEY";model.Accounts.Secret="DUMMY-SECRET";model.Accounts.Reveal=true;Check(model.Accounts.SecretMask=='\0',"one reveal checkbox controls both in-memory key editors");
        model.Accounts.Connect.Execute(null);Pump(()=>!model.Accounts.Busy&&model.Online);Check(broker.ConnectCalls==1&&created==1&&model.ModeText.Contains("더미"),"online mode is activated only after verifying the selected fake broker account");
        Check(model.Accounts.AppKey==""&&model.Accounts.Secret==""&&!model.Accounts.Reveal,"successful account connection clears key input and reveal state");dialog.Close();
        model.ChartValue="5";model.ChartUnit="분";Avalonia.Threading.Dispatcher.UIThread.RunJobs();var customButton=window.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="지정봉");Check(customButton.IsEffectivelyEnabled,"connected minute-capable account exposes an enabled actual custom-chart button");ClickAt(window,customButton,.5);Pump(()=>!model.Busy&&model.ChartPeriod=="5분"&&model.ChartBars.Length>0);Check(model.SelectedRange.Months==-1&&model.ChartBars.All(b=>DailyQuoteMerger.Date(b.Time,Exchange.KRX)==new DateOnly(2026,11,6)),"clicking the actual online custom-chart button loads broker minute bars and selects the one-day range");
        Check(broker.MinuteCalls==1,"display-only minute request does not download indicator warmup history");
        model.ChartUnit="시간";Check(model.ChartValues.Length==12&&model.ChartValue=="1","hour selection exposes1 through12 and resets its value to1");ClickAt(window,customButton,.5);Pump(()=>!model.Busy&&model.ChartPeriod=="1시"&&model.ChartBars.Length>0);Check(broker.MinuteCalls==2&&model.ChartBars[0] is {Open:10000,High:10069,Low:9990,Close:10064,Volume:60000}&&model.ChartBars[^1] is {IsComplete:false,Close:10100},"actual custom button aggregates60 broker minutes into the exact hourly OHLCV and keeps current observations explicitly incomplete");model.ChartUnit="분";model.ChartValue="5";ClickAt(window,customButton,.5);Pump(()=>!model.Busy&&model.ChartPeriod=="5분");
        model.Accounts.Refresh.Execute(null);Pump(()=>!model.Accounts.Busy);Check(model.Accounts.Holdings.Count==1&&model.Accounts.Orders.Count==1,"account tab shows the existing broker balance/order models without sending orders");
        model.Region="해외";model.Query="애플";model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SelectedStock?.Symbol=="AAPL");Check(model.ChartPeriod=="일봉"&&model.ChartBars.Length>0&&!model.ChartCustomAvailable,"switching to a market without broker minutes resets the chart to available daily data");var beforeUnsupported=broker.MinuteCalls;ClickAt(window,customButton,.5);Pump(()=>window.OwnedWindows.Any(w=>w.Title=="안내"));Check(broker.MinuteCalls==beforeUnsupported&&window.OwnedWindows.Single(w=>w.Title=="안내").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("해외 분·시간봉")==true),"unsupported custom-chart click explains the exact market restriction in a popup without sending a request");window.OwnedWindows.Single(w=>w.Title=="안내").Close();
        model.Accounts.Offline.Execute(null);Pump(()=>!model.Busy);Check(!model.Online&&model.ChartPeriod=="일봉"&&!model.ChartCustomAvailable,"offline mode immediately hides custom periods and returns to daily data");
        model.Accounts.OnlineMode.Execute(null);Pump(()=>!model.Busy);Check(model.Online&&broker.ConnectCalls==1&&created==1,"returning online reuses the verified in-process session without authenticating again");
        model.Accounts.Confirm=_=>Task.FromResult(true);model.Accounts.ResetSelected.Execute(null);Pump(()=>!model.Accounts.Busy&&!model.Busy);Check(!model.Online&&broker.Disposed&&broker.SubmitCalls==0,"reset revokes the active in-memory session and tests never submit a real order");
        Check(services.Store.States("").All(s=>!s.Json.Contains("DUMMY-SECRET")&&!s.Json.Contains("DUMMY-KEY")),"no credential is persisted in the independent database");window.Close();
    }
    internal sealed class ReadOnlyBroker(DateTimeOffset now):IAccountBroker,ICurrentMarketData,IMinuteHistoryBroker,IRangedMinuteHistoryBroker
    {
        public int ConnectCalls,SubmitCalls,MinuteCalls;public bool Disposed,ConnectTimeout,BalanceTimeout;
        public string Id=>"fixture";public string Name=>"더미 증권";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account=>"DUMMY-ACCOUNT";public string MaskedAccount=>"****0000";public bool SupportsMarketOrders=>true;
        public Task ConnectAsync(){ConnectCalls++;return ConnectTimeout?Task.FromException(new TaskCanceledException("FIXTURE_TIMEOUT")):Task.CompletedTask;}
        public Task<OrderReceipt> SubmitAsync(OrderRequest order){SubmitCalls++;throw new InvalidOperationException("No orders allowed in fixture");}
        public Task<Balance> GetBalanceAsync()=>BalanceTimeout?Task.FromException<Balance>(new TaskCanceledException("FIXTURE_TIMEOUT")):Task.FromResult(new Balance([new("005930","삼성전자",2,10000,10100,200)],1000000,1020200));
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerOrder>>([new(date.ToString(),"DUMMY-ORDER","005930","삼성전자","매수",2,10000,2,0,false,false)]);
        public Task<decimal> CurrentPrice(string symbol)=>Task.FromResult(10100m);
        public Task<OrderReceipt> CancelAsync(string number,string symbol)=>throw new InvalidOperationException("No order mutations allowed");
        public Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation)=>MinuteHistory(symbol,new(2026,11,6),new(2026,11,6),cancellation);
        public Task<PriceBar[]> MinuteHistory(string symbol,DateOnly from,DateOnly to,CancellationToken cancellation)
        {MinuteCalls++;var start=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));return Task.FromResult(Enumerable.Range(0,90).Select(n=>new PriceBar(start.AddMinutes(n),10000+n,10010+n,9990+n,10005+n,1000)).ToArray());}
        public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)=>Task.FromResult(new CurrentMarketSnapshot(stock,new(stock.Name,10100,100,1,now,"OPEN"),now,10000,10100,9900,50000,500000000,"SYNTHETIC_BROKER_FIXTURE",TimeSpan.FromSeconds(1)));
        public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
        public void Dispose()=>Disposed=true;
    }
}
