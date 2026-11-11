using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Stock.Core.Strategies;
using Stock.Core.Strategies.Injes;
using Stock.Core.Strategies.Bins;
using Stock.Core.Trading;
using Stock.Desktop;
internal static partial class Program
{
    private static void StrategySettingsChecks()
    {
        var db=Path.Combine(output,"strategy-editor-"+Guid.NewGuid().ToString("N")+".db");using var store=new TradingStore(db,true);
        var editor=new StrategyEditor(store);var first=editor.Selected;
        Check(editor.Names.Count==1&&editor.UseInje&&!editor.UseBin&&editor.InjeChoices.Length==4,"one profile contains both systems and four independent Inje algorithms");
        var min=editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="commonParameters.MinPrice");min.Text="1.2345";
        editor.Save.Execute(null);Check(editor.Registered(first).Injes!.CommonParameters.MinPrice==1.2345m,"exact decimal Inje field saved in aggregate profile");
        editor.InjeId="quote-scalping";editor.InjeId="momentum-pullback";Check(editor.Current().Injes!.CommonParameters.MinPrice==1.2345m,"switching Inje algorithms preserves each draft");
        editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="commonParameters.MinPrice").Text="1.7777";editor.Restore.Execute(null);Check(editor.Current().Injes!.CommonParameters.MinPrice==1.2345m,"restore discards unsaved fields");
        var mode=editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="strategyMode");mode.Text="Standard";Check(editor.InjeGroups.Last().Fields.Length==6,"Standard shows six strengths");
        mode=editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="strategyMode");mode.Text="Advanced";Check(editor.InjeGroups.Last().Fields.Length==10,"Advanced shows independent strategy parameters");
        editor.UseBin=true;Check(editor.UseBin&&!editor.UseInje&&editor.Current().Bins is {Enabled:true}&&editor.Current().Injes==null,"Bin use selects only the Bin execution configuration");
        var duration=editor.BinGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="entries.uptrendBreakout.holdDuration");duration.Text="1";duration.Unit="Minute";
        editor.AddUsdFee.Execute(null);Check(editor.FeeRows.Count==1,"dated fee row can be added from UI");
        editor.Save.Execute(null);var profile=new StrategyProfileStore(store).Get(first);Check(profile.SelectedSystem==StrategySystem.Bin&&profile.Bin.Entries.UptrendBreakout.HoldDuration.Elapsed==TimeSpan.FromMinutes(1)&&profile.Bin.Market.CountryProfiles.US.Fees.Length==1,"Bin duration and dated fee fields survive save");
        var rebuilt=new StrategyEditor(store);Check(rebuilt.UseBin&&!rebuilt.UseInje&&rebuilt.CurrentProfile().InjeStrategies.Single(d=>d.StrategyId=="momentum-pullback").CommonParameters.MinPrice==1.2345m,"reconstruction retains inactive Inje values and active Bin selection");
        editor.UseBin=false;Check(!editor.UseBin&&!editor.UseInje,"unchecking active strategy leaves both disabled");bool blocked=false;try{editor.Current();}catch(InvalidOperationException){blocked=true;}Check(blocked,"no selected strategy cannot start an execution");editor.UseInje=true;
        var panel=new StrategyPanel{DataContext=editor};var window=new Window{Width=1250,Height=920,Content=panel};window.Show();Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var inje=panel.FindControl<Expander>("InjeStrategyExpander")!;var bin=panel.FindControl<Expander>("BinStrategyExpander")!;inje.IsExpanded=bin.IsExpanded=false;Dispatcher.UIThread.RunJobs();
        Check(panel.FindControl<CheckBox>("InjeUse")!.IsEffectivelyVisible&&panel.FindControl<CheckBox>("BinUse")!.IsEffectivelyVisible,"both use checkboxes stay visible with collapsed parameters");
        panel.FindControl<CheckBox>("BinUse")!.IsChecked=true;Dispatcher.UIThread.RunJobs();Check(editor.UseBin&&panel.FindControl<CheckBox>("InjeUse")!.IsChecked==false&&!bin.IsExpanded,"header binding switches system without expanding parameters");
        Check(panel.FindControl<Expander>("CrashStrategyExpander")==null&&panel.FindControl<ItemsControl>("CommonSections")==null,"old top-level strategy sections are absent");
        inje.IsExpanded=bin.IsExpanded=true;Dispatcher.UIThread.RunJobs();foreach(var e in panel.GetVisualDescendants().OfType<Expander>().ToArray())e.IsExpanded=true;Dispatcher.UIThread.RunJobs();window.UpdateLayout();AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var inputs=panel.GetVisualDescendants().OfType<SettingInput>().ToArray();var expected=editor.InjeGroups.Concat(editor.BinGroups).Sum(g=>g.Fields.Length);Check(inputs.Length==expected&&inputs.All(i=>i.Content is Control),"every actual parameter creates an editor ("+expected+")");
        var time=inputs.Where(i=>((LegacyParameter)i.DataContext!).HasTimeUnit).ToArray();Check(time.Length>20&&time.All(i=>i.Content is Grid g&&g.Children.OfType<ComboBox>().Any()),"every Bin duration and timeframe has an adjacent unit combo");
        Check(inputs.Where(i=>((LegacyParameter)i.DataContext!).ReadOnly).All(i=>((Control)i.Content!).IsEnabled==false),"fixed limit-only and no-pyramiding policy controls cannot be altered");
        Check(!panel.GetVisualDescendants().OfType<TextBox>().Any(b=>b.AcceptsReturn),"no legacy market facts JSON editor remains");
        var helpField=editor.InjeGroups.SelectMany(g=>g.Fields).Single(f=>f.Path=="commonParameters.MinPrice");
        Check(helpField.Help.Split('\n').Count(line=>line==helpField.Metadata.DetailedDescription)==1,"identical short/detail description is included once in help text");
        var helpButton=panel.GetVisualDescendants().OfType<Button>().Single(b=>ReferenceEquals(b.DataContext,helpField)&&b.Content?.ToString()=="?");
        helpButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        var helpDialog=window.OwnedWindows.Single();var shown=helpDialog.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text==helpField.Help).Text!;
        Check(shown.Split('\n').Distinct().Count()==shown.Split('\n').Length,"actual question-mark popup displays each description line once");helpDialog.Close();

        foreach(var e in panel.GetVisualDescendants().OfType<Expander>().ToArray())e.IsExpanded=false;Dispatcher.UIThread.RunJobs();AvaloniaHeadlessPlatform.ForceRenderTimerTick();using(var image=window.CaptureRenderedFrame())image!.Save(Path.Combine(output,"strategy-two-systems.png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        window.Close();editor.FeeRows[0].Remove.Execute(null);Check(editor.CurrentProfile().Bin.Market.CountryProfiles.US.Fees.Length==0,"fee delete changes only current draft");
    }
}
