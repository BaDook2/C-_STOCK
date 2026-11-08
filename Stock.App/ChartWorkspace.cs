using System.Collections.ObjectModel;
using Stock.Core.Research;
using Stock.Core;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private bool chartLoading,executing;
    private string chartMessage="데이터가 없습니다.";
    private PriceBar[] minuteBars=[];
    private string minuteStock="";
    private string tickHistoryStock="";
    private bool fitOneDayChartRange;
    public bool ChartLoading {get=>chartLoading;private set{if(chartLoading==value)return;chartLoading=value;Changed();}}
    public string ChartMessage {get=>ChartPeriods.Intraday(ChartPeriod)?TickStatus:chartMessage;private set{chartMessage=value;Changed();}}
    public bool CanUseIntraday=>CanConfigure&&IsOnline&&selected!=null&&broker is ITickHistoryBroker&&broker.Exchange==selected.Exchange;
    public bool CanUseMinutes=>CanConfigure&&IsOnline&&selected!=null&&broker is IMinuteHistoryBroker&&broker.Exchange==selected.Exchange;
    public string IntradayHelp=>IsOffline?"오프라인 데이터 기준은 일봉입니다. 지정봉은 온라인 연결 후 사용할 수 있습니다.":selected==null?"먼저 종목을 선택하세요.":broker is IMinuteHistoryBroker&&broker.Exchange==selected.Exchange?"연결 증권사의 공식 분봉·시간봉을 사용합니다.":"연결 계좌에서 선택 종목의 지정봉 조회를 지원하지 않습니다.";
    public string[] TickIntervals {get;}=ChartPeriods.Intervals.Select(n=>n+"틱").ToArray();
    public string[] MinuteIntervals {get;}=ChartPeriods.Intervals.Select(n=>n+"분").ToArray();
    public string? SelectedTickInterval {get=>ChartPeriod.EndsWith("틱")?ChartPeriod:null;set{if(value!=null)ChartPeriod=value;}}
    public string? SelectedMinuteInterval {get=>ChartPeriod.EndsWith("분")?ChartPeriod:null;set{if(value!=null)ChartPeriod=value;}}
    private string chartBarValue="1",chartBarUnit="분";
    public ObservableCollection<string> ChartBarValues {get;}=new(TimeBarSelection.Values("분"));
    public TimeBarUnitOption[] ChartBarUnitOptions=>TimeBarSelection.Options;
    public string ChartBarValue
    {
        get=>chartBarValue;
        set
        {
            if(value==null||!CanUseMinutes)return;
            var next=TimeBarSelection.NormalizeValue(value,chartBarUnit);if(chartBarValue==next)return;
            chartBarValue=next;Changed();ChartPeriod=chartBarValue+chartBarUnit;
        }
    }
    public string ChartBarUnit
    {
        get=>chartBarUnit;
        set
        {
            if(!CanUseMinutes)return;
            var next=TimeBarSelection.NormalizeUnit(value);if(chartBarUnit==next)return;
            chartBarUnit=next;chartBarValue=TimeBarSelection.NormalizeValue(chartBarValue,chartBarUnit);
            Changed(nameof(ChartBarValue));RefreshChartBarValues();Changed();ChartPeriod=chartBarValue+chartBarUnit;
        }
    }
    private bool IsMinuteChart=>ChartPeriod.EndsWith("분")&&ChartPeriods.Minutes(ChartPeriod)>0;
    public bool CustomChartSelected {get=>ChartPeriods.Minutes(ChartPeriod)>0;set{if(value&&CanUseMinutes)ChartPeriod=chartBarValue+chartBarUnit;}}
    private void NotifyChartBarSelection()
    {
        if(ChartPeriods.Minutes(ChartPeriod)>0){chartBarUnit=ChartPeriod[^1..];chartBarValue=ChartPeriod[..^1];}
        // Select a shared valid item before shrinking the list (e.g. 59 minutes -> 1 hour).
        if(ChartBarValues.Contains(chartBarValue))Changed(nameof(ChartBarValue));
        RefreshChartBarValues();Changed(nameof(ChartBarUnit));Changed(nameof(ChartBarValue));Changed(nameof(CustomChartSelected));
    }
    /// <summary>Preserve shared 1..12 items instead of replacing a ComboBox source during its selection callback.</summary>
    private void RefreshChartBarValues()
    {
        var values=TimeBarSelection.Values(chartBarUnit);
        while(ChartBarValues.Count>values.Length)ChartBarValues.RemoveAt(ChartBarValues.Count-1);
        while(ChartBarValues.Count<values.Length)ChartBarValues.Add(values[ChartBarValues.Count]);
    }
    private void BeginIntraday()
    {
        if(ChartPeriods.Minutes(ChartPeriod)>0)_=Run(LoadMinutes);
        else if(ChartPeriod.EndsWith("틱")&&broker is ITickHistoryBroker)_=Run(LoadTicks);
        else StartTicks();
    }
    private async Task LoadMinutes()
    {
        if(selected==null||ChartPeriods.Minutes(ChartPeriod)==0)return;
        if(ChartRangeDaySelected)fitOneDayChartRange=true;
        var stock=selected;var operationToken=Token;var apiToken=ApiToken;using var request=CancellationTokenSource.CreateLinkedTokenSource(operationToken,apiToken);request.CancelAfter(TimeSpan.FromSeconds(15));ChartLoading=true;
        var generation=dailyGeneration;
        try
        {
            if(minuteStock!=stock.Key||minuteBars.Length==0)
            {
                PriceBar[] loaded;
                if(IsOffline&&stock.Exchange==Exchange.KRX&&provider is IPublicMinuteMarketData publicMinutes)
                    loaded=await publicMinutes.MinuteHistory(stock,KoreaClock.Today.AddDays(-14),KoreaClock.Today,request.Token);
                else if(IsOnline&&broker is IMinuteHistoryBroker source&&stock.Exchange==broker.Exchange)
                    loaded=await source.MinuteHistory(stock.Symbol,request.Token);
                else throw new InvalidOperationException(IntradayHelp);
                if(generation!=dailyGeneration||selected?.Key!=stock.Key)return;
                minuteBars=loaded.OrderBy(b=>b.Time).DistinctBy(b=>b.Time).TakeLast(20000).ToArray();minuteStock=stock.Key;
            }
            TickStatus=minuteBars.Length==0?"분봉 데이터가 없습니다.":$"{(IsOffline?"네이버 KRX 공개":broker!.Name+" 공식")} 1분봉 {minuteBars.Length:N0}개 · 마지막 {minuteBars[^1].KoreaTime}"+(IsOffline?" · 진행봉 가격은 현재가 관측, 거래량은 분봉 조회값":"");
        }
        catch(OperationCanceledException) when(!operationToken.IsCancellationRequested&&!apiToken.IsCancellationRequested){minuteBars=[];TickStatus="분봉 응답 시간이 15초를 넘었습니다. 잠시 후 다시 선택하거나 API 조회 권한을 확인하세요.";}
        catch(OperationCanceledException){TickStatus="분봉 불러오기를 중지했습니다.";throw;}
        catch(Exception ex){minuteBars=[];TickStatus="분봉을 불러오지 못했습니다. "+ex.Message;}
        finally{if(generation==dailyGeneration){ChartBars=visibleDaily;Changed(nameof(TickStatus));Changed(nameof(ChartMessage));}ChartLoading=false;}
    }
    private async Task LoadTicks()
    {
        if(broker is not ITickHistoryBroker source||selected==null||!ChartPeriod.EndsWith("틱"))return;
        var stock=selected;var operationToken=Token;var apiToken=ApiToken;using var request=CancellationTokenSource.CreateLinkedTokenSource(operationToken,apiToken);request.CancelAfter(TimeSpan.FromSeconds(15));ChartLoading=true;
        try
        {
            if(stock.Exchange!=broker.Exchange){tickBars.Clear();TickStatus="이 계좌에서 지원하지 않는 시장의 틱입니다.";return;}
            if(tickHistoryStock!=stock.Key||tickBars.Count==0)
            {
                var history=await source.TickHistory(stock.Symbol,request.Token);tickBars.Clear();tickBars.AddRange(history.TakeLast(2_000));tickStock=stock.Key;tickHistoryStock=stock.Key;
            }
            TickStatus=tickBars.Count==0?"틱 데이터가 없습니다.":$"{broker.Name} 공식 1틱 {tickBars.Count:N0}개 · 마지막 {tickBars[^1].KoreaTime}";
        }
        catch(OperationCanceledException) when(!operationToken.IsCancellationRequested&&!apiToken.IsCancellationRequested){tickBars.Clear();TickStatus="틱 응답 시간이 15초를 넘었습니다. 잠시 후 다시 선택하거나 API 조회 권한을 확인하세요.";}
        catch(OperationCanceledException){TickStatus="틱 불러오기를 중지했습니다.";throw;}
        catch(Exception ex){tickBars.Clear();TickStatus="틱을 불러오지 못했습니다. "+ex.Message;}
        finally
        {
            ChartBars=visibleDaily;Changed(nameof(TickStatus));Changed(nameof(ChartMessage));ChartLoading=false;
            if(!disposed&&selected?.Key==stock.Key&&ChartPeriod.EndsWith("틱"))StartTicks();
        }
    }
    private IReadOnlyList<PriceBar> IntradayBars()=>ChartPeriods.Minutes(ChartPeriod)>0&&minuteStock==selected?.Key
        ?IsOffline?ChartPeriods.MergeCurrentObservation(minuteBars,pendingDaily,ChartPeriod):ChartPeriods.MergeLiveTrades(minuteBars,tickBars,ChartPeriod)
        :ChartPeriods.FromTrades(tickBars,ChartPeriod);
    /// <summary>Display-only daily window. Keep all received minutes for aggregation and strategy input.</summary>
    private IReadOnlyList<PriceBar> ChartDisplayBars(IReadOnlyList<PriceBar> daily)
    {
        IReadOnlyList<PriceBar> bars=ChartPeriods.Intraday(ChartPeriod)?IntradayBars():ChartPeriods.Aggregate(daily,ChartPeriod);
        if(!ChartRangeDaySelected||!IsMinuteChart||bars.Count==0)return bars;
        var exchange=selected?.Exchange??Exchange.KRX;
        var lastDay=DailyQuoteMerger.Date(bars[^1].Time,exchange);
        bars=bars.Where(b=>DailyQuoteMerger.Date(b.Time,exchange)==lastDay).ToArray();
        if(fitOneDayChartRange)
        {
            ChartWindowCount=bars.Count;Changed(nameof(ChartWindowCount));fitOneDayChartRange=false;
        }
        return bars;
    }
    public void SetChartRange(DateTimeOffset date,bool start)
    {
        var day=date.ToOffset(TimeSpan.FromHours(9)).Date;
        if(start)From=To.HasValue&&day>To.Value.Date?To.Value.Date:day;
        else To=From.HasValue&&day<From.Value.Date?From.Value.Date:day;
    }
}
