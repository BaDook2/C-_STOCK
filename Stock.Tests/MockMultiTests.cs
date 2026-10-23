using System.IO;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Trading;
internal static partial class Program
{
    private static async Task MockMultiTests()
    {
        using var db=new TradingStore(Path.Combine(Dir,"mock-batch-direct.db"));
        using var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db);
        var a=new StockItem("005930","삼성전자",Exchange.KRX,"005930","반도체");
        var b=new StockItem("000660","SK하이닉스",Exchange.KRX,"000660","반도체");
        vm.WatchGroups[0].Items.Add(new(a));vm.WatchGroups[1].Items.Add(new(a));vm.WatchGroups[1].Items.Add(new(b));
        var multi=vm.MultiTrading;multi.ReloadWatchGroups(vm.WatchGroups);multi.Groups[0].Selected=true;multi.Groups[1].Selected=true;
        Check(multi.SelectedStocks().Length==2&&multi.Rows.Count==2,"mock batch deduplicates overlapping watch groups before execution");
        vm.From=new(2025,1,1);vm.To=new(2025,8,20);await vm.RunSelectedMockBatch();
        var id=db.State<string>("mock-batch-latest");var record=id==null?null:db.State<MockBatchRecord>(id);
        Check(record is {Results.Count:2,Status:"완료"}&&record.Stocks.Select(s=>s.Key).Distinct().Count()==2,"mock batch persists two independently evaluated results");
        Check(vm.History.Count==2&&multi.Running&&multi.IsLocked&&vm.MultiSessionExists,"mock batch completes with durable history and locks editing");
        Check(vm.IsOffline&&vm.MultiStatus.Contains("실제 주문 없음"),"mock batch never enters a live account or reports a real fill");
        await vm.StopMulti();Check(!multi.Running&&multi.CanEdit&&!vm.MultiSessionExists&&db.State<MockBatchRecord>(id!)!=null,"explicit stop unlocks the popup without deleting mock results");
    }
}
