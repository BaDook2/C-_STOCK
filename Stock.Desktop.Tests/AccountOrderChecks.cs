using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
using Stock.Desktop;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls;
internal static partial class Program
{
    private static void AccountOrderChecks()
    {
        var now=new DateTimeOffset(2026,11,6,10,30,0,TimeSpan.FromHours(9));var fake=new OrderBroker(now);
        using var services=new AppServices(Path.Combine(output,"order-ui",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>fake,clock:()=>now,credentials:MemoryCredentials());
        using var model=new MainViewModel(services);var window=new Stock.Desktop.MainWindow{DataContext=model};window.Show();model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SelectedStock!=null);
        var account=model.Accounts;account.AppKey="DUMMY";account.Secret="DUMMY";account.Connect.Execute(null);Pump(()=>!account.Busy&&account.Online);
        Check(!account.Authorized&&!account.Buy.CanExecute(null)&&fake.SubmitCalls==0,"verified actual-account connection starts locked and cannot submit an order");
        account.Confirm=_=>Task.FromResult(false);account.EnableOrders.Execute(null);Check(!account.Authorized,"cancelled real-money activation leaves manual and automatic orders locked");
        account.Confirm=_=>Task.FromResult(true);account.EnableOrders.Execute(null);Pump(()=>account.Authorized);Check(account.Buy.CanExecute(null),"explicit account-and-limit confirmation enables only the selected verified account");
        account.Confirm=text=>Task.FromResult(!text.Contains("전송할까요"));account.Buy.Execute(null);Pump(()=>!account.Busy);Check(fake.SubmitCalls==0,"cancelling the per-order confirmation sends no request");
        account.Confirm=_=>Task.FromResult(true);account.Buy.Execute(null);Pump(()=>!account.Busy);Check(fake.SubmitCalls==1&&account.Orders.Count==1&&account.Executions.Count==0&&services.Store.States("actual-fill:").Count==0,"accepted fake market order appears as pending without inventing a fill or chart marker: "+account.Status);
        account.Buy.Execute(null);Pump(()=>!account.Busy);Check(fake.SubmitCalls==1,"same-symbol pending order and durable guard prevent a duplicate submission");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
        account.SelectedOrder=account.Orders[0];account.CancelOrder.Execute(null);Pump(()=>!account.Busy);Check(fake.CancelCalls==1&&account.Session!.Unknown&&account.Orders[0].Remaining==1,"cancel acceptance is not cancellation completion and blocks new orders until reconciliation");
        fake.CancelConfirmed=true;account.Refresh.Execute(null);Pump(()=>!account.Busy);Check(!account.Session!.Unknown&&account.Orders[0].Cancelled,"broker-confirmed cancellation clears the pending reconciliation gate");
        fake.FillSeen=true;account.Refresh.Execute(null);Pump(()=>!account.Busy);Check(account.Executions.Count==1&&account.Executions[0].Price==10000&&account.Executions[0].Time==now&&account.Executions[0].AccountScope==account.Session.Scope,"execution endpoint supplies exact time, price and account scope for confirmed chart fills");
        model.ShowActual=true;Check(model.ChartMarkers.Count(m=>m.Source=="실제매매")==1,"actual-fill checkbox independently displays confirmed executions on the selected stock chart");model.ShowActual=false;Check(!model.ChartMarkers.Any(m=>m.Source=="실제매매"),"actual-fill checkbox hides those fills without deleting their durable record");
        account.Refresh.Execute(null);Pump(()=>!account.Busy);Check(services.Store.States("actual-fill:").Count==1,"repeated confirmed-execution queries are idempotent");
        model.ActiveTab=3;account.Holdings.Add(new("005930","삼성전자 · UI 합성 표본",1,10000,10100,100));Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();Check(account.Holdings[0].AveragePrice==10000&&account.Holdings[0].CurrentPrice==10100&&account.Holdings[0].Profit==100&&account.Executions[0].Price==10000,"display-only table conversion never writes zeros or nulls back into account/execution records");Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="10,100")&&window.GetVisualDescendants().OfType<TextBlock>().Count(t=>t.Text=="10,000")>=2,"actual account price and fill cells display the original floored prices with thousands separators");using(var frame=window.CaptureRenderedFrame()){Check(frame is {PixelSize.Width:>1000,PixelSize.Height:>700},"actual-account balance, order and execution tables render together with synthetic populated rows");frame!.Save(Path.Combine(output,"synthetic-account-tables.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
        account.Emergency();Check(!account.Authorized&&account.Session.Stopped&&!account.Buy.CanExecute(null),"emergency synchronously latches all account order gates and never liquidates holdings");window.Close();
        var race=new OrderBroker(now);using var raceServices=new AppServices(Path.Combine(output,"order-race",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>race,clock:()=>now,credentials:MemoryCredentials());using var runtime=new AccountWorkspace(raceServices);runtime.Initialize();runtime.OrderStock=()=>new("005930","삼성전자",Exchange.KRX,"005930");runtime.Confirm=_=>Task.FromResult(true);runtime.AppKey="DUMMY";runtime.Secret="DUMMY";runtime.Connect.Execute(null);Pump(()=>!runtime.Busy&&runtime.Online);runtime.EnableOrders.Execute(null);Pump(()=>runtime.Authorized);race.PauseBalance=true;runtime.Buy.Execute(null);Pump(()=>race.BalanceEntered);runtime.Emergency();race.Release.TrySetResult();Pump(()=>!runtime.Busy);Check(race.SubmitCalls==0,"emergency during the final account refresh is rechecked immediately before transmission");
    }
    private sealed class OrderBroker(DateTimeOffset now):IAccountBroker,IExecutionHistoryBroker
    {
        public int SubmitCalls,CancelCalls;public bool FillSeen,CancelConfirmed,PauseBalance,BalanceEntered;public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string AccountValue="DUMMY-ORDERS";public string Id=>"kiwoom";public string Name=>"더미 주문 계좌";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account=>AccountValue;public string MaskedAccount=>"****TEST";public bool SupportsMarketOrders=>true;
        public Task ConnectAsync()=>Task.CompletedTask;
        public async Task<Balance> GetBalanceAsync(){if(PauseBalance){BalanceEntered=true;await Release.Task;}return new([],1_000_000,1_000_000,OrderableCash:1_000_000);}
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerOrder>>(SubmitCalls==0?[]:[new(date.ToString(),"FAKE-1","005930","삼성전자","매수",1,10000,FillSeen?1:0,FillSeen||CancelConfirmed?0:1,CancelConfirmed,false,AverageFillPrice:FillSeen?10000:null,LastFillTime:FillSeen?now:null)]);
        public Task<decimal> CurrentPrice(string symbol)=>Task.FromResult(10000m);
        public Task<OrderReceipt> SubmitAsync(OrderRequest request){SubmitCalls++;return Task.FromResult(new OrderReceipt("FAKE-1"));}
        public Task<OrderReceipt> CancelAsync(string number,string symbol){CancelCalls++;return Task.FromResult(new OrderReceipt("FAKE-CANCEL"));}
        public Task<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerExecution>>(FillSeen?[new("EXEC-1","FAKE-1","005930",true,1,10000,now)]:[]);
        public void Dispose(){}
    }
}
