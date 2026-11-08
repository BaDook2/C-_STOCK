using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Stock.Core.Research;
using Stock.Desktop;

internal static partial class Program
{
    private static void TradeOutcomeColorChecks()
    {
        var time=new DateTimeOffset(2026,10,1,15,30,0,TimeSpan.FromHours(9));
        var buy=new TradeMarker("buy","KRX:005930",time,true,1,100,"FIXTURE",TradeNumber:1);
        var win=buy with{Id="win",Buy=false,Time=time.AddDays(2),Reason="EmergencyStop",RealizedPnl=12};
        var loss=win with{Id="loss",Time=time.AddDays(27),Reason="TP1",RealizedPnl=-30};
        Check(TradeMarkerPalette.ColorFor([win])==TradeMarkerPalette.Profit,"a profitable exit is red even when its reason is a stop");
        Check(TradeMarkerPalette.ColorFor([loss])==TradeMarkerPalette.Loss,"a losing exit is blue even when its reason is TP1");
        Check(TradeMarkerPalette.ColorFor([buy,win])==TradeMarkerPalette.Profit&&TradeMarkerPalette.ColorFor([buy,win,loss])==TradeMarkerPalette.Loss,"collapsed buy/sell groups use the complete realized net outcome");
        Check(TradeMarkerPalette.ColorFor([win with{RealizedPnl=0}])==TradeMarkerPalette.Neutral&&TradeMarkerPalette.ColorFor([win with{RealizedPnl=null,Reason="TrailingStop"}])==TradeMarkerPalette.Neutral,"breakeven and unknown trailing outcomes never invent a profit or loss");
        Check(TradeMarkerPalette.ColorFor([win with{RealizedPnl=null,Reason="TP1"}])==TradeMarkerPalette.Profit&&TradeMarkerPalette.ColorFor([win with{RealizedPnl=null}])==TradeMarkerPalette.Loss,"known exit intent retains red/blue when its realized amount is unavailable");
        var unknownGroup=ChartMarkerLayout.Build([buy,win with{RealizedPnl=null}],_=>1,0,30,1100).Single();Check(unknownGroup.Caption.Contains("미확인")&&!unknownGroup.Caption.Contains("본전"),"unknown grouped realized amounts are not displayed as zero");
        var chart=new StockChart{Bars=Enumerable.Range(0,32).Select(i=>new PriceBar(time.AddDays(i),100,110,90,101,1000)).ToArray(),Markers=[win,loss]};
        var window=new Window{Width=1100,Height=700,Content=chart};window.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var badges=chart.FindControl<Canvas>("TradeLayer")!.Children.OfType<Border>().ToArray();
        Check(badges.Length==2&&badges.All(b=>b.Child is TextBlock text&&text.Foreground is ISolidColorBrush ink&&b.BorderBrush is ISolidColorBrush border&&ink.Color==Color.Parse(((ChartMarkerGroup)b.Tag!).Trades[0].Id=="win"?TradeMarkerPalette.Profit:TradeMarkerPalette.Loss)&&border.Color==ink.Color),"actual shared chart renders both winning and losing captions/borders with the requested colors");
        chart.OverviewNavigation=true;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();Check(chart.RenderedMarkers==2,"locked screening overview retains profit/loss annotations");window.Close();
    }
}
