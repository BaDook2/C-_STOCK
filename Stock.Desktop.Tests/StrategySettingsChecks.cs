using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core.Research;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void StrategySettingsChecks()
    {
        using var store=new TradingStore(Path.Combine(output,"settings-"+Guid.NewGuid().ToString("N")+".db"),true);
        var editor=new StrategyEditor(store);
        Check(!editor.UseCrashStrategy,"old presets retain the legacy algorithm until explicitly enabled");
        Check(editor.CrashGroups.SelectMany(g=>g.Fields).All(f=>f.Path is not ("Configuration.PlannedInvestment" or "Configuration.CommissionPercent" or "Configuration.TaxPercent" or "Configuration.SlippagePercent")),"integrated crash settings exclude duplicate capital and execution costs");
        var periods=editor.Groups.SelectMany(g=>g.Fields).Where(f=>f.HasTimeframe).ToArray();
        Check(periods.Where(f=>f.Metadata.Key!="MovingAveragePeriods").All(f=>f.TimeframeChoices.Select(c=>c.Label).SequenceEqual(new[]{"분","시간","일봉","주봉","월봉"})),"every bar-count parameter exposes the same five explicit timeframes");
        Check(periods.Single(f=>f.Metadata.Key=="MovingAveragePeriods").TimeframeChoices.Select(c=>c.Label).SequenceEqual(new[]{"일봉","주봉","월봉"}),"moving-average periods have the three calendar timeframe choices");
        Check(editor.Groups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="InitialCapital").Text=="10000000","integer-valued capital no longer has forced decimal places");
        editor.UseCrashStrategy=true;periods.Single(f=>f.Metadata.Key=="RsiPeriod").Timeframe="Week";
        editor.Save.Execute(null);Dispatcher.UIThread.RunJobs();
        var restored=new StrategyEditor(store);var saved=restored.Registered(editor.Selected);
        Check(saved.UseCrashStrategy&&saved.CrashStrategy!=null&&saved.ParameterTimeframes["RsiPeriod"].Unit==TimeframeUnit.Week,"algorithm choice and indicator timeframes survive save and restart");
        restored.UseCrashStrategy=false;restored.Restore.Execute(null);Check(restored.UseCrashStrategy,"Restore also restores the saved algorithm switch");
        restored.UseCrashStrategy=false;
        var panel=new StrategyPanel{DataContext=restored};var window=new Window{Width=1250,Height=920,Content=panel};window.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var crash=panel.FindControl<Expander>("CrashStrategyExpander")!;var use=panel.FindControl<CheckBox>("CrashStrategyUse")!;
        var backtest=panel.FindControl<ItemsControl>("BacktestSections")!;var common=panel.FindControl<ItemsControl>("CommonSections")!;
        Check(backtest.TranslatePoint(new(0,0),panel)!.Value.Y<crash.TranslatePoint(new(0,0),panel)!.Value.Y&&crash.TranslatePoint(new(0,0),panel)!.Value.Y<common.TranslatePoint(new(0,0),panel)!.Value.Y,"crash strategy appears between backtest-only and common strategy sections");
        Check(!panel.GetVisualDescendants().OfType<Expander>().Any(e=>e.Header?.ToString()?.Contains("시점별 검증 자료")==true||e.Header?.ToString()?.Contains("시장 세션 JSON")==true)&&!panel.GetVisualDescendants().OfType<TextBox>().Any(t=>t.AcceptsReturn),"strategy page removes both manual verification-data sections");
        Check(!crash.IsExpanded&&use.IsEffectivelyVisible,"strategy-use checkbox remains visible beside the collapsed section title");
        ClickAt(window,use,.3);Check(restored.UseCrashStrategy&&!crash.IsExpanded,"clicking the header checkbox enables the strategy without expanding it");
        ClickAt(window,use,.3);Check(!restored.UseCrashStrategy&&!crash.IsExpanded,"clicking the header checkbox disables the strategy without toggling the expander");
        using(var image=window.CaptureRenderedFrame())image!.Save(Path.Combine(output,"strategy-sections-collapsed.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        foreach(var e in backtest.GetVisualDescendants().OfType<Expander>())e.IsExpanded=false;Dispatcher.UIThread.RunJobs();
        var crashHeader=crash.GetVisualDescendants().OfType<ToggleButton>().First(b=>b.Name=="PART_ToggleButton");ClickAt(window,crashHeader,.01);
        Check(crash.IsExpanded,"crash strategy header expands while strategy use is off");
        var groups=crash.GetVisualDescendants().OfType<Expander>().Where(e=>e.DataContext is DayTradingParameterGroup).ToArray();
        Check(groups.Length==restored.CrashGroups.Count,"all crash strategy groups remain available while use is off");
        foreach(var group in groups)
        {
            var header=group.GetVisualDescendants().OfType<ToggleButton>().First(b=>b.Name=="PART_ToggleButton");header.BringIntoView();Dispatcher.UIThread.RunJobs();
            ClickAt(window,header,.01);Check(group.IsExpanded,"disabled strategy group opens by mouse: "+group.Header);
            Check(group.GetVisualDescendants().OfType<SettingInput>().Any()&&group.GetVisualDescendants().OfType<SettingInput>().All(i=>!i.IsEffectivelyEnabled),"only parameter editors are disabled while use is off: "+group.Header);
            ClickAt(window,header,.01);Check(!group.IsExpanded,"strategy group collapses by mouse: "+group.Header);
        }
        use.BringIntoView();Dispatcher.UIThread.RunJobs();ClickAt(window,use,.3);
        var basic=groups.First(e=>e.Header?.ToString()=="기본 분석 기준");var basicHeader=basic.GetVisualDescendants().OfType<ToggleButton>().First(b=>b.Name=="PART_ToggleButton");ClickAt(window,basicHeader,.01);
        Check(restored.UseCrashStrategy&&basic.IsExpanded&&basic.GetVisualDescendants().OfType<SettingInput>().All(i=>i.IsEffectivelyEnabled),"enabling the header switch unlocks parameter editors without disabling group navigation");
        Check(!panel.GetVisualDescendants().OfType<Button>().Any(b=>b.Content?.ToString()?.Contains("간편/고급")==true),"separate strategy D settings buttons are removed");
        using(var image=window.CaptureRenderedFrame())image!.Save(Path.Combine(output,"integrated-strategy-settings.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        window.Close();
        var monday=new DateTimeOffset(2026,11,2,9,0,0,TimeSpan.FromHours(9));
        var days=Enumerable.Range(0,5).Select(n=>new StrategyBar(new(monday.AddDays(n).AddHours(6.5),100,110,90,100+n,1000),monday.AddDays(n),monday.AddDays(n).AddHours(6.5),n+1,100000,"FIXTURE")).ToArray();
        var week=new TimeframeSpec(1,TimeframeUnit.Week);var before=StrategyFrameSeries.Aggregate(days,week,monday.AddDays(4).AddHours(3));var after=StrategyFrameSeries.Aggregate(days,week,monday.AddDays(7));
        Check(before.Length==0&&after.Length==1&&after[0].KnownAt>days[^1].ClosedAt,"weekly indicators never expose an unfinished or future weekly candle");
        var config=new OversoldConfig{RsiPeriod=2,ParameterTimeframes=new(){["RsiPeriod"]=week}};
        var facts=new HistoricalFacts{StrategyFrames=after};var cache=new OversoldIndicatorCache(config,facts:facts);cache.Append(new(monday.AddDays(4),100,100,100,100,1));Check(cache.Current.Rsi==null,"an independent weekly RSI with insufficient completed history stays unavailable");
        var warm=Enumerable.Range(0,280).Select(n=>new PriceBar(monday.AddDays(n-280),100+n,110+n,90+n,100+n,1000)).ToArray();
        var chart=new StockChart{Bars=warm.TakeLast(20).ToArray(),IndicatorBars=warm,DailyBars=warm,Zones=[new(warm[0].Time,warm[^1].Time,300,300,true,"급락 판단 지점",true)]};
        var chartWindow=new Window{Width=1000,Height=700,Content=chart};chartWindow.Show();Pump(()=>chart.RenderedMa240&&chart.RenderedCrashLines==1);
        Check(chart.RenderedMa240&&chart.RenderedCrashLines==1&&chart.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="급락 판단 지점"),"short result ranges still render warmed MA240 and the purple horizontal crash line");
        using(var image=chartWindow.CaptureRenderedFrame())image!.Save(Path.Combine(output,"ma240-crash-line.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);chartWindow.Close();
    }
}
