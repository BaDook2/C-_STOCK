using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
namespace Stock.App;
public sealed class ScreeningResultDialog:Window
{
    public ScreeningResultDialog(Func<OversoldScreeningRun?> read,Func<StockItem,string> trades,Func<StockItem,OversoldScreeningRun,Task>? open=null)
    {
        Title="스크리닝 결과";Width=1180;Height=800;MinWidth=760;MinHeight=500;FontSize=14;Background=System.Windows.Media.Brushes.White;
        var root=new DockPanel{Margin=new Thickness(20),Background=System.Windows.Media.Brushes.White};Content=root;var heading=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var tabs=new TabControl();root.Children.Add(tabs);
        var funnelPanel=new DockPanel();
        var funnelHelp=new TextBlock
        {
            Text="자료부족: 해당 단계에 필요한 과거 봉·종목 상태·계산값을 확보하지 못함  ·  미평가: 아직 처리 전이거나 이전 단계의 탈락·자료 부족으로 뒤 조건을 실행하지 않음",
            TextWrapping=TextWrapping.Wrap,Foreground=System.Windows.Media.Brushes.SlateGray,Margin=new Thickness(10,10,10,12),
            ToolTip="진행 중에는 아직 처리하지 않은 종목도 미평가에 포함됩니다. 한 종목에서 최초 자료 결손은 자료부족, 그 뒤 실행하지 않은 조건은 미평가로 함께 집계될 수 있습니다."
        };
        DockPanel.SetDock(funnelHelp,Dock.Top);funnelPanel.Children.Add(funnelHelp);
        var funnel=new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false};funnel.AutoGeneratingColumn+=(_,e)=>e.Column.Width=e.PropertyName=="조건"?new DataGridLength(210):new DataGridLength(90);funnelPanel.Children.Add(funnel);tabs.Items.Add(new TabItem{Header="단계별 집계",Content=funnelPanel});
        var grid=new Grid();grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        var openButton=new Button{Content="백테스트 보기",IsEnabled=false,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,0,8),ToolTip="선택한 종목을 실행 당시 설정으로 백테스트해 새 창에서 봅니다."};grid.Children.Add(openButton);
        var symbols=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false};Grid.SetRow(symbols,1);grid.Children.Add(symbols);
        foreach(var (title,path) in new[]{("코드","Stock.Symbol"),("종목","Stock.Name"),("현재가","Price"),("상태","Status")})symbols.Columns.Add(new DataGridTextColumn{Header=title,Binding=new System.Windows.Data.Binding(path),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        var details=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,AcceptsReturn=true};Grid.SetRow(details,2);grid.Children.Add(details);tabs.Items.Add(new TabItem{Header="종목별 조건·체결",Content=grid});
        symbols.SelectionChanged+=(_,_)=>{openButton.IsEnabled=open!=null&&symbols.SelectedItem!=null;if(symbols.SelectedItem is not ScreeningObservation row)return;details.Text="기간 내 기술 후보일: "+(row.MatchingDates.Length==0?"없음":string.Join(", ",row.MatchingDates))+"\n"+string.Join("\n",row.Evaluation?.Conditions.Select(c=>$"{(c.Diagnostic?"[독립 진단] ":"")}{c.Condition}: {c.Outcome} · 계산 {c.Value} · 기준 {c.Threshold} · {c.Reason}")??[row.Status])+"\n\n"+trades(row.Stock);};
        OversoldScreeningRun? shown=null;
        symbols.MouseDoubleClick+=async(_,e)=>{if(open==null||shown==null||e.OriginalSource is not DependencyObject origin||ItemsControl.ContainerFromElement(symbols,origin) is not DataGridRow {DataContext:ScreeningObservation row})return;e.Handled=true;await open(row.Stock,shown);};
        openButton.Click+=async(_,_)=>{if(open!=null&&shown!=null&&symbols.SelectedItem is ScreeningObservation row)await open(row.Stock,shown);};
        var snapshot=new TextBox{IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};tabs.Items.Add(new TabItem{Header="실행 당시 설정",Content=snapshot});
        void Refresh(){var run=read();if(run==null||ReferenceEquals(run,shown))return;shown=run;heading.Text=$"{run.ScreeningTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss} · {run.BarSpecification?.ToString()??"일봉"} · {run.Status} · 기간 {run.From?.ToString()??"미설정"} ~ {run.To?.ToString()??"미설정"}\n검사 {run.Results.Length:N0}/{run.Targets.Length:N0} · 최종 후보 {run.FinalCandidates.Length:N0} · 사용 안함은 통과 유지, 지지 유형 3개는 독립 진단입니다.";funnel.ItemsSource=run.Funnel().Select(r=>new{조건=r.Condition,통과=r.Passed,실패=r.Failed,미평가=r.NotEvaluated,자료부족=r.MissingData,사용안함=r.Disabled,독립진단=r.Diagnostic}).ToArray();symbols.ItemsSource=run.Results;snapshot.Text=string.Join("\n",Stock.Core.Trading.ParameterCatalog.Properties(run.Configuration).Select(p=>Stock.Core.Trading.ParameterCatalog.For(p).DisplayName+": "+NumericDisplay.Format(p.GetValue(run.Configuration))));}
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(_,_)=>Refresh();Closed+=(_,_)=>timer.Stop();Refresh();timer.Start();
    }
}
public static class DecisionSnapshotText
{
    public static string Format(OversoldDecisionSnapshot? s)
    {
        if(s==null)return "이전 기록 · 판단 당시 세부 snapshot 없음";
        var text=$"판단 가격 {s.SignalPrice:N2} · 급락 {s.Indicators.MediumDrawdown:N2}% · RSI {s.Indicators.Rsi:N2} · 거래량 {s.Indicators.VolumeRatio:N2}배\n";
        text+=string.Join("\n",s.Clusters.Select(c=>$"군집 {c.Lower:N2} / {c.Center:N2} / {c.Upper:N2}, 지지 {c.Count:N0}, 진입 사용 {c.UsedForEntry}\n"+string.Join("\n",c.Supports.Select(p=>$"  {p.Type}: {p.Lower:N2} ~ {p.Upper:N2} (중심 {p.Center:N2})"))));
        if(s.AdditionalEntry is {} a)text+=$"\n추가매수: 직전 체결 {a.PreviousFill:N2}, 낙폭 {a.DropPercent:N2}%, 기준 {a.Threshold:N2}, 판단가 {a.JudgmentPrice:N2}, 가격 통과 {a.PricePass}, 지지 통과 {a.SupportPass}, 최종 {a.FinalDecision}";
        if(s.PositionRules is {} p)text+=$"\n지지 이탈: 최초 하단 {p.FirstSupportLower:N2}, 비율 {p.SupportBreakPercent:N2}%, stop {p.SupportStop:N2}, 판단가 {p.JudgmentPrice:N2}\n추적: 평균 {p.Average:N2}, 이전 peak {p.PriorPeak:N2}, 비율 {p.TrailingPercent:N2}%, stop {p.TrailingStop:N2}, 활성 {p.TrailingActive}, 청산 {p.Exit??"없음"}";
        return text;
    }
}
