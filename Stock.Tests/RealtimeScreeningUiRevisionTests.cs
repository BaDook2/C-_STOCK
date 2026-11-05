using System.IO;
using System.Reflection;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class RealtimeBacktestCapture:IOversoldBacktestService
    {
        private readonly OversoldBacktestService inner=new();
        public OversoldBacktestRequest? Request;
        public Task<OversoldResult> RunAsync(OversoldBacktestRequest request,CancellationToken token){Request=request;return inner.RunAsync(request,token);}
        public void Clear()=>inner.Clear();
    }
    private static void RealtimeScreeningUiRevisionTests(MainWindow window,string output)
    {
        var previous=window.DataContext;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(var online in new[]{false,true})
        {
            using var db=new TradingStore(Path.Combine(Dir,"realtime-selection-"+online+".db"));
            using var fakeBroker=new ChartHistoryBroker();
            var backtests=new RealtimeBacktestCapture();
            using var vm=new WorkspaceViewModel(_=>false,provider:new ParallelScreeningFixture(),store:db,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>fakeBroker,oversoldBacktestService:backtests);
            window.DataContext=vm;vm.ActiveTab=1;vm.RealtimeScreeningMode=true;
            if(online){vm.IsOnline=true;PumpUntil(()=>!vm.Busy);Check(vm.IsOnline,"online realtime selection fixture uses the connected fake broker");}
            var config=DailyConfig();config.MinimumAverageTradingValue=0;config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.UsePivotSupport=config.UseApproximateVolumeZone=false;
            var warmup=Enumerable.Range(0,2).Select(i=>new StrategyBar(DailyBar(i,100) with{Time=DailyTime(i,9,1)},DailyTime(i,9,0),DailyTime(i,9,1),i,4_000_000_000,"TEST_FIXTURE")).ToArray();
            var stocks=new[]{DailyStock,DailyStock with{Symbol="000660",Name="두번째 종목",ProviderCode="000660"}};
            var core=new RealtimeOversoldScreeningService(()=>DailyTime(2,9));
            core.Start(new(stocks.Select(stock=>new LiveBatchItem(stock,"기본",BatchSettings(),20000){Oversold=new(config,new(1,BarUnit.Minute),warmup,DailyFacts(5),ResearchOnly:true,PublicPaper:!online)}).ToArray(),"기본",new(1,BarUnit.Minute)));
            typeof(WorkspaceViewModel).GetField("realtimeScreening",flags)!.SetValue(vm,core);
            var apply=typeof(WorkspaceViewModel).GetMethod("ApplyRealtimeSnapshot",flags)!;
            void Apply(RealtimeScreeningSnapshot value)=>apply.Invoke(vm,[value,new Dictionary<string,LiveBatchRowStatus>(),Array.Empty<RealtimeScreeningDisplayRow>()]);
            Apply(core.Snapshot());window.UpdateLayout();
            var grid=(DataGrid)window.FindName("RealtimeScreeningGrid");var selected=vm.RealtimeScreeningRows.Single(r=>r.Code==DailyStock.Symbol);grid.SelectedItem=selected;
            int resets=0;vm.RealtimeScreeningRows.CollectionChanged+=(_,e)=>{if(e.Action==NotifyCollectionChangedAction.Reset)resets++;};
            var before=core.Snapshot();
            for(int i=0;i<20;i++)Apply(before with{Rows=before.Rows.Reverse().Select(r=>r with{Price=100+i,ChangePercent=i}).ToArray()});
            window.UpdateLayout();
            Check(ReferenceEquals(grid.SelectedItem,selected)&&ReferenceEquals(vm.SelectedRealtimeScreeningRow,selected)&&selected.Price.Contains("119")&&resets==0,$"{(online?"online":"offline")} realtime price/rank updates retain the selected row identity without collection resets");
            var fill=DailyTime(1,9,0).AddSeconds(10);
            db.State("trade-marker:realtime-test-buy",new TradeMarker("buy",DailyStock.Key,fill,true,2,100,"TEST_FIXTURE",Reason:"Entry1"));
            db.State("trade-marker:realtime-test-sell",new TradeMarker("sell",DailyStock.Key,fill.AddSeconds(20),false,1,110,"TEST_FIXTURE",Reason:"TP1",RealizedPnl:10));
            var load=vm.LoadRealtimeScreeningChart(DailyStock.Symbol);PumpUntil(()=>load.IsCompleted);var chartData=load.GetAwaiter().GetResult();
            Check(backtests.Request?.BarSpecification==new BarSpecification(1,BarUnit.Minute)&&Serialization.Pack(backtests.Request.Configuration)==Serialization.Pack(core.ChartData(DailyStock.Key).Configuration)&&chartData.Result!=null,"realtime chart uses the injected common backtest service with the running strategy snapshot and exact selected bars");
            Check(chartData.Series.Single().Period=="1분"&&chartData.Series[0].Bars.Length==warmup.Length&&chartData.Markers.Length==2&&chartData.Markers.Any(m=>m.SideLabel.Contains("익절"))&&chartData.Markers.All(m=>CandleChart.TradeBarIndex(chartData.Series[0].Bars,m,"1분")==1),"realtime popup reuses completed selected bars and maps confirmed BUY/SELL to the correct minute candle with take-profit captions");
            var container=(DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(selected);grid.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=Control.MouseDoubleClickEvent,Source=container});
            PumpUntil(()=>System.Windows.Application.Current.Windows.OfType<ScreeningChartDialog>().Any(),10);
            var dialog=System.Windows.Application.Current.Windows.OfType<ScreeningChartDialog>().Single();
            Check(dialog.Snapshot.Stock.Key==DailyStock.Key&&dialog.Snapshot.Markers.Length==2&&vm.ActiveTab==1,"double-clicking a realtime row opens the shared chart popup with BUY/SELL without changing tabs");
            Render(dialog.ReviewChart,Path.Combine(output,$"realtime-{(online?"online":"offline")}-chart.png"));dialog.Close();
            var next=vm.Presets.First(p=>p!=vm.SelectedOversoldPreset);vm.SelectedOversoldPreset=next;
            PumpUntil(()=>!vm.RealtimeScreeningStrategyChanging);
            Check(vm.RealtimeScreeningRows.Count==0&&grid.SelectedItem==null&&resets==1&&core.Snapshot().StrategyName==next,"running strategy change clears rows exactly once and replaces the research configuration");
            Apply(before);Check(vm.RealtimeScreeningRows.Count==0,"queued snapshots from the previous strategy cannot repopulate the reset table");
            Apply(core.Snapshot());window.UpdateLayout();
            var stop=(Button)window.FindName("RealtimeScreenStopButton");
            Check(vm.RealtimeScreeningRows.Count==2&&stop.IsEnabled&&((SolidColorBrush)stop.Background).Color==Color.FromRgb(255,240,240),"new strategy rows repopulate and the running stop button uses pale red");
            var box=Descendants(stop).OfType<Border>().FirstOrDefault(b=>b.Name=="Box");
            Check(box?.Background is SolidColorBrush rendered&&rendered.Color==Color.FromRgb(255,240,240),"running stop button template renders its pale red background instead of the disabled gray brush");
            vm.ScreenWatchlist=true;window.UpdateLayout();
            var groups=(ScrollViewer)window.FindName("ScreenWatchGroupOptions");
            Check(groups.Visibility==Visibility.Visible&&Descendants(window).OfType<ComboBox>().Single(c=>ReferenceEquals(c.ItemsSource,vm.ScreeningSizes)).Visibility==Visibility.Collapsed,"watchlist group controls replace the hidden market-top size controls");
            core.Stop();
        }
        using(var db=new TradingStore(Path.Combine(Dir,"offline-minute-chart.db")))
        using(var vm=new WorkspaceViewModel(_=>false,provider:new ParallelScreeningFixture(),store:db))
        {
            window.DataContext=vm;vm.Actions["검색"].Execute(null);PumpUntil(()=>!vm.Busy);
            Check(vm.IsOffline&&vm.CanUseMinutes&&!vm.CanUseIntraday,"offline KRX enables public minute/hour charts while raw ticks still require a broker");
            vm.ChartBarValue="5";PumpUntil(()=>!vm.Busy);Check(vm.ChartPeriod=="5분"&&vm.ChartBars.Count>0&&vm.ChartBarValues.Length==59,"offline chart loads public minute OHLCV and offers 1 through 59 minutes");
            vm.ChartBarUnit="시";PumpUntil(()=>!vm.Busy);vm.ChartBarValue="1";PumpUntil(()=>!vm.Busy);
            Check(vm.ChartPeriod=="1시"&&vm.ChartBars.Count>0&&vm.ChartBarValues.Length==12&&vm.TickStatus.Contains("네이버 KRX 공개"),"offline chart uses public data for hour bars and offers 1 through 12 hours");
        }
        Check(TradeOutcome.Label(false,"TP1",10).Contains("익절")&&TradeOutcome.Label(false,"EmergencyStop",-10).Contains("손절")&&TradeOutcome.Label(false,"TrailingStop",10).Contains("익절")&&TradeOutcome.Label(false,"TrailingStop",-10).Contains("손절"),"shared chart captions distinguish profit/loss and preserve trailing exit intent");
        Check(!TradeOutcome.Label(false,"").Contains("손절")&&!TradeOutcome.Label(false,"").Contains("익절"),"legacy fills with unknown cost and reason never invent an exit outcome");
        var historical=new[]{new PriceBar(DailyTime(1,9),100,102,99,101,50,true)};
        var observation=new CurrentMarketSnapshot(DailyStock,new("quote",110,10,10,historical[0].Time.AddSeconds(40),"OPEN"),ReceivedAt:default,Open:100,High:110,Low:99,Volume:100000,TradingValue:null,Source:"FIXTURE",PollInterval:TimeSpan.FromSeconds(1));
        var merged=ChartPeriods.MergeCurrentObservation(historical,observation,"1분");
        Check(merged.Single() is{Close:110,High:110,Volume:50,IsComplete:false}&&historical[0].Close==101&&historical[0].IsComplete,"public current quote changes display-only candle price without assigning daily volume to a minute bar or mutating history");
        var hours=Enumerable.Range(0,180).Select(i=>new PriceBar(DailyTime(1,9).AddMinutes(i),100,101,99,100,1)).ToArray();
        Check(ChartPeriods.FromTrades(hours,"2시").Length==2&&ChartPeriods.FromTrades(hours,"7분").Length==26,"arbitrary minute and hour chart aggregation uses the KRX session boundary");
        window.DataContext=previous;
    }
}
