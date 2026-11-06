using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

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
    private readonly DispatcherTimer quoteTimer;
    public ScreeningChartSnapshot Snapshot {get;private set;}
    public ScreeningChartViewModel ViewModel {get;}
    public TextBlock AnalysisPeriodLabel {get;}
    public CandleChart ReviewChart=>chart;

    public ScreeningChartDialog(ScreeningChartSnapshot snapshot,Func<int,CancellationToken,Task<ScreeningChartSnapshot?>>? navigate=null,ScreeningChartViewModel? viewModel=null)
    {
        Snapshot=snapshot;this.navigate=navigate;ViewModel=viewModel??new(snapshot);DataContext=ViewModel;Title=snapshot.Stock.Name+" · 전략 백테스트";Width=1280;Height=920;MinWidth=920;MinHeight=680;Background=(Brush?)Application.Current.TryFindResource("CanvasBrush")??Brushes.White;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new Grid{Margin=new Thickness(22),Background=(Brush?)Application.Current.TryFindResource("CanvasBrush")??Brushes.White};root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new(){Height=new GridLength(250)});root.RowDefinitions.Add(new(){Height=GridLength.Auto});Content=root;
        var header=new Grid{Margin=new Thickness(0,0,0,12)};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});for(var i=0;i<4;i++)header.RowDefinitions.Add(new(){Height=GridLength.Auto});root.Children.Add(header);
        heading=new TextBlock{FontSize=24,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(25,31,40))};header.Children.Add(heading);
        AnalysisPeriodLabel=new TextBlock{FontSize=14,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromRgb(107,118,132)),ToolTip="전략을 평가한 스크리닝 기준입니다. 아래 표시 봉을 바꾸어도 이 기준과 매매 결과는 같습니다."};AnalysisPeriodLabel.SetBinding(TextBlock.TextProperty,new Binding(nameof(ViewModel.PeriodDescription)));Grid.SetColumn(AnalysisPeriodLabel,1);header.Children.Add(AnalysisPeriodLabel);
        var quote=new WrapPanel{Margin=new Thickness(0,8,0,0)};quote.SetBinding(VisibilityProperty,new Binding(nameof(ViewModel.IsRealtime)){Converter=new BooleanToVisibilityConverter()});
        var price=new TextBlock{FontSize=28,FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center};price.SetBinding(TextBlock.TextProperty,new Binding(nameof(ViewModel.PriceText)));price.SetBinding(TextBlock.ForegroundProperty,new Binding(nameof(ViewModel.PriceColor)));quote.Children.Add(price);
        var change=new TextBlock{FontSize=16,Margin=new Thickness(14,0,12,0),VerticalAlignment=VerticalAlignment.Center};change.SetBinding(TextBlock.TextProperty,new Binding(nameof(ViewModel.ChangeText)));change.SetBinding(TextBlock.ForegroundProperty,new Binding(nameof(ViewModel.PriceColor)));quote.Children.Add(change);
        var quoteState=new TextBlock{FontSize=12,VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.SlateGray};quoteState.SetBinding(TextBlock.TextProperty,new Binding(nameof(ViewModel.QuoteStatus)));quote.Children.Add(quoteState);Grid.SetRow(quote,1);Grid.SetColumnSpan(quote,2);header.Children.Add(quote);
        source=new TextBlock{Margin=new Thickness(0,7,0,0),TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(107,118,132))};Grid.SetRow(source,2);Grid.SetColumnSpan(source,2);header.Children.Add(source);
        var toolbar=new DockPanel{Margin=new Thickness(0,10,0,0)};Grid.SetRow(toolbar,3);Grid.SetColumnSpan(toolbar,2);header.Children.Add(toolbar);
        var periods=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(periods,Dock.Right);toolbar.Children.Add(periods);
        var custom=new RadioButton{Content="지정봉",GroupName="ScreeningDisplayPeriod",Padding=new Thickness(10,6,10,6),VerticalContentAlignment=VerticalAlignment.Center};custom.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,new Binding(nameof(ViewModel.CustomSelected)){Mode=BindingMode.TwoWay});custom.SetBinding(IsEnabledProperty,new Binding(nameof(ViewModel.CanUseMinutes)));periods.Children.Add(custom);
        var number=new ComboBox{Width=64,MinWidth=64,Height=36,Margin=new Thickness(2),ToolTip="표시할 분봉 또는 시간봉의 크기"};number.SetBinding(ItemsControl.ItemsSourceProperty,new Binding(nameof(ViewModel.BarValues)));number.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,new Binding(nameof(ViewModel.BarValue)){Mode=BindingMode.TwoWay});number.SetBinding(IsEnabledProperty,new Binding(nameof(ViewModel.CanUseMinutes)));periods.Children.Add(number);
        var unit=new ComboBox{Width=86,MinWidth=86,Height=36,Margin=new Thickness(2),SelectedValuePath=nameof(TimeBarUnitOption.Value),DisplayMemberPath=nameof(TimeBarUnitOption.Label),ToolTip="분봉 1~59, 시간봉 1~12. 연결된 공개/증권사 시세로 표시합니다."};unit.SetBinding(ItemsControl.ItemsSourceProperty,new Binding(nameof(ViewModel.BarUnits)));unit.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty,new Binding(nameof(ViewModel.BarUnit)){Mode=BindingMode.TwoWay});unit.SetBinding(IsEnabledProperty,new Binding(nameof(ViewModel.CanUseMinutes)));periods.Children.Add(unit);
        foreach(var (label,property) in new[]{("일봉",nameof(ViewModel.DailySelected)),("주봉",nameof(ViewModel.WeeklySelected)),("월봉",nameof(ViewModel.MonthlySelected))})
        {var tab=new RadioButton{Content=label,GroupName="ScreeningDisplayPeriod",Padding=new Thickness(10,6,10,6),VerticalContentAlignment=VerticalAlignment.Center};tab.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,new Binding(property){Mode=BindingMode.TwoWay});periods.Children.Add(tab);}
        var status=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var displayed=new TextBlock{FontSize=14};displayed.SetBinding(TextBlock.TextProperty,new Binding(nameof(ViewModel.DisplayPeriod)){StringFormat="표시 봉 · {0}"});status.Children.Add(displayed);var loading=new TextBlock{FontSize=12,Foreground=Brushes.SlateGray,MaxWidth=560,TextTrimming=TextTrimming.CharacterEllipsis};loading.SetBinding(TextBlock.TextProperty,new Binding(nameof(ViewModel.Message)));loading.SetBinding(ToolTipProperty,new Binding(nameof(ViewModel.Message)));status.Children.Add(loading);toolbar.Children.Add(status);
        chart=new CandleChart{MovingAverages=true,Ichimoku=false,DisplayBarCount=120,Margin=new Thickness(0,0,0,12)};chart.TradeMarkerClicked+=marker=>SelectTrade(marker.Time,marker.Buy);Grid.SetRow(chart,1);root.Children.Add(chart);
        summary=new TextBlock{Padding=new Thickness(12),Margin=new Thickness(0,0,0,10),TextWrapping=TextWrapping.Wrap,Background=new SolidColorBrush(Color.FromRgb(247,249,250)),FontSize=14};Grid.SetRow(summary,2);root.Children.Add(summary);
        var lower=new Grid();lower.ColumnDefinitions.Add(new(){Width=new GridLength(3,GridUnitType.Star)});lower.ColumnDefinitions.Add(new(){Width=new GridLength(2,GridUnitType.Star)});Grid.SetRow(lower,3);root.Children.Add(lower);
        trades=new DataGrid{AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeaderWidth=0,Margin=new Thickness(0,0,10,0)};
        detail=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Text="거래 행이나 차트의 ▲/▼ 표식을 선택하면 당시 판단과 Support Cluster를 표시합니다."};Grid.SetColumn(detail,1);lower.Children.Add(detail);
        trades.Columns.Add(Column("한국시간",nameof(ScreeningChartTrade.TimeText),170));trades.Columns.Add(Column("구분",nameof(ScreeningChartTrade.Side),160));trades.Columns.Add(Column("가격",nameof(ScreeningChartTrade.PriceText),100));trades.Columns.Add(Column("수량",nameof(ScreeningChartTrade.QuantityText),80));trades.Columns.Add(Column("출처",nameof(ScreeningChartTrade.Source),190));trades.Columns.Add(Column("사유",nameof(ScreeningChartTrade.Reason),new DataGridLength(1,DataGridLengthUnitType.Star)));trades.SelectionChanged+=(_,_)=>{if(trades.SelectedItem is ScreeningChartTrade row){detail.Text=row.Detail;chart.FocusTrade(row.Time,row.Buy);}};lower.Children.Add(trades);
        var footer=new DockPanel{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footer,4);root.Children.Add(footer);var close=new Button{Content="닫기",MinWidth=90};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Right);footer.Children.Add(close);
        var next=new Button{Content="다음 종목",MinWidth=100,Style=(Style?)TryFindResource("Secondary")};next.Click+=async(_,_)=>await Navigate(1);DockPanel.SetDock(next,Dock.Right);footer.Children.Add(next);
        var previous=new Button{Content="이전 종목",MinWidth=100,Margin=new Thickness(0,0,8,0),Style=(Style?)TryFindResource("Secondary")};previous.Click+=async(_,_)=>await Navigate(-1);DockPanel.SetDock(previous,Dock.Right);footer.Children.Add(previous);
        var reload=new Button{Content="다시 실행",MinWidth=100,Style=(Style?)TryFindResource("Secondary")};reload.Click+=async(_,_)=>await Navigate(0);footer.Children.Add(reload);
        ViewModel.PropertyChanged+=(_,e)=>{if(e.PropertyName is null or nameof(ViewModel.DisplaySeries))ApplySeries();};
        quoteTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};quoteTimer.Tick+=(_,_)=>ViewModel.RefreshQuote();
        Loaded+=(_,_)=>{if(ViewModel.IsRealtime)quoteTimer.Start();};
        Closed+=(_,_)=>{generation++;quoteTimer.Stop();ViewModel.Dispose();navigationCancellation?.Cancel();navigationCancellation?.Dispose();};ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(ScreeningChartSnapshot snapshot)
    {
        Snapshot=snapshot;Title=snapshot.Stock.Name+(snapshot.Run.Status=="실시간 감시 스냅샷"?" · 실시간 전략 차트":" · 전략 백테스트");heading.Text=snapshot.Stock.Name+"  "+snapshot.Stock.Symbol;
        source.Text=$"{snapshot.Run.StrategyName} · 스크리닝 당시 설정 사용 · 기간 {snapshot.Run.From:yyyy-MM-dd} ~ {snapshot.Run.To:yyyy-MM-dd}"+(snapshot.Notice.Length>0?"\n"+snapshot.Notice:"");
        ViewModel.SetSnapshot(snapshot);if(IsLoaded&&ViewModel.IsRealtime)quoteTimer.Start();else quoteTimer.Stop();trades.ItemsSource=snapshot.Trades;detail.Text="거래 행이나 확대된 차트의 ▲/▼ 표식을 선택하면 당시 판단과 Support Cluster를 표시합니다.";
        var m=snapshot.Result?.Performance;summary.Text=m==null?"지정 봉 스크리닝은 현재 신호 평가 결과만 표시합니다. 일봉 백테스트 결과가 없습니다.":$"완결 거래 {m.Trades:N0}회 · 승률 {Percent(m.WinRate)} · 누적 수익률 {((m.FinalEquity-snapshot.Result!.Configuration.InitialCapital)/snapshot.Result.Configuration.InitialCapital*100):N2}% · 평균 수익 {Percent(m.AverageWinPercent)} · 평균 손실 {Percent(m.AverageLossPercent)} · 평균 보유 {m.AverageHoldingDays?.ToString("N2")??"미산출"}일 · Profit Factor {(m.InfiniteProfitFactor?"∞":m.ProfitFactor?.ToString("N2")??"미산출")} · Expectancy {m.Expectancy?.ToString("N0")??"미산출"}원 · MDD {m.MaxDrawdown:N2}% · Sharpe {m.Sharpe?.ToString("N2")??"미산출"}";
        ApplySeries();
        static string Percent(decimal? value)=>value is {} number?number.ToString("N2")+"%":"미산출";
    }
    private void ApplySeries(){var series=ViewModel.DisplaySeries;chart.DisplayBarCount=Math.Clamp(series.Bars.Length,25,2000);chart.Bars=series.Bars;chart.Period=series.Period;chart.TradeMarkers=Snapshot.Markers;chart.SupportZones=Snapshot.SupportZones;chart.EnableScreeningReviewMode(500);}
    private async Task Navigate(int delta)
    {
        if(navigate==null)return;navigationCancellation?.Cancel();navigationCancellation?.Dispose();navigationCancellation=new();var token=navigationCancellation.Token;var mine=++generation;source.Text="백테스트 자료를 불러오는 중...";
        try{var next=await navigate(delta,token);if(next!=null&&mine==generation&&!token.IsCancellationRequested)ApplySnapshot(next);}catch(OperationCanceledException){}catch(InvalidOperationException ex){if(mine==generation)source.Text=ex.Message;}catch(Exception){if(mine==generation)source.Text="차트 자료 또는 저장소 읽기 오류입니다. 기존 차트와 데이터는 유지됩니다.";}
    }
    private void SelectTrade(DateTimeOffset time,bool buy)
    {var match=Snapshot.Trades.OrderBy(t=>Math.Abs((t.Time-time).Ticks)).FirstOrDefault(t=>t.Buy==buy);if(match!=null){trades.SelectedItem=match;trades.ScrollIntoView(match);}}
    private static DataGridTextColumn Column(string title,string path,double width)=>new(){Header=title,Binding=new Binding(path),Width=width};
    private static DataGridTextColumn Column(string title,string path,DataGridLength width)=>new(){Header=title,Binding=new Binding(path),Width=width};
}
