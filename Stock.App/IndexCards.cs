using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Stock.Core.Research;
namespace Stock.App;

public sealed class IndexCard(string code,string name) : Observable
{
    public string Code {get;}=code;
    public string Name {get;}=name;
    public MarketQuote? Quote {get;private set;}
    public IndexTrend? Trend {get;private set;}
    public DateTimeOffset ChartFetched {get;private set;}
    public string Price=>Quote?.PriceText??"—";
    public string Change=>Quote==null?"—":$"{(Quote.Change>0?"▲":Quote.Change<0?"▼":"—")} {Math.Abs(Quote.Change):N0}";
    public string Percent=>Quote==null?"—":$"{Math.Abs(Quote.Percent):N2}%";
    public string Color=>Quote==null||Quote.Change==0?"#8B95A1":Quote.Change>0?"#F04452":"#3182F6";
    public string Time=>Quote?.KoreaTime??"시세 확인 중";
    public string ChartCaption {get;private set;}="당일 추이 불러오는 중";
    public bool HasChart=>Trend?.Points.Length>1;
    public string QuoteStatus {get;private set;}="";
    public void SetQuote(MarketQuote quote){Quote=quote;QuoteStatus="";Changed(nameof(Price));Changed(nameof(Change));Changed(nameof(Percent));Changed(nameof(Color));Changed(nameof(Time));Changed(nameof(QuoteStatus));}
    public void SetTrend(IndexTrend trend){Trend=trend;ChartFetched=DateTimeOffset.UtcNow;ChartCaption=trend.Points.Length>1?$"{trend.Points[0].Time.ToOffset(TimeSpan.FromHours(9)):MM.dd HH:mm}–{trend.Points[^1].Time.ToOffset(TimeSpan.FromHours(9)):HH:mm} · 5분 추이":"추이 데이터가 부족합니다";Changed(nameof(Trend));Changed(nameof(HasChart));Changed(nameof(ChartCaption));}
    public void QuoteFailed(){QuoteStatus="조회 실패 · 이전 시각 확인";Changed(nameof(QuoteStatus));}
    public void ChartFailed(){ChartCaption=Trend==null?"추이를 불러오지 못했습니다":"추이 갱신 실패 · 이전 자료";Changed(nameof(ChartCaption));}
}

/// <summary>관측한 지수의 시간·가격과 전일 종가 기준선만 그리는 작은 차트입니다.</summary>
public sealed class IndexSparkline : FrameworkElement
{
    public static readonly DependencyProperty SeriesProperty=DependencyProperty.Register(nameof(Series),typeof(IndexTrend),typeof(IndexSparkline),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LineColorProperty=DependencyProperty.Register(nameof(LineColor),typeof(Brush),typeof(IndexSparkline),new FrameworkPropertyMetadata(Brushes.RoyalBlue,FrameworkPropertyMetadataOptions.AffectsRender));
    public IndexTrend? Series {get=>(IndexTrend?)GetValue(SeriesProperty);set=>SetValue(SeriesProperty,value);}
    public Brush LineColor {get=>(Brush)GetValue(LineColorProperty);set=>SetValue(LineColorProperty,value);}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var series=Series;if(series?.Points.Length<2||series==null||ActualWidth<10||ActualHeight<10)return;
        var p=series.Points;var min=Math.Min(series.PreviousClose,p.Min(v=>v.Value));var max=Math.Max(series.PreviousClose,p.Max(v=>v.Value));var range=Math.Max(max-min,Math.Max(max*.0001m,.0001m));min-=range*.08m;range*=1.16m;
        double Y(decimal value)=>ActualHeight-6-(double)((value-min)/range)*(ActualHeight-12);
        var span=Math.Max(1,(p[^1].Time-p[0].Time).TotalSeconds);double X(IndexPoint point)=>4+(point.Time-p[0].Time).TotalSeconds/span*(ActualWidth-8);
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(205,210,216)),1){DashStyle=DashStyles.Dash},new(0,Y(series.PreviousClose)),new(ActualWidth,Y(series.PreviousClose)));
        var line=new StreamGeometry();using(var c=line.Open()){c.BeginFigure(new(X(p[0]),Y(p[0].Value)),false,false);foreach(var point in p.Skip(1))c.LineTo(new(X(point),Y(point.Value)),true,false);}line.Freeze();
        dc.DrawGeometry(null,new Pen(LineColor,2.2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},line);dc.DrawEllipse(LineColor,null,new(X(p[^1]),Y(p[^1].Value)),3,3);
    }
}
public sealed partial class WorkspaceViewModel
{
    private readonly IndexCard[] domesticIndices=[new("KOSPI","코스피"),new("KOSDAQ","코스닥")];
    private readonly IndexCard[] overseasIndices=[new("DOW","다우존스"),new("NASDAQ","나스닥"),new("SP500","S&P 500")];
    public ObservableCollection<IndexCard> VisibleIndices {get;}=[];
    private bool domesticMarket=true;
    public bool DomesticMarket {get=>domesticMarket;set{if(!value||domesticMarket)return;domesticMarket=true;ChangeIndexMarket();}}
    public bool OverseasMarket {get=>!domesticMarket;set{if(!value||!domesticMarket)return;domesticMarket=false;ChangeIndexMarket();}}
    private void ChangeIndexMarket(){Changed(nameof(DomesticMarket));Changed(nameof(OverseasMarket));Replace(VisibleIndices,domesticMarket?domesticIndices:overseasIndices);if(ActiveTab==5)_=RefreshMarket();}
    private async Task RefreshIndexCard(IndexCard card,IMarketOverviewData market)
    {
        try
        {
            if(card.Code is "DOW" or "NASDAQ" or "SP500"&&provider is IIndexChartData abroad)
            {var trend=await abroad.IndexHistory(card.Code,ApiToken);if(disposed)return;card.SetQuote(trend.Quote);card.SetTrend(trend);}
            else{var quote=await market.Index(card.Code,ApiToken);if(disposed)return;card.SetQuote(quote);if(card.Code=="KOSPI"){Kospi=quote;Changed(nameof(Kospi));}if(card.Code=="KOSDAQ"){Kosdaq=quote;Changed(nameof(Kosdaq));}}
        }catch(OperationCanceledException){return;}catch{if(!disposed)card.QuoteFailed();}
        if(card.Code is "KOSPI" or "KOSDAQ"&&provider is IIndexChartData charts&&DateTimeOffset.UtcNow-card.ChartFetched>TimeSpan.FromMinutes(1))
        {try{var trend=await charts.IndexHistory(card.Code,ApiToken);if(!disposed)card.SetTrend(trend);}catch(OperationCanceledException){}catch{if(!disposed)card.ChartFailed();}}
    }
}
