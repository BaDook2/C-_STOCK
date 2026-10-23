using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

/// <summary>Read-only audit presentation; calculations remain in the core backtest.</summary>
public sealed class OversoldResultDialog:Window
{
    public OversoldResultDialog(OversoldResult r)
    {
        Title="국내 과매도·낙주 상세 기록 · "+r.Stock.Name;Width=1060;Height=800;MinWidth=720;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brushes.White;
        var root=new DockPanel{Margin=new Thickness(20),Background=Brushes.White};Content=root;
        var heading=new TextBlock{Text=$"{r.Stock.Name} · {r.From:yyyy.MM.dd} ~ {r.To:yyyy.MM.dd}\n{r.Source}",FontSize=19,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14)};DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var close=new Button{Content="닫기",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,MinWidth=90,Margin=new Thickness(0,12,0,0)};close.Click+=(_,_)=>Close();DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
        var tabs=new TabControl();root.Children.Add(tabs);
        string N(decimal? v)=>v?.ToString("N2")??"미산출";string Time(DateTimeOffset t)=>t.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm");
        FrameworkElement Text(string text)=>new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,VerticalContentAlignment=VerticalAlignment.Top,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(14),BorderThickness=new Thickness(0)};
        void Tab(string title,object content)=>tabs.Items.Add(new TabItem{Header=title,Content=content});
        var m=r.Performance;Tab("성과",Text($"완결 거래 {m.Trades:N0}회 / 미청산 {m.OpenTrades:N0}회\n승률 {N(m.WinRate)}%\n평균 수익률 {N(m.AverageWinPercent)}% / 평균 손실률 {N(m.AverageLossPercent)}%\n평균 보유기간 {N(m.AverageHoldingDays)} 시장 거래일\nProfit Factor {(m.InfiniteProfitFactor?"∞":N(m.ProfitFactor))}\n기대 손익 {m.Expectancy?.ToString("N0")??"미산출"}원 / 기대 수익률 {N(m.ExpectancyPercent)}%\nCAGR {N(m.Cagr)}% / MDD {N(m.MaxDrawdown)}% / Sharpe {N(m.Sharpe)}\n최종 평가 {m.FinalEquity:N0}원 / 현금 {r.Cash:N0}원 / 보유 {r.Quantity:N0}주\n미체결 대기 {(r.PendingOrderAtEnd?"있음":"없음")}\n\n"+string.Join("\n",r.Notices)));
        var tradePanel=new Grid();tradePanel.ColumnDefinitions.Add(new(){Width=new GridLength(235)});tradePanel.ColumnDefinitions.Add(new());var list=new ListBox{ItemsSource=r.Trades.Select((t,i)=>$"{i+1:N0}. {Time(t.FirstExecutionDate)}\n{(t.Closed?t.ExitReason:"미청산")} · {t.RealizedPnl:N0}원").ToArray()};tradePanel.Children.Add(list);var detail=(TextBox)Text("거래를 선택하면 진입 근거와 모든 체결을 볼 수 있습니다.");Grid.SetColumn(detail,1);tradePanel.Children.Add(detail);
        list.SelectionChanged+=(_,_)=>{if(list.SelectedIndex<0)return;var t=r.Trades[list.SelectedIndex];detail.Text=$"{t.Symbol} · 급락 {t.TriggerDate:yyyy.MM.dd} {t.TriggerType}\n중기 {t.MediumDrawdown:N2}% / 단기 {t.ShortDrawdown:N2}%\n신호 {Time(t.EntrySignalDate)} / 최초 체결 {Time(t.FirstExecutionDate)}\nRSI {N(t.EntryRsi)} / 거래량 배수 {N(t.VolumeRatio)}\n최초 cluster 하단 {t.Cluster.Lower:N0} / 중심 {t.Cluster.Center:N0} / 상단 {t.Cluster.Upper:N0} / 후보 {t.Cluster.Count:N0}개\n"+string.Join("\n",t.Cluster.Supports.Select(s=>$"  {s.Type} {s.Id}: {s.Lower:N0} ~ {s.Upper:N0}, 대표 {s.Center:N0}, 발생 {s.Occurred}, 확정 {s.Confirmed}"))+"\n\n"+string.Join("\n\n",t.Executions.Select(e=>$"{e.Reason} · 신호 {Time(e.SignalTime)} → 체결 {Time(e.ExecutionTime)}\n{(e.Buy?"매수":"매도")} {e.Quantity:N0}주 × {e.Price:N0}원 · 비용 {e.Fee:N0}원 · 평균가 {e.AveragePriceAfter:N0}원 · 실현 {e.RealizedPnl:N0}원\n{DecisionSnapshotText.Format(e.Snapshot)}"))+$"\n\n취득원가 {t.AcquisitionCost:N0}원 / 실현손익 {t.RealizedPnl:N0}원\n최종 수익률 {N(t.FinalReturnPercent)}% / 보유 {t.HoldingDays:N0}일 / 재진입 {t.Reentry}\n청산 이유 {t.ExitReason}";};Tab("거래 상세",tradePanel);if(r.Trades.Count>0)list.SelectedIndex=0;
        Tab("날짜별 판단",new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,ItemsSource=r.Decisions.Select(d=>new{날짜=d.Date.ToString(),상태=d.State.ToString(),판단=d.Reason,중기낙폭=N(d.Indicators.MediumDrawdown),단기낙폭=N(d.Indicators.ShortDrawdown),RSI=N(d.Indicators.Rsi),거래량배수=N(d.Indicators.VolumeRatio),반등저점=d.ReboundLow?.ToString("N0")??"없음"}).ToArray()});
        Tab("상태 전이",new DataGrid{IsReadOnly=true,AutoGenerateColumns=true,CanUserAddRows=false,ItemsSource=r.Transitions.Select(t=>new{한국시간=Time(t.Time),상태=t.State.ToString(),이유=t.Reason}).ToArray()});
        Tab("설정 스냅샷",Text(string.Join("\n",ParameterCatalog.Properties(r.Configuration).Select(p=>{var meta=ParameterCatalog.For(p);return $"{meta.DisplayName} ({p.Name}): {(meta.Unit=="%"&&p.GetValue(r.Configuration) is decimal number?number.ToString("N2"):NumericDisplay.Format(p.GetValue(r.Configuration)))} {meta.Unit}";}))+"\n\n시세 해시: "+r.DataHash+"\n시점 자료 해시: "+r.FactsHash));
    }
}
