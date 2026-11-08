using System.IO;
using System.Text.Json.Nodes;
using Stock.App;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void DayTradingChecks()
    {
        DayTradingConfigurationChecks();
        DayTradingDataChecks();
        StrategyDSignalChecks();
        IntradayPositionRuleChecks();
        DayTradingPortfolioChecks();
        IntradayReplayChecks();
        ClosedBarReplayChecks();
        DayTradingSessionChecks();
    }
    private static void DayTradingConfigurationChecks()
    {
        var legacy=new OversoldConfig();var json=OversoldPresetExport.Json(OversoldPresetExport.Snapshot("구 전략",legacy,Epoch));
        var imported=StrategyDefinitionJson.Import(json);
        Check(imported.SchemaVersion==1&&!imported.IsStrategyD&&imported.DayTrading==null&&Serialization.Pack(imported.Configuration)==Serialization.Pack(legacy),"D schema reader retains the exact legacy 61-parameter config without migration");
        Check(ParameterCatalog.Properties(legacy).Count()==61,"legacy parameter contract remains exactly 61 fields");
        var returned=OversoldPresetExport.Parse(StrategyDefinitionJson.Export(imported));
        Check(Serialization.Pack(returned.Configuration)==Serialization.Pack(legacy),"new reader export remains readable by the existing legacy importer");
        var definition=StrategyDDefaults.Create(Epoch);definition.Validate();
        var restored=StrategyDefinitionJson.Import(StrategyDefinitionJson.Export(definition));
        Check(restored.IsStrategyD&&restored.SchemaVersion==2&&restored.Configuration.MaPeriods().SequenceEqual([240])&&restored.DayTrading!.Support.MovingAverageTimeframe==new TimeframeSpec(1,TimeframeUnit.TradingDay),"strategy D has a separate ID/version and actual daily MA240 reference");
        Check(restored.Configuration.CommissionPercent==.015m&&restored.Configuration.ReboundPercent==.5m&&restored.Configuration.MaxEntries==1&&restored.Configuration.Ratios().SequenceEqual([100m]),"D defaults retain fee precision and explicitly use experimental one-entry rebound values");
        Check(restored.DayTrading!.Evaluation==SignalEvaluationMode.OnTick&&restored.DayTrading.Reentry.Mode==ReentryMode.SameZoneWithFreshSignal&&restored.DayTrading.Session.DayOnly,"D defaults select tick evaluation, fresh same-zone reentry and independent day-only policy");
        Throws<InvalidOperationException>(()=>OversoldPresetExport.Parse(StrategyDefinitionJson.Export(definition)),"old importer cannot silently run strategy D with the legacy engine");
        var copy=definition.Snapshot();copy.Configuration.CommissionPercent=.019m;copy.DayTrading!.Crash.RollingWindow=new(5,TimeframeUnit.Second);
        Check(definition.Configuration.CommissionPercent==.015m&&definition.DayTrading!.Crash.RollingWindow==new TimeframeSpec(30,TimeframeUnit.Minute),"strategy snapshots isolate every nested config and preserve the original definition");
        using(var db=new TradingStore(Path.Combine(Dir,"day-definition.db")))db.State("strategy-definition-v2:test",restored);
        using(var db=new TradingStore(Path.Combine(Dir,"day-definition.db")))
        {
            var stored=db.State<StrategyDefinition>("strategy-definition-v2:test")!;stored.Validate();
            Check(StrategyDefinitionJson.Export(stored)==StrategyDefinitionJson.Export(restored),"existing SQLite state repository roundtrips versioned D configuration losslessly");
        }
        new TimeframeSpec(1,TimeframeUnit.Trade).Validate();new TimeframeSpec(1,TimeframeUnit.Second).Validate();
        Check(new TimeframeSpec(1,TimeframeUnit.Trade).Duration==null&&new TimeframeSpec(1,TimeframeUnit.Second).Duration==TimeSpan.FromSeconds(1),"one trade is never equated with one second");
        foreach(var bad in new[]{new TimeframeSpec(0,TimeframeUnit.Minute),new TimeframeSpec(-1,TimeframeUnit.Second),new TimeframeSpec(2,TimeframeUnit.TradingDay),new TimeframeSpec(1,(TimeframeUnit)999)})
            Throws<InvalidOperationException>(bad.Validate,"invalid analysis timeframe rejected: "+bad);
        void Reject(Action<StrategyDefinition> mutate,string reason)
        {var bad=definition.Snapshot();mutate(bad);Throws<InvalidOperationException>(bad.Validate,reason);}
        Reject(d=>d.DayTrading!.Crash.DailyEnabled=d.DayTrading.Crash.IntradayEnabled=false,"no enabled crash rule rejected");
        Reject(d=>d.DayTrading!.Crash.MediumEnabled=d.DayTrading.Crash.ShortEnabled=false,"enabled daily crash cannot contain no rules");
        Reject(d=>d.DayTrading!.Crash.SessionDrawdownPercent=0,"nonnegative intraday drawdown rejected");
        Reject(d=>d.DayTrading!.Crash.RollingWindow=new(2,TimeframeUnit.Trade),"rolling elapsed window cannot be a tick count");
        Reject(d=>d.Configuration.RequireReboundConfirmation=false,"D cannot disable its mandatory fresh rebound");
        Reject(d=>d.Configuration.UseApproximateVolumeZone=false,"MA-only D support cannot replace the mandatory volume zone");
        Reject(d=>d.Configuration.UseMovingAverageSupport=false,"volume-only D support cannot replace mandatory MA evidence");
        Reject(d=>d.DayTrading!.Exit.Mode=ExitMode.TrailingOnly,"TP1-dependent trailing-only configuration rejected");
        var trailing=definition.Snapshot();trailing.DayTrading!.Exit.Mode=ExitMode.TrailingOnly;trailing.Configuration.TrailingActivation=TrailingActivation.AfterEntry;trailing.Validate();Check(true,"trailing-only with immediate protection validates");
        Reject(d=>{d.DayTrading!.Exit.Mode=ExitMode.FullTakeProfit;d.Configuration.UseTakeProfit1=false;},"full-profit mode needs an enabled target");
        Reject(d=>{d.DayTrading!.Exit.Mode=ExitMode.FullTakeProfit;d.DayTrading.Exit.BreakEvenAfterTakeProfit1=true;},"break-even cannot depend on a full-liquidation TP1");
        Reject(d=>{d.DayTrading!.Exit.SupportStopEnabled=false;d.Configuration.UseEmergencyStop=d.Configuration.UseTrailingStop=false;},"D cannot run with no protective stop");
        Reject(d=>d.DayTrading!.Risk.MaxOrderCash=d.DayTrading.Risk.MaxConcurrentExposure+1,"per-order amount exceeding account exposure rejected");
        Reject(d=>d.DayTrading!.Session.NoNewEntryAfter=new(16,0),"entry cutoff after liquidation start rejected");
        Reject(d=>d.DayTrading!.Quality.MaximumQueuedEvents=0,"unbounded or zero queue safety limit rejected");
        Reject(d=>d.DayTrading!.Reentry.MinimumInterval=new(-1,WaitUnit.Second),"negative reentry interval rejected");
        Reject(d=>d.DayTrading!.Exit.Mode=(ExitMode)999,"future unknown enum policy rejected");
        Reject(d=>d.DayTrading!.IndicatorTimeframes[(IndicatorKind)999]=new(1,TimeframeUnit.Minute),"unknown indicator override rejected");
        foreach(var item in new[]{"schemaVersion","strategyId","configuration"})
        {
            var missing=JsonNode.Parse(json)!.AsObject();missing.Remove(item);
            Throws<InvalidOperationException>(()=>StrategyDefinitionJson.Import(missing.ToJsonString()),"missing legacy identity/config cannot become a D default: "+item);
        }
        var future=JsonNode.Parse(StrategyDefinitionJson.Export(definition))!;future["schemaVersion"]=999;
        Throws<InvalidOperationException>(()=>StrategyDefinitionJson.Import(future.ToJsonString()),"future strategy schema never falls back to a known algorithm");
        var unknown=JsonNode.Parse(json)!;unknown["strategyId"]="unknown";
        Throws<InvalidOperationException>(()=>StrategyDefinitionJson.Import(unknown.ToJsonString()),"unknown strategy ID rejected");
        Throws<InvalidOperationException>(()=>StrategyDefinitionJson.Import("{\"schemaVersion\":1,\"SchemaVersion\":2}"),"duplicate case-insensitive JSON identity rejected");
        Check(legacy.MaPeriods().SequenceEqual([120,240,480,999])&&legacy.MaxEntries==3&&legacy.ReentryCooldownDays==5,"creating D defaults never mutates original strategy defaults");
    }
}
