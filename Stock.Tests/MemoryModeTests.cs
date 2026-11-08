using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void MemoryModeTests(string output)
    {
        var path=Path.Combine(Dir,"memory-policy.db");var key="KRX:005930";
        using(var legacy=new TradingStore(path)){legacy.State("bars:"+key,"old cached price");legacy.State("watch-groups",Array.Empty<WatchGroupData>());}
        using(var store=new TradingStore(path,memoryMarketData:true))
        {
            Check(store.State<string>("bars:"+key)==null,"memory policy ignores legacy disk market caches without deleting them");
            store.State("bars:"+key,"new price");store.State("live-ticks:"+key,new[]{123.45m});store.State("catalog",new[]{"005930"});
            Check(store.State<string>("bars:"+key)=="new price"&&store.State<decimal[]>("live-ticks:"+key)![0]==123.45m,"price and tick caches remain readable in the current process");
            var e=new MarketEvent(new("005930",Exchange.KRX),EventKind.Trade,Epoch,Epoch,"TEST",7){TradePrice=123.45m,TradeSize=2};
            Check(store.InsertEvents([e,e])==1&&store.ReadEvents(key,"TEST",Epoch,Epoch).Count==1,"raw ticks stay in memory with exact values and duplicate suppression");
            store.State("chart-run:policy",new{Profit=12.34m});store.State("screen-data:policy",new[]{123.45m});
        }
        using(var disk=new TradingStore(path))
        {
            Check(disk.ReadEvents(key,"TEST",Epoch,Epoch).Count==0&&disk.State<decimal[]>("live-ticks:"+key)==null&&disk.State<string[]>("catalog")==null,"raw ticks and reloadable caches never enter SQLite");
            Check(disk.State<string>("bars:"+key)=="old cached price"&&disk.State<System.Text.Json.JsonElement>("chart-run:policy").GetProperty("Profit").GetDecimal()==12.34m,"legacy rows and durable research history remain intact");
        }
        using(var restarted=new TradingStore(path,memoryMarketData:true))Check(restarted.ReadEvents(key,"TEST",Epoch,Epoch).Count==0&&restarted.State<string>("bars:"+key)==null,"new process store starts with no stale memory prices or raw ticks");
        Check(DisplayNumber.Price(1234.999m)=="1,234"&&DisplayNumber.Price(.99m)=="0"&&DisplayNumber.Price(-1.01m)=="-2","all displayed prices use floor without changing source precision");
        var marker=new TradeMarker("profit",key,Epoch,false,1,123.99m,"TEST",RealizedPnl:2345.67m);
        Check(marker.ProfitText=="실현손익 +2,345 원"&&marker.Price==123.99m,"sell annotations expose net profit while preserving the execution price");
        var day=new BarSpecification(1,BarUnit.Day);day.Validate();Check(day.ToString()=="일봉","daily live strategy has an explicit typed data specification");
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var close=open.AddMinutes(390);var instrument=new Instrument("005930",Exchange.KRX);
        var ticks=new[]{new MarketEvent(instrument,EventKind.Trade,open,open,"DAY_TEST",1){TradePrice=100,TradeSize=1,CumulativeVolume=1},new MarketEvent(instrument,EventKind.Trade,close.AddMinutes(-1),close.AddMinutes(-1),"DAY_TEST",2){TradePrice=90,TradeSize=1,CumulativeVolume=2}};
        var sessions=new[]{new DailyMarketSession(new(2026,11,6),open,close)};var coverage=new BarCoverage(open,close,true,"DAY_TEST");
        Check(StrategyBarAggregator.FromTrades(ticks,instrument,day,sessions,coverage,close.AddMinutes(-1)).Bars.Length==0,"daily strategy does not emit or trade an unfinished daily candle");
        var daily=StrategyBarAggregator.FromTrades(ticks,instrument,day,sessions,coverage,close).Bars.Single();
        Check(daily.OpenTime==open&&daily.ClosedAt==close&&daily.Price.Open==100&&daily.Price.Close==90&&daily.Price.Volume==2,"daily aggregation follows the verified trading session with exact OHLCV");
        var priceBars=Enumerable.Range(0,5).Select(i=>DailyBar(i,100-i)).ToArray();var data=new BarDataset(DailyStock,priceBars,"TEST",Epoch);var config=DailyConfig();var facts=DailyFacts(5);var service=new OversoldBacktestService();
        var request=new OversoldBacktestRequest(data,config,DailyDates.Of(priceBars[0]),DailyDates.Of(priceBars[^1]),facts);
        var standard=service.RunAsync(request,default).GetAwaiter().GetResult();var realtimeDaily=service.RunAsync(request with{BarSpecification=day,StrategyBars=priceBars.Select((b,i)=>new StrategyBar(b,b.Time,b.Time,i,null,"TEST")).ToArray()},default).GetAwaiter().GetResult();
        Check(standard.Id==realtimeDaily.Id&&realtimeDaily.BarSpecification==null,"realtime daily popup reuses the exact daily backtest engine and cache entry");
        Throws<InvalidOperationException>(()=>service.RunAsync(request with{BarSpecification=new(2,BarUnit.Day)},default).GetAwaiter().GetResult(),"daily normalization cannot bypass data specification validation");
        var broker=new ModeCacheBroker();int prompts=0;var alerts=new List<string>();
        using var db=new TradingStore(Path.Combine(Dir,"mode-ui.db"),memoryMarketData:true);
        using var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db,alert:alerts.Add,connectionPrompt:()=>{prompts++;return Connection(Brokerage.Kiwoom);},accountFactory:(_,_)=>broker,sessionDirectory:Path.Combine(Dir,"mode-session"));
        var window=new MainWindow(vm){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();
        vm.Query="삼성전자";vm.Actions["검색"].Execute(null);PumpUntil(()=>!vm.Busy);
        vm.ChartPeriod="5분";vm.ScreenCustomBars=true;vm.RealtimeScreeningMode=true;
        Check(vm.ChartPeriod=="일봉"&&vm.ScreenDaily&&!vm.ShowScreenCustomBars&&!vm.CanUseMinutes&&vm.MultiTrading.PublicDaily&&!vm.MultiTrading.ShowPublicCustom,"offline chart screening and multi-strategy settings consistently offer daily bars only");
        vm.IsOnline=true;PumpUntil(()=>!vm.Busy);Check(vm.IsOnline&&vm.CanUseMinutes,"first online switch authenticates the selected broker");
        var reads=broker.Reads;var connections=broker.Connects;var accountSummary=vm.AccountInfo;
        var durations=new List<double>();for(int n=0;n<3;n++){var timer=System.Diagnostics.Stopwatch.StartNew();vm.IsOffline=true;PumpUntil(()=>!vm.Busy);Check(vm.IsOffline&&!vm.CanUseMinutes,"offline switch removes access to the parked real account");vm.IsOnline=true;PumpUntil(()=>!vm.Busy);durations.Add(timer.Elapsed.TotalMilliseconds);}
        Check(vm.IsOnline&&prompts==1&&broker.Reads==reads&&broker.Connects==connections&&!broker.Disposed,"repeated online switches reuse authentication without account API calls or disposing the broker");
        Check(vm.AccountInfo==accountSummary,"mode switching preserves one account summary without accumulating connection notices");
        File.WriteAllText(Path.Combine(output,"mode-switch-timing.json"),Serialization.Pack(new{RoundTripsMs=durations,AdditionalAuthentication=broker.Connects-connections,AdditionalAccountReads=broker.Reads-reads}));
        vm.ActiveTab=0;window.UpdateLayout();
        var custom=(RadioButton)window.FindName("ChartCustomButton");var number=(ComboBox)window.FindName("ChartCustomBarNumber");var unit=(ComboBox)window.FindName("ChartCustomBarUnit");
        var originalWidth=window.Width;var originalHeight=window.Height;
        foreach(var size in new[]{(1540d,1040d),(1160d,760d)})
        {
            window.Width=size.Item1;window.Height=size.Item2;window.UpdateLayout();
            var host=(FrameworkElement)custom.Parent;var toolbar=(FrameworkElement)((FrameworkElement)host.Parent).Parent;
            var right=unit.TranslatePoint(new Point(unit.ActualWidth,0),toolbar).X;
            Check(custom.IsVisible&&custom.IsEnabled&&number.IsVisible&&unit.IsVisible&&right<=toolbar.ActualWidth+1,"online custom-bar button and selectors stay inside chart toolbar at "+size);
            Render(window,Path.Combine(output,$"custom-chart-{(int)size.Item1}.png"));
        }
        window.Width=originalWidth;window.Height=originalHeight;window.UpdateLayout();
        custom.IsChecked=true;PumpUntil(()=>!vm.Busy);Check(vm.CustomChartSelected&&vm.ChartPeriod=="1분"&&vm.ChartBars.Count>0,"custom button renders the selected minute chart directly");
        vm.DailySelected=true;custom.IsChecked=true;PumpUntil(()=>!vm.Busy);Check(vm.CustomChartSelected&&vm.ChartPeriod=="1분","custom button returns from daily to the selected custom period without changing the combos");
        OneDayRangeUiTests(window,vm,broker,alerts,output);
        vm.DailySelected=true;var tools=(TabControl)window.FindName("WorkspaceTools");
        var headers=Descendants(tools).OfType<TabItem>().Where(x=>x.IsVisible).ToArray();
        Check(headers.Length==4&&Math.Abs(headers[0].ActualWidth-headers[1].ActualWidth)<1&&Math.Abs(headers[0].TranslatePoint(new Point(),tools).Y-headers[1].TranslatePoint(new Point(),tools).Y)<1&&headers[2].TranslatePoint(new Point(),tools).Y>headers[0].TranslatePoint(new Point(),tools).Y,"four account and backtest headers use two aligned equal-width rows");
        var scroll=new ScrollViewer{Width=150,Height=100,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Visible,Content=new Border{Width=500,Height=500}};
        scroll.Measure(new Size(150,100));scroll.Arrange(new Rect(0,0,150,100));scroll.UpdateLayout();
        var bars=Descendants(scroll).OfType<ScrollBar>().ToArray();
        Check(bars.Length==2&&bars.All(x=>x.Template!=null&&x.Template.FindName("Grip",x) is Thumb),"global rounded scrollbar template covers both horizontal and vertical scrollbars");
        vm.WorkspaceToolTab=2;window.UpdateLayout();
        var content=(FrameworkElement)tools.SelectedContent;var bottom=headers.Max(h=>h.TranslatePoint(new Point(),tools).Y+h.ActualHeight);
        Check(content.TranslatePoint(new Point(),tools).Y-bottom<20,"tool content starts directly below the aligned headers without a vertically centered blank gap");
        Render(window,Path.Combine(output,"daily-memory-tools.png"));
        var chart=new CandleChart{Bars=priceBars,Period="일봉",TradeMarkers=[new("buy",key,priceBars[0].Time,true,10,100,"TEST"),new("win",key,priceBars[2].Time,false,5,105,"TEST",Reason:"TP1",RealizedPnl:24.9m),new("loss",key,priceBars[4].Time,false,5,90,"TEST",Reason:"EmergencyStop",RealizedPnl:-50.1m)]};chart.Measure(new Size(1000,550));chart.Arrange(new Rect(0,0,1000,550));Render(chart,Path.Combine(output,"sell-profit-markers.png"));
        vm.IsOffline=true;PumpUntil(()=>!vm.Busy);vm.Actions["긴급중지"].Execute(null);PumpUntil(()=>vm.Status.StartsWith("긴급중지 완료",StringComparison.Ordinal));
        Check(db.State<bool>(broker.Id+"-live-"+TradingStore.Hash(broker.Account)[..24]+"-stopped")&&broker.Reads==reads,"offline emergency stop durably blocks the parked session without calling broker APIs");
        window.Close();Check(broker.Disposed,"closing the application disposes a parked authenticated connection");
    }
    private sealed class ModeCacheBroker:IAccountBroker,IMinuteHistoryBroker
    {
        public int Connects,Reads,MinuteReads;public bool Disposed;public string Id=>"mode-fixture";public string Name=>"검증 증권사";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account=>"1234567890";public string MaskedAccount=>"******7890";public bool SupportsMarketOrders=>true;
        public Task ConnectAsync(){Connects++;return Task.CompletedTask;}
        public Task<decimal> CurrentPrice(string symbol)=>Task.FromResult(123.99m);
        public Task<Balance> GetBalanceAsync(){Reads++;return Task.FromResult(new Balance([],10000000,10000000));}
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date){Reads++;return Task.FromResult<IReadOnlyList<BrokerOrder>>([]);}
        public Task<OrderReceipt> SubmitAsync(OrderRequest order)=>throw new InvalidOperationException("No real orders in tests");
        public Task<OrderReceipt> CancelAsync(string number,string symbol)=>throw new InvalidOperationException("No real orders in tests");
        public Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken cancellation)
        {
            MinuteReads++;
            var open=new DateTimeOffset(2026,11,5,9,0,0,TimeSpan.FromHours(9));
            return Task.FromResult(Enumerable.Range(0,2).SelectMany(d=>Enumerable.Range(0,120).Select(i=>new PriceBar(open.AddDays(d).AddMinutes(i),100,101,99,100,10))).ToArray());
        }
        public void Dispose()=>Disposed=true;
    }
}
