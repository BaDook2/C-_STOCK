using Stock.Core.Research;
using Stock.Core;
using Stock.Core.Research.Oversold;
namespace Stock.Desktop;
public sealed partial class MainViewModel
{
    private string chartUnit="분",chartValue="5";
    private PriceBar[] displayIntraday=[];
    private long chartRequest;
    public string[] ChartUnits {get;}=["분","시간"];
    public string[] ChartValues=>Enumerable.Range(1,chartUnit=="분"?59:12).Select(n=>n.ToString()).ToArray();
    public bool ChartCustomAvailable=>Online&&SelectedStock?.Exchange==Stock.Core.Trading.Exchange.KRX&&Broker is IMinuteHistoryBroker;
    public string ChartCustomHelp=>!Online?"증권사 계좌를 연결한 뒤 분·시간봉을 선택하세요.":SelectedStock==null?"먼저 국내 종목을 검색하세요.":SelectedStock.Exchange!=Stock.Core.Trading.Exchange.KRX?"이 연결은 해외 분·시간봉을 지원하지 않습니다.":Broker is not IMinuteHistoryBroker?$"{Broker!.Name} 연결은 이 버전에서 분·시간봉 조회를 지원하지 않습니다.":"분1~59 또는 시간1~12를 선택한 뒤 지정봉을 누르세요.";
    public string ChartUnit {get=>chartUnit;set{if(value is not("분" or "시간"))return;if(Set(ref chartUnit,value)){Changed(nameof(ChartValues));ChartValue="1";}}}
    public string ChartValue {get=>chartValue;set{if(value!=null)Set(ref chartValue,value);}}
    public AsyncCommand ApplyChartTimeframe {get;private set;}=null!;
    private void InitializeChartTimeframe()
    {
        PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(ChartCustomAvailable))Changed(nameof(ChartCustomHelp));};
        ApplyChartTimeframe=new(()=>
        {
            if(!ChartCustomAvailable){Notify(ChartCustomHelp);return Task.CompletedTask;}
            if(!int.TryParse(ChartValue,out var value)||value<1||value>(ChartUnit=="분"?59:12)){Notify("분은1~59, 시간은1~12로 선택하세요.");return Task.CompletedTask;}
            var target=value+(ChartUnit=="분"?"분":"시");if(ChartPeriod==target)return LoadChartTimeframe();
            ChartPeriod=target;return Task.CompletedTask;
        });
    }
    private Task LoadChartTimeframe()=>Run(async token=>
    {
        if(selectedStock==null)return;if(!ChartCustomAvailable)throw new InvalidOperationException("연결된 계좌에서 분·시간봉을 조회할 수 없습니다.");
        var stock=selectedStock;var period=ChartPeriod;var request=++chartRequest;var minutes=ChartPeriods.Minutes(period);var spec=new BarSpecification(period.EndsWith("시")?minutes/60:minutes,period.EndsWith("시")?BarUnit.Hour:BarUnit.Minute);
        var today=DailyQuoteMerger.Date(services.Clock(),stock.Exchange);var from=period.EndsWith("분")?today:today.AddMonths(-Math.Max(1,SelectedRange.Months));
        Status=period+" 조회 중…";var data=await HistoryService.Chart(stock,spec,from,today,token);token.ThrowIfCancellationRequested();if(request!=chartRequest||stock.Key!=selectedStock?.Key||period!=ChartPeriod)return;
        displayIntraday=data;RefreshChart();Status=$"{period} · {data.Length:N0}개 · 연결 증권사 자료";
    });
}
