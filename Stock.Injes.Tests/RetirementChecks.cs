using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void RetirementChecks()
    {
        var directory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"InjesRetirement-"+System.Guid.NewGuid().ToString("N"));
        using var store=new TradingStore(System.IO.Path.Combine(directory,"isolated.db"),true);
        store.State("daytrading-definitions-v2",new{Secret="not a real secret"});store.State("daytrading-history:one",new{Id="one"});store.State("oversold-preset:old",new{Period=240});
        store.State("desktop-history-index:old",new{Kind=2});store.State("desktop-history-index:recurring",new{Kind=3});
        store.State("watch-groups",new[]{"preserve"});store.State("account-default",new{Id="fake account"});store.State("live-order-wal:one",new{Status="Unknown"});
        store.State("inje-preset:preserve",new{Family="inje"});store.InsertEvents([Observation(0).Market]);
        string[] retired=["paper-live-batch:old","multi-trade-draft","mock-batch:old","chart-preset:old","chart-settings","desktop-run:old","desktop-screen:old","screen-bars:old","test-bars:old"];
        foreach(var key in retired)store.State(key,new{Legacy=true});
        store.State("trade-marker:old-paper",new{AccountScope="paper-live:old"});store.State("trade-marker:real",new{AccountScope="live:preserve"});store.State("trade-marker:inje",new{AccountScope="inje-paper:preserve"});
        var deleted=store.RetireLegacyStrategies();
        Check(deleted>=4&&store.States("daytrading-").Count==0&&store.States("oversold-").Count==0,"explicit retirement deletes old strategy/D settings and research records");
        Check(store.State<object>("desktop-history-index:old")==null&&store.State<object>("desktop-history-index:recurring")!=null,"D research index is removed while recurring purchase history remains");
        Check(store.State<object>("account-default")!=null&&store.State<object>("live-order-wal:one")!=null&&store.State<string[]>("watch-groups")!.Single()=="preserve","retirement preserves accounts, unresolved real-order safety and watchlists");
        Check(store.State<object>("inje-preset:preserve")!=null&&store.ReadEvents(Stock.Key,"TEST",Epoch,Epoch.AddSeconds(1)).Count==1,"retirement preserves new Inje settings and original data");
        Check(retired.All(key=>store.State<object>(key)==null)&&store.State<object>("trade-marker:old-paper")==null,"retirement removes old virtual ledgers, preset aliases and chart strategy snapshots");
        Check(store.State<object>("trade-marker:real")!=null&&store.State<object>("trade-marker:inje")!=null,"retirement preserves actual-account markers and new native paper evidence");
        Check(store.RetireLegacyStrategies()==0,"retirement transaction is idempotent");
        var definition=InjesStrategyDefinition.Create("momentum-pullback",Epoch);var editor=new InjesEditorModel(definition);
        var mode=editor.ModeFields.Single();var changed=0;editor.ModeChanged+=()=>changed++;mode.Text=null!;mode.Text="Advanced";
        Check(changed==0,"initial unselected ComboBox value never rebuilds or collapses existing groups");
        mode.Text="Standard";Check(changed==1&&editor.StrategyFields.Length==6,"actual mode change rebuilds exactly once");
        editor.StrategyFields.Single(f=>f.Path=="standardParameters.volume").Text="5";var mapped=editor.Apply(definition.StrategyName,Epoch.AddSeconds(1));Check(mapped.EffectiveAdvancedParameters.GetProperty("minRVOL").GetDecimal()==10,"edited Standard field resolves to the exact executed RVOL");
        store.DeleteState("inje-preset:preserve");var presets=new InjesPresetStore(store);presets.EnsureDefaults();var before=presets.Names;var renamed=presets.Get(before[0]);renamed.StrategyName="renamed native preset";presets.Rename(before[0],renamed);presets.EnsureDefaults();
        Check(presets.Names.Length==before.Length&&!presets.Names.Contains(before[0])&&store.State<object>(InjesPresetStore.Prefix+before[0])==null,"renamed native presets remain renamed after startup without stale keys or duplicate defaults");
    }
}
