using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Input;
using Stock.Core.Research;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
 private static void LivePublicChecks()
 {
  using var services=new AppServices(Path.Combine(output,"isolated-live",Guid.NewGuid().ToString("N")),brokerFactory:(_,_)=>throw new InvalidOperationException("Public verification cannot connect a broker"));using var model=new MainViewModel(services);var notices=new List<string>();model.NotificationRequested+=notices.Add;var window=new MainWindow{DataContext=model,Width=1540,Height=1040};window.Show();Dispatcher.UIThread.RunJobs();
  foreach(var (region,query,symbol) in new[]{("국내","삼성전자","005930"),("국내","SK하이닉스","000660"),("해외","애플","AAPL")})
  {
   model.Region=region;Dispatcher.UIThread.RunJobs();var text=window.FindControl<TextBox>("StockSearch")!;text.Focus();text.SelectAll();window.KeyTextInput(query);Dispatcher.UIThread.RunJobs();Check(model.Query==query,"real search TextBox commits "+query+" to its view model");var timer=Stopwatch.StartNew();
   if(symbol=="000660"){window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r");window.KeyRelease(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r");}else Click(window,window.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="이름으로 검색"));
   Pump(()=>!model.Busy,65);var chart=window.GetVisualDescendants().OfType<StockChart>().Single();
   Console.WriteLine($"LIVE {query}: {timer.ElapsedMilliseconds}ms; results={model.SearchResults.Count}; bars={model.ChartBars.Length}; rendered={chart.RenderedCandles}; status={model.Status}");
   foreach(var message in notices)Console.WriteLine("NOTICE: "+message);notices.Clear();
   Check(model.SelectedStock?.Symbol==symbol&&model.ChartBars.Length>0&&chart.RenderedCandles==model.ChartBars.Length,"live public "+query+" search reaches rendered candles through the actual Avalonia view");
   using var frame=window.CaptureRenderedFrame();frame!.Save(Path.Combine(output,model.SelectedStock!.Symbol+"-live-chart.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
  }
  model.Region="국내";model.Query="삼성전자";model.SearchCommand.Execute(null);Pump(()=>!model.Busy,60);Check(model.SelectedStock?.Symbol=="005930","live return to a cached domestic stock resets the correct chart");
  foreach(var range in model.Ranges.Where(r=>r.Months>0)){model.SelectedRange=range;Dispatcher.UIThread.RunJobs();Check(model.ChartBars.Length>0&&model.ChartBars.All(b=>b.Open>0&&b.Close>0),"live daily chart range "+range.Label+" preserves valid candles");}
  model.Strategies.Selected=StrategyDiagnosticPreset.HoldOneDayName;model.Research.From=DateTime.Today.AddMonths(-1);model.Research.To=DateTime.Today;model.Research.Run.Execute(null);Pump(()=>!model.Research.Busy,60);Check(model.Research.Result is {} research&&research.Result.Decisions.Count>0&&research.Trades.Length>0,"real Samsung daily history produces decisions and executed backtest fills");Check(model.ChartMarkers.Length>0&&model.History.Any(h=>h.Entry.Kind==DesktopRunKind.Backtest),"real-data backtest renders markers and archives its reproducible result");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
  model.Recurring.From=DateTime.Today.AddMonths(-3);model.Recurring.To=DateTime.Today;model.Recurring.Run.Execute(null);Pump(()=>model.History.Any(h=>h.Entry.Kind==DesktopRunKind.Recurring)||notices.Count>0,60);Check(model.History.Any(h=>h.Entry.Kind==DesktopRunKind.Recurring),"recurring backtest completes using the same real public price history");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();notices.Clear();
  model.Screening.Market="코스피";model.Screening.TopCount=10;model.Screening.ShowAll=true;model.Screening.To=DateTime.Today;model.Screening.Start.Execute(null);Pump(()=>!model.Screening.Busy,90);Console.WriteLine("SCREEN: "+model.Screening.Status);Check(model.Screening.Snapshot is {Results.Length:>0} run&&run.Results.Length==run.Targets.Length,"real KOSPI top10 screening evaluates every requested target");Check(model.Screening.Rows.Any(r=>r.Price!="—"),"real screening has observed prices instead of silently marking every stock missing");
  model.Screening.Selected=model.Screening.Rows.First(r=>r.Price!="—");model.Screening.OpenResult.Execute(null);Pump(()=>window.OwnedWindows.Any(w=>w.Content is ResearchBrowserPanel)||notices.Count>0,60);Check(window.OwnedWindows.Any(w=>w.Content is ResearchBrowserPanel),"real screening result opens the shared backtest chart popup");foreach(var owned in window.OwnedWindows.ToArray())owned.Close();
  model.RefreshMarket.Execute(null);Pump(()=>!model.Busy,90);foreach(var row in model.MarketIndices)Console.WriteLine("INDEX "+row.Code+": "+row.Price+"; "+row.State);Check(model.MarketIndices.All(i=>i.Price!="—"&&i.Points.Length>0),"all five real public index charts supply a quote and history");
  IReadOnlyList<StockItem> Universe(string name,int count){var task=services.MarketData.Universe(name,count,CancellationToken.None);Pump(()=>task.IsCompleted,90);return task.GetAwaiter().GetResult();}
  var kosdaq=Universe("코스닥",10);Check(kosdaq.Count>0&&kosdaq.All(s=>s.Board=="KOSDAQ"),"live KOSDAQ top selection never leaks KOSPI symbols");
  var sp=Universe("S&P500",10);var nq=Universe("나스닥100",10);var union=Universe("S&P500+나스닥100",10);Console.WriteLine($"UNIVERSE S&P500={sp.Count}; Nasdaq100={nq.Count}; union={union.Count}");Check(sp.Count>450&&sp.Any(s=>s.Symbol=="AAPL"),"real S&P500 constituents include Apple");Check(nq.Count>=90&&nq.Any(s=>s.Symbol=="AAPL"),"real Nasdaq100 constituents include Apple");Check(union.Select(s=>s.Key).Distinct().Count()==union.Count&&union.Count>=Math.Max(sp.Count,nq.Count),"combined overseas screening universe removes overlapping symbols");
  if(DateTime.Today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday){notices.Clear();model.Realtime.Start.Execute(null);Pump(()=>!model.Realtime.Busy,30);Check(!model.Realtime.Running&&notices.Any(n=>n.Contains("장 외 시간")),"real weekend realtime start shows a closed-market notice and starts no trading");foreach(var popup in window.OwnedWindows.ToArray())popup.Close();}
  model.ToggleWatch.Execute(null);Check(model.WatchGroups.Any(g=>g.Stocks.Any(s=>s.Symbol=="005930")),"real searched stock can be stored in an isolated watch group");model.EmergencyCommand.Execute(null);Check(!model.Busy&&!model.Realtime.Running,"public-data workflow emergency stops query and realtime work");window.Close();Console.WriteLine($"PASS: {passed} live-public checks; isolated DB, no account keys or orders");
 }
 private static void Click(Window window,Control control)
 {var point=control.TranslatePoint(new(control.Bounds.Width/2,control.Bounds.Height/2),window)??throw new InvalidOperationException("Control is not attached");window.MouseMove(point);window.MouseDown(point,MouseButton.Left);window.MouseUp(point,MouseButton.Left);Dispatcher.UIThread.RunJobs();}
}
