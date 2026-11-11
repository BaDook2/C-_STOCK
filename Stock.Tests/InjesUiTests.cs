using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

internal static partial class Program
{
    private sealed class InjesFixtureFiles(string path):IStrategyFileDialogs
    {public string? Import()=>path;public string? Export(string suggestedName)=>path;}
    private static void InjesUiChecks(string output)
    {
        Directory.CreateDirectory(output);InjesParameterAdapterChecks(output);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));var app=new Stock.App.App{ManualWindowStartup=true};app.InitializeComponent();
        using var store=new TradingStore(Path.Combine(Dir,"native-injes-ui.db"),true);var json=Path.Combine(output,"native-wpf-roundtrip.json");
        using var vm=new WorkspaceViewModel(_=>false,provider:new FixtureMarketData(),store:store,strategyFileDialogs:new InjesFixtureFiles(json));var window=new MainWindow(vm){ShowActivated=false,Left=-20000,Top=0,WindowStartupLocation=WindowStartupLocation.Manual};window.Show();vm.ActiveTab=2;window.UpdateLayout();
        void Do(string action){vm.Actions[action].Execute(null);PumpUntil(()=>!vm.Busy);window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
        Check(vm.Presets.Count==4&&new InjesPresetStore(store).Names.Length==4,"WPF selector contains only the four native Inje defaults");
        Check(vm.OversoldGroups.Sum(g=>g.Fields.Count)==35&&vm.OversoldGroups.SelectMany(g=>g.Fields).Count(f=>f.Metadata.Key.StartsWith("commonParameters."))==14,"WPF existing cards bind all35 native fields including exactly14 common inputs");
        var original=vm.SelectedOversoldPreset!;var price=vm.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="commonParameters.MinPrice");price.Value="1.2345";Do("저장");
        Check(new InjesPresetStore(store).Get(original).CommonParameters.MinPrice==1.2345m,"WPF existing Save persists sub-dollar precision");
        vm.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="commonParameters.MinPrice").Value="1.5";Do("복구");Check(vm.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="commonParameters.MinPrice").Value=="1.2345","WPF Restore discards unsaved native input");
        vm.Preset="WPF Inje copy";Do("추가");var copy=vm.SelectedOversoldPreset!;Check(new InjesPresetStore(store).Get(copy).StrategyName==copy,"WPF Add saves the new native name rather than overwriting its source");
        vm.Preset="WPF Inje renamed";Do("이름변경");Check(!new InjesPresetStore(store).Names.Contains(copy)&&store.State<object>(InjesPresetStore.Prefix+copy)==null,"WPF Rename removes the old native key and selector entry");
        Do("다운로드");var exported=InjesStrategyJson.Import(File.ReadAllText(json));Check(exported.CommonParameters.MinPrice==1.2345m&&exported.StrategyName=="WPF Inje renamed","WPF download contains the exact native strategy and common values");
        var count=vm.Presets.Count;Do("불러오기");Check(vm.Presets.Count==count+1&&new InjesPresetStore(store).Get(vm.SelectedOversoldPreset!).CommonParameters.MinPrice==1.2345m,"WPF JSON import creates a matching uniquely named native preset");
        Check(!Descendants(window).OfType<Button>().Any(b=>b.Content?.ToString()?.Contains("전략D")==true),"WPF no longer exposes strategy D buttons");
        Render((FrameworkElement)window.Content,Path.Combine(output,"injes-wpf-settings.png"));window.Close();app.Shutdown();
    }
}
