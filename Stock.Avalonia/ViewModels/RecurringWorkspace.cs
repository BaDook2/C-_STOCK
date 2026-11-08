using System.Globalization;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;
public sealed record FrequencyChoice(PurchaseFrequency Value,string Label);
public sealed record TimingChoice(PurchaseTiming Value,string Label);
public sealed class RecurringResultViewModel(RecurringPurchaseResult result,PriceBar[] bars)
{
    public RecurringPurchaseResult Result {get;}=result;
    public string Title=>$"{Result.Stock.Name} · 정기매수 · {Result.Config.From:yyyy.MM.dd} ~ {Result.Config.To:yyyy.MM.dd}";
    public string Summary=>$"납입 {DisplayNumber.Price(Result.Contributions)} · 평가 {DisplayNumber.Price(Result.Value)} · 손익 {DisplayNumber.SignedPrice(Result.Value-Result.Contributions)} · 수익률 {Result.ReturnPercent:N2}% · MDD {Result.MaxDrawdown:N2}%";
    public PriceBar[] Bars {get;}=bars;
    public TradeMarker[] Markers {get;}=result.Purchases.Select((f,index)=>new TradeMarker(result.Id+":"+index,result.Stock.Key,f.Time,true,f.Quantity,f.Price,"정기매수 일봉 가정",TradeNumber:index+1,Reason:f.Reason)).ToArray();
    public IndexPoint[] Equity {get;}=result.Equity.Select(e=>new IndexPoint(e.Time,e.Equity)).ToArray();
    public BarFill[] Purchases=>Result.Purchases;
}
public sealed class RecurringWorkspace(AppServices services,MarketHistoryService history,Func<StockItem?> stock):Bindable
{
    private CancellationTokenSource? cancel;
    public DateTime? From {get;set;}=DateTime.Today.AddYears(-1);
    public DateTime? To {get;set;}=DateTime.Today;
    public string Amount {get;set;}="100000";
    public string Initial {get;set;}="3000000";
    public string Commission {get;set;}="0.015";
    public FrequencyChoice[] Frequencies {get;}=[new(PurchaseFrequency.Daily,"매일"),new(PurchaseFrequency.Weekly,"매주"),new(PurchaseFrequency.Monthly,"매월")];
    public TimingChoice[] Timings {get;}=[new(PurchaseTiming.Open,"시가 가정"),new(PurchaseTiming.Close,"종가 가정")];
    public PurchaseFrequency Frequency {get;set;}=PurchaseFrequency.Monthly;
    public PurchaseTiming Timing {get;set;}=PurchaseTiming.Open;
    private string status="정수 주식으로 매수하며 남는 현금은 이월합니다.";
    public string Status {get=>status;private set=>Set(ref status,value);}
    public AsyncCommand Run {get;private set;}=null!;
    public event Action<string>? Notice;
    public event Action<RecurringResultViewModel>? Completed;
    public void Initialize()=>Run=new(Execute);
    public void Stop()=>cancel?.Cancel();
    private async Task Execute()
    {
        cancel?.Dispose();cancel=new();var token=cancel.Token;
        try
        {
            var selected=stock()??throw new InvalidOperationException("먼저 종목을 선택하세요.");if(From==null||To==null||To.Value.Date>DateTime.Today)throw new InvalidOperationException("기간과 미래 날짜를 확인하세요.");
            decimal Number(string text)=>decimal.Parse(text,NumberStyles.Number,CultureInfo.InvariantCulture);
            var config=new RecurringPurchaseConfig{From=From.Value,To=To.Value,Amount=Number(Amount),InitialAmount=Number(Initial),CommissionPercent=Number(Commission),Frequency=Frequency,Timing=Timing};config.Validate();Status="정기매수 계산 중…";
            var data=await history.Get(selected,new(),new(),null,DateOnly.FromDateTime(config.From),DateOnly.FromDateTime(config.To),token);var result=await Task.Run(()=>RecurringPurchaseBacktest.Run(data.Dataset,config,token),token);token.ThrowIfCancellationRequested();
            var bars=data.Dataset.CompletedBars.Where(b=>DailyDates.Of(b)>=DateOnly.FromDateTime(config.From)&&DailyDates.Of(b)<=DateOnly.FromDateTime(config.To)).ToArray();services.Store.State("desktop-recurring:"+result.Id,result);services.Store.State("desktop-recurring-bars:"+result.Id,bars);var view=new RecurringResultViewModel(result,bars);Status=view.Summary;Completed?.Invoke(view);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){Status="정기매수 계산 취소";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=OperationError.Describe(ex);Notice?.Invoke(Status);}
    }
}
public sealed partial class MainViewModel
{
    public RecurringWorkspace Recurring {get;private set;}=null!;
    public event Action<RecurringResultViewModel>? RecurringPopupRequested;
    private void InitializeRecurring(){Recurring=new(services,HistoryService,()=>SelectedStock);Recurring.Initialize();Recurring.Notice+=Notify;Recurring.Completed+=view=>{RecordHistory(new(view.Result.Id,DateTimeOffset.UtcNow,DesktopRunKind.Recurring,"정기매수",view.Result.Stock.Name,view.Result.Config.From.ToString("yyyy.MM.dd")+" ~ "+view.Result.Config.To.ToString("yyyy.MM.dd"),view.Result.Purchases.Length.ToString(),"desktop-recurring:"+view.Result.Id));RecurringPopupRequested?.Invoke(view);};}
}
