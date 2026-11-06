using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class ScreeningFillBroker:IAccountBroker,IMinuteHistoryBroker,IRangedMinuteIntervalHistoryBroker
    {
        private readonly ChartHistoryBroker inner=new();
        public int Reads,OrderCalls;
        public string Id=>"screening-fill-fixture";public string Name=>"체결 차트 검증";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;
        public string Account=>inner.Account;public string MaskedAccount=>inner.MaskedAccount;public bool SupportsMarketOrders=>true;
        public Task ConnectAsync()=>Task.CompletedTask;
        public Task<decimal> CurrentPrice(string symbol)=>inner.CurrentPrice(symbol);
        public Task<Balance> GetBalanceAsync()=>inner.GetBalanceAsync();
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly day)=>inner.GetOrdersAsync(day);
        public Task<OrderReceipt> SubmitAsync(OrderRequest order){OrderCalls++;throw new InvalidOperationException("No orders in chart test");}
        public Task<OrderReceipt> CancelAsync(string number,string symbol)=>throw new InvalidOperationException("No cancel in chart test");
        public bool SupportsMinuteInterval(int minutes)=>minutes is 1 or 5 or 60;
        public Task<PriceBar[]> MinuteHistory(string symbol,CancellationToken token)=>MinuteHistory(symbol,new(2025,1,3),new(2025,1,3),1,token);
        public Task<PriceBar[]> MinuteHistory(string symbol,DateOnly first,DateOnly last,int minutes,CancellationToken token)
        {
            Reads++;decimal[] prices=[100,100,80,82,95,95];
            return Task.FromResult(prices.Select((price,i)=>new PriceBar(DailyTime(2,9,0).AddMinutes(i*minutes),price,i==2?100:price,price,price,100)).ToArray());
        }
        public void Dispose()=>inner.Dispose();
    }

    private static void ScreeningTradeChartTests(string output)
    {
        using var store=new TradingStore(Path.Combine(Dir,"screening-trade-chart-"+Guid.NewGuid().ToString("N")+".db"));
        using var broker=new ScreeningFillBroker();var service=new RealtimeBacktestCapture();
        using var workspace=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:store,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>broker,oversoldBacktestService:service);
        workspace.IsOnline=true;PumpUntil(()=>!workspace.Busy);
        var config=DailyConfig();config.UsePivotSupport=config.UseApproximateVolumeZone=false;config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.MinimumAverageTradingValue=0;config.UseTakeProfit1=true;
        var day=new DateOnly(2025,1,3);var facts=DailyFacts(3);
        foreach(var specification in new[]{new BarSpecification(5,BarUnit.Minute),new BarSpecification(1,BarUnit.Hour)})
        {
            var run=OversoldScreeningRun.Start([DailyStock],config,specification,DailyTime(3),day,day,"체결 차트 검증") with{FactsJson=Serialization.Pack(facts)};
            var task=workspace.LoadScreeningChart(DailyStock,run);PumpUntil(()=>task.IsCompleted);var snapshot=task.GetAwaiter().GetResult();
            Check(service.Request?.BarSpecification==specification&&service.Request.StrategyBars?.Length==6&&snapshot.Result is {Decisions.Count:>0},specification+" screening popup invokes the shared intraday backtest with the frozen configuration and bars");
            Check(snapshot.Markers.Any(m=>m.Buy)&&snapshot.Markers.Any(m=>!m.Buy)&&snapshot.Trades.Length==snapshot.Markers.Length,specification+" chart contains calculated BUY and SELL fills without pre-existing history");
            Check(snapshot.Markers.All(m=>CandleChart.TradeBarIndex(snapshot.Initial.Bars,m,snapshot.Initial.Period)>=0)&&snapshot.Trades.All(t=>t.Execution!=null&&t.Time==t.Execution.ExecutionTime),specification+" execution times map to the exact displayed intraday candle");
            var dialog=new ScreeningChartDialog(snapshot){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();dialog.UpdateLayout();
            try
            {
                var chart=dialog.ReviewChart;var grid=Descendants(dialog).OfType<DataGrid>().Single();
                Draw(chart);
                Check(chart.ScreeningInteractionEnabled&&chart.VisibleBarCount==6&&!chart.ScreeningOverviewLocked,specification+" short chart is interactive without a no-op right click");
                Check(chart.RenderedTradeMarkers.Count==snapshot.Markers.Length,specification+" BUY/SELL labels are actually rendered in the screening chart");
                Check(chart.RenderedTradeMarkers.All(hit=>hit.Bounds.Left>=0&&hit.Bounds.Right<=chart.ActualWidth-88),specification+" trade labels remain fully visible at the right edge");
                foreach(var hit in chart.RenderedTradeMarkers.ToArray())
                {
                    var point=new Point(hit.Bounds.Right-2,hit.Bounds.Top+hit.Bounds.Height/2);
                    chart.SelectMarkerAtPoint(point);dialog.UpdateLayout();
                    Check(grid.SelectedItem is ScreeningChartTrade selected&&selected.Time==hit.Marker.Time&&selected.Buy==hit.Marker.Buy&&selected.Price==hit.Marker.Price&&ReferenceEquals(selected,dialog.ViewModel.SelectedTrade),specification+" clicking the trade label selects its exact grid row and detail");
                    Check(chart.ViewOffset==0,specification+" selecting a marker keeps the clicked chart viewport stable");
                }
                Render((FrameworkElement)dialog.Content,Path.Combine(output,"screening-fills-"+specification.Unit+".png"));
                chart.ZoomAt(chart.ActualWidth/2,120);Check(chart.VisibleBarCount==5,specification+" wheel zoom works immediately on a short chart");
                chart.ToggleScreeningReviewView(chart.ActualWidth/2);Check(chart.VisibleBarCount==6&&chart.ScreeningInteractionEnabled,specification+" right-click reset of a short chart does not re-lock it");
            }
            finally{dialog.Close();}
            var reads=broker.Reads;var again=workspace.LoadScreeningChart(DailyStock,run);PumpUntil(()=>again.IsCompleted);again.GetAwaiter().GetResult();Check(broker.Reads==reads,specification+" reopening reuses the warm-up-inclusive bar cache");
        }
        Check(broker.OrderCalls==0,"screening model fills never send broker orders");

        var origin=DailyTime(2,9,0);var bars=Enumerable.Range(0,240).Select(i=>new PriceBar(origin.AddMinutes(i),100,106,94,100,100)).ToArray();
        var firstMarker=new TradeMarker("group-first",DailyStock.Key,bars[20].Time,true,2,100,"모형",TradeNumber:1,Reason:"Entry1");
        var secondMarker=firstMarker with{Id="group-second",Time=bars[24].Time,TradeNumber=2};
        var groupChart=new CandleChart{Bars=bars,Period="1분",TradeMarkers=new[]{firstMarker,secondMarker},DisplayBarCount=240};groupChart.Measure(new Size(900,550));groupChart.Arrange(new Rect(0,0,900,550));Draw(groupChart);
        var badge=groupChart.RenderedTradeMarkers.Single();var badgePoint=new Point(badge.Bounds.Left+badge.Bounds.Width/2,badge.Bounds.Top+badge.Bounds.Height/2);
        Check(groupChart.HoverBarIndex(badgePoint.X)!=20&&groupChart.HoverBarIndex(badgePoint.X)!=24&&groupChart.MarkerAtPoint(badgePoint) is {} represented&&(represented==firstMarker||represented==secondMarker),"aggregated trade badge is clickable even when its centre is not a traded candle");
        var sameTime=firstMarker with{Id="other-source",Source="확인된 가상체결",Quantity=7};
        var review=ReviewFixture() with{Markers=[firstMarker,sameTime],Trades=[new(firstMarker.Time,true,2,100,"Entry1",firstMarker.Source,TradeNumber:1),new(sameTime.Time,true,7,100,"Entry1",sameTime.Source)]};
        using var selection=new ScreeningChartViewModel(review);Check(selection.SelectMarker(sameTime)&&selection.SelectedTrade?.Quantity==7&&selection.SelectedTrade.Source==sameTime.Source,"same-time fills select by quantity price and source, not nearest-time heuristics");
        Check(!selection.SelectMarker(firstMarker with{Time=firstMarker.Time.AddSeconds(1)})&&selection.SelectedTrade?.Quantity==7,"a missing exact fill cannot select an unrelated nearby trade");
        selection.SetSnapshot(ReviewFixture());Check(selection.SelectedTrade==null,"switching symbols clears the previously selected fill");
        foreach(var count in new[]{1,5,12,25,26})
        {
            var chart=new CandleChart{Bars=bars.Take(count).ToArray(),Period="1분"};chart.EnableScreeningReviewMode();
            Check(chart.ScreeningInteractionEnabled==(count<=25),"overview locks only when right-click can actually narrow "+count+" bars");
        }
        static void Draw(CandleChart chart){Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);chart.UpdateLayout();new RenderTargetBitmap((int)chart.ActualWidth,(int)chart.ActualHeight,96,96,PixelFormats.Pbgra32).Render(chart);}
    }

    private static void ScreeningTradeChartDemo(string output)
    {
        using var store=new TradingStore(Path.Combine(Dir,"native-trade-chart.db"));using var broker=new ScreeningFillBroker();
        using var workspace=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:store,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>broker);
        workspace.IsOnline=true;PumpUntil(()=>!workspace.Busy);
        var config=DailyConfig();config.UsePivotSupport=config.UseApproximateVolumeZone=false;config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.MinimumAverageTradingValue=0;config.UseTakeProfit1=true;
        var run=OversoldScreeningRun.Start([DailyStock],config,new(1,BarUnit.Hour),DailyTime(3),new(2025,1,3),new(2025,1,3),"UI 검증 · 실제 주문 없음") with{FactsJson=Serialization.Pack(DailyFacts(3))};
        var loading=workspace.LoadScreeningChart(DailyStock,run);PumpUntil(()=>loading.IsCompleted);var snapshot=loading.GetAwaiter().GetResult();
        var vm=new ScreeningChartViewModel(snapshot,(_,minute,_)=>Task.FromResult(minute
            ?new ScreeningChartSeries("1분","1분",Enumerable.Range(0,360).Select(i=>new PriceBar(DailyTime(2,9,0).AddMinutes(i),82,100,80,95,100)).ToArray(),"검증 원본")
            :new ScreeningChartSeries("일봉","일봉",[DailyBar(2,95,open:100,low:80)],"검증 원본")),()=>true);
        var dialog=new ScreeningChartDialog(snapshot,viewModel:vm);var telemetry=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
        telemetry.Tick+=(_,_)=>File.WriteAllText(Path.Combine(output,"demo-state.json"),System.Text.Json.JsonSerializer.Serialize(new{vm.DisplayPeriod,Locked=dialog.ReviewChart.ScreeningOverviewLocked,Visible=dialog.ReviewChart.VisibleBarCount,Offset=dialog.ReviewChart.ViewOffset,SelectedTime=vm.SelectedTrade?.TimeText,SelectedSide=vm.SelectedTrade?.Side,Markers=dialog.ReviewChart.RenderedTradeMarkers.Count,Orders=broker.OrderCalls}));
        dialog.Closed+=(_,_)=>telemetry.Stop();telemetry.Start();dialog.ShowDialog();
    }
}
