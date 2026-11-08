using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Stock.App;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void AuditTableAlignment(FrameworkElement root,string path)
    {
        var summaries=new List<object>();
        foreach(var grid in Descendants(root).OfType<DataGrid>().Where(g=>g.IsVisible&&g.Items.Count>0))
        {
            var cells=Descendants(grid).OfType<DataGridCell>().Where(c=>c.IsVisible).ToArray();
            foreach(var cell in cells)
            {
                var host=Descendants(cell).OfType<ContentPresenter>().FirstOrDefault();if(host==null)continue;
                var center=host.TranslatePoint(new Point(host.ActualWidth/2,host.ActualHeight/2),cell);
                if(Math.Abs(center.X-cell.ActualWidth/2)>1.5||Math.Abs(center.Y-cell.ActualHeight/2)>1.5)throw new Exception($"Table alignment: {Path.GetFileName(path)} / {cell.Column?.Header} / delta {center.X-cell.ActualWidth/2:N2},{center.Y-cell.ActualHeight/2:N2}");
            }
            summaries.Add(new{Columns=grid.Columns.Select(c=>c.Header?.ToString()).ToArray(),Rows=grid.Items.Count,CheckedCells=cells.Length});
        }
        if(summaries.Count>0)File.WriteAllText(Path.ChangeExtension(path,".tables.json"),Serialization.Pack(summaries));
    }
    private static void CompactUiChecks(string output)
    {
        using var db=new TradingStore(Path.Combine(Dir,"compact-ui.db"));using var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>new ModeCacheBroker(),sessionDirectory:Path.Combine(Dir,"compact-sessions"));
        var window=new MainWindow(vm){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();vm.ActiveTab=2;
        for(var pass=0;pass<4;pass++){foreach(var e in Descendants(window).OfType<Expander>().ToArray())e.IsExpanded=true;window.UpdateLayout();}
        var panel=Descendants(window).OfType<OversoldParameterPanel>().Single();var cards=Descendants(panel).OfType<Border>().Where(b=>Equals(b.Tag,"ParameterCard")&&b.DataContext is ParameterField).ToArray();
        File.WriteAllText(Path.Combine(output,"card-layout.json"),Serialization.Pack(cards.Select(c=>new{Field=((ParameterField)c.DataContext).Metadata.Key,c.ActualHeight,c.ActualWidth,c.MinHeight})));Render(window,Path.Combine(output,"compact-strategy-initial.png"));
        Check(cards.Length==61&&cards.Average(c=>c.ActualHeight)<105&&cards.All(c=>c.MinHeight<145),"strategy cards fit content instead of reserving 145 pixels each");
        var inputs=Descendants(panel).OfType<TextBox>().Where(t=>t.DataContext is ParameterField {IsText:true}).ToArray();
        foreach(var input in inputs){input.BringIntoView();window.UpdateLayout();var glyph=input.GetRectFromCharacterIndex(0);Check(!glyph.IsEmpty&&glyph.Top>=0&&glyph.Bottom<=input.ActualHeight-1,"input glyph fits without clipping: "+((ParameterField)input.DataContext).Metadata.Key);}
        var hints=Descendants(panel).OfType<TextBlock>().Where(t=>t.DataContext is ParameterField f&&t.Text==f.Range&&f.ShowRange).ToArray();
        Check(hints.Length>30&&hints.All(t=>Grid.GetColumn(t)==1),"numeric/list range hints occupy the column beside their editors");
        Check(!Descendants(panel).OfType<TextBlock>().Any(t=>t.IsVisible&&t.Text=="ON / OFF"),"obvious boolean help is not displayed in parameter cards");
        cards.First(c=>((ParameterField)c.DataContext).Metadata.Key=="RsiPeriod").BringIntoView();Render(window,Path.Combine(output,"compact-strategy.png"));
        vm.IsOnline=true;PumpUntil(()=>!vm.Busy);vm.ActiveTab=3;
        vm.Orders.Add(new("20261106","1001","005930","삼성전자","매수",10,70000,10,0,false,false));vm.Orders.Add(new("20261106","1002","000660","SK하이닉스","매도",2,200000,2,0,false,false));
        vm.Holdings.Add(new("005930","삼성전자",10,70000,71000,10000));vm.Holdings.Add(new("000660","SK하이닉스",2,200000,190000,-20000));window.UpdateLayout();Render(window,Path.Combine(output,"compact-account-tables.png"));
        Check(Descendants(window).OfType<DataGridCell>().Count(c=>c.IsVisible)>10,"populated account tables render centered text and prices");
        vm.MultiTrading.PrepareLaunchMode(false);var real=new MultiTradeDialog(vm.MultiTrading){Owner=window,ShowActivated=false,Left=-20000,WindowStartupLocation=WindowStartupLocation.Manual};real.Show();Check(real.Title=="실제 계좌 전략 실행","real strategy popup uses account-specific title");real.Close();
        vm.MultiTrading.PrepareLaunchMode(true);vm.MultiTrading.SeedSymbols([DailyStock,DailyStock with{Symbol="000660",Name="SK하이닉스"}]);var paper=new MultiTradeDialog(vm.MultiTrading){Owner=window,ShowActivated=false,Left=-20000,WindowStartupLocation=WindowStartupLocation.Manual};paper.Show();paper.UpdateLayout();Render(paper,Path.Combine(output,"compact-virtual-tables.png"));Check(paper.Title=="가상 계좌 전략 실행","virtual strategy popup uses account-specific title");paper.Close();
        vm.Query="삼성전자";vm.Actions["검색"].Execute(null);PumpUntil(()=>!vm.Busy);vm.Actions["관심등록"].Execute(null);PumpUntil(()=>!vm.Busy);vm.ActiveTab=6;window.UpdateLayout();Render(window,Path.Combine(output,"compact-watchlist.png"));
        Check(Descendants(window).OfType<Button>().Where(b=>Equals(b.Content,"편집")&&b.IsVisible).All(b=>b.Foreground is SolidColorBrush brush&&brush.Color==Color.FromRgb(49,130,246)),"watch group edit buttons use the shared readable blue foreground");
        var auditResult=DailyRun([DailyBar(0,100),DailyBar(1,100),DailyBar(2,80),DailyBar(3,82),DailyBar(4,90)]);db.State(auditResult.Id,auditResult);vm.History.Add(auditResult.ToHistory());vm.ResearchFrom=DateTime.MinValue;vm.ResearchTo=DateTime.MaxValue.Date;vm.ActiveTab=4;window.UpdateLayout();Render(window,Path.Combine(output,"compact-history.png"));
        var diagnostics=new OversoldResultDialog(auditResult){Owner=window,ShowActivated=false,Left=-20000,WindowStartupLocation=WindowStartupLocation.Manual};diagnostics.Show();var diagnosticTabs=Descendants(diagnostics).OfType<TabControl>().Single();foreach(var tabIndex in new[]{2,3}){diagnosticTabs.SelectedIndex=tabIndex;diagnostics.UpdateLayout();Render(diagnostics,Path.Combine(output,$"compact-decision-table-{tabIndex}.png"));}diagnostics.Close();
        var prices=Enumerable.Range(0,400).Select(i=>DailyBar(i,100)).ToArray();
        var trades=new[]{45,95,145,195,245,295,345}.SelectMany((i,n)=>new[]{new TradeMarker("b"+n,DailyStock.Key,prices[i].Time,true,100,100,"TEST",TradeNumber:n+1),new TradeMarker("s"+n,DailyStock.Key,prices[i+2].Time,false,100,100,"TEST",TradeNumber:n+1,Reason:"TrailingStop",RealizedPnl:n%2==0?12345:-3456)}).ToArray();
        var chart=new CandleChart{Bars=prices,Period="일봉",TradeMarkers=trades};chart.EnableScreeningReviewMode();chart.Measure(new Size(1000,540));chart.Arrange(new Rect(0,0,1000,540));Render(chart,Path.Combine(output,"merged-trade-overview.png"));
        var overview=chart.RenderedTradeMarkers.ToArray();Check(overview.Length<trades.Length,"dense trade text is merged at full-period overview");
        Check(!overview.SelectMany((a,i)=>overview.Skip(i+1).Select(b=>a.Bounds.IntersectsWith(b.Bounds))).Any(v=>v),"measured trade badges do not overlap, including long profit/loss captions");
        chart.ToggleScreeningReviewView(452);for(var i=0;i<30;i++)chart.ZoomAt(452,120);chart.FocusTrade(prices[196].Time,true);Render(chart,Path.Combine(output,"merged-trade-focus.png"));Check(chart.RenderedTradeMarkers.Count>=2&&chart.RenderedTradeMarkers.All(m=>trades.Any(t=>t.Id==m.Marker.Id)),"focused labels still select original exact fill records");
        window.Close();
    }
    private static void CompactDemo(string output)
    {
        Directory.CreateDirectory(output);using var db=new TradingStore(Path.Combine(Dir,"compact-demo.db"));using var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:db,connectionPrompt:()=>Connection(Brokerage.Kiwoom),accountFactory:(_,_)=>new ModeCacheBroker(),sessionDirectory:Path.Combine(Dir,"compact-demo-sessions"));
        var window=new MainWindow(vm){Title="STOCK · 격리 화면 검증",Width=1540,Height=1040};window.Show();vm.ActiveTab=2;
        for(var pass=0;pass<4;pass++){foreach(var e in Descendants(window).OfType<Expander>().ToArray())e.IsExpanded=true;window.UpdateLayout();}
        var frame=new DispatcherFrame();window.Closed+=(_,_)=>frame.Continue=false;Dispatcher.PushFrame(frame);
    }
}
