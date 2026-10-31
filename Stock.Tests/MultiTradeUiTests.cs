using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private sealed class BatchFeed:IMarketDataProvider
    {
        public string Description=>"TEST ONLY";public readonly Channel<MarketEvent> Events=Channel.CreateUnbounded<MarketEvent>();public bool Disposed;
        public async IAsyncEnumerable<MarketEvent> Stream([EnumeratorCancellation] CancellationToken token){await foreach(var e in Events.Reader.ReadAllAsync(token))yield return e;}
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private static void MultiTradeUiTests(MainWindow window,string output,bool selectionOnly=false)
    {
        using var db=new TradingStore(Path.Combine(Dir,"batch-ui.db"));var b=new BatchBroker{Now=DailyTime(2,9,0)};var feeds=new List<BatchFeed>();var requested=new List<Instrument[]>();
        var liveConfig=DailyConfig();liveConfig.UseEmergencyStop=true;liveConfig.MinimumAverageTradingValue=0;liveConfig.MovingAveragePeriods="1";liveConfig.MinimumSupportCount=1;liveConfig.UsePivotSupport=liveConfig.UseApproximateVolumeZone=false;
        db.State("oversold-config-v1",liveConfig);db.State("oversold-facts-v1",DailyFacts(5));db.State("oversold-screen-bars",new ScreeningBarPreference(false,"1","분"));
        var raw=Enumerable.Range(0,2).SelectMany(day=>Enumerable.Range(0,2).Select(tick=>new MarketEvent(new(DailyStock.Symbol,Exchange.KRX),EventKind.Trade,DailyTime(day,9,0).AddSeconds(tick),DailyTime(day,9,0).AddSeconds(tick),"KIWOOM",day*2+tick+1){TradePrice=100,TradeSize=10,CumulativeVolume=10*(tick+1),PreviousClose=100})).ToArray();
        db.InsertEvents(raw);db.State("verified-tick-coverage:"+DailyStock.Key,new BarCoverage(raw[0].ExchangeTime,raw[^1].ExchangeTime,true,"KIWOOM",raw[0].Sequence,raw[^1].Sequence));
        var config=new StrategyConfiguration{Enabled=true,StrategyType=StrategyType.Box};config.Standard.Common.Investment=10000;config.Standard.Common.TradingStart="09:00";config.Standard.Common.TradingEnd="15:20";
        db.State("chart-presets",new[]{"등록한 박스 전략"});db.State("strategy-configuration-preset:등록한 박스 전략",config);db.State("strategy-tick-preset:등록한 박스 전략",BatchSettings());
        var vm=new WorkspaceViewModel(_=>true,provider:new FixtureMarketData(),store:db,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>b,sessionDirectory:Path.Combine(Dir,"ui-orders"),multiClock:()=>b.Now,multiFeedFactory:(_,symbols,_,_)=>{var f=new BatchFeed();feeds.Add(f);requested.Add(symbols.ToArray());return f;});
        var old=window.DataContext;window.DataContext=vm;vm.IsOnline=true;PumpUntil(()=>!vm.Busy);
        Check(vm.IsOnline&&vm.CanOpenMulti&&vm.MultiStatus.Contains("검증된 시점별 자료"),"selected online account can inspect the guarded batch popup");
        var multi=vm.MultiTrading;var actionNotices=new List<string>();multi.NotificationRequested+=actionNotices.Add;multi.Reload();Check(multi.UseMock&&multi.Presets.SequenceEqual(vm.Presets),"multi-trade strategy choices mirror the registered strategies shown in strategy settings");
        multi.UseMock=false;Check(!multi.UseMock&&multi.RunCaption=="실제 계좌 일괄수행","unchecking mock selects only the guarded live route");
        Check(!vm.MultiSessionExists&&feeds.Count==0&&b.Requests.Count==0,"changing mode alone creates no feed or broker orders");
        multi.SeedSymbols([DailyStock]);multi.Actions["실행"].Execute(null);PumpUntil(()=>vm.MultiSessionExists&&actionNotices.Count>0||multi.Message.Contains("확인"));
        Check(vm.MultiSessionExists&&requested.Single().Single().Symbol==DailyStock.Symbol&&b.Requests.Count==0,"selected fake account starts only selected oversold symbol without immediate orders");Check(actionNotices.Count>0,"successful live start publishes a separate popup notice instead of relying on the footer");
        // The fixture starts with one completed warm-up bar.  The default short-crash
        // lookback needs three bars, so close two live minute bars and observe one
        // later trade before expecting the next-event order.
        var liveOffsets=new[]{0,59,60,119,120,121};
        for(int n=0;n<liveOffsets.Length;n++)
        {
            var time=DailyTime(2,9,0).AddSeconds(liveOffsets[n]);b.Now=time;var instrument=new Instrument(DailyStock.Symbol,Exchange.KRX);
            feeds.Single().Events.Writer.TryWrite(new(instrument,EventKind.Trade,time,time,"KIWOOM",n*2+1){TradePrice=80,TradeSize=10,CumulativeVolume=10*(n+1),PreviousClose=100});
            feeds.Single().Events.Writer.TryWrite(new(instrument,EventKind.Quote,time,time,"KIWOOM",n*2+2){Bid=80,Ask=81,BidSize=1000,AskSize=1000});
            Thread.Sleep(300);Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);
        }
        var orderDeadline=DateTime.UtcNow.AddSeconds(5);while(b.Requests.Count==0&&vm.MultiSessionExists&&DateTime.UtcNow<orderDeadline){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);Thread.Sleep(20);}var liveDetail=string.Join(" | ",multi.Rows.Select(r=>$"{r.State}/{r.Signal}/{r.Error}/owned={r.Owned}/pending={r.Pending}"));
        Check(b.Requests.Count==1&&b.Requests[0].Symbol==DailyStock.Symbol,"app routes completed-bar signal to only the selected fake account · "+liveDetail);
        var stopping=vm.StopMulti();PumpUntil(()=>stopping.IsCompleted);stopping.GetAwaiter().GetResult();Check(!vm.MultiSessionExists,"explicit all-stop releases the selected account lease");
        multi.UseMock=true;multi.UseRealtime=true;Check(multi.RunCaption=="실시간 모의매매 시작"&&multi.Actions["실행"].CanExecute(null),"mock plus realtime selects the live-price virtual-account route");
        var noticesBeforePaper=actionNotices.Count;multi.Actions["실행"].Execute(null);PumpUntil(()=>vm.MultiSessionExists&&actionNotices.Count>noticesBeforePaper||multi.Message.Contains("확인"));Check(vm.MultiSessionExists&&feeds.Count==2&&b.Requests.Count==1,"realtime paper starts a market feed without sending a second broker order");Check(actionNotices.Count>noticesBeforePaper,"successful realtime paper start publishes a visible popup notice");
        var paperOffsets=new[]{0,59,60,119,120,121,122};
        for(int n=0;n<paperOffsets.Length&&!db.States("trade-marker:").Any(s=>Serialization.Unpack<TradeMarker>(s.Json).Source==RealtimePaperStrategyBatch.MarkerSource);n++)
        {
            var time=DailyTime(2,9,2).AddSeconds(paperOffsets[n]);b.Now=time;var instrument=new Instrument(DailyStock.Symbol,Exchange.KRX);var price=n<4?80:79-(n-4);
            feeds[1].Events.Writer.TryWrite(new(instrument,EventKind.Trade,time,time,"KIWOOM",200+n*2){TradePrice=price,TradeSize=1000,CumulativeVolume=1000*(n+1),PreviousClose=100});
            feeds[1].Events.Writer.TryWrite(new(instrument,EventKind.Quote,time,time,"KIWOOM",201+n*2){Bid=price,Ask=n<4?81:price,BidSize=1000,AskSize=1000});
            Thread.Sleep(400);Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.Background);
        }
        PumpUntil(()=>db.States("trade-marker:").Any(s=>Serialization.Unpack<TradeMarker>(s.Json).Source==RealtimePaperStrategyBatch.MarkerSource),10);
        var paperMarker=db.States("trade-marker:").Any(s=>Serialization.Unpack<TradeMarker>(s.Json).Source==RealtimePaperStrategyBatch.MarkerSource);
        if(!paperMarker){var detail=string.Join(" | ",multi.Rows.Select(r=>$"{r.State}/{r.Signal}/{r.Error}/owned={r.Owned}/pending={r.Pending}"));Console.WriteLine("DEBUG realtime paper marker missing: "+detail+"; session="+vm.MultiSessionExists);stopping=vm.StopMulti();PumpUntil(()=>stopping.IsCompleted);try{stopping.GetAwaiter().GetResult();}catch(Exception ex){Console.WriteLine("DEBUG stop failed: "+ex);}Check(false,"realtime paper marker missing: "+detail);}
        Check(b.Requests.Count==1&&paperMarker,"realtime paper fill is persisted for charts without any broker submission");
        stopping=vm.StopMulti();PumpUntil(()=>stopping.IsCompleted);stopping.GetAwaiter().GetResult();Check(!vm.MultiSessionExists,"realtime paper all-stop ends its shared feed without liquidating through a broker");
        vm.SelectedStock=DailyStock;vm.ShowTradeMarkers=true;
        Check(vm.TradeMarkers.Any(m=>m.Source==RealtimePaperStrategyBatch.MarkerSource&&m.StockKey==DailyStock.Key),"online chart reload includes the realtime paper fill marker for the selected stock");
        Check(db.State<string[]>("chart-presets")!.Length>0,"legacy preset storage retained without execution");vm.IsOffline=true;PumpUntil(()=>!vm.Busy);Check(multi.UseMock&&multi.CanToggleMock&&multi.CanUseLiveBudget,"offline transition resets mock while keeping the choice and budget editors enabled");
        string? unsupportedNotice=null;multi.NotificationRequested+=message=>unsupportedNotice=message;multi.UseMock=false;Check(!multi.UseMock&&multi.Actions["실행"].CanExecute(null),"unsupported actual mode keeps execution clickable so it can explain why it cannot start");multi.Actions["실행"].Execute(null);PumpUntil(()=>unsupportedNotice!=null);Check(unsupportedNotice!.Contains("키움 국내 실계좌"),"unsupported execution publishes an explicit popup reason instead of silently disabling the button");multi.UseMock=true;multi.UseRealtime=false;
        var samsung=new StockItem("005930","삼성전자",Exchange.KRX,"005930","반도체");var hynix=new StockItem("000660","SK하이닉스",Exchange.KRX,"000660","반도체");
        vm.WatchGroups[0].Items.Add(new(samsung));vm.WatchGroups[1].Items.Add(new(samsung));vm.WatchGroups[1].Items.Add(new(hynix));
        multi.Reload();multi.ReloadWatchGroups(vm.WatchGroups);
        var added=new StockItem("035420","NAVER",Exchange.KRX,"035420","인터넷");multi.SelectedResult=added;multi.Actions["추가"].Execute(null);PumpUntil(()=>multi.Rows.Any(r=>r.Stock.Key==added.Key));Check(multi.Rows.Single(r=>r.Stock.Key==added.Key).Preset==multi.Presets[0],"manual add selects the first registered strategy by default");multi.SelectedRow=multi.Rows.Single(r=>r.Stock.Key==added.Key);multi.Actions["삭제"].Execute(null);PumpUntil(()=>multi.Rows.All(r=>r.Stock.Key!=added.Key));
        Check(vm.CanOpenMulti&&multi.Actions["실행"].CanExecute(null)&&multi.Rows.Count==2&&multi.Groups.Count==2,"offline batch popup seeds two deduplicated watch-group symbols");
        multi.Groups[0].Members[0].Selected=true;Check(multi.Groups[1].Members[0].Selected&&multi.Rows.Single(r=>r.Stock.Key==samsung.Key).Selected,"duplicate member checkbox uses one central selection set");
        multi.Groups[1].Members[0].Selected=false;Check(!multi.Groups[0].Members[0].Selected&&!multi.Rows.Single(r=>r.Stock.Key==samsung.Key).Selected,"unchecking a duplicate clears every view of that stock");
        multi.Groups[1].Selected=true;Check(multi.SelectedStocks().Length==2&&multi.Groups[0].Selected==true,"group selection checks unique stocks and updates overlapping groups");
        multi.Save();Check(db.State<MultiTradeDrafts>("multi-trade-draft") is {GroupIds.Length:>0} draft&&draft.Rows.Length==2,"batch selection and groups persist as one draft");
        multi.PrepareLaunchMode(true);Check(multi.UseMock&&multi.UseRealtime&&!multi.CanToggleMock&&!multi.CanToggleRealtime&&multi.LaunchContextText.Contains("가상 계좌"),"virtual account entry locks the popup to realtime paper trading");multi.PrepareLaunchMode(null);
        string? notice=null;var noticeModel=new MultiTradeViewModel(new FixtureMarketData(),db,()=>Task.CompletedTask,_=>Task.CompletedTask,()=>Task.CompletedTask,(_,_)=>new TradingSettings(),()=>"가상 계좌",()=>true,realtimeMockStart:()=>Task.FromException(new InvalidOperationException("실시간 실행은 분봉 또는 시간봉만 지원합니다.")),realtimeSupported:()=>true,strategyNames:()=>["전략 A","전략 B"]);noticeModel.NotificationRequested+=message=>notice=message;noticeModel.PrepareLaunchMode(true);noticeModel.Actions["실행"].Execute(null);PumpUntil(()=>notice!=null);Check(noticeModel.Presets.SequenceEqual(new[]{"전략 A","전략 B"})&&notice!.Contains("분봉 또는 시간봉"),"dialog actions publish strategy-specific failures for a separate information popup");
        if(selectionOnly){var selectedPopup=new MultiTradeDialog(multi){Owner=window,ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};selectedPopup.Show();selectedPopup.UpdateLayout();var editable=Descendants(selectedPopup).OfType<TextBox>().Where(t=>t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path is "Budget" or "TotalBudget" or "DailyLoss").ToArray();var mock=Descendants(selectedPopup).OfType<CheckBox>().Single(c=>Equals(c.Content,"모의 · 실제 주문 없음"));var realtime=Descendants(selectedPopup).OfType<CheckBox>().Single(c=>Equals(c.Content,"실시간"));Check(editable.Length>=4&&editable.All(t=>t.IsEnabled)&&mock.IsEnabled&&realtime.IsEnabled,"per-symbol budgets and separate mock/realtime choices are editable before execution");Render((FrameworkElement)selectedPopup.Content,Path.Combine(output,"multi-trade-dialog.png"));selectedPopup.Close();Check(multi.SelectedStocks().Length==2,"closing and reopening the popup preserves app-level selection");window.DataContext=old;vm.Dispose();return;}
        vm.Actions["검색"].Execute(null);PumpUntil(()=>!vm.Busy);vm.From=new(2025,1,1);vm.To=new(2025,8,20);vm.RecurringAmount="100,000";vm.RecurringInitial="3,000,000";vm.RecurringFee="0.015";vm.Actions["정기매수실행"].Execute(null);PumpUntil(()=>!vm.Busy);
        var originalWidth=window.Width;var originalHeight=window.Height;var heights=new List<double>();
        foreach(var size in new[]{(1540d,1040d),(1160d,760d)})
        {
            window.Width=size.Item1;window.Height=size.Item2;vm.ActiveTab=0;
            foreach(var tab in new[]{1,2,3}){vm.WorkspaceToolTab=tab;window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);var chart=(CandleChart)window.FindName("PriceChart");Check(chart.ActualHeight>200,"chart stretches at "+size+" tool tab "+tab);Render((FrameworkElement)window.Content,Path.Combine(output,$"tools-{(int)size.Item1}-{tab}.png"));}
            heights.Add(((CandleChart)window.FindName("PriceChart")).ActualHeight);
        }
        Check(heights[0]>heights[1]+150,"increasing window height grows chart instead of blank card space");
        vm.WorkspaceToolTab=3;window.UpdateLayout();var box=Descendants(window).OfType<TextBox>().Single(t=>t.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path=="RecurringFee");box.ApplyTemplate();window.UpdateLayout();
        var display=box.Template.FindName("PercentReadout",box) as TextBlock;Check(display?.Text=="0.02"&&box.Text=="0.015"&&vm.RecurringFee=="0.015","percent readout rounds only presentation and preserves original fee binding");
        box.Focus();Keyboard.Focus(box);window.UpdateLayout();Check(box.Text=="0.015","editing exposes original precise percent value");Keyboard.ClearFocus();
        Check(new MarketQuote("fixture",100,1,5.71m,b.Now,"").ChangeText.Contains("5.71%")&&new MarketQuote("fixture",100,0,0,b.Now,"").ChangeText.Contains("0.00%"),"positive and zero percentage displays have exactly two decimals");
        window.Width=originalWidth;window.Height=originalHeight;
        var popup=new MultiTradeDialog(multi){Owner=window,ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};popup.Show();popup.Close();Check(multi.SelectedStocks().Length==2,"closing the popup preserves app-level selection state");
        window.DataContext=old;vm.Dispose();
    }
}
