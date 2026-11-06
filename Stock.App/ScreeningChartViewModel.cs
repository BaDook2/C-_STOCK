using Stock.Core.Research;

namespace Stock.App;

public sealed record ScreeningChartQuote(string StockKey,decimal Price,decimal? ChangePercent,DateTimeOffset? Time,string State,bool Running);

/// <summary>Display periods and live quotes are independent of the immutable screening/backtest snapshot.</summary>
public sealed class ScreeningChartViewModel:Observable,IDisposable
{
    private readonly Func<ScreeningChartSnapshot,bool,CancellationToken,Task<ScreeningChartSeries>>? loadBase;
    private readonly Func<bool>? minuteAvailable;
    private readonly Func<string,ScreeningChartQuote?>? quoteReader;
    private readonly Action<string>? alert;
    private readonly Func<DateTimeOffset> clock;
    private readonly Dictionary<string,ScreeningChartSeries> cache=[];
    private CancellationTokenSource? loading;
    private long generation;
    private bool disposed,updating,busy;
    private string value="1",unit="분",period="일봉",message="";
    private ScreeningChartQuote? quote;
    public ScreeningChartSnapshot Snapshot {get;private set;}=null!;
    public ScreeningChartSeries DisplaySeries {get;private set;}=null!;
    public Task PendingLoad {get;private set;}=Task.CompletedTask;
    public string AnalysisPeriod=>Snapshot.Run.BarSpecification?.ToString()??"일봉";
    public string PeriodDescription=>"스크리닝 기준 · "+AnalysisPeriod;
    public string DisplayPeriod=>period;
    public string[] BarValues=>TimeBarSelection.Values(unit);
    public TimeBarUnitOption[] BarUnits=>TimeBarSelection.Options;
    public bool CanUseMinutes=>(minuteAvailable?.Invoke()??false)||cache.ContainsKey("1분");
    public bool Busy=>busy;
    public string Message=>message;
    public string BarValue {get=>value;set{if(updating||value==null)return;this.value=TimeBarSelection.NormalizeValue(value,unit);Changed();RequestPeriod(this.value+unit);}}
    public string BarUnit {get=>unit;set{if(updating||value==null)return;unit=TimeBarSelection.NormalizeUnit(value);this.value=TimeBarSelection.NormalizeValue(this.value,unit);updating=true;Changed();Changed(nameof(BarValues));Changed(nameof(BarValue));updating=false;RequestPeriod(this.value+unit);}}
    public bool DailySelected {get=>period=="일봉";set{if(value)RequestPeriod("일봉");}}
    public bool CustomSelected {get=>ChartPeriods.Minutes(period)>0;set{if(value)RequestPeriod(this.value+unit);}}
    public bool WeeklySelected {get=>period=="주봉";set{if(value)RequestPeriod("주봉");}}
    public bool MonthlySelected {get=>period=="월봉";set{if(value)RequestPeriod("월봉");}}
    public bool IsRealtime=>Snapshot.Run.Status=="실시간 감시 스냅샷";
    public string PriceText=>quote==null?"시세 대기":DisplayNumber.Price(quote.Price)+" 원";
    public string ChangeText=>quote?.ChangePercent is {} change?$"전일 대비 {change:+0.00;-0.00;0.00}%":"전일 대비 —";
    public string PriceColor=>quote?.ChangePercent is >0?"#F04452":quote?.ChangePercent is <0?"#3182F6":"#6B7684";
    public string QuoteStatus=>quote==null?"실시간 시세 수신 대기":(!quote.Running?"감시 중지 · 마지막 시세":quote.Time is {} at&&(clock()-at).Duration()>TimeSpan.FromSeconds(20)?"시세 지연 · 마지막 수신가":quote.State)+" · "+(quote.Time?.ToOffset(TimeSpan.FromHours(9)).ToString("HH:mm:ss")??"시각 미제공");

    public ScreeningChartViewModel(ScreeningChartSnapshot snapshot,
        Func<ScreeningChartSnapshot,bool,CancellationToken,Task<ScreeningChartSeries>>? loadBase=null,
        Func<bool>? minuteAvailable=null,Func<string,ScreeningChartQuote?>? quoteReader=null,
        Action<string>? alert=null,Func<DateTimeOffset>? clock=null)
    {this.loadBase=loadBase;this.minuteAvailable=minuteAvailable;this.quoteReader=quoteReader;this.alert=alert;this.clock=clock??(()=>DateTimeOffset.UtcNow);SetSnapshot(snapshot);}

    public void SetSnapshot(ScreeningChartSnapshot snapshot)
    {
        CancelLoad();Snapshot=snapshot;cache.Clear();foreach(var series in snapshot.Series)cache[series.Period]=series;
        DisplaySeries=snapshot.Series.FirstOrDefault(s=>s.Period==AnalysisPeriod)??snapshot.Initial;
        period=DisplaySeries.Period;quote=null;message="";busy=false;SyncControls();Changed(null);RefreshQuote();
    }

    public void RequestPeriod(string selected)
    {
        if(disposed||updating||selected==period&&!busy)return;
        if(ChartPeriods.Minutes(selected)==0&&selected is not("일봉" or "주봉" or "월봉"))return;
        CancelLoad();var mine=generation;loading=new();busy=true;message=selected+" 자료를 불러오는 중…";Changed(nameof(Busy));Changed(nameof(Message));
        PendingLoad=Load(selected,mine,loading.Token);
    }

    private async Task Load(string selected,long mine,CancellationToken token)
    {
        try
        {
            // Unit and numeric selector changes in the same gesture produce only one request.
            await Task.Delay(100,token);
            var captured=Snapshot;
            var series=cache.GetValueOrDefault(selected);
            if(series==null)
            {
                var minute=ChartPeriods.Minutes(selected)>0;var key=minute?"1분":"일봉";
                var basis=cache.GetValueOrDefault(key);
                if(basis==null)
                {
                    if(loadBase==null||minute&&!CanUseMinutes)throw new InvalidOperationException("선택한 봉의 원본 자료를 제공하는 API가 연결되지 않았습니다.");
                    basis=await loadBase(captured,minute,token);
                    token.ThrowIfCancellationRequested();if(mine!=generation||disposed)return;cache[key]=basis;
                }
                var input=basis;
                series=await Task.Run(()=>
                {
                    token.ThrowIfCancellationRequested();
                    var rows=minute?ChartPeriods.FromTrades(input.Bars,selected):ChartPeriods.Aggregate(input.Bars,selected);
                    if(minute)rows=rows.Select(b=>b with{Time=DateTimeOffset.FromUnixTimeSeconds(ChartPeriods.MinuteBucket(b,ChartPeriods.Minutes(selected))).ToOffset(TimeSpan.FromHours(9))}).ToArray();
                    if(rows.Length==0)throw new InvalidOperationException("선택 기간에 표시할 "+selected+" 자료가 없습니다.");
                    return new ScreeningChartSeries(selected,selected,rows,input.Source);
                },token);
            }
            token.ThrowIfCancellationRequested();if(mine!=generation||disposed)return;
            // Keep one daily and one minute basis; bound the derived display-series cache.
            if(cache.Count>=12)foreach(var key in cache.Keys.Where(k=>k is not("일봉" or "1분")).ToArray())cache.Remove(key);
            cache[selected]=series;DisplaySeries=series;period=selected;message=series.Bars.Length.ToString("N0")+"봉 · "+series.Source;
            SyncControls();Changed(nameof(DisplaySeries));Changed(nameof(CanUseMinutes));
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){}
        catch(OperationCanceledException)
        {if(mine==generation&&!disposed){message="차트 조회가 중지되었거나 응답 시간을 초과했습니다.";SyncControls();alert?.Invoke(message);}}
        catch(Exception ex)
        {
            if(mine!=generation||disposed)return;
            message=ex is InvalidOperationException?ex.Message:"차트 자료를 읽지 못했습니다. 연결 상태를 확인해 주세요.";
            SyncControls();alert?.Invoke(message);
        }
        finally{if(mine==generation&&!disposed){busy=false;Changed(nameof(Busy));Changed(nameof(Message));}}
    }

    public void RefreshQuote()
    {
        if(disposed||!IsRealtime)return;
        var latest=quoteReader?.Invoke(Snapshot.Stock.Key);
        if(latest?.StockKey==Snapshot.Stock.Key&&latest.Price>0)quote=latest;
        else if(quote!=null)quote=quote with{Running=false};
        Changed(nameof(PriceText));Changed(nameof(ChangeText));Changed(nameof(PriceColor));Changed(nameof(QuoteStatus));Changed(nameof(CanUseMinutes));
    }

    private void SyncControls()
    {
        updating=true;
        if(ChartPeriods.Minutes(period)>0){unit=period[^1..];value=period[..^1];}
        Changed(nameof(BarUnit));Changed(nameof(BarValues));Changed(nameof(BarValue));Changed(nameof(DisplayPeriod));Changed(nameof(CustomSelected));Changed(nameof(DailySelected));Changed(nameof(WeeklySelected));Changed(nameof(MonthlySelected));
        updating=false;
    }
    private void CancelLoad(){generation++;loading?.Cancel();loading?.Dispose();loading=null;}
    public void Dispose(){if(disposed)return;disposed=true;CancelLoad();}
}
