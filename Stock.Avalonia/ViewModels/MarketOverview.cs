using System.Collections.ObjectModel;
using Stock.Core.Research;
namespace Stock.Desktop;
public sealed class MarketIndexViewModel(string code,string name):Bindable
{
    public string Code {get;}=code;public string Name {get;}=name;
    private IndexTrend? trend;
    private string error="새로고침으로 시세를 조회하세요.";
    public string Price=>trend?.Quote.PriceText??"—";
    public string Change=>trend?.Quote.ChangeText??"—";
    public string Time=>trend?.Quote.KoreaTime??"";
    public IndexPoint[] Points=>trend?.Points??[];
    public string State=>error;
    public void Update(IndexTrend value){trend=value;error="제공처 관측 추이 · 미래 자료 제외";foreach(var name in new[]{nameof(Price),nameof(Change),nameof(Time),nameof(Points),nameof(State)})Changed(name);}
    public void Error(string text){error=text;Changed(nameof(State));}
}
public sealed partial class MainViewModel
{
    public ObservableCollection<MarketIndexViewModel> MarketIndices {get;}=[];
    public AsyncCommand RefreshMarket {get;private set;}=null!;
    private void InitializeMarketOverview()
    {
        foreach(var (code,name) in new[]{("KOSPI","코스피"),("KOSDAQ","코스닥"),("SP500","S&P500"),("NASDAQ","나스닥 종합"),("DOW","다우존스")})MarketIndices.Add(new(code,name));
        RefreshMarket=new(()=>Run(async token=>
        {
            if(services.MarketData is not IIndexChartData source)throw new InvalidOperationException("현재 자료 공급자는 지수 추이를 지원하지 않습니다.");
            foreach(var card in MarketIndices)
            {
                token.ThrowIfCancellationRequested();try{var data=await source.IndexHistory(card.Code,token);card.Update(data);}catch(OperationCanceledException){throw;}catch(Exception ex){card.Error(ex.Message);}
            }
            Status="시장 지수 조회 완료 · 제공처 시각을 확인하세요.";
        }));
    }
}
