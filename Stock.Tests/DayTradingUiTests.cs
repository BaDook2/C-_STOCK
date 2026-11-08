using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void DayTradingUiChecks(string output)
    {
        Directory.CreateDirectory(output);SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app=new Stock.App.App{ManualWindowStartup=true};app.InitializeComponent();
        using var store=new TradingStore(Path.Combine(Dir,"D-ui.db"),true);var before=new OversoldConfig();store.State("oversold-config-v1",before);
        var definition=StrategyDTestConfig();definition.Configuration.PlannedInvestment=100000;
        var file=Path.Combine(output,"strategy-d-fixture.json");File.WriteAllText(file,StrategyDefinitionJson.Export(definition));var files=new AuditFileDialogs(file);var alerts=new List<string>();
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var stock=new StockItem("005930","삼성전자",Exchange.KRX,"005930");
        decimal[] prices=[10100,9900,9920,9950,9950,10100,10120,10160,10180,10100,10090];
        var bars=prices.Select((p,i)=>new StrategyBar(new(open.AddMinutes(i+1),p,p,p,p,10000),open.AddMinutes(i),open.AddMinutes(i+1),i+1,p*10000,"OHLC_FIXTURE")).ToArray();
        PreparedDayTrading[] Prepared(StrategyDefinition config)=>[new(stock,new(new(stock.Symbol,stock.Exchange),config,StrategyDSeed(open),"D"),bars,[])];
        var modelClock=open.AddMinutes(20);var paperFeed=new ControlledDayFeed();var model=new DayTradingViewModel(store,files,alerts.Add,(_,config,_,_,_,_,_)=>Task.FromResult(Prepared(config)),_=>paperFeed,()=>[stock],()=>modelClock){StartDate=open.Date,EndDate=open.Date,SeedCash="200000"};
        void Execute(string key){model.Commands[key].Execute(null);PumpUntil(()=>!model.Busy);}
        Execute("불러오기");Execute("저장");Check(store.State<StrategyDefinition[]>("daytrading-definitions-v2")?.Single().IsStrategyD==true,"D popup imports and stores a schema-2 preset in its own namespace");
        Check(Serialization.Pack(store.State<OversoldConfig>("oversold-config-v1"))==Serialization.Pack(before),"D import/save leaves the legacy 61-parameter strategy byte-for-byte unchanged");
        var dialog=new DayTradingDialog(model,0){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};dialog.Show();dialog.UpdateLayout();
        model.Advanced=true;dialog.UpdateLayout();
        Check(Descendants(dialog).OfType<TextBox>().Any(t=>t.Text=="0.015"),"actual WPF D editor renders 0.015 without changing it to 0.02");
        Check(Descendants(dialog).OfType<ComboBox>().Any(c=>c.Items.Count>2),"D editor renders real enum choice containers for timeframes and policies");
        var pages=(TabControl)dialog.FindName("Pages");pages.SelectedIndex=1;dialog.UpdateLayout();Execute("연구");dialog.UpdateLayout();
        Check(alerts.Count==0&&model.Latest is {Signals.Length:1,Fills.Length:4},"WPF D research command uses the common engine and returns one signal plus four confirmed fills");
        Check(model.Markers.Length==4&&model.Zones.Length==1&&model.VisibleFills.Count==4,"D result projection keeps exact fill timestamps and frozen entry support zone");
        Check(store.States("daytrading-history:").Count==1&&store.States("daytrading-result-definition:").Count==1,"D research persists execution history and its exact configuration snapshot without raw ticks");
        var chart=Descendants(dialog).OfType<CandleChart>().First(c=>c.IsVisible);var chartImage=new RenderTargetBitmap(Math.Max(1,(int)chart.ActualWidth),Math.Max(1,(int)chart.ActualHeight),96,96,PixelFormats.Pbgra32);chartImage.Render(chart);
        Check(chart.RenderedTradeMarkers.Count>0&&chart.Bars?.Count==11,"D candle chart actually renders BUY/SELL markers for the complete selected bar interval");
        var hit=chart.RenderedTradeMarkers[0];chart.SelectMarkerAtPoint(new Point(hit.Bounds.Left+2,hit.Bounds.Top+hit.Bounds.Height/2));dialog.UpdateLayout();
        Check(Descendants(dialog).OfType<DataGrid>().Any(g=>g.SelectedItem is IntradayFillRow),"single-click D chart marker selects its confirmed fill row and evidence detail");
        var bitmap=new RenderTargetBitmap((int)dialog.ActualWidth,(int)dialog.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(dialog);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(Path.Combine(output,"strategy-d-research.png")))png.Save(stream);
        pages.SelectedIndex=2;dialog.UpdateLayout();Check(Descendants(dialog).OfType<CheckBox>().Any(c=>c.Content?.ToString()=="신규·추가매수 허용"&&c.IsChecked==false),"D paper execution defaults new-entry authorization to OFF");
        Execute("가상시작");Check(model.Running&&alerts.Count==0,"actual D popup starts its background paper service against a controlled market feed");
        dialog.Close();Check(model.Running&&model.Latest?.Fills.Length==4,"closing the D popup preserves application-owned live session and research results");model.DisposeAsync().AsTask().GetAwaiter().GetResult();Check(paperFeed.Disposed,"application disposal closes the paper feed after popup closure");app.Shutdown();
    }
}
