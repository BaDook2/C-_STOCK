using ScottPlot;
using Stock.Core.Research;
namespace Stock.Desktop;
public partial class StockChart
{
 private void ConfigureAppearance()
 {
  foreach(var plot in new[]{PricePlot.Plot,VolumePlot.Plot})
  {
   plot.Axes.Frame(false);plot.Axes.Color(Color.FromHex("8B95A1"));plot.Axes.Left.IsVisible=false;plot.Axes.Top.IsVisible=false;plot.Axes.Right.IsVisible=true;plot.Axes.Right.MinimumSize=74;plot.Axes.Right.TickLabelStyle.FontSize=11;plot.Axes.Bottom.TickLabelStyle.FontSize=11;plot.Grid.XAxisStyle.IsVisible=false;plot.Grid.YAxisStyle.MajorLineStyle.Color=Color.FromHex("F1F3F5");plot.Axes.Right.MajorTickStyle.Length=0;plot.Axes.Bottom.MajorTickStyle.Length=0;plot.Axes.Left.MajorTickStyle.Length=0;
  }
  PricePlot.Plot.Axes.Bottom.IsVisible=false;VolumePlot.Plot.Grid.IsVisible=false;VolumePlot.Plot.Axes.Right.TickGenerator=new ScottPlot.TickGenerators.NumericManual();
  PricePlot.Plot.Layout.Fixed(new PixelPadding(left:6,right:82,top:8,bottom:8));VolumePlot.Plot.Layout.Fixed(new PixelPadding(left:6,right:82,top:0,bottom:22));
  if(VolumePlot.Plot.Axes.Bottom.TickGenerator is ScottPlot.TickGenerators.DateTimeAutomatic dates){dates.MaxTickCount=6;dates.LabelFormatter=dt=>dt.ToString(ChartPeriods.Intraday(Period)?"MM.dd HH:mm":"yy.MM.dd");}
 }
 private void SynchronizeAppearance()
 {
  var plot=PricePlot.Plot;var limits=plot.Axes.GetLimits();plot.Axes.Right.Min=limits.Bottom;plot.Axes.Right.Max=limits.Top;
  if(!double.IsFinite(limits.Top-limits.Bottom)||limits.Top<=limits.Bottom)return;
  var positions=Enumerable.Range(0,5).Select(i=>limits.Bottom+(limits.Top-limits.Bottom)*i/4).ToArray();var labels=positions.Select(v=>Currency=="USD"?v.ToString("0.########",System.Globalization.CultureInfo.InvariantCulture):Math.Floor(v).ToString("N0")).ToArray();plot.Axes.Right.SetTicks(positions,labels);plot.Axes.Left.SetTicks(positions,labels);
 }
 private void AddIndicators(PriceBar[] visible)
 {
  // Chart warm-up never modifies the strategy input or a stored backtest snapshot.
  var history=IndicatorBars is {Length:>0} warm&&warm[0].Time<=visible[0].Time?warm:visible;
  var index=history.Select((b,i)=>(b.Time,i)).ToDictionary(x=>x.Time,x=>x.i);
  RenderedMa240=false;indicatorExtents.Clear();
  if(ShowMovingAverages)foreach(var (period,hex) in new[]{(5,"FFA132"),(20,"8E5EEF"),(60,"22A68C"),(120,"E45D83"),(240,"000000")})
  {
   if(period==240&&DailyBars is not {Length:>0}&&Period!="일봉")continue;
   var indicatorHistory=period==240&&DailyBars is {Length:>0}?DailyBars:history;
   var values=new Dictionary<DateTimeOffset,double>();decimal sum=0;
   for(var i=0;i<indicatorHistory.Length;i++){sum+=indicatorHistory[i].Close;if(i>=period)sum-=indicatorHistory[i-period].Close;if(i>=period-1)values[indicatorHistory[i].Time]=(double)(sum/period);}
   if(period==240&&DailyBars is {Length:>0})
   {
       var dailyValues=values.OrderBy(v=>v.Key).ToArray();values=new();var cursor=0;double? prior=null;
       foreach(var bar in visible){while(cursor<dailyValues.Length&&(ChartPeriods.Intraday(Period)?dailyValues[cursor].Key.Date<bar.Time.Date:dailyValues[cursor].Key.Date<=bar.Time.Date)){prior=dailyValues[cursor++].Value;}if(prior.HasValue)values[bar.Time]=prior.Value;}
   }
   var bars=visible.Where(b=>values.ContainsKey(b.Time)).ToArray();if(bars.Length<2)continue;var series=PricePlot.Plot.Add.SignalXY(bars.Select(b=>TimeCoordinate(b.Time)).ToArray(),bars.Select(b=>values[b.Time]).ToArray());series.Color=Color.FromHex(hex);series.LineWidth=period==240?1.5f:1;if(period==240){RenderedMa240=true;indicatorExtents.AddRange(bars.Select(b=>(TimeCoordinate(b.Time),(decimal)values[b.Time])));}
  }
  if(!ShowCloud)return;
  double? Mid(int end,int length)=>end+1<length?null:(double)(history.Skip(end-length+1).Take(length).Max(b=>b.High)+history.Skip(end-length+1).Take(length).Min(b=>b.Low))/2;
  var xs=new List<double>();var leadingA=new List<double>();var leadingB=new List<double>();
  foreach(var bar in visible)
  {
   if(!index.TryGetValue(bar.Time,out var at)||at<26)continue;var conversion=Mid(at-26,9);var standard=Mid(at-26,26);var span=Mid(at-26,52);if(conversion==null||standard==null||span==null)continue;xs.Add(TimeCoordinate(bar.Time));leadingA.Add((conversion.Value+standard.Value)/2);leadingB.Add(span.Value);
  }
  if(xs.Count<2)return;
  // Leading spans are shifted into existing bars only; no empty future candle slots.
  for(var i=1;i<xs.Count;i++){var shape=PricePlot.Plot.Add.Polygon([new(xs[i-1],leadingA[i-1]),new(xs[i],leadingA[i]),new(xs[i],leadingB[i]),new(xs[i-1],leadingB[i-1])]);shape.FillColor=Color.FromHex(leadingA[i]>=leadingB[i]?"F04452":"3182F6").WithAlpha(.06);shape.LineWidth=0;}
  var a=PricePlot.Plot.Add.SignalXY(xs.ToArray(),leadingA.ToArray());a.Color=Color.FromHex("F18499");a.LineWidth=1;var b=PricePlot.Plot.Add.SignalXY(xs.ToArray(),leadingB.ToArray());b.Color=Color.FromHex("8DB8FF");b.LineWidth=1;
 }
}
