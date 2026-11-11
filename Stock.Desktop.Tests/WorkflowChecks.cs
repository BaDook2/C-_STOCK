using System.Globalization;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;

internal static partial class Program
{
 private static void WorkflowChecks()
 {
  var now=new DateTimeOffset(2026,11,6,10,30,0,TimeSpan.FromHours(9));var brokers=new Dictionary<Brokerage,ReadOnlyBroker>();
  using var services=new AppServices(Path.Combine(output,"workflow-isolated",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(c,_)=>brokers[c.Brokerage]=new(now),clock:()=>now,credentials:MemoryCredentials());
  using var model=new MainViewModel(services);var window=new MainWindow{DataContext=model};window.Show();model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.ChartBars.Length>0);
  model.ActiveTab=2;Dispatcher.UIThread.RunJobs();var name=model.Strategies.Selected;var field=model.Strategies.Groups.SelectMany(g=>g.Fields).Single(f=>f.Path=="commonParameters.OrderBudget");field.Text="321.1234";model.Strategies.Save.Execute(null);Check(model.Strategies.Registered(name).Injes!.CommonParameters.OrderBudget==321.1234m,"strategy Save applies exact native editor values to the selected preset");
  model.Strategies.Groups.SelectMany(g=>g.Fields).Single(f=>f.Path=="commonParameters.OrderBudget").Text="400";model.Strategies.Restore.Execute(null);Check(model.Strategies.Current().Injes!.CommonParameters.OrderBudget==321.1234m,"Restore discards unsaved native edits");model.Strategies.Defaults.Execute(null);Check(model.Strategies.Current().Injes!.CommonParameters.OrderBudget==500&&model.Strategies.Registered(name).Injes!.CommonParameters.OrderBudget==321.1234m,"Defaults edits the native draft without overwriting its saved values");
  model.Strategies.Name="워크플로 검증";model.Strategies.Add.Execute(null);Check(model.Strategies.Names.Contains("워크플로 검증"),"Add creates a separate native preset");model.Strategies.Name="워크플로 검증 변경";model.Strategies.Rename.Execute(null);Check(model.Strategies.Selected=="워크플로 검증 변경"&&!model.Strategies.Names.Contains("워크플로 검증"),"Rename removes the old native preset name");
  var jsonPath=Path.Combine(output,"workflow-strategy.json");model.Strategies.FileDialogs=new FixtureStrategyFiles(jsonPath);model.Strategies.Export.Execute(null);Pump(()=>File.Exists(jsonPath));Check(Stock.Core.Strategies.StrategyProfileJson.Import(File.ReadAllText(jsonPath)).InjeStrategies.Single(d=>d.StrategyId==model.Strategies.InjeId).ExecutionPolicy.FeePercent==.05m,"native JSON download preserves the exact fee and schema");var count=model.Strategies.Names.Count;model.Strategies.Import.Execute(null);Pump(()=>model.Strategies.Names.Count>count);Check(model.Strategies.Selected.StartsWith("워크플로 검증 변경"),"native JSON import registers a unique preset");
  foreach(var brokerId in new[]{Brokerage.Kiwoom,Brokerage.KoreaInvestment,Brokerage.Meritz,Brokerage.Toss})
  {
   model.Accounts.Selected=brokerId;model.Accounts.AppKey="DUMMY";model.Accounts.Secret="DUMMY-SECRET";model.Accounts.Account="12345678";model.Accounts.Mac="001122334455";model.Accounts.Connect.Execute(null);Pump(()=>!model.Accounts.Busy&&model.Online);Check(model.Accounts.Active==brokers[brokerId],brokerId+" account selection routes only to its verified fake session");model.Accounts.Refresh.Execute(null);Pump(()=>!model.Accounts.Busy);Check(model.Accounts.Holdings.Count==1&&model.Accounts.Orders.Count==1,brokerId+" account/balance/order refresh is read-only");
   foreach(var popup in window.OwnedWindows.ToArray())popup.Close();
  }
  model.Accounts.Offline.Execute(null);Pump(()=>!model.Busy);Check(!model.Online&&model.VisibleChartPeriods.SequenceEqual(new[]{"일봉"}),"offline mode exposes daily candles and keeps connected fake sessions in memory");model.Accounts.OnlineMode.Execute(null);Pump(()=>!model.Busy);Check(model.Online&&brokers.Values.All(b=>b.ConnectCalls==1),"online/offline switching reuses all four verified sessions without reauthentication");
  model.Accounts.Confirm=_=>Task.FromResult(true);model.Accounts.Selected=Brokerage.Kiwoom;model.Accounts.ResetSelected.Execute(null);Pump(()=>!model.Accounts.Busy);Check(model.Online&&brokers[Brokerage.Kiwoom].Disposed,"resetting an inactive account does not disconnect the active account");
  model.ChartValue="1";model.ChartUnit="시간";model.ApplyChartTimeframe.Execute(null);Pump(()=>!model.Busy&&model.ChartBars.Length>0);Check(ChartPeriods.Minutes(model.ChartPeriod)==60&&model.ChartBars.Length>0,"online hourly chart uses the same minute aggregation service");
  model.Research.Custom=true;model.Research.Value="5";model.Research.Unit="분";model.Research.Value="5";model.Research.From=new DateTime(2026,11,6);model.Research.To=new DateTime(2026,11,6);model.Research.Run.Execute(null);Pump(()=>!model.Research.Busy);Check(model.Research.Result==null&&model.Research.Status.Contains("미국"),"changing the displayed five-minute candles never permits a native strategy to use Korean daily data");foreach(var popup in window.OwnedWindows.ToArray())popup.Close();
  model.Accounts.ResetAll.Execute(null);Pump(()=>!model.Accounts.Busy&&!model.Busy);Check(!model.Online&&brokers.Values.All(b=>b.Disposed)&&brokers.Values.All(b=>b.SubmitCalls==0),"all API resets revoke every fake session without any order request");
  model.ActiveTab=0;model.ToggleWatch.Execute(null);model.SelectedWatchStock=model.SelectedStock;model.OpenWatchStock.Execute(null);Check(model.ActiveTab==0,"watchlist chart command opens the selected stock in the main workspace");model.RemoveWatchStock.Execute(null);Check(!model.WatchItems!.Any(s=>s.Key==model.SelectedStock!.Key),"watchlist remove updates the selected group without deleting other groups");
  var priorCulture=CultureInfo.CurrentCulture;var expectedFee=model.Strategies.Current().CommissionPercent;try{foreach(var culture in new[]{"ko-KR","en-US"}){CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(culture);Check(DecimalEditing.Format("0.015",3)=="0.015"&&model.Strategies.Current().CommissionPercent==expectedFee,"numeric configuration remains stable under "+culture);}}finally{CultureInfo.CurrentCulture=priorCulture;}
  window.Close();FaultAndCancellationChecks();TimeoutSurfaceChecks();UnresponsiveProviderChecks();
 }
 private sealed class FaultMarket:IPublicMarketData
 {
  internal readonly TaskCompletionSource<BarDataset> Slow=new(TaskCreationOptions.RunContinuationsAsynchronously);
  internal bool Fail,Wait,Timeout;
  internal string? TimeoutSymbol;
  private readonly MarketFixture source=new();
  public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>([new("005930","삼성전자",Exchange.KRX,"005930"),new("000660","SK하이닉스",Exchange.KRX,"000660")]);
  public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>Search("","",token);
  public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>Task.FromResult(stock);
  public Task<BarDataset> History(StockItem stock,CancellationToken token)=>Timeout&&(TimeoutSymbol==null||stock.Symbol==TimeoutSymbol)?Task.FromException<BarDataset>(new TaskCanceledException("TEST_HTTP_TIMEOUT")):Fail?Task.FromException<BarDataset>(new HttpRequestException("TEST_NETWORK_UNAVAILABLE")):Wait&&stock.Symbol=="005930"?Slow.Task:source.History(stock with{Symbol="005930"},token).ContinueWith(t=>t.Result with{Stock=stock},token);
 }
 private static void FaultAndCancellationChecks()
 {
  var source=new FaultMarket{Fail=true};using var services=new AppServices(Path.Combine(output,"fault-isolated",Guid.NewGuid().ToString("N")),source);using var model=new MainViewModel(services);string? notice=null;model.NotificationRequested+=s=>notice=s;var window=new MainWindow{DataContext=model};window.Show();model.SearchCommand.Execute(null);Pump(()=>!model.Busy);Check(model.ChartBars.Length==0&&notice?.Contains("TEST_NETWORK_UNAVAILABLE")==true,"public download failure is surfaced and never turned into fake or successful empty candles");foreach(var popup in window.OwnedWindows.ToArray())popup.Close();
  source.Fail=false;source.Timeout=true;notice=null;model.SearchCommand.Execute(null);Pump(()=>!model.Busy);Check(notice?.Contains("응답") ==true&&!model.Status.Contains("취소했습니다"),"provider timeout raises an error instead of being mistaken for a user cancellation");foreach(var popup in window.OwnedWindows.ToArray())popup.Close();source.Timeout=false;
  source.Wait=true;model.SearchCommand.Execute(null);Pump(()=>model.Busy);model.EmergencyCommand.Execute(null);Check(!model.Busy&&model.ChartBars.Length==0,"emergency removes a waiting chart overlay immediately");
  model.SelectedStock=model.SearchResults[1];Pump(()=>!model.Busy&&model.SelectedStock?.Symbol=="000660");var bars=model.ChartBars;source.Slow.TrySetResult(new(model.SearchResults[0],[new(DateTimeOffset.UtcNow,1,1,1,1,1)],"CANCELLED_OLD",DateTimeOffset.UtcNow));Pump(()=>source.Slow.Task.IsCompleted);Check(model.SelectedStock?.Symbol=="000660"&&ReferenceEquals(bars,model.ChartBars),"late completion of a cancelled stock cannot overwrite the new stock chart");window.Close();
 }
 private static void TimeoutSurfaceChecks()
 {
  var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930");var source=new FaultMarket{Timeout=true};using var services=new AppServices(Path.Combine(output,"timeout-surfaces",Guid.NewGuid().ToString("N")),source);var strategies=new StrategyEditor(services.Store);var history=new MarketHistoryService(services,()=>null);string? notice=null;
  var research=new ResearchWorkspace(services,strategies,history,()=>stock,()=>false);research.Notice+=s=>notice=s;research.Run.Execute(null);Pump(()=>!research.Busy);Check(notice?.Contains("응답")==true&&!research.Status.Contains("취소"),"backtest history timeout produces an explicit error notification");
  notice=null;var recurring=new RecurringWorkspace(services,history,()=>stock);recurring.Initialize();recurring.Notice+=s=>notice=s;recurring.Run.Execute(null);Pump(()=>notice!=null);Check(notice!.Contains("응답")&&!recurring.Status.Contains("취소"),"recurring history timeout is not silently treated as a user stop");
  source.TimeoutSymbol=stock.Symbol;var screening=new ScreeningWorkspace(services,strategies,history,()=>false,()=>[],()=>Serialization.Pack(new HistoricalFacts())){ShowAll=true};screening.Start.Execute(null);Pump(()=>!screening.Busy,20);Check(screening.Snapshot is {Results.Length:2}&&screening.Rows.Any(r=>r.Code=="005930"&&r.State.Contains("응답"))&&screening.Rows.Any(r=>r.Code=="000660"&&r.State.Contains("MissingData")),"one screening symbol timeout is recorded while the other symbol still completes");Check(screening.Status.Contains("완료")&&!screening.Status.Contains("취소"),"per-symbol timeout does not cancel the entire screening run");
  using var browser=new ResearchBrowserViewModel([stock],(_,_)=>Task.FromException<ResearchResultViewModel>(new TaskCanceledException("FIXTURE_TIMEOUT")),"timeout");browser.Initialize();notice=null;browser.Notice+=s=>notice=s;var load=browser.Go(0);Pump(()=>load.IsCompleted);Check(notice?.Contains("응답")==true&&!browser.Busy,"result-popup timeout releases loading and reports an error");
  var now=new DateTimeOffset(2026,11,6,10,0,0,TimeSpan.FromHours(9));var fake=new ReadOnlyBroker(now){ConnectTimeout=true};using var accountServices=new AppServices(Path.Combine(output,"account-timeout",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>fake,credentials:MemoryCredentials());using var account=new AccountWorkspace(accountServices);account.Initialize();notice=null;account.Notice+=s=>notice=s;account.AppKey="DUMMY";account.Secret="DUMMY";account.Connect.Execute(null);Pump(()=>!account.Busy);Check(!account.Online&&fake.Disposed&&notice?.Contains("응답")==true,"account verification timeout creates no connected session and disposes its candidate");
  var connected=new ReadOnlyBroker(now);using var refreshServices=new AppServices(Path.Combine(output,"account-refresh-timeout",Guid.NewGuid().ToString("N")),new MarketFixture(),brokerFactory:(_,_)=>connected,credentials:MemoryCredentials());using var refreshAccount=new AccountWorkspace(refreshServices);refreshAccount.Initialize();refreshAccount.AppKey="DUMMY";refreshAccount.Secret="DUMMY";refreshAccount.Connect.Execute(null);Pump(()=>!refreshAccount.Busy&&refreshAccount.Online);var previousCash=refreshAccount.Cash;var previousHoldings=refreshAccount.Holdings.ToArray();connected.BalanceTimeout=true;notice=null;refreshAccount.Notice+=s=>notice=s;refreshAccount.Refresh.Execute(null);Pump(()=>!refreshAccount.Busy);Check(notice?.Contains("조회 실패")==true&&refreshAccount.Online&&refreshAccount.Cash==previousCash&&refreshAccount.Holdings.SequenceEqual(previousHoldings)&&connected.SubmitCalls==0,"account refresh timeout keeps the verified session and prior balance, reports stale values, and sends no order");
 }
 private sealed class PartialQuoteMarket(Func<DateTimeOffset> clock):IPublicMarketData,ICurrentMarketData
 {
  private readonly MarketFixture source=new(){TwoTargets=true,Clock=clock};
  internal int GoodQuotes;private int firstSymbolQuotes;
  public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>source.Search(name,region,token);
  public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>source.Universe(region,count,token);
  public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>source.Describe(stock,token);
  public Task<BarDataset> History(StockItem stock,CancellationToken token){source.ReleaseSecond.TrySetResult();return source.History(stock,token);}
  public Task<CurrentMarketSnapshot> Current(StockItem stock,CancellationToken token)
  {if(stock.Symbol=="005930"&&Interlocked.Increment(ref firstSymbolQuotes)>1)return Task.FromException<CurrentMarketSnapshot>(new TaskCanceledException("ONE_SYMBOL_TIMEOUT"));if(stock.Symbol!="005930")Interlocked.Increment(ref GoodQuotes);return source.Current(stock,token);}
  public Task<DailySecurityStatus?> CurrentSecurity(StockItem stock,CancellationToken token)=>Task.FromResult<DailySecurityStatus?>(null);
 }
 private static void UnresponsiveProviderChecks()
 {
  var source=new FaultMarket{Wait=true};using var services=new AppServices(Path.Combine(output,"unresponsive-query",Guid.NewGuid().ToString("N")),source,queryTimeout:TimeSpan.FromMilliseconds(180));using var model=new MainViewModel(services);string? notice=null;model.NotificationRequested+=s=>notice=s;var window=new MainWindow{DataContext=model};window.Show();model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.SearchCommand.CanExecute(null),5);Check(!source.Slow.Task.IsCompleted&&notice?.Contains("응답")==true,"chart loading has a bounded deadline even when the provider never completes or honors cancellation");foreach(var popup in window.OwnedWindows.ToArray())popup.Close();
  source.Wait=false;model.SearchCommand.Execute(null);Pump(()=>!model.Busy&&model.ChartBars.Length>0);Check(model.SelectedStock?.Symbol=="005930"&&model.SearchCommand.CanExecute(null),"a timed-out chart query releases the search command so the user can retry");window.Close();
  var paused=new FaultMarket{Wait=true};using var cancelServices=new AppServices(Path.Combine(output,"cancel-paused-query",Guid.NewGuid().ToString("N")),paused);using var cancelModel=new MainViewModel(cancelServices);var cancelWindow=new MainWindow{DataContext=cancelModel};cancelWindow.Show();cancelModel.SearchCommand.Execute(null);Pump(()=>cancelModel.Busy);Click(cancelWindow,cancelWindow.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="작업 취소"));Pump(()=>!cancelModel.Busy&&cancelModel.SearchCommand.CanExecute(null),3);Check(!paused.Slow.Task.IsCompleted,"the visible Cancel Work button releases loading and the search button without waiting for a stuck provider");cancelWindow.Close();
 }
}
