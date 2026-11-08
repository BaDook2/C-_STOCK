using System.Collections.ObjectModel;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed record ResearchTradeRow(int Number,OversoldExecution Fill)
{
    public string Time=>Fill.ExecutionTime.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss");
    public string Side=>Fill.Buy?"매수":"매도";public string Price=>DisplayNumber.Price(Fill.Price);public int Quantity=>Fill.Quantity;
    public string Profit=>Fill.Buy?"—":DisplayNumber.SignedPrice(Fill.RealizedPnl);
    public string Reason=>TradeOutcome.Label(Fill.Buy,Fill.Reason,Fill.Buy?null:Fill.RealizedPnl);
}
public sealed record ResearchArchive(string Id,DateTimeOffset At,string Name,OversoldResult Result,PriceBar[] Bars);
public sealed class ResearchResultViewModel:Bindable
{
    public OversoldResult Result {get;}
    public PriceBar[] Bars {get;}
    public TradeMarker[] Markers {get;}
    public ChartSupportZone[] Zones {get;}
    public ResearchTradeRow[] Trades {get;}
    public string Period=>Result.BarSpecification?.ToString()??"일봉";
    public string Title=>Result.Stock.Name+" · "+Result.From+" ~ "+Result.To+" · "+Period;
    public string Summary=>$"완결 거래 {Result.Performance.Trades:N0}회 · 승률 {Result.Performance.WinRate?.ToString("N2")??"—"}% · 손익 {DisplayNumber.SignedPrice(Result.Performance.FinalEquity-Result.Configuration.InitialCapital)} · MDD {Result.Performance.MaxDrawdown:N2}%";
    private ResearchTradeRow? selected;
    private string detail;
    public string Detail {get=>detail;private set=>Set(ref detail,value);}
    public ResearchTradeRow? Selected {get=>selected;set{if(Set(ref selected,value)&&value!=null)Detail=$"거래 #{value.Number} · {value.Side} · {value.Time}\n가격 {value.Price} · 수량 {value.Quantity:N0} · 실현손익 {value.Profit}\n사유 {value.Reason}\n"+Stock.Core.Research.DayTrading.IntradayPresentation.PrettyJson(Serialization.Pack(value.Fill.Snapshot));}}
    public ResearchResultViewModel(OversoldResult result,PriceBar[] bars)
    {
        Result=result;Bars=bars;detail=string.Join("\n",result.Notices)+"\n성과지표\n"+Stock.Core.Research.DayTrading.IntradayPresentation.PrettyJson(Serialization.Pack(result.Performance));
        Trades=result.Trades.SelectMany((trade,index)=>trade.Executions.Select(fill=>new ResearchTradeRow(index+1,fill))).ToArray();
        Markers=Trades.Select((t,index)=>new TradeMarker(result.Id+":"+index,result.Stock.Key,t.Fill.ExecutionTime,t.Fill.Buy,t.Quantity,t.Fill.Price,"백테스트",TradeNumber:t.Number,Reason:t.Fill.Reason,RealizedPnl:t.Fill.Buy?null:t.Fill.RealizedPnl)).ToArray();
        var last=bars.LastOrDefault()?.Time??DateTimeOffset.UtcNow;
        Zones=result.Trades.SelectMany(t=>t.Executions.Where(e=>e.Buy&&e.Snapshot!=null).SelectMany(e=>e.Snapshot!.Clusters.Select(z=>new ChartSupportZone(e.SignalTime,t.LastExecutionDate??last,z.Lower,z.Upper,z.UsedForEntry,string.Join(", ",z.Supports.Select(s=>s.Id)))))).ToArray();
    }
    public void SelectMarker(TradeMarker marker)
    {Selected=Trades.FirstOrDefault(t=>t.Fill.ExecutionTime==marker.Time&&t.Fill.Buy==marker.Buy&&t.Fill.Price==marker.Price);}
}
public sealed class ResearchWorkspace:Bindable
{
    private readonly AppServices services;
    private readonly StrategyEditor strategies;
    private readonly MarketHistoryService history;
    private readonly Func<StockItem?> stock;
    private readonly Func<bool> online;
    private readonly Func<bool> customAvailable;
    private CancellationTokenSource? cancellation;
    private bool busy,daily=true;
    private string value="5",unit="분",status="종목과 전략을 선택한 뒤 백테스트하세요.";
    private ResearchResultViewModel? result;
    public DateTime? From {get;set;}=DateTime.Today.AddMonths(-1);
    public DateTime? To {get;set;}=DateTime.Today;
    public string FactsJson {get;set;}=Serialization.Pack(new HistoricalFacts());
    public bool Busy {get=>busy;private set{if(Set(ref busy,value))Run.Refresh();}}
    public string Status {get=>status;private set=>Set(ref status,value);}
    public bool CanCustom=>online()&&customAvailable();
    public bool Daily {get=>!CanCustom||daily;set{daily=value;Changed();Changed(nameof(Custom));}}
    public bool Custom {get=>CanCustom&&!Daily;set{if(value&&!CanCustom){Notice?.Invoke("해당 종목의 분봉을 지원하는 온라인 계좌가 필요합니다.");return;}Daily=!value;}}
    public string[] Units {get;}=["분","시간"];
    public string[] Values=>Enumerable.Range(1,unit=="분"?59:12).Select(n=>n.ToString()).ToArray();
    public string Unit {get=>unit;set{if(Set(ref unit,value)){Value="1";Changed(nameof(Values));}}}
    public string Value {get=>value;set=>Set(ref this.value,value);}
    public bool HasResult=>result!=null;
    public ResearchResultViewModel? Result {get=>result;private set{if(Set(ref result,value))Changed(nameof(HasResult));}}
    public AsyncCommand Run {get;}
    public AsyncCommand Cancel {get;}
    public event Action<string>? Notice;
    public event Action<ResearchArchive>? Completed;
    public ResearchWorkspace(AppServices services,StrategyEditor strategies,MarketHistoryService history,Func<StockItem?> stock,Func<bool> online,Func<bool>? customAvailable=null)
    {
        this.services=services;this.strategies=strategies;this.history=history;this.stock=stock;this.online=online;this.customAvailable=customAvailable??online;
        Run=new(Execute,()=>!Busy);Cancel=new(()=>{cancellation?.Cancel();return Task.CompletedTask;});
    }
    public void Stop()=>cancellation?.Cancel();
    public void ModeChanged(){if(!CanCustom)daily=true;Changed(nameof(Daily));Changed(nameof(Custom));Changed(nameof(CanCustom));}
    private async Task Execute()
    {
        cancellation?.Dispose();cancellation=new();var token=cancellation.Token;Busy=true;
        try
        {
            var item=stock()??throw new InvalidOperationException("먼저 종목을 검색하세요.");if(From==null||To==null||From>To||To.Value.Date>DateTime.Today)throw new InvalidOperationException("기간의 순서와 미래 날짜를 확인하세요.");
            var strategyName=strategies.Selected;var configuration=strategies.Current();var facts=Serialization.Unpack<HistoricalFacts>(FactsJson);facts.Validate();var first=DateOnly.FromDateTime(From.Value);var last=DateOnly.FromDateTime(To.Value);
            BarSpecification? specification=Daily?null:new(int.Parse(Value),Unit=="분"?BarUnit.Minute:BarUnit.Hour);
            Status="과거 자료를 준비하고 있습니다…";var data=await history.Get(item,configuration,facts,specification,first,last,token);
            var calculated=await services.Backtests.RunAsync(new(data.Dataset,configuration,first,last,data.Facts,specification,data.Bars),token);token.ThrowIfCancellationRequested();
            var prices=(data.Bars?.Select(b=>b.Price with{Time=b.OpenTime})??data.Dataset.CompletedBars).Where(b=>DailyDates.Of(b)>=first&&DailyDates.Of(b)<=last).ToArray();
            Result=new(calculated,prices);Status=Result.Summary;if(calculated.Trades.Count==0)Status+=" · "+(calculated.Decisions.LastOrDefault()?.Reason??"선택 기간에 평가 가능한 완성봉이 없습니다.");var archived=new ResearchArchive("desktop-run:"+Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,strategyName,calculated,prices);services.Store.State(archived.Id,archived);Completed?.Invoke(archived);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){Status="백테스트를 취소했습니다.";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=OperationError.Describe(ex);Notice?.Invoke(Status);}
        finally{Busy=false;}
    }
}
