using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static BrokerConnection Connection(Brokerage id,bool defaultAccount=false)=>new(){Brokerage=id,AppKey="fixture-app-key-"+id,Secret="fixture-secret-"+id,Account="12345678",Product="01",MacAddress="001122334455",UseAsDefault=defaultAccount};
    private static async Task ConnectionTests()
    {
        var folder=Path.Combine(Dir,"vault");var vault=new BrokerCredentialStore(folder);vault.Save(Connection(Brokerage.Kiwoom));vault.Save(Connection(Brokerage.KoreaInvestment,true));
        Check(vault.Load(Brokerage.Kiwoom)?.Secret=="fixture-secret-Kiwoom"&&vault.Load(Brokerage.KoreaInvestment)?.Account=="12345678","DPAPI credentials reopen independently per brokerage");
        Check(new BrokerCredentialStore(folder).DefaultBroker==Brokerage.KoreaInvestment,"default online account survives restart");
        Check(Directory.GetFiles(folder,"*.protected").All(p=>!Encoding.UTF8.GetString(File.ReadAllBytes(p)).Contains("fixture")&&!Encoding.Unicode.GetString(File.ReadAllBytes(p)).Contains("fixture")),"persisted credential files contain no plaintext keys");
        vault.Save(Connection(Brokerage.Meritz));Check(vault.DefaultBroker==Brokerage.KoreaInvestment,"saving another broker without default option preserves default");vault.Save(Connection(Brokerage.Kiwoom,true));Check(vault.DefaultBroker==Brokerage.Kiwoom,"default account can be changed explicitly");
        File.WriteAllText(Path.Combine(folder,"Meritz.protected"),"damaged");Throws<InvalidOperationException>(()=>vault.Load(Brokerage.Meritz),"tampered credentials fail closed");
        Throws<InvalidOperationException>(()=>Connection(Brokerage.Samsung).Validate(),"unsupported broker cannot be marked connected");
        int stopped=0;var resets=new CredentialResetService(vault,id=>{stopped++;Check(vault.Load(Brokerage.Kiwoom)!=null,"active-session stop precedes persisted key deletion");return Task.CompletedTask;});
        Check(!await resets.Reset(Brokerage.Kiwoom,_=>false)&&stopped==0&&vault.Load(Brokerage.Kiwoom)!=null,"cancel reset changes neither keys nor sessions");
        Check(await resets.Reset(Brokerage.Kiwoom,_=>true)&&stopped==1&&vault.Load(Brokerage.Kiwoom)==null&&vault.Load(Brokerage.KoreaInvestment)!=null,"selected reset removes only selected key after session stop");
        vault.Save(Connection(Brokerage.Kiwoom));File.WriteAllText(Path.Combine(folder,"Samsung.protected"),"fixture-legacy");File.WriteAllText(Path.Combine(folder,"trades.db"),"fixture-preserved");
        Check(await resets.Reset(null,_=>true)&&Directory.GetFiles(folder,"*.protected").Length==0&&File.ReadAllText(Path.Combine(folder,"trades.db"))=="fixture-preserved","all reset includes removed brokers but preserves unrelated records");
        vault.Save(Connection(Brokerage.Kiwoom));var stopFailure=new CredentialResetService(vault,_=>throw new InvalidOperationException("fixture stop failure"));await ThrowsAsync<InvalidOperationException>(()=>stopFailure.Reset(null,_=>true),"stop failure prevents key deletion");Check(vault.Load(Brokerage.Kiwoom)!=null,"failed reset retains key for reconciliation");
        var kisAuth=new Credentials{AppKey="fixture",AppSecret="fixture",Account="12345678",Product="01"};int orders=0;
        using(var kis=new KisBroker(kisAuth,new Handler(async r=>
        {
            Check(r.RequestUri!.Host=="openapi.koreainvestment.com","KIS explicit live connection uses fixed production host");
            if(r.RequestUri.AbsolutePath=="/oauth2/tokenP")return Json("""{"access_token":"fixture-token","expires_in":86400}""");
            var tr=r.Headers.GetValues("tr_id").Single();
            if(r.RequestUri.AbsolutePath.EndsWith("order-cash")){orders++;using var doc=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());var b=doc.RootElement;Check(tr=="TTTC0012U"&&b.GetProperty("ORD_DVSN").GetString()=="01"&&b.GetProperty("ORD_UNPR").GetString()=="0","KIS live market order uses exact TR, market code and zero price");return Json("""{"rt_cd":"0","output":{"ODNO":"17","KRX_FWDG_ORD_ORGNO":"001"}}""");}
            if(tr=="TTTC8434R")return Json("""{"rt_cd":"0","output1":[],"output2":[{"dnca_tot_amt":"1000000","tot_evlu_amt":"1000000"}]}""");
            if(tr=="TTTC0081R")return Json("""{"rt_cd":"0","output1":[{"ord_dt":"20260928","odno":"17","pdno":"005930","prdt_name":"삼성전자","sll_buy_dvsn_cd":"02","ord_qty":"1","ord_unpr":"0","tot_ccld_qty":"0","rmn_qty":"1","cncl_yn":"N","rjct_qty":"0","ord_gno_brno":"001"}]}""");
            if(tr=="TTTC0013U"){using var doc=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());Check(doc.RootElement.GetProperty("ORGN_ODNO").GetString()=="17"&&doc.RootElement.GetProperty("KRX_FWDG_ORD_ORGNO").GetString()=="001","KIS cancel uses original account order branch");return Json("""{"rt_cd":"0","output":{"ODNO":"18"}}""");}
            throw new Exception("unexpected KIS API");
        }),live:true))
        {await kis.ConnectAsync();Check((await kis.GetBalanceAsync()).Cash==1000000,"KIS real account balance parsed");await kis.SubmitAsync(new("005930",1,70000,true,Exchange.KRX,OrderKind.Market));Check((await kis.CancelAsync("17","005930")).Number=="18","KIS cancellation receipt parsed");await ThrowsAsync<InvalidOperationException>(()=>kis.SubmitAsync(new("AAPL",1,200,true,Exchange.NASDAQ,OrderKind.Market)),"KIS unsupported US market order never sent");Check(orders==1,"KIS exactly one order request");}
        using(var invalid=new KisBroker(kisAuth,new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))),live:true))await ThrowsAsync<LiveKeyRejectedException>(invalid.ConnectAsync,"KIS invalid real credentials rejected");
        using(var missing=new KisBroker(kisAuth,new Handler(r=>Task.FromResult(Json(r.RequestUri!.AbsolutePath=="/oauth2/tokenP"?"""{"access_token":"fixture-token","expires_in":86400}""":"""{"rt_cd":"0","output1":[],"output2":[{}]}"""))),live:true)){await missing.ConnectAsync();await ThrowsAsync<InvalidOperationException>(async()=>await missing.GetBalanceAsync(),"KIS missing account numbers are never silently zero balances");}
        int meritzPosts=0;bool warn=true;int page=0;
        using(var meritz=new MeritzBroker(Connection(Brokerage.Meritz),new Handler(async r=>
        {
            Check(r.RequestUri!.Host=="openapi.imeritz.com","Meritz uses fixed official host");
            if(r.RequestUri.AbsolutePath=="/oauth2/token"){var body=await r.Content!.ReadAsStringAsync();Check(body.Contains("scope=login")&&body.Contains("client_secret="),"Meritz form token schema includes login scope");return Json("""{"access_token":"fixture-token","expires_in":604800}""");}
            Check(r.Headers.GetValues("mac_address").Single()=="001122334455","Meritz sends selected device MAC");
            if(r.RequestUri.AbsolutePath.EndsWith("/deposit"))return Json("""{"rsp_cd":"5766","data":{"tfam":1000000}}""");
            if(r.RequestUri.AbsolutePath.EndsWith("/holdings"))return Json("""{"rsp_cd":"5766","tr_cont":"0","data":[]}""");
            if(r.RequestUri.AbsolutePath.EndsWith("/history")){page++;if(page==1)return Json("""{"rsp_cd":"5762","tr_cont_key":"next  ","data":[]}""");Check(r.RequestUri.Query.Contains("tr_cont_key=next%20%20"),"Meritz continuation preserves trailing spaces in query");return Json("""{"rsp_cd":"5820","data":[]}""");}
            if(r.RequestUri.AbsolutePath.EndsWith("/buy")){meritzPosts++;using var body=JsonDocument.Parse(await r.Content!.ReadAsStringAsync());Check(body.RootElement.GetProperty("iscd").GetString()=="A005930"&&body.RootElement.GetProperty("oder_cls_code").GetString()=="05"&&body.RootElement.GetProperty("oder_unpr").GetString()=="0"&&!body.RootElement.TryGetProperty("acno",out _),"Meritz market order schema and gateway-bound account");return Json(warn?"""{"rsp_cd":"0001","data":{"oder_no":123,"warn_cls_code":"1"}}""":"""{"rsp_cd":"0001","data":{"oder_no":123,"warn_cls_code":"0"}}""");}
            throw new Exception("unexpected Meritz API");
        })))
        {await meritz.ConnectAsync();Check((await meritz.GetBalanceAsync()).Cash==1000000,"Meritz account validated using deposit and holdings");await meritz.GetOrdersAsync(KoreaClock.Today);await ThrowsAsync<BrokerRejectedException>(()=>meritz.SubmitAsync(new("005930",1,70000,true,Exchange.KRX,OrderKind.Market)),"Meritz warning with success code is not order acceptance");Check(meritzPosts==1,"Meritz warning never automatically resubmitted");warn=false;Check((await meritz.SubmitAsync(new("005930",1,70000,true,Exchange.KRX,OrderKind.Market))).Number=="123","Meritz accepted order requires zero warning and receipt");}
        var postCount=0;using(var lost=new MeritzBroker(Connection(Brokerage.Meritz),new Handler(r=>{if(r.RequestUri!.AbsolutePath=="/oauth2/token")return Task.FromResult(Json("""{"access_token":"fixture","expires_in":3600}"""));postCount++;throw new TaskCanceledException();})))await ThrowsAsync<UnknownOrderException>(()=>lost.SubmitAsync(new("005930",1,70000,true)),"Meritz lost order response remains unknown");Check(postCount==1,"Meritz lost response never retries POST");
        using(var malformed=new MeritzBroker(Connection(Brokerage.Meritz),new Handler(r=>Task.FromResult(Json(r.RequestUri!.AbsolutePath=="/oauth2/token"?"""{"access_token":"fixture","expires_in":3600}""":"""{"rsp_cd":"malformed","data":{"oder_no":123}}""")))))await ThrowsAsync<UnknownOrderException>(()=>malformed.SubmitAsync(new("005930",1,70000,true)),"unrecognized Meritz order code is unknown, not a confirmed rejection");
    }
    private static void ConnectionUiTests(MainWindow window,string output)
    {
        using var store=new TradingStore(Path.Combine(Dir,"new-ui.db"));var market=new CountingMarket();using var vm=new WorkspaceViewModel(_=>true,provider:market,store:store);var previous=window.DataContext;window.DataContext=vm;vm.ActiveTab=0;window.UpdateLayout();
        var workspaceTabs=((TabControl)window.FindName("WorkspaceTools")).Items.OfType<TabItem>().ToArray();var actualTab=workspaceTabs.Single(t=>Equals(t.Header,"실제 계좌 주문"));var virtualTab=workspaceTabs.Single(t=>Equals(t.Header,"가상 계좌 주문"));
        Check(actualTab.Visibility==Visibility.Collapsed&&virtualTab.Visibility==Visibility.Visible&&vm.WorkspaceToolTab==1,"offline mode hides actual account orders and opens the virtual account tab");
        window.UpdateLayout();var visibleButtons=Descendants(window).OfType<Button>().ToArray();
        Check(visibleButtons.Any(b=>Equals(b.Content,"실시간 가상 전략매매 설정"))&&!visibleButtons.Any(b=>Equals(b.Content,"가상 매수")||Equals(b.Content,"가상 매도")||Equals(b.Content,"가상 대기 주문 취소")),"offline virtual account uses the same realtime strategy controls and removes the legacy day-step manual account");
        var search=(TextBox)window.FindName("StockSearch");search.Text="삼성전자";search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,Key.Return){RoutedEvent=Keyboard.KeyUpEvent});PumpUntil(()=>!vm.Busy);
        Check(market.Searches==1&&vm.SearchResults.Count>0,"Enter in stock name input searches once");
        var list=(ListBox)window.FindName("StockSearchResults");window.UpdateLayout();var row=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
        row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseUpEvent});PumpUntil(()=>!vm.Busy);
        Check(market.Reads.Count==1&&vm.ChartBars.Count==600,"single stock click immediately loads chart without data button");
        Check(!Descendants(window).OfType<Button>().Any(b=>Equals(b.Content,"데이터 가져오기")),"manual data-fetch button removed");
        var periodTabs=Descendants(window).OfType<RadioButton>().Where(b=>b.GroupName=="ChartPeriod").ToArray();Check(periodTabs.Length==3,"day week month use tabs while tick and minute use combos");periodTabs.Single(b=>Equals(b.Content,"주봉")).IsChecked=true;Check(vm.ChartPeriod=="주봉"&&vm.ChartBars.Count<600,"period tab switches aggregated chart");vm.DailySelected=true;
        var samsung=vm.SelectedStock! with{Board="KOSPI"};var apple=new StockItem("AAPL","애플",Exchange.NASDAQ,"AAPL.O","정보기술");vm.WatchGroups[0].Items.Add(new(samsung));vm.WatchGroups[0].Items.Add(new(apple));vm.WatchGroups[1].Items.Add(new(samsung));vm.ScreenWatchlist=true;foreach(var g in vm.WatchGroups)g.IncludeInScreening=true;
        market.Reads.Clear();vm.Actions["스크리닝"].Execute(null);PumpUntil(()=>!vm.Busy);Check(market.Universes==0&&market.Reads.All(k=>k==samsung.Key)&&market.Reads.Count<=1&&vm.LatestScreening!.Results.Select(r=>r.Stock.Key).SequenceEqual(new[]{samsung.Key})&&vm.ScreenInfo.Contains("선택시장 밖 1개 제외"),"domestic market screening deduplicates watch groups, reuses a fresh daily cache and excludes overseas symbols before evaluation");Check(vm.ScreenInfo.Contains("중복 1개 제외"),"screening explains removed overlap");
        vm.ScreeningMarketSelection="코스피";market.Reads.Clear();vm.Actions["스크리닝"].Execute(null);PumpUntil(()=>!vm.Busy);Check(market.Reads.All(k=>k==samsung.Key)&&market.Reads.Count<=1&&vm.LatestScreening!.Results.Select(r=>r.Stock.Key).SequenceEqual(new[]{samsung.Key}),"watchlist screening respects the selected KOSPI board and may reuse fresh daily history");vm.ScreeningMarketSelection="코스닥";Check(vm.SelectedScreeningStocks().Count==0,"KOSDAQ selection excludes KOSPI watchlist stocks");vm.ScreeningMarketSelection="코스피+코스닥";vm.Sector=samsung.Sector;Check(vm.SelectedScreeningStocks().Single().Symbol==samsung.Symbol,"watchlist screening respects the domestic sector filter");
        foreach(var g in vm.WatchGroups)g.IncludeInScreening=false;market.Reads.Clear();vm.Actions["스크리닝"].Execute(null);PumpUntil(()=>!vm.Busy);Check(market.Reads.Count==0&&vm.Status.Contains("하나 이상"),"no selected group does not silently scan market");
        foreach(var g in vm.WatchGroups)g.IncludeInScreening=true;vm.Sector="전체";vm.ActiveTab=1;window.UpdateLayout();Render(window.Content as FrameworkElement,Path.Combine(output,"watch-screening.png"));
        var vault=new BrokerCredentialStore(Path.Combine(Dir,"ui-vault"));vault.Save(Connection(Brokerage.KoreaInvestment,true));
        var dialog=new ConnectionDialog(vault){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();dialog.UpdateLayout();
        var tiles=Descendants(dialog).OfType<RadioButton>().Where(r=>r.GroupName=="Brokerage").ToArray();Check(tiles.Length==4&&tiles.Single(r=>r.IsChecked==true).Tag is Brokerage.KoreaInvestment,"four remaining logo tiles preselect saved default broker");Check(Descendants(dialog).OfType<Image>().Count(i=>i.Source!=null)==4,"all four broker logos load from embedded assets");Render(dialog.Content as FrameworkElement,Path.Combine(output,"broker-connection.png"));Check(!tiles.Any(r=>r.Tag is Brokerage.Samsung or Brokerage.Mirae or Brokerage.Shinhan),"removed broker tiles do not delete stored credentials");
        var secretInputs=Descendants(dialog).OfType<SecretKeyInput>().ToArray();var showKeys=Descendants(dialog).OfType<CheckBox>().Single(c=>Equals(c.Content,"키 표시"));Check(secretInputs.Length==2&&secretInputs.All(x=>!x.Revealed),"key editors default to masked");showKeys.IsChecked=true;Check(secretInputs.All(x=>x.Revealed),"one checkbox reveals both dummy keys");var plain=Descendants(secretInputs[0]).OfType<TextBox>().Single();plain.Text="fixture-edited";Check(secretInputs[0].Value=="fixture-edited","visible key input synchronizes masked value");showKeys.IsChecked=false;Check(secretInputs.All(x=>!x.Revealed)&&secretInputs[0].Value=="fixture-edited","hide preserves edits");showKeys.IsChecked=true;plain.SelectAll();Check(!ApplicationCommands.Copy.CanExecute(null,plain),"revealed key copy is blocked");showKeys.IsChecked=false;dialog.Close();Check(secretInputs.All(x=>x.Value==""&&!x.Revealed),"closing clears secret input and reveal state");
        BrokerConnection selected=Connection(Brokerage.Kiwoom);List<RecordingBroker> created=[];var saves=new List<BrokerConnection>();
        using(var active=new WorkspaceViewModel(_=>true,provider:market,store:store,connectionPrompt:()=>selected,saveConnection:saves.Add,sessionDirectory:Path.Combine(Dir,"routing-journal"),accountFactory:(c,e)=>{var b=new RecordingBroker(c.Brokerage);created.Add(b);return b;}))
        {
            var open=active.OpenStock(samsung);PumpUntil(()=>open.IsCompleted);open.GetAwaiter().GetResult();active.IsOnline=true;PumpUntil(()=>!active.Busy);active.Actions["매수"].Execute(null);PumpUntil(()=>!active.Busy);
            Check(active.IsOnline&&created.Count==1&&created[0].Submitted==1,"online submits only to the one selected account");Check(active.ConnectedAccountText.Contains("Kiwoom")&&active.Actions["백테스트"].CanExecute(null),"online mode shows its connected account and keeps strategy backtest available");
            selected=Connection(Brokerage.KoreaInvestment);active.Actions["키설정"].Execute(null);PumpUntil(()=>!active.Busy);active.Actions["매수"].Execute(null);PumpUntil(()=>!active.Busy);
            Check(created.Count==2&&created[0].Submitted==1&&created[1].Submitted==1&&created[0].Disposed,"switch account routes next order exclusively to newly selected broker");Check(saves.Select(c=>c.Brokerage).SequenceEqual(new[]{Brokerage.Kiwoom,Brokerage.KoreaInvestment}),"only successfully verified selected accounts saved");
            active.PrepareCredentialReset(Brokerage.Kiwoom).GetAwaiter().GetResult();Check(active.IsOnline&&!created[1].Disposed,"reset of another broker leaves selected session active");var resetTask=active.PrepareCredentialReset(Brokerage.KoreaInvestment);PumpUntil(()=>resetTask.IsCompleted);resetTask.GetAwaiter().GetResult();Check(active.IsOffline&&created[1].Disposed&&!active.Actions["매수"].CanExecute(null),"active account reset revokes session and blocks further orders before key deletion");
        }
        int defaultPrompts=0;var defaultBroker=new RecordingBroker(Brokerage.Kiwoom);
        using(var automatic=new WorkspaceViewModel(_=>false,provider:market,store:store,connectionPrompt:()=>{defaultPrompts++;return null;},accountFactory:(_,_)=>defaultBroker,defaultConnectionLoader:()=>Connection(Brokerage.Kiwoom,true)))
        {automatic.IsOnline=true;PumpUntil(()=>!automatic.Busy);Check(automatic.IsOnline&&defaultPrompts==0&&automatic.ConnectedAccountText.Contains("Kiwoom"),"online mode connects the saved default account without opening the key dialog");}
        using(var fail=new WorkspaceViewModel(_=>false,provider:market,store:store,connectionPrompt:()=>Connection(Brokerage.Meritz,true),saveConnection:vault.Save,accountFactory:(c,e)=>new RecordingBroker(c.Brokerage){FailBalance=true}))
        {fail.IsOnline=true;PumpUntil(()=>!fail.Busy);Check(fail.IsOffline&&vault.Load(Brokerage.Meritz)==null&&vault.DefaultBroker==Brokerage.KoreaInvestment,"failed account verification saves no key and preserves default");}
        window.DataContext=previous;
    }
    private static void Render(FrameworkElement? root,string path)
    {root!.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var f=File.Create(path);encoder.Save(f);}
    private sealed class CountingMarket:IPublicMarketData
    {
        private readonly FixtureMarketData inner=new();public int Searches,Universes;public List<string> Reads=[];
        public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token){Searches++;return inner.Search(name,region,token);}
        public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token){Universes++;return inner.Universe(region,count,token);}
        public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>inner.Describe(stock,token);
        public async Task<BarDataset> History(StockItem stock,CancellationToken token){Reads.Add(stock.Key);await Task.Delay(5,token);return await inner.History(stock,token);}
    }
    private sealed class RecordingBroker(Brokerage id):IAccountBroker
    {
        public int Submitted;public bool Disposed,FailBalance;public string Id=>"fixture-"+id;public string Name=>id.ToString();public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account=>"fixture-"+id;public string MaskedAccount=>"****0000";public bool SupportsMarketOrders=>true;
        public Task ConnectAsync()=>Task.CompletedTask;
        public Task<Balance> GetBalanceAsync()=>FailBalance?throw new InvalidOperationException("fixture account denied"):Task.FromResult(new Balance([],10000000,10000000));
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerOrder>>([]);
        public Task<OrderReceipt> SubmitAsync(OrderRequest o){Submitted++;return Task.FromResult(new OrderReceipt("fixture-"+Submitted));}
        public Task<OrderReceipt> CancelAsync(string number,string symbol)=>Task.FromResult(new OrderReceipt("fixture-cancel"));
        public Task<decimal> CurrentPrice(string symbol)=>Task.FromResult(70000m);
        public void Dispose()=>Disposed=true;
    }
}
