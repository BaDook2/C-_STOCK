using Stock.Core.Research;
using Stock.Core;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private bool chartLoading,executing;
    private string chartMessage="데이터가 없습니다.";
    private PriceBar[] minuteBars=[];
    private string minuteStock="";
    private string tickHistoryStock="";
    public bool ChartLoading {get=>chartLoading;private set{if(chartLoading==value)return;chartLoading=value;Changed();}}
    public string ChartMessage {get=>ChartPeriods.Intraday(ChartPeriod)?TickStatus:chartMessage;private set{chartMessage=value;Changed();}}
    public bool CanUseIntraday=>CanConfigure&&IsOnline&&selected!=null&&broker is ITickHistoryBroker&&broker.Exchange==selected.Exchange;
    public bool CanUseMinutes=>CanConfigure&&IsOnline&&selected!=null&&broker is IMinuteHistoryBroker&&broker.Exchange==selected.Exchange;
    public string IntradayHelp=>!IsOnline||broker==null?"연결된 시세 API가 없어 사용할 수 없습니다. 온라인 투자에서 지원 계좌를 연결하세요.":selected==null?"먼저 종목을 선택하세요.":broker.Exchange!=selected.Exchange?"연결 계좌와 선택 종목의 시장이 달라 사용할 수 없습니다.":"키움 공식 과거 틱·1분봉과 실시간 체결을 차트에 표시합니다.";
    public string[] TickIntervals {get;}=ChartPeriods.Intervals.Select(n=>n+"틱").ToArray();
    public string[] MinuteIntervals {get;}=ChartPeriods.Intervals.Select(n=>n+"분").ToArray();
    public string? SelectedTickInterval {get=>ChartPeriod.EndsWith("틱")?ChartPeriod:null;set{if(value!=null)ChartPeriod=value;}}
    public string? SelectedMinuteInterval {get=>ChartPeriod.EndsWith("분")?ChartPeriod:null;set{if(value!=null)ChartPeriod=value;}}
    private void BeginIntraday()
    {
        if(ChartPeriod.EndsWith("분")&&broker is IMinuteHistoryBroker)_=Run(LoadMinutes);
        else if(ChartPeriod.EndsWith("틱")&&broker is ITickHistoryBroker)_=Run(LoadTicks);
        else StartTicks();
    }
    private async Task LoadMinutes()
    {
        if(broker is not IMinuteHistoryBroker source||selected==null||!ChartPeriod.EndsWith("분"))return;
        var stock=selected;var operationToken=Token;var apiToken=ApiToken;using var request=CancellationTokenSource.CreateLinkedTokenSource(operationToken,apiToken);request.CancelAfter(TimeSpan.FromSeconds(15));ChartLoading=true;
        try
        {
            if(stock.Exchange!=broker.Exchange){minuteBars=[];TickStatus="이 계좌에서 지원하지 않는 시장의 분봉입니다.";return;}
            if(minuteStock!=stock.Key||minuteBars.Length==0){minuteBars=await source.MinuteHistory(stock.Symbol,request.Token);minuteStock=stock.Key;}
            TickStatus=minuteBars.Length==0?"분봉 데이터가 없습니다.":$"{broker.Name} 공식 1분봉 {minuteBars.Length:N0}개 · 마지막 {minuteBars[^1].KoreaTime}";
        }
        catch(OperationCanceledException) when(!operationToken.IsCancellationRequested&&!apiToken.IsCancellationRequested){minuteBars=[];TickStatus="분봉 응답 시간이 15초를 넘었습니다. 잠시 후 다시 선택하거나 API 조회 권한을 확인하세요.";}
        catch(OperationCanceledException){TickStatus="분봉 불러오기를 중지했습니다.";throw;}
        catch(Exception ex){minuteBars=[];TickStatus="분봉을 불러오지 못했습니다. "+ex.Message;}
        finally{ChartBars=visibleDaily;Changed(nameof(TickStatus));Changed(nameof(ChartMessage));ChartLoading=false;}
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
    private IReadOnlyList<PriceBar> IntradayBars()=>ChartPeriod.EndsWith("분")&&broker is IMinuteHistoryBroker&&minuteStock==selected?.Key
        ?ChartPeriods.MergeLiveTrades(minuteBars,tickBars,ChartPeriod)
        :ChartPeriods.FromTrades(tickBars,ChartPeriod);
    public void SetChartRange(DateTimeOffset date,bool start)
    {
        var day=date.ToOffset(TimeSpan.FromHours(9)).Date;
        if(start)From=To.HasValue&&day>To.Value.Date?To.Value.Date:day;
        else To=From.HasValue&&day<From.Value.Date?From.Value.Date:day;
    }
}
