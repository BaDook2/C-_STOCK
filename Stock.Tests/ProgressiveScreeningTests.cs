using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class ProgressiveScreeningFixture:IPublicMarketData
    {
        public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Waiting,Reads;
        public StockItem[] Stocks {get;}=[DailyStock,..Enumerable.Range(2,4).Select(i=>DailyStock with{Symbol=i.ToString("D6"),Name="대기 종목 "+i})];
        public Task<IReadOnlyList<StockItem>> Search(string name,string region,CancellationToken token)=>Task.FromResult<IReadOnlyList<StockItem>>(Stocks);
        public Task<IReadOnlyList<StockItem>> Universe(string region,int count,CancellationToken token)=>Search("",region,token);
        public Task<StockItem> Describe(StockItem stock,CancellationToken token)=>Task.FromResult(stock);
        public async Task<BarDataset> History(StockItem stock,CancellationToken token)
        {
            Interlocked.Increment(ref Reads);
            if(stock.Key!=DailyStock.Key){Interlocked.Increment(ref Waiting);await Release.Task.WaitAsync(token);}
            return new(stock,[DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,82),DailyBar(4,90)],"PROGRESSIVE_FIXTURE",DailyTime(5));
        }
    }

    private static void ProgressiveScreeningTests(MainWindow window,string output)
    {
        var old=window.DataContext;using var store=new TradingStore(Path.Combine(Dir,"progressive-"+Guid.NewGuid().ToString("N")+".db"));var provider=new ProgressiveScreeningFixture();
        var cfg=DailyConfig();cfg.MovingAveragePeriods="1";cfg.MinimumSupportCount=1;cfg.UsePivotSupport=cfg.UseApproximateVolumeZone=false;
        store.State("oversold-config-v1",cfg);store.State("oversold-facts-v1",DailyFacts(5));
        using var vm=new WorkspaceViewModel(_=>false,store:store,provider:provider);window.DataContext=vm;vm.ActiveTab=1;vm.ScreenFrom=new(2025,1,3);vm.ScreenTo=new(2025,1,5);
        try
        {
            vm.Actions["스크리닝"].Execute(null);PumpUntil(()=>provider.Waiting==4&&vm.LatestScreening?.Results.Length==1&&vm.Matches.Count==1);window.UpdateLayout();
            Check(vm.Busy&&vm.LatestScreening!.Status=="진행 중"&&provider.Reads==5,"a completed symbol is published while the next queued symbol starts without waiting for slow peers");
            vm.SelectedMatch=vm.Matches[0];window.UpdateLayout();var chartButton=Descendants(window).OfType<Button>().Single(b=>ReferenceEquals(b.Command,vm.Actions["결과열기"])&&b.IsVisible);
            var resultsButton=Descendants(window).OfType<Button>().Single(b=>ReferenceEquals(b.Command,vm.Actions["스크리닝결과"])&&b.IsVisible);
            Check(chartButton.IsEnabled&&resultsButton.IsEnabled&&!vm.Actions["스크리닝"].CanExecute(null),"in-progress result and chart buttons stay enabled while duplicate screening stays blocked");
            var market=Descendants(window).OfType<ComboBox>().Single(b=>ReferenceEquals(b.ItemsSource,vm.ScreeningMarkets));Check(!market.IsEnabled,"running screening retains its disabled market configuration");
            ((IInvokeProvider)new ButtonAutomationPeer(resultsButton)).Invoke();PumpUntil(()=>Application.Current.Windows.OfType<ScreeningResultDialog>().Any());var results=Application.Current.Windows.OfType<ScreeningResultDialog>().Single();results.UpdateLayout();
            var tabs=Descendants(results).OfType<TabControl>().Single();tabs.SelectedIndex=1;results.UpdateLayout();var symbols=Descendants(results).OfType<DataGrid>().Single(g=>g.Columns.Any(c=>Equals(c.Header,"코드")));symbols.SelectedIndex=0;
            Check(symbols.Items.Count==1&&Descendants(results).OfType<TextBlock>().Any(t=>t.Text.Contains("검사 1/5")),"result dialog shows processed counts before the screening has finished");
            var open=Descendants(results).OfType<Button>().Single(b=>Equals(b.Content,"백테스트 보기"));open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));PumpUntil(()=>Application.Current.Windows.OfType<ScreeningChartDialog>().Any());
            var chart=Application.Current.Windows.OfType<ScreeningChartDialog>().Single();Check(vm.Busy&&chart.Snapshot.Run.Results.Length==1&&chart.Snapshot.Result?.Decisions.Count>0&&vm.ActiveTab==1,"processed-symbol chart runs independently without stopping or redirecting the active screen");chart.Close();
            ((IInvokeProvider)new ButtonAutomationPeer(chartButton)).Invoke();PumpUntil(()=>Application.Current.Windows.OfType<ScreeningChartDialog>().Any());var mainChart=Application.Current.Windows.OfType<ScreeningChartDialog>().Single();Check(vm.Busy&&mainChart.Snapshot.Stock.Key==DailyStock.Key,"main screening chart button bypasses the foreground-operation gate safely");mainChart.Close();
            provider.Release.TrySetResult();PumpUntil(()=>!vm.Busy&&vm.LatestScreening!.Results.Length==5);PumpUntil(()=>symbols.Items.Count==5);
            Check((symbols.SelectedItem as ScreeningObservation)?.Stock.Key==DailyStock.Key,"live result refresh retains the selected stock and its details");
            Check(vm.LatestScreening!.Status=="완료"&&provider.Reads==5&&vm.ResearchHistory.Any(r=>r.Id==vm.LatestScreening.Id),"reviewing partial results does not duplicate requests and the complete run remains recorded");results.Close();
        }
        finally
        {
            provider.Release.TrySetResult();if(vm.Busy){vm.Actions["취소"].Execute(null);PumpUntil(()=>!vm.Busy);}
            foreach(var dialog in Application.Current.Windows.Cast<Window>().Where(w=>w is ScreeningResultDialog or ScreeningChartDialog).ToArray())dialog.Close();window.DataContext=old;
        }
    }
}
