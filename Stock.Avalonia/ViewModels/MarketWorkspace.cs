using System.Collections.ObjectModel;
using Avalonia.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Desktop;

public sealed record RangeChoice(string Label,int Months){public override string ToString()=>Label;}
public sealed partial class MainViewModel:IDisposable
{
    private string query="삼성전자",region="전체",period="일봉";
    private StockItem? selectedStock;
    private BarDataset? dataset;
    private PriceBar[] displayDaily=[];
    private MarketQuote? quote;
    private CurrentMarketSnapshot? currentSnapshot;
    private bool busy;
    private bool disposed;
    private CancellationTokenSource? operation;
    private long generation;
    private SelectedQuotePump? quotePump;
    private RangeChoice selectedRange=new("3개월",3);
    internal IAccountBroker? Broker=>Accounts?.Active;
    public event Action<string>? NotificationRequested;
    public ObservableCollection<StockItem> SearchResults {get;}=[];
    public string[] Regions {get;}=["전체","국내","해외"];
    public string[] Periods {get;}=["일봉","주봉","월봉"];
    public RangeChoice[] Ranges {get;}=[new("1일",-1),new("1개월",1),new("3개월",3),new("6개월",6),new("1년",12),new("3년",36),new("5년",60),new("10년",120)];
    public string Query {get=>query;set=>Set(ref query,value);}
    public string Region {get=>region;set{if(Set(ref region,value)&&value is "국내" or "해외")Query=value=="국내"?"삼성전자":"애플";}}
    public bool Busy {get=>busy;private set{if(Set(ref busy,value)){Changed(nameof(Editable));SearchCommand.Refresh();}}}
    public bool Editable=>!Busy;
    public bool Online=>Broker!=null;
    public string ModeText=>Online?"온라인 · "+Broker!.Name+" "+Broker.MaskedAccount:"오프라인 · 공개 시세";
    public StockItem? SelectedStock {get=>selectedStock;set{if(value==null||selectedStock?.Key==value.Key)return;_ = Run(async token=>{await LoadStock(value,token);});}}
    public string StockTitle=>dataset?.Stock.Name??"어떤 주식을 살펴볼까요?";
    public string StockCode=>dataset?.Stock.Symbol??"";
    public string PriceText=>quote?.PriceText??(dataset?.Bars.LastOrDefault() is {} bar?DisplayNumber.Price(bar.Close):"—");
    public string ChangeText=>quote?.ChangeText??(dataset?.Bars.TakeLast(2).ToArray() is {Length:2} bars?$"{DisplayNumber.SignedPrice(bars[1].Close-bars[0].Close)} ({(bars[1].Close/bars[0].Close-1)*100:+0.00;-0.00;0.00}%)":"");
    public string QuoteTime=>quote?.KoreaTime??dataset?.Description??"";
    public string PriceColor=>quote?.Change>=0?"#F04452":"#3182F6";
    public string ChartPeriod {get=>period;set{if(string.IsNullOrWhiteSpace(value))return;if(Set(ref period,value)){if(ChartPeriods.Minutes(value)>0){selectedRange=value.EndsWith("분")?Ranges[0]:Ranges[1];displayIntraday=[];RefreshChart();_ = LoadChartTimeframe();}else{chartRequest++;if(selectedRange.Months<0)selectedRange=Ranges[1];RefreshChart();}Changed(nameof(SelectedRange));}}}
    public RangeChoice SelectedRange
    {
        get=>Ranges.FirstOrDefault(r=>r.Months==selectedRange.Months)??Ranges[2];
        set{if(value==null)return;if(value.Months<0&&!ChartPeriod.EndsWith("분")){Notify("1일 기간은 분봉에서만 선택할 수 있습니다.");Changed();return;}selectedRange=value;Changed();if(ChartPeriods.Minutes(ChartPeriod)>0)_=LoadChartTimeframe();else RefreshChart();}
    }
    public PriceBar[] ChartBars {get;private set;}=[];
    public PriceBar[] ChartIndicatorBars {get;private set;}=[];
    public TradeMarker[] ChartMarkers {get;private set;}=[];
    public ChartSupportZone[] ChartZones {get;private set;}=[];
    public AsyncCommand SearchCommand {get;private set;}=null!;
    public AsyncCommand EmergencyCommand {get;private set;}=null!;
    private void InitializeMarket()
    {
        selectedRange=Ranges[2];SearchCommand=new(()=>Run(Search));EmergencyCommand=new(()=>{operation?.Cancel();Accounts.Emergency();Realtime.Emergency();Research.Stop();Recurring.Stop();Screening.Stop();DayTrading.EmergencyStop();generation++;quotePump?.Stop();Status="긴급중지 · 조회/가격 갱신 취소 · 보유분 임의청산 없음";Busy=false;return Task.CompletedTask;});
    }
    private void Notify(string message){Status=message;NotificationRequested?.Invoke(message);}
    private async Task Run(Func<CancellationToken,Task> action)
    {
        operation?.Cancel();var current=new CancellationTokenSource();operation=current;Busy=true;
        try{await action(current.Token).WaitAsync(services.QueryTimeout,current.Token);}catch(OperationCanceledException) when(current.IsCancellationRequested){if(operation==current)Status="작업을 취소했습니다.";}
        catch(TimeoutException){current.Cancel();if(operation==current)Notify(OperationError.ResponseTimeout);}
        catch(Exception ex) when(ex is not OutOfMemoryException){if(operation==current)Notify(OperationError.Describe(ex));}
        finally{if(operation==current){operation=null;Busy=false;}current.Dispose();}
    }
    private async Task Search(CancellationToken token)
    {
        ResetBacktestDisplay();Status="종목 검색 중…";var results=await services.MarketData.Search(Query,Region,token);token.ThrowIfCancellationRequested();
        SearchResults.Clear();foreach(var stock in results)SearchResults.Add(stock);
        if(results.Count==0){Status="검색 결과가 없습니다.";return;}await LoadStock(results[0],token);
    }
    private async Task LoadStock(StockItem stock,CancellationToken token)
    {
        var version=++generation;quotePump?.Dispose();quotePump=null;quote=null;currentSnapshot=null;displayIntraday=[];ResetBacktestDisplay();Status=stock.Name+" 차트 조회 중…";
        var loaded=services.Store.State<BarDataset>("bars:"+stock.Key);
        if(loaded==null||DateTimeOffset.UtcNow-loaded.RetrievedAt>TimeSpan.FromMinutes(10)){loaded=await services.MarketData.History(stock,token);token.ThrowIfCancellationRequested();services.Store.State("bars:"+stock.Key,loaded);}
        if(version!=generation)return;foreach(var bar in loaded.Bars)bar.Validate();if(loaded.Bars.Length==0)throw new InvalidOperationException("가격 자료가 없습니다.");
        dataset=loaded;displayDaily=loaded.Bars;selectedStock=stock;Research.ModeChanged();if(!ChartCustomAvailable&&ChartPeriods.Intraday(ChartPeriod)){period="일봉";selectedRange=Ranges[2];Changed(nameof(ChartPeriod));Changed(nameof(SelectedRange));}RefreshPaperMarkers();Accounts.RefreshOrderCommands();Changed(nameof(SelectedStock));Changed(nameof(WatchStar));Changed(nameof(StockTitle));Changed(nameof(StockCode));Changed(nameof(ChartCustomAvailable));RefreshChart();NotifyQuote();Status=loaded.Description;if(ChartPeriods.Minutes(ChartPeriod)>0)_=LoadChartTimeframe();
        var current=Online&&Broker is {} broker?new BrokerCurrentMarketData(broker,_=>dataset.CompletedBars.Reverse().Skip(1).FirstOrDefault()?.Close):services.MarketData as ICurrentMarketData;
        if(current==null)return;quotePump=new(current,fast:!Online);
        quotePump.Select(stock,snapshot=>Dispatcher.UIThread.Post(()=>
        {
            if(disposed||version!=generation||selectedStock?.Key!=snapshot.Stock.Key)return;
            quote=snapshot.Quote;currentSnapshot=snapshot;displayDaily=DailyQuoteMerger.Merge(dataset.Bars,snapshot,DateTimeOffset.UtcNow);NotifyQuote();RefreshChart(false);
        }),error=>Dispatcher.UIThread.Post(()=>{if(version==generation)Status=error;}));
    }
    private void NotifyQuote(){foreach(var name in new[]{nameof(PriceText),nameof(ChangeText),nameof(QuoteTime),nameof(PriceColor)})Changed(name);}
    private void RefreshChart(bool reset=true)
    {
        if(displayDaily.Length==0)return;
        var source=ChartPeriods.Minutes(ChartPeriod)>0?ChartPeriods.MergeCurrentObservation(displayIntraday,currentSnapshot,ChartPeriod):ChartPeriods.Aggregate(displayDaily,ChartPeriod);if(source.Length==0){ChartBars=[];ChartIndicatorBars=[];ChartDisplayCount=0;Changed(nameof(ChartBars));Changed(nameof(ChartIndicatorBars));Changed(nameof(ChartDisplayCount));return;}
        var last=source[^1].Time;var cutoff=DailyQuoteMerger.Date(last,dataset!.Stock.Exchange).AddMonths(-Math.Max(1,SelectedRange.Months));
        // Keep the loaded history available for panning; a period chooses the initial
        // viewport, not which older bars survive in the chart's backing array.
        ChartBars=visibleBacktest!=null?source.Where(b=>DailyDates.Of(b)<=visibleBacktest.Result.To).ToArray():source;
        ChartDisplayCount=ChartBars.Count(b=>visibleBacktest!=null?DailyDates.Of(b)>=visibleBacktest.Result.From:SelectedRange.Months<0?DailyQuoteMerger.Date(b.Time,dataset.Stock.Exchange)==DailyQuoteMerger.Date(last,dataset.Stock.Exchange):DailyQuoteMerger.Date(b.Time,dataset.Stock.Exchange)>=cutoff);
        Changed(nameof(ChartDisplayCount));
        ChartIndicatorBars=source;Changed(nameof(ChartIndicatorBars));Changed(nameof(ChartBars));if(reset){ChartRevision++;Changed(nameof(ChartRevision));}
    }
    public int ChartRevision {get;private set;}
    public int ChartDisplayCount {get;private set;}
    private void ResetBacktestDisplay(){visibleBacktest=null;showBacktest=false;ChartMarkers=[];ChartZones=[];Changed(nameof(ShowBacktest));Changed(nameof(ChartMarkers));Changed(nameof(ChartZones));}
    public void Dispose(){if(disposed)return;disposed=true;AsyncCommand.Failed-=CommandFailed;operation?.Cancel();Research.Stop();Recurring.Stop();Screening.Stop();Realtime.DisposeAsync().AsTask().GetAwaiter().GetResult();ActualTrading.DisposeAsync().AsTask().GetAwaiter().GetResult();dayTimer?.Stop();DayTrading.DisposeAsync().AsTask().GetAwaiter().GetResult();quotePump?.Dispose();Accounts.Dispose();}
}
public sealed record ChartSupportZone(DateTimeOffset From,DateTimeOffset To,decimal Lower,decimal Upper,bool UsedForEntry,string Label);
