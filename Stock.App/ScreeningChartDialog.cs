using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Stock.App;

/// <summary>스크리닝 당시 설정과 공통 백테스트 서비스 결과를 한 창에서 연속 검토한다.</summary>
public sealed class ScreeningChartDialog:Window
{
    private readonly CandleChart chart;
    private readonly TextBlock source;
    private readonly TextBlock heading;
    private readonly TextBlock summary;
    private readonly TextBox detail;
    private readonly DataGrid trades;
    private readonly Func<int,CancellationToken,Task<ScreeningChartSnapshot?>>? navigate;
    private CancellationTokenSource? navigationCancellation;
    private int generation;
    public ScreeningChartSnapshot Snapshot {get;private set;}
    public ComboBox PeriodSelector {get;}
    public CandleChart ReviewChart=>chart;

    public ScreeningChartDialog(ScreeningChartSnapshot snapshot,Func<int,CancellationToken,Task<ScreeningChartSnapshot?>>? navigate=null)
    {
        Snapshot=snapshot;this.navigate=navigate;Title=snapshot.Stock.Name+" · 전략 백테스트";Width=1280;Height=920;MinWidth=920;MinHeight=680;Background=(Brush?)Application.Current.TryFindResource("CanvasBrush")??Brushes.White;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new Grid{Margin=new Thickness(22),Background=(Brush?)Application.Current.TryFindResource("CanvasBrush")??Brushes.White};root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(250)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=root;
        var header=new Grid{Margin=new Thickness(0,0,0,12)};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});root.Children.Add(header);
        var title=new StackPanel();title.Children.Add(heading=new TextBlock{FontSize=24,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(25,31,40))});title.Children.Add(source=new TextBlock{Margin=new Thickness(0,7,0,0),TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(107,118,132))});header.Children.Add(title);
        var periods=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};periods.Children.Add(new TextBlock{Text="차트 주기",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});PeriodSelector=new ComboBox{DisplayMemberPath=nameof(ScreeningChartSeries.Label),MinWidth=110};PeriodSelector.SelectionChanged+=(_,_)=>ApplySeries();periods.Children.Add(PeriodSelector);Grid.SetColumn(periods,1);header.Children.Add(periods);
        chart=new CandleChart{MovingAverages=true,Ichimoku=false,DisplayBarCount=120,Margin=new Thickness(0,0,0,12)};chart.TradeMarkerClicked+=marker=>SelectTrade(marker.Time,marker.Buy);Grid.SetRow(chart,1);root.Children.Add(chart);
        summary=new TextBlock{Padding=new Thickness(12),Margin=new Thickness(0,0,0,10),TextWrapping=TextWrapping.Wrap,Background=new SolidColorBrush(Color.FromRgb(247,249,250)),FontSize=14};Grid.SetRow(summary,2);root.Children.Add(summary);
        var lower=new Grid();lower.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});lower.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});Grid.SetRow(lower,3);root.Children.Add(lower);
        trades=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeaderWidth=0,Margin=new Thickness(0,0,10,0)};
        detail=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text="거래 행이나 차트의 ▲/▼ 표식을 선택하면 당시 판단과 Support Cluster를 표시합니다."};Grid.SetColumn(detail,1);lower.Children.Add(detail);
        trades.Columns.Add(Column("한국시간",nameof(ScreeningChartTrade.TimeText),170));trades.Columns.Add(Column("구분",nameof(ScreeningChartTrade.Side),70));trades.Columns.Add(Column("가격",nameof(ScreeningChartTrade.PriceText),100));trades.Columns.Add(Column("수량",nameof(ScreeningChartTrade.QuantityText),80));trades.Columns.Add(Column("사유 · 출처",nameof(ScreeningChartTrade.Reason),new DataGridLength(1,DataGridLengthUnitType.Star)));trades.SelectionChanged+=(_,_)=>{if(trades.SelectedItem is ScreeningChartTrade row){detail.Text=row.Detail;chart.FocusTrade(row.Time,row.Buy);}};lower.Children.Add(trades);
        var footer=new DockPanel{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footer,4);root.Children.Add(footer);var close=new Button{Content="닫기",MinWidth=90};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Right);footer.Children.Add(close);
        var next=new Button{Content="다음 종목",MinWidth=100,Style=(Style?)TryFindResource("Secondary")};next.Click+=async(_,_)=>await Navigate(1);DockPanel.SetDock(next,Dock.Right);footer.Children.Add(next);
        var previous=new Button{Content="이전 종목",MinWidth=100,Margin=new Thickness(0,0,8,0),Style=(Style?)TryFindResource("Secondary")};previous.Click+=async(_,_)=>await Navigate(-1);DockPanel.SetDock(previous,Dock.Right);footer.Children.Add(previous);
        var reload=new Button{Content="다시 실행",MinWidth=100,Style=(Style?)TryFindResource("Secondary")};reload.Click+=async(_,_)=>await Navigate(0);footer.Children.Add(reload);
        Closed+=(_,_)=>{navigationCancellation?.Cancel();navigationCancellation?.Dispose();};ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(ScreeningChartSnapshot snapshot)
    {
        Snapshot=snapshot;Title=snapshot.Stock.Name+" · 전략 백테스트";heading.Text=snapshot.Stock.Name+"  "+snapshot.Stock.Symbol;
        source.Text=$"{snapshot.Run.StrategyName} · 스크리닝 당시 설정 사용 · {snapshot.Run.BarSpecification?.ToString()??"일봉"} · 기간 {snapshot.Run.From:yyyy-MM-dd} ~ {snapshot.Run.To:yyyy-MM-dd}\n전체 기간에서는 차트 입력이 잠깁니다. 원하는 봉에서 우클릭해 확대하면 휠·드래그·매매 표식 선택을 사용할 수 있고, 다시 우클릭하면 전체 기간으로 돌아갑니다."+(snapshot.Notice.Length>0?"\n"+snapshot.Notice:"");
        PeriodSelector.ItemsSource=snapshot.Series;var initial=snapshot.Run.BarSpecification?.ToString()??"일봉";PeriodSelector.SelectedItem=snapshot.Series.FirstOrDefault(s=>s.Period==initial)??snapshot.Series.FirstOrDefault();trades.ItemsSource=snapshot.Trades;detail.Text="거래 행이나 확대된 차트의 ▲/▼ 표식을 선택하면 당시 판단과 Support Cluster를 표시합니다.";
        var m=snapshot.Result?.Performance;summary.Text=m==null?"지정 봉 스크리닝은 현재 신호 평가 결과만 표시합니다. 일봉 백테스트 결과가 없습니다.":$"완결 거래 {m.Trades:N0}회 · 승률 {Percent(m.WinRate)} · 누적 수익률 {((m.FinalEquity-snapshot.Result!.Configuration.InitialCapital)/snapshot.Result.Configuration.InitialCapital*100):N2}% · 평균 수익 {Percent(m.AverageWinPercent)} · 평균 손실 {Percent(m.AverageLossPercent)} · 평균 보유 {m.AverageHoldingDays?.ToString("N2")??"미산출"}일 · Profit Factor {(m.InfiniteProfitFactor?"∞":m.ProfitFactor?.ToString("N2")??"미산출")} · Expectancy {m.Expectancy?.ToString("N0")??"미산출"}원 · MDD {m.MaxDrawdown:N2}% · Sharpe {m.Sharpe?.ToString("N2")??"미산출"}";
        ApplySeries();
        static string Percent(decimal? value)=>value?.ToString("N2")+"%"??"미산출";
    }
    private void ApplySeries(){if(PeriodSelector.SelectedItem is not ScreeningChartSeries series)return;chart.DisplayBarCount=Math.Clamp(series.Bars.Length,25,2000);chart.Bars=series.Bars;chart.Period=series.Period;chart.TradeMarkers=Snapshot.Markers;chart.SupportZones=Snapshot.SupportZones;chart.EnableScreeningReviewMode(500);}
    private async Task Navigate(int delta)
    {
        if(navigate==null)return;navigationCancellation?.Cancel();navigationCancellation?.Dispose();navigationCancellation=new();var token=navigationCancellation.Token;var mine=++generation;IsEnabled=false;source.Text="백테스트 자료를 불러오는 중...";
        try{var next=await navigate(delta,token);if(next!=null&&mine==generation&&!token.IsCancellationRequested)ApplySnapshot(next);}catch(OperationCanceledException){}catch(InvalidOperationException ex){source.Text=ex.Message;}finally{if(mine==generation)IsEnabled=true;}
    }
    private void SelectTrade(DateTimeOffset time,bool buy)
    {var match=Snapshot.Trades.OrderBy(t=>Math.Abs((t.Time-time).Ticks)).FirstOrDefault(t=>t.Buy==buy);if(match!=null){trades.SelectedItem=match;trades.ScrollIntoView(match);}}
    private static DataGridTextColumn Column(string title,string path,double width)=>new(){Header=title,Binding=new Binding(path),Width=width};
    private static DataGridTextColumn Column(string title,string path,DataGridLength width)=>new(){Header=title,Binding=new Binding(path),Width=width};
}
