using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Desktop;

internal static partial class Program
{
    private static void FillFrameDefaults(OversoldConfig config)
    {
        foreach(var property in ParameterCatalog.Properties(config).Where(p=>ParameterCatalog.For(p).Unit=="봉"||p.Name=="MovingAveragePeriods"))
            config.ParameterTimeframes.TryAdd(property.Name,new(1,TimeframeUnit.TradingDay));
    }
    private static string ConfigFingerprint(OversoldConfig config)
    {
        var node=JsonSerializer.SerializeToNode(config,Serialization.Json)!;
        void Strip(JsonNode? value)
        {
            if(value is JsonObject obj){obj.Remove("ExportedAt");obj.Remove("Preset");foreach(var child in obj.ToArray())Strip(child.Value);}
            else if(value is JsonArray array)foreach(var child in array)Strip(child);
        }
        Strip(node);return node.ToJsonString();
    }
    private static bool SameConfig(OversoldConfig left,OversoldConfig right)=>JsonNode.DeepEquals(JsonNode.Parse(ConfigFingerprint(left)),JsonNode.Parse(ConfigFingerprint(right)));
    private static void StrategyJsonChecks()
    {
        var started=passed;
        var path=Path.Combine(output,"strategy-import.json");var notes=new List<string>();
        OversoldConfig RoundTrip(OversoldConfig expected,string name)
        {
            var json=StrategyDefinitionJson.Export(new(1,StrategyDefinition.LegacyId,name,DateTimeOffset.UtcNow,expected));File.WriteAllText(path,json);
            using var store=new TradingStore(Path.Combine(output,"json-"+Guid.NewGuid().ToString("N")+".db"),true);
            var editor=new StrategyEditor(store){FileDialogs=new FixtureStrategyFiles(path)};editor.Notice+=notes.Add;editor.Import.Execute(null);
            Pump(()=>editor.Selected==name||notes.Count>0);Check(editor.Selected==name,"actual import command accepts "+name+": "+string.Join(" / ",notes));
            Check(SameConfig(editor.Registered(name),expected),"imported saved settings match the complete source configuration: "+name);
            foreach(var field in editor.Groups.SelectMany(g=>g.Fields).Where(f=>f.HasTimeframe))field.Timeframe=field.Timeframe;
            var current=editor.Current();
            if(!SameConfig(current,expected)){File.WriteAllText(Path.Combine(output,"expected.json"),ConfigFingerprint(expected));File.WriteAllText(Path.Combine(output,"actual.json"),ConfigFingerprint(current));}
            Check(SameConfig(current,expected),"all values survive import into the editable fields: "+name);
            foreach(var field in editor.Groups.SelectMany(g=>g.Fields))
            {
                var property=typeof(OversoldConfig).GetProperty(field.Metadata.Key)!;
                var read=current.GetType().GetProperty(property.Name)!.GetValue(current);
                Check(Equals(read,property.GetValue(expected)),"legacy JSON field matched: "+field.Metadata.Key);
            }
            editor.Export.Execute(null);Pump(()=>File.Exists(path));var exported=StrategyDefinitionJson.Import(File.ReadAllText(path));
            Check(SameConfig(exported.Configuration,expected),"actual export command preserves the imported configuration: "+name);
            editor.UseCrashStrategy=!expected.UseCrashStrategy;editor.Restore.Execute(null);
            Check(SameConfig(editor.Current(),expected),"Restore preserves all JSON values: "+name);
            var reload=new StrategyEditor(store);reload.Selected=name;
            Check(SameConfig(reload.Current(),expected),"saved JSON values survive rebuilding the editor: "+name);
            return current;
        }
        var legacy=new OversoldConfig();FillFrameDefaults(legacy);legacy.CrashStrategy=StrategyDDefaults.Create(DateTimeOffset.UtcNow);FillFrameDefaults(legacy.CrashStrategy.Configuration);
        RoundTrip(legacy,"JSON-기본값");
        var varied=legacy.Clone();
        foreach(var p in ParameterCatalog.Properties(varied))
        {
            var v=p.GetValue(varied)!;
            if(p.PropertyType==typeof(bool))p.SetValue(varied,!(bool)v);
            else if(p.PropertyType==typeof(int))p.SetValue(varied,(int)v+1);
            else if(p.PropertyType==typeof(decimal))p.SetValue(varied,(decimal)v==0?1.125m:(decimal)v*1.1m);
            else if(p.PropertyType.IsEnum)p.SetValue(varied,Enum.GetValues(p.PropertyType).Cast<object>().Last());
        }
        varied.UseMovingAverageSupport=true;varied.MovingAveragePeriods="37,111,240";varied.EntryRatios="25,25,25,25";
        varied.CommissionPercent=.023456m;varied.TaxPercent=.173m;varied.SlippagePercent=.127m;
        var units=new[]{TimeframeUnit.Minute,TimeframeUnit.Hour,TimeframeUnit.TradingDay,TimeframeUnit.Week,TimeframeUnit.Month};var n=0;
        foreach(var key in varied.ParameterTimeframes.Keys.ToArray())varied.ParameterTimeframes[key]=new(1,key=="MovingAveragePeriods"?TimeframeUnit.Month:units[n++%units.Length]);
        var d=varied.CrashStrategy!;d.Configuration.InitialCapital=varied.InitialCapital;d.Configuration.PlannedInvestment=varied.PlannedInvestment;d.Configuration.CommissionPercent=varied.CommissionPercent;d.Configuration.TaxPercent=varied.TaxPercent;d.Configuration.SlippagePercent=varied.SlippagePercent;
        d.Configuration.MovingAveragePeriods="55,144,240";d.Configuration.ReboundPercent=.875m;d.Configuration.RsiPeriod=23;d.Configuration.MaximumRsi=27.375m;
        var policy=d.DayTrading!;policy.SimpleTimeframe=new(7,TimeframeUnit.Minute);policy.AdvancedTimeframes=true;policy.Evaluation=SignalEvaluationMode.OnBarClosed;
        policy.IndicatorTimeframes[IndicatorKind.Rsi]=new(3,TimeframeUnit.Hour);policy.Support.MovingAverageTimeframe=new(1,TimeframeUnit.Week);policy.Support.ProfileTimeframe=new(1,TimeframeUnit.Month);
        policy.Crash.ShortEnabled=false;policy.Crash.DailyCombination=RuleCombination.All;policy.Crash.DailyIntradayCombination=RuleCombination.All;policy.Crash.SessionDrawdownPercent=-6.125m;policy.Crash.RollingWindow=new(47,TimeframeUnit.Minute);policy.Crash.RollingDrawdownPercent=-3.75m;policy.Crash.CandidateLifetime=new(9,WaitUnit.Bar);
        policy.Support.IncludeActiveMovingAverageBar=true;policy.Support.MaxOverlapDistancePercent=.625m;policy.Support.MaxZoneWidthPercent=3.125m;policy.Support.MaxEntryDistanceFromSupportPercent=1.875m;policy.Support.ReboundCandidateLifetime=new(11,WaitUnit.Bar);
        policy.Reentry.Mode=ReentryMode.NewLowerZoneOnly;policy.Reentry.MinimumInterval=new(91,WaitUnit.Second);policy.Reentry.AfterProfitCooldown=new(4,WaitUnit.Minute);policy.Reentry.AfterStopCooldown=new(8,WaitUnit.Minute);policy.Reentry.MaxEntriesPerSymbolPerDay=7;policy.Reentry.MaxEntriesPerStrategyPerDay=31;policy.Reentry.MaxEntriesPerAccountPerDay=67;
        policy.Exit.PeakBasis=TrailingPeakBasis.ResetAtTakeProfit1;policy.Exit.BreakEvenAfterTakeProfit1=true;policy.Exit.RiskEvaluation=SignalEvaluationMode.OnBarClosed;
        policy.Session.DayOnly=false;policy.Session.NoNewEntryAfter=new(14,31);policy.Session.LiquidationStartAt=new(15,7);policy.Risk.MaxOrderCash=765432;policy.Risk.MaxConcurrentExposure=3456789;policy.Risk.MaxDailyLoss=234567;policy.Risk.RiskBudgetPerPosition=12345;policy.Risk.MaxConsecutiveStops=7;
        policy.Quality.MaximumAgeMilliseconds=3456;policy.Quality.MaximumBufferedEvents=123456;policy.Quality.MaximumQueuedEvents=2345;policy.Quality.RequireContinuousTrades=false;policy.Quality.RequireVerifiedExchangeTime=false;
        varied.UseCrashStrategy=true;varied.Validate();RoundTrip(varied,"JSON-전체설정-변경값");
        varied.UseCrashStrategy=false;RoundTrip(varied,"JSON-D꺼짐-설정보존");
        // A valid non-unit frame imported through JSON must not silently be reset by the UI editor.
        d.Configuration.ParameterTimeframes["RsiPeriod"]=new(3,TimeframeUnit.Hour);varied.ParameterTimeframes["RsiPeriod"]=new(5,TimeframeUnit.Minute);varied.UseCrashStrategy=true;
        RoundTrip(varied,"JSON-개별봉크기-보존");
        var rootField=new LegacyParameter(varied,typeof(OversoldConfig).GetProperty("RsiPeriod")!);rootField.Timeframe="TradingDay";
        Check(varied.ParameterTimeframes["RsiPeriod"]==new TimeframeSpec(1,TimeframeUnit.TradingDay),"changing the root period unit intentionally starts at size one");
        var dEditor=new StrategyDEditorModel(d);dEditor.Parameters.Single(f=>f.Path=="Configuration.ParameterTimeframes.RsiPeriod.Unit").Text=((int)TimeframeUnit.TradingDay).ToString();
        Check(dEditor.Apply("새 봉 기준").Configuration.ParameterTimeframes["RsiPeriod"]==new TimeframeSpec(1,TimeframeUnit.TradingDay),"changing the D period unit intentionally starts at size one");
        var shared=BaselineForSharedValues(legacy);
        var effective=SelectedStrategy.Definition(shared);
        Check(effective.Configuration.InitialCapital==shared.InitialCapital&&effective.Configuration.PlannedInvestment==shared.PlannedInvestment&&effective.Configuration.CommissionPercent==shared.CommissionPercent&&effective.Configuration.TaxPercent==shared.TaxPercent&&effective.Configuration.SlippagePercent==shared.SlippagePercent,"D execution uses the documented enclosing capital and cost values");
        using(var sharedStore=new TradingStore(Path.Combine(output,"json-shared-"+Guid.NewGuid().ToString("N")+".db"),true))
        {
            var sharedPath=Path.Combine(output,"json-shared-values.json");File.WriteAllText(sharedPath,StrategyDefinitionJson.Export(new(1,StrategyDefinition.LegacyId,"공유 자금 검사",DateTimeOffset.UtcNow,shared)));
            var sharedEditor=new StrategyEditor(sharedStore){FileDialogs=new FixtureStrategyFiles(sharedPath)};sharedEditor.Import.Execute(null);Pump(()=>sharedEditor.Selected=="공유 자금 검사");
            var normalized=sharedEditor.Current();var inner=normalized.CrashStrategy!.Configuration;
            Check(inner.InitialCapital==shared.InitialCapital&&inner.PlannedInvestment==shared.PlannedInvestment&&inner.CommissionPercent==shared.CommissionPercent&&inner.TaxPercent==shared.TaxPercent&&inner.SlippagePercent==shared.SlippagePercent,"the editor normalizes duplicate D money and cost fields to the same enclosing values");
        }
        StrategyJsonInvalidChecks(legacy);
        StrategyJsonReference(legacy);
        Console.WriteLine($"PASS: {passed-started} strategy JSON checks; all files and stores isolated");
    }

    private static OversoldConfig BaselineForSharedValues(OversoldConfig source)
    {
        var value=source.Clone();var c=value.CrashStrategy!.Configuration;
        c.InitialCapital=20000000;c.PlannedInvestment=2000000;c.CommissionPercent=.02m;c.TaxPercent=.2m;c.SlippagePercent=.1m;return value;
    }

    private sealed class CancelStrategyFiles:IStrategyFileDialogs
    {public Task<string?> Import()=>Task.FromResult<string?>(null);public Task<string?> Export(string suggested)=>Task.FromResult<string?>(null);}
    private static void StrategyJsonInvalidChecks(OversoldConfig baseline)
    {
        var valid=StrategyDefinitionJson.Export(new(1,StrategyDefinition.LegacyId,"JSON 오류 검사",DateTimeOffset.UtcNow,baseline));
        string Change(Action<JsonNode> edit){var node=JsonNode.Parse(valid)!;edit(node);return node.ToJsonString();}
        var invalid=new Dictionary<string,string>
        {
            ["잘못된 JSON 문법"]="{broken",["객체가 아닌 루트"]="[]",
            ["필수 버전 누락"]=Change(n=>n.AsObject().Remove("schemaVersion")),
            ["지원하지 않는 ID"]=Change(n=>n["strategyId"]="other"),
            ["설정 null"]=Change(n=>n["configuration"]=null),
            ["알 수 없는 최상위 항목"]=Change(n=>n["note"]="typo"),
            ["숫자 항목 오타"]=Change(n=>n["configuration"]!["reboundPercnet"]=3),
            ["정수에 소수 입력"]=Change(n=>n["configuration"]!["rsiPeriod"]=14.5m),
            ["숫자를 문자열로 입력"]=Change(n=>n["configuration"]!["maximumRsi"]="30"),
            ["지원하지 않는 enum"]=Change(n=>n["configuration"]!["touchPolicy"]=19),
            ["범위를 벗어난 수치"]=Change(n=>n["configuration"]!["maximumRsi"]=101),
            ["불일치 분할 비율"]=Change(n=>n["configuration"]!["entryRatios"]="100"),
            ["알 수 없는 기간 키"]=Change(n=>n["configuration"]!["parameterTimeframes"]!["RsiPeroid"]=new JsonObject{["size"]=1,["unit"]=4}),
            ["기간 아닌 설정에 기간 추가"]=Change(n=>n["configuration"]!["parameterTimeframes"]!["UseCrashStrategy"]=new JsonObject{["size"]=1,["unit"]=4}),
            ["기간 null"]=Change(n=>n["configuration"]!["parameterTimeframes"]!["RsiPeriod"]=null),
            ["일봉 크기 2"]=Change(n=>n["configuration"]!["parameterTimeframes"]!["RsiPeriod"]!["size"]=2),
            ["MA 분봉"]=Change(n=>n["configuration"]!["parameterTimeframes"]!["MovingAveragePeriods"]!["unit"]=2),
            ["D OFF 상태의 잘못된 정책"]=Change(n=>n["configuration"]!["crashStrategy"]!["dayTrading"]!["risk"]!["maxOrderCash"]=-1),
            ["D OFF 상태의 빈 그룹"]=Change(n=>n["configuration"]!["crashStrategy"]!["dayTrading"]!["support"]=null),
            ["D 정책 항목 오타"]=Change(n=>n["configuration"]!["crashStrategy"]!["dayTrading"]!["risk"]!["maxOrderCahs"]=123),
            ["중첩 전략 재귀"]=Change(n=>n["configuration"]!["crashStrategy"]!["configuration"]!["useCrashStrategy"]=true),
            ["루트 중복 항목"]=valid.Replace("\"schemaVersion\": 1,","\"schemaVersion\": 1,\"SchemaVersion\":1,"),
            ["내부 중복 항목"]=valid.Replace("\"rsiPeriod\": 14,","\"rsiPeriod\": 14,\"RsiPeriod\":7,"),
            ["봉 키 대소문자 중복"]=valid.Replace("\"RsiPeriod\": {","\"rsiPeriod\": {\"size\":1,\"unit\":4},\"RsiPeriod\": {")
        };
        var path=Path.Combine(output,"invalid-strategy.json");using var store=new TradingStore(Path.Combine(output,"json-invalid-"+Guid.NewGuid().ToString("N")+".db"),true);
        var editor=new StrategyEditor(store){FileDialogs=new FixtureStrategyFiles(path)};var notices=new List<string>();editor.Notice+=notices.Add;
        var before=ConfigFingerprint(editor.Current());var names=editor.Names.ToArray();var states=store.States("oversold-preset:").Count;
        foreach(var (label,json) in invalid)
        {
            notices.Clear();File.WriteAllText(path,json);editor.Import.Execute(null);Pump(()=>notices.Count>0);
            Check(notices.Count==1&&editor.Names.SequenceEqual(names)&&store.States("oversold-preset:").Count==states&&ConfigFingerprint(editor.Current())==before,"invalid JSON fails without modifying editor or saved presets: "+label);
        }
        editor.FileDialogs=new CancelStrategyFiles();editor.Import.Execute(null);Dispatcher.UIThread.RunJobs();
        Check(editor.Names.SequenceEqual(names)&&ConfigFingerprint(editor.Current())==before,"cancelling import preserves the current preset and edits");
        var named=Change(n=>{n["configuration"]!["touchPolicy"]="CloseOnly";n["configuration"]!["parameterTimeframes"]!["RsiPeriod"]!["unit"]="Hour";});
        var imported=StrategyDefinitionJson.Import(named);Check(imported.Configuration.TouchPolicy==DailyTouchPolicy.CloseOnly&&imported.Configuration.ParameterTimeframes["RsiPeriod"].Unit==TimeframeUnit.Hour,"import accepts documented English enum names alongside existing numeric enums");
        var mixed=Change(n=>{var frames=n["configuration"]!["parameterTimeframes"]!.AsObject();var frame=frames["RsiPeriod"];frames.Remove("RsiPeriod");frames["rsiPeriod"]=frame;});
        Check(StrategyDefinitionJson.Import(mixed).Configuration.ParameterTimeframes.ContainsKey("RsiPeriod"),"period dictionary keys normalize to their canonical field names");
        var partial=StrategyDefinitionJson.Import("{\"schemaVersion\":1,\"strategyId\":\"krx-oversold-rebound\",\"preset\":\"이전 파일\",\"configuration\":{\"reboundPercent\":4.25}}");
        Check(partial.Configuration.ReboundPercent==4.25m&&!partial.Configuration.UseCrashStrategy&&partial.Configuration.RsiPeriod==14,"old schema-1 files without added settings retain legacy defaults");
        var standalone=StrategyDefinitionJson.Export(baseline.CrashStrategy!);File.WriteAllText(path,standalone);editor.FileDialogs=new FixtureStrategyFiles(path);notices.Clear();editor.Import.Execute(null);Pump(()=>editor.UseCrashStrategy||notices.Count>0);
        Check(notices.Count==0&&editor.UseCrashStrategy&&editor.Current().CrashStrategy!.IsStrategyD,"standalone schema-2 D files import into the unified editor with D explicitly enabled");
        File.WriteAllText(path,standalone);var original=editor.Selected;editor.Import.Execute(null);Pump(()=>editor.Selected!=original||notices.Count>0);
        Check(editor.Selected==original+" 2"&&editor.Names.Contains(original),"duplicate preset names get a unique suffix while preserving the existing preset");
    }

    private static void StrategyJsonReference(OversoldConfig defaults)
    {
        defaults=defaults.Clone();defaults.CrashStrategy=defaults.CrashStrategy! with{ExportedAt=new DateTimeOffset(2026,11,10,0,0,0,TimeSpan.Zero)};
        var definition=new StrategyDefinition(1,StrategyDefinition.LegacyId,"설명용 기본값 · 기존 전략",new DateTimeOffset(2026,11,10,0,0,0,TimeSpan.Zero),defaults);
        var json=StrategyDefinitionJson.Export(definition);File.WriteAllText(Path.Combine(output,"strategy-example-full.json"),json);
        Check(SameConfig(StrategyDefinitionJson.Import(json).Configuration,defaults),"complete documentation JSON imports with the official parser");
        var active=defaults.Clone();active.UseCrashStrategy=true;
        File.WriteAllText(Path.Combine(output,"strategy-example-crash-enabled.json"),StrategyDefinitionJson.Export(definition with{Preset="설명용 기본값 · 급락 전략",Configuration=active}));
        var metadata=ParameterCatalog.Properties(defaults).Select(p=>new{Key=p.Name,JsonKey=JsonNamingPolicy.CamelCase.ConvertName(p.Name),Type=p.PropertyType.Name,Default=p.GetValue(defaults),Metadata=ParameterCatalog.For(p),Enum=p.PropertyType.IsEnum?Enum.GetNames(p.PropertyType):null});
        var fields=new StrategyDEditorModel(defaults.CrashStrategy!).Parameters.Select(f=>new{f.Path,f.Label,f.Group,f.Unit,f.Help,f.Text,f.Choices,Timeframe=f.TimeframeField?.Path});
        var enums=new[]{typeof(TimeframeUnit),typeof(WaitUnit),typeof(SignalEvaluationMode),typeof(RuleCombination),typeof(VolumeProfileSource),typeof(ReentryMode),typeof(ExitMode),typeof(TrailingPeakBasis),typeof(IndicatorKind),typeof(DailyTouchPolicy),typeof(TrailingActivation)}.ToDictionary(t=>t.Name,t=>Enum.GetValues(t).Cast<object>().ToDictionary(v=>v.ToString()!,Convert.ToInt32));
        File.WriteAllText(Path.Combine(output,"strategy-reference-catalog.json"),JsonSerializer.Serialize(new{Fields=metadata,CrashFields=fields,Enums=enums},new JsonSerializerOptions{WriteIndented=true}));
    }
}
