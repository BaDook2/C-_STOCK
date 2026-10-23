using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private sealed class BatchBroker:IAccountBroker,IExecutionHistoryBroker
    {
        public string Id=>"kiwoom";public string Name=>"TEST ONLY";public bool IsLive=>true;public Exchange Exchange=>Exchange.KRX;public string Account {get;set;}=Guid.NewGuid().ToString("N");public string MaskedAccount=>"TEST-****";public bool SupportsMarketOrders=>true;
        public decimal Cash=1000000;public decimal? Available=1000000;public List<Holding> Holdings=[];public List<BrokerOrder> Orders=[];public List<BrokerExecution> Executions=[];public List<OrderRequest> Requests=[];public List<string> Cancels=[];public bool Unknown,Reject;public Func<Task>? BeforeBalance;public Func<Task>? BeforeSubmit;public DateTimeOffset Now=DateTimeOffset.Parse("2026-09-30T10:00:00+09:00");
        public Task ConnectAsync()=>Task.CompletedTask;public void Dispose(){}public Task<decimal> CurrentPrice(string s)=>Task.FromResult(1000m);
        public async Task<Balance> GetBalanceAsync(){if(BeforeBalance!=null)await BeforeBalance();return new(Holdings.ToArray(),Cash,Cash+Holdings.Sum(h=>h.Quantity*h.CurrentPrice),"KRW",Available);}
        public Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerOrder>>(Orders.ToArray());
        public Task<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(DateOnly date)=>Task.FromResult<IReadOnlyList<BrokerExecution>>(Executions.ToArray());
        public async Task<OrderReceipt> SubmitAsync(OrderRequest o){Requests.Add(o);if(BeforeSubmit!=null)await BeforeSubmit();if(Reject)throw new BrokerRejectedException("test rejected");if(Unknown)throw new UnknownOrderException();var id=Requests.Count.ToString();Orders.Add(new("20260930",id,o.Symbol,o.Symbol,o.Buy?"매수":"매도",o.Quantity,o.Price,0,o.Quantity,false,false));return new(id);}
        public Task<OrderReceipt> CancelAsync(string id,string symbol){Cancels.Add(id);return Task.FromResult(new OrderReceipt("C"+id));}
        public void CancelConfirmed(string id){var i=Orders.FindIndex(o=>o.Number==id);Orders[i]=Orders[i] with{Cancelled=true,Remaining=0};}
        public void Fill(string id,int qty,decimal price){var i=Orders.FindIndex(o=>o.Number==id);var o=Orders[i];var buy=o.Side=="매수";Orders[i]=o with{Filled=o.Filled+qty,Remaining=o.Remaining-qty};Executions.Add(new("E"+Executions.Count,id,o.Symbol,buy,qty,price,Now));var h=Holdings.FirstOrDefault(h=>h.Symbol==o.Symbol);Holdings.RemoveAll(h=>h.Symbol==o.Symbol);var n=(h?.Quantity??0)+(buy?qty:-qty);if(n>0)Holdings.Add(new(o.Symbol,o.Symbol,n,buy?((h?.Quantity??0)*(h?.AveragePrice??0)+qty*price)/n:h!.AveragePrice,price,0));Cash+=qty*price*(buy?-1:1);Available=Cash;}
    }
    private sealed class BatchSignal:ITradingStrategy
    {public string Name=>"fixture";public StrategySignal Evaluate(MarketContext c)=>new(Name,c.Position?.Quantity>0?false:true,c.Price,"test signal");}
    private static TradingSettings BatchSettings()
    {
        var cfg=new StrategyConfiguration{Enabled=true,StrategyType=StrategyType.Box};var a=cfg.Resolve();a.Common.Investment=10000;a.Common.TradingStart="09:00";a.Common.TradingEnd="15:20";a.Risk.DailyLossLimit=100000;a.Risk.MaxPositionPercent=100;a.Risk.ReentryCooldown=1;a.Execution.OrderTimeoutMilliseconds=60000;a.Execution.Commission=0;
        var baseline=new TradingSettings();baseline.Execution.StaleSeconds=10;baseline.Risk.MaxOrderCash=100000;baseline.Risk.MaxSymbolCash=200000;baseline.Risk.MaxTradeLoss=100000;baseline.Risk.MaxDailyLoss=100000;baseline.Filters.ForEach(f=>f.Enabled=false);return StrategyPresetMapper.ToEngine(a,baseline);
    }
    private static async Task LiveBatchTests()
    {
        var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930","반도체");var second=new StockItem("000660","SK하이닉스",Exchange.KRX,"000660","반도체");
        LiveBatchPlan Plan(params StockItem[] stocks)=>new(stocks.Select(s=>new LiveBatchItem(s,"테스트 등록 전략",BatchSettings(),20000)).ToArray(),100000,10000);
        (BatchBroker Broker,TradingStore Store,RemoteTradingSession Session,LiveStrategyBatch Batch) Setup(){var b=new BatchBroker();var db=new TradingStore(Path.Combine(Dir,"batch-"+b.Account+".db"));var s=BatchSettings();var remote=new RemoteTradingSession(b,s,db,Path.Combine(Dir,"batch-orders"));return(b,db,remote,new(remote,db,()=>b.Now,_=>new BatchSignal()));}
        void Feed(BatchBroker b,LiveStrategyBatch batch,StockItem s,decimal price=1000){var ins=new Instrument(s.Symbol,s.Exchange);batch.OnMarket(new(ins,EventKind.Trade,b.Now,b.Now,"KIWOOM",b.Now.UtcTicks){TradePrice=price,TradeSize=10,PreviousClose=price});batch.OnMarket(new(ins,EventKind.Quote,b.Now,b.Now,"KIWOOM",b.Now.UtcTicks+1){Bid=price,Ask=price+1,BidSize=1000,AskSize=1000});}
        var x=Setup();using(x.Store)
        {
            var plan=Plan(stock,second);await x.Batch.Start(plan);plan.Items[0].Settings.Advanced!.Common.Investment=999999;Feed(x.Broker,x.Batch,stock);Feed(x.Broker,x.Batch,second);await x.Batch.Pulse();
            Check(x.Broker.Requests.Count==2&&x.Broker.Requests.All(o=>o.Quantity==10),"batch selected symbols use independent immutable config snapshots · requests="+string.Join(",",x.Broker.Requests.Select(o=>o.Symbol+":"+o.Quantity))+" · rows="+string.Join(",",x.Batch.Snapshot().Rows.Select(r=>r.Name+":"+r.Error+":"+r.Status)));Check(x.Batch.Snapshot().Rows.All(r=>r.Owned==0&&r.Pending==10),"broker acceptance and price ticks never create fictitious fills");
            await ThrowsAsync<InvalidOperationException>(()=>x.Session.Submit(new("035420",1,1000,true)),"manual orders cannot bypass active account lease");
            x.Broker.Fill("1",4,1001);x.Broker.Now=x.Broker.Now.AddSeconds(3);Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();Check(x.Batch.Snapshot().Rows[0].Owned==4&&x.Batch.Snapshot().Rows[0].Pending==6,"individual executions apply partial fill quantities and reserve remainder");
            await x.Batch.Stop(stock.Key);Check(x.Broker.Cancels.SequenceEqual(new[]{"1"})&&x.Batch.Snapshot().Running,"individual stop cancels only owned symbol and keeps other row running");Check(x.Batch.Snapshot().Rows[0].Pending==6,"cancel receipt does not release unconfirmed remainder");
            x.Broker.CancelConfirmed("1");x.Broker.Now=x.Broker.Now.AddSeconds(3);Feed(x.Broker,x.Batch,second);await x.Batch.Pulse();Check(x.Batch.Snapshot().Rows[0].Pending==0&&x.Batch.Snapshot().Rows[0].Owned==4,"confirmed cancel preserves actual partial position");
            await x.Batch.Stop();Check(x.Broker.Cancels.SequenceEqual(new[]{"1","2"})&&x.Broker.Requests.Count==2&&!x.Session.AutomationClaimed,"all stop cancels own pending without automatic liquidation");
            await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(Plan(second)),"unreconciled durable run blocks restart");
        }
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();x.Broker.Fill("1",10,1000);x.Broker.Now=x.Broker.Now.AddSeconds(3);Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();Check(x.Broker.Requests.Count==2&&!x.Broker.Requests[1].Buy&&x.Broker.Requests[1].Quantity==10,"exit orders sell only confirmed strategy-owned shares");x.Broker.Fill("2",10,1010);x.Broker.Now=x.Broker.Now.AddSeconds(3);Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();Check(x.Batch.Snapshot().Rows[0].Profit==100&&x.Batch.Snapshot().Rows[0].Owned==0,"execution prices determine realized profit and flat state");await x.Batch.Pulse();Check(x.Batch.Snapshot().Rows[0].Profit==100,"duplicate broker execution pages are idempotent");await x.Batch.Stop();}
        x=Setup();using(x.Store){x.Broker.Holdings.Add(new(stock.Symbol,stock.Name,1,1000,1000,0));await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(Plan(stock)),"existing manual holdings cannot be adopted");Check(x.Broker.Requests.Count==0&&!x.Session.AutomationClaimed,"failed start releases account without order");}
        x=Setup();using(x.Store){x.Broker.Available=null;await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(Plan(stock)),"missing real cash buying power blocks start");}
        x=Setup();using(x.Store){await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(Plan(stock,stock)),"duplicate selected symbols blocked");await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(Plan()),"empty batch blocked");x.Broker.Now=x.Broker.Now.AddHours(6);await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(Plan(stock)),"after hours automatic start blocked");}
        x=Setup();using(x.Store){x.Broker.Unknown=true;await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();await x.Batch.Pulse();Check(x.Broker.Requests.Count==1&&!x.Batch.Snapshot().Running&&x.Session.Unknown,"unknown submission stops all and never retries");await x.Batch.Stop();var journal=x.Store.State<LiveBatchJournal>("live-batch:"+x.Session.Scope)!;Check(journal.Orders.Single().Pending,"unknown intent persists across restart");}
        x=Setup();using(x.Store){x.Broker.Reject=true;await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();Check(x.Broker.Requests.Count==1&&x.Batch.Snapshot().Rows[0].Pending==0,"explicit rejection releases reservation without fill");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);x.Broker.BeforeBalance=()=>{x.Batch.RequestStop();return Task.CompletedTask;};await x.Batch.Pulse();Check(x.Broker.Requests.Count==0,"stop during account refresh prevents transmission");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);x.Broker.BeforeSubmit=()=>{x.Batch.RequestStop();return Task.CompletedTask;};await x.Batch.Pulse();await x.Batch.Stop();Check(x.Broker.Requests.Count==1&&x.Broker.Cancels.Count==1,"stop after transmission tracks receipt and cancels without resend");}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);x.Broker.Now=x.Broker.Now.AddSeconds(15);await x.Batch.Pulse();Check(x.Broker.Requests.Count==0&&!x.Batch.Snapshot().Running,"stale feed blocks orders");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);x.Broker.BeforeBalance=()=>{x.Broker.Available=1;return Task.CompletedTask;};await x.Batch.Pulse();Check(x.Broker.Requests.Count==0,"cash change just before submission blocks order");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock,second));Feed(x.Broker,x.Batch,stock);Feed(x.Broker,x.Batch,second);x.Broker.BeforeBalance=()=>{x.Broker.Available=15000;return Task.CompletedTask;};await x.Batch.Pulse();Check(x.Broker.Requests.Count==1,"pending first-symbol reservation prevents second-symbol cash overcommit");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();x.Broker.Fill("1",10,1000);x.Broker.Holdings[0]=x.Broker.Holdings[0] with{Quantity=15};x.Broker.Now=x.Broker.Now.AddSeconds(3);Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();Check(x.Broker.Requests.Count==1&&x.Batch.Snapshot().Rows[0].Owned==10,"external holding changes block sale instead of selling manual shares");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);x.Batch.FeedFault("fixture disconnect");await x.Batch.Pulse();Check(x.Broker.Requests.Count==0&&!x.Batch.Snapshot().Running,"feed disconnect immediately prevents new orders");await x.Batch.Stop();}
        x=Setup();using(x.Store){await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);x.Broker.BeforeBalance=()=>{x.Broker.Account="CHANGED-FIXTURE";return Task.CompletedTask;};await x.Batch.Pulse();Check(x.Broker.Requests.Count==0,"account identity change while awaiting balance prevents transmission");await x.Batch.Stop();}
        x=Setup();using(x.Store){x.Store.State("live-batch-daily:"+x.Session.Scope+":20260930",new Dictionary<string,Dictionary<string,decimal>>{{"previous-explicit-run",new(){{stock.Key,-10001}}}});await x.Batch.Start(Plan(stock));Feed(x.Broker,x.Batch,stock);await x.Batch.Pulse();Check(x.Broker.Requests.Count==0,"same-day realized loss budget survives explicit run restart");await x.Batch.Stop();}
        var oversoldConfig=DailyConfig();oversoldConfig.UseEmergencyStop=true;oversoldConfig.MinimumAverageTradingValue=0;oversoldConfig.MovingAveragePeriods="1";oversoldConfig.MinimumSupportCount=1;oversoldConfig.UsePivotSupport=oversoldConfig.UseApproximateVolumeZone=false;
        var oversoldFacts=DailyFacts(5);
        var warmup=Enumerable.Range(0,2).Select(i=>new StrategyBar(DailyBar(i,100),DailyTime(i,9,0),DailyTime(i),i,4_000_000_000,"TEST_FIXTURE")).ToArray();
        var profile=new OversoldLiveConfiguration(oversoldConfig,new(1,BarUnit.Tick),warmup,oversoldFacts);
        x=Setup();using(x.Store)
        {
            x.Broker.Now=DailyTime(2,9,0);var settings=BatchSettings();
            var profilePlan=new LiveBatchPlan([new LiveBatchItem(DailyStock,"국내 과매도·낙주 반등",settings,20000){Oversold=profile}],20000,10000);
            await x.Batch.Start(profilePlan);
            void Tick(int n)
            {
                x.Broker.Now=DailyTime(2,9,0).AddSeconds(n);var instrument=new Instrument(DailyStock.Symbol,Exchange.KRX);
                x.Batch.OnMarket(new(instrument,EventKind.Trade,x.Broker.Now,x.Broker.Now,"KIWOOM",n){TradePrice=80,TradeSize=10,CumulativeVolume=10*(n+1),PreviousClose=100});
                x.Batch.OnMarket(new(instrument,EventKind.Quote,x.Broker.Now,x.Broker.Now,"KIWOOM",n+100){Bid=80,Ask=81,BidSize=1000,AskSize=1000});
            }
            Tick(0);Tick(1);await x.Batch.Pulse();Check(x.Broker.Requests.Count==0,"oversold signal cannot trade on its completing tick");
            Tick(2);await x.Batch.Pulse();Check(x.Broker.Requests.Count==1&&x.Broker.Requests[0].Buy&&x.Batch.Snapshot().Rows.Single().Owned==0,"oversold route submits only the selected fake account and does not invent fills");
            x.Broker.Fill("1",x.Broker.Requests[0].Quantity,80);x.Broker.Now=x.Broker.Now.AddSeconds(3);await x.Batch.Pulse();
            Check(x.Batch.Snapshot().Rows.Single().Owned==x.Broker.Requests[0].Quantity,"oversold ownership follows identified broker execution only");
            await x.Batch.Stop();
        }
        x=Setup();using(x.Store){x.Broker.Now=DailyTime(2,9,0);var unprotected=oversoldConfig.Clone();unprotected.UseEmergencyStop=false;var plan=new LiveBatchPlan([new LiveBatchItem(DailyStock,"낙주",BatchSettings(),20000){Oversold=profile with{Configuration=unprotected}}],20000,10000);await ThrowsAsync<InvalidOperationException>(()=>x.Batch.Start(plan),"oversold live start requires configured emergency stop");Check(x.Broker.Requests.Count==0,"rejected oversold profile creates no broker order");}
    }
}
