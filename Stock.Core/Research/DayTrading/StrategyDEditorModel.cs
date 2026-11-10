using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record DayTradingChoice(string Value,string Label);

/// <summary>Framework-independent editor metadata. Decimal text preserves 0.015 exactly; no display rounding is written back.</summary>
public sealed class DayTradingParameter:INotifyPropertyChanged
{
    private string text;
    internal Type Type {get;}
    public string Path {get;}
    public string Label {get;}
    public string Group {get;}
    public string Unit {get;}
    public string Help {get;}
    public bool Advanced {get;}
    public bool IsBool=>Type==typeof(bool);
    public bool IsChoice=>Type.IsEnum;
    public bool IsText=>!IsBool&&!IsChoice;
    public DayTradingParameter? TimeframeField {get;internal set;}
    public bool HasTimeframe=>TimeframeField!=null;
    public DayTradingChoice[] Choices {get;}
    public string Text {get=>text;set{if(text==value)return;text=value;Changed(nameof(Text));Changed(nameof(BoolValue));}}
    public bool BoolValue {get=>bool.TryParse(Text,out var value)&&value;set=>Text=value.ToString();}
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed(string name)=>PropertyChanged?.Invoke(this,new(name));
    internal DayTradingParameter(string path,Type type,object value,string label,string group,string unit,string help,bool advanced)
    {
        Path=path;Type=type;Label=label;Group=group;Unit=unit;Help=help+"\n"+path;Advanced=advanced;
        text=type.IsEnum?Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture):Convert.ToString(value,CultureInfo.InvariantCulture)??"";
        Choices=type.IsEnum?Enum.GetValues(type).Cast<object>().Where(v=>type!=typeof(TimeframeUnit)||v is TimeframeUnit.Minute or TimeframeUnit.Hour or TimeframeUnit.TradingDay or TimeframeUnit.Week or TimeframeUnit.Month).Select(v=>new DayTradingChoice(Convert.ToInt32(v).ToString(CultureInfo.InvariantCulture),EnumLabel(v))).ToArray():[];
    }
    internal JsonNode? Parse()
    {
        try
        {
            object value=Type==typeof(bool)?bool.Parse(Text):Type.IsEnum?Enum.ToObject(Type,int.Parse(Text,CultureInfo.InvariantCulture)):
                Type==typeof(int)?int.Parse(Text,CultureInfo.InvariantCulture):Type==typeof(decimal)?decimal.Parse(Text,NumberStyles.Number,CultureInfo.InvariantCulture):
                Type==typeof(TimeOnly)?TimeOnly.ParseExact(Text,["HH:mm:ss","HH:mm","HH:mm:ss.fffffff"],CultureInfo.InvariantCulture):Text;
            if(Type.IsEnum&&!Enum.IsDefined(Type,value))throw new FormatException();
            return JsonSerializer.SerializeToNode(value,Type,Serialization.Json);
        }
        catch(Exception ex) when(ex is FormatException or OverflowException or ArgumentException){throw new InvalidOperationException(Label+"의 값과 단위를 확인하세요.",ex);}
    }
    private static string EnumLabel(object value)=>value.ToString() switch
    {
        "Trade"=>"체결 수 (원본 필요)","Second"=>"초 (원본 필요)","Minute"=>"분","Hour"=>"시간","TradingDay"=>"일봉","Week"=>"주봉","Month"=>"월봉",
        "OnTick"=>"새 체결마다","OnActiveBarUpdate"=>"진행봉 갱신마다","OnBarClosed"=>"봉 마감 때",
        "Any"=>"하나 이상 (OR)","All"=>"모두 (AND)","Approximate"=>"OHLCV 근사 매물대","TradeBased"=>"실제 체결 매물대 (원본 필요)",
        "NewLowerZoneOnly"=>"더 낮은 새 구간만","SameZoneWithFreshSignal"=>"같은 구간 새 반등 허용","NewZoneAnyDirection"=>"새 구간 허용","Disabled"=>"재진입 안 함",
        "FullTakeProfit"=>"목표에서 전량 익절","PartialTakeProfit"=>"1·2차 분할 익절","TrailingOnly"=>"추적 중심",
        "FromEntry"=>"첫 체결 후 고점 유지","ResetAtTakeProfit1"=>"1차 익절 후 고점 재시작","AfterEntry"=>"첫 체결 후","AfterTakeProfit1"=>"1차 익절 체결 후",_=>value.ToString()??""
    };
}

/// <summary>Edits a private snapshot atomically. Legacy definitions and user settings are never implicitly migrated.</summary>
public sealed class StrategyDEditorModel
{
    private readonly StrategyDefinition original;
    public IReadOnlyList<DayTradingParameter> Parameters {get;}
    public StrategyDEditorModel(StrategyDefinition definition)
    {
        original=definition.Snapshot();if(!original.IsStrategyD)throw new InvalidOperationException("전략 D 편집기에는 D 정의가 필요합니다.");
        var result=new List<DayTradingParameter>();
        var activeLegacy=new HashSet<string>("AverageTradingValueLookbackDays MinimumAverageTradingValue CrashLookbackDays MinimumDrawdownPercent ShortCrashLookbackDays ShortCrashPercent UseRsiFilter RsiPeriod MaximumRsi UseVolumeFilter VolumeLookbackDays CrashVolumeRatio MovingAveragePeriods VolumeZoneLookbackDays VolumeZoneBinSize VolumeZoneMaxBins VolumeZoneThresholdPercent VolumeZoneMergeRangePercent SupportDistancePercent ClusterChangeTolerancePercent ReboundPercent PlannedInvestment MaxEntries EntryRatios AdditionalEntryDropPercent RequireSupportForAdditionalEntry SupportBreakPercent UseEmergencyStop EmergencyStopLossPercent UseTakeProfit1 TakeProfit1Percent TakeProfit1Ratio UseTakeProfit2 TakeProfit2Percent TakeProfit2Ratio UseTrailingStop TrailingStopPercent TrailingActivation MaxHoldingDays CommissionPercent TaxPercent SlippagePercent".Split(' '));
        var simple=new HashSet<string>("MovingAveragePeriods ReboundPercent PlannedInvestment SupportBreakPercent UseEmergencyStop EmergencyStopLossPercent TakeProfit1Percent TakeProfit2Percent TrailingStopPercent CommissionPercent TaxPercent SlippagePercent".Split(' '));
        foreach(var property in ParameterCatalog.Properties(original.Configuration).Where(p=>activeLegacy.Contains(p.Name)))
        {
            var meta=ParameterCatalog.For(property);result.Add(new("Configuration."+property.Name,property.PropertyType,property.GetValue(original.Configuration)!,meta.DisplayName,"공통 전략 수치",meta.Unit,meta.ShortDescription+(meta.Min is {} min?$" · 허용 {min} ~ {meta.Max}":""),!simple.Contains(property.Name)));
        }
        void Walk(object target,string path)
        {
            foreach(var p in target.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanWrite))
            {
                var value=p.GetValue(target);if(value==null||p.Name==nameof(DayTradingPolicy.IndicatorTimeframes))continue;
                var key=path+"."+p.Name;var type=p.PropertyType;
                if(type.IsClass&&type!=typeof(string)||type==typeof(TimeframeSpec)||type==typeof(WaitPeriod)){Walk(value,key);continue;}
                var name=Names.GetValueOrDefault(p.Name,p.Name);var group=path.Split('.').Skip(1).FirstOrDefault()??"기본 분석";
                group=Names.GetValueOrDefault(group,group);
                var advanced=key.Contains("Quality.")||key.Contains("Crash.")||key.Contains("Reentry.")||key.Contains("MovingAverageTimeframe")||key.Contains("ProfileTimeframe")||key.Contains("CandidateLifetime")||p.Name is "TimeZoneId" or "IncludeActiveMovingAverageBar" or "RiskEvaluation" or "AdvancedTimeframes" or "PeakBasis";
                var unit=p.Name.Contains("Percent")?"%":p.Name.Contains("Milliseconds")?"ms":p.Name.Contains("Cash")||p.Name.Contains("Exposure")||p.Name.Contains("Loss")||p.Name.Contains("Budget")?"원":p.Name.Contains("Entries")||p.Name.Contains("Events")||p.Name.Contains("Stops")?"회/개":p.Name=="Value"||p.Name=="Size"?"옆의 단위 기준 개수":type==typeof(TimeOnly)?"HH:mm · 확인된 시장 세션 내":"";
                result.Add(new(key,type,value,name,group,unit,HelpFor(key),advanced));
            }
        }
        Walk(original.DayTrading!,"DayTrading");
        foreach(var property in ParameterCatalog.Properties(original.Configuration).Where(p=>ParameterCatalog.For(p).Unit=="봉"&&activeLegacy.Contains(p.Name)))
        {
            var frame=original.Configuration.ParameterTimeframes.GetValueOrDefault(property.Name)??new TimeframeSpec(1,TimeframeUnit.TradingDay);
            var unit=new DayTradingParameter("Configuration.ParameterTimeframes."+property.Name+".Unit",typeof(TimeframeUnit),frame.Unit,"단위","공통 전략 수치","","지표별 판단 봉 기준입니다.",false);
            result.First(p=>p.Path=="Configuration."+property.Name).TimeframeField=unit;result.Add(unit);
        }
        foreach(var field in result.Where(p=>p.Path.EndsWith(".Size")||p.Path.EndsWith(".Value")))
            field.TimeframeField=result.FirstOrDefault(p=>p.Path==field.Path[..field.Path.LastIndexOf('.')]+".Unit");
        var ma=result.FirstOrDefault(p=>p.Path=="Configuration.MovingAveragePeriods");
        if(ma!=null)ma.TimeframeField=result.FirstOrDefault(p=>p.Path=="DayTrading.Support.MovingAverageTimeframe.Unit");
        Parameters=result;
    }
    public StrategyDefinition Apply(string name)
    {
        var root=JsonSerializer.SerializeToNode(original,Serialization.Json)!;
        foreach(var field in Parameters)
        {
            var parts=field.Path.Split('.');var node=root;
            foreach(var part in parts[..^1]){if(node[part]==null)node[part]=new JsonObject();node=node[part]!;}
            var parsed=field.Parse();
            // The unit-only UI must preserve an imported 3-hour/5-minute frame.
            // A newly selected unit starts at one, including calendar units.
            if(field.Path.Contains(".ParameterTimeframes.")&&(node["Size"]==null||!JsonNode.DeepEquals(node[parts[^1]],parsed)))node["Size"]=1;
            node[parts[^1]]=parsed;
        }
        root[nameof(StrategyDefinition.Preset)]=name.Trim();root[nameof(StrategyDefinition.ExportedAt)]=DateTimeOffset.UtcNow;
        var result=root.Deserialize<StrategyDefinition>(Serialization.Json)!;result.Validate();return result.Snapshot();
    }
    private static string HelpFor(string path)=>path switch
    {
        _ when path.Contains("MovingAverageTimeframe")=>"기본은 전일 확정 일봉 MA240입니다. 기본 분석봉을 분봉으로 바꿔도 자동으로 240분선이 되지 않습니다.",
        _ when path.Contains("Quality")=>"시각/연속성/당시 상태가 미확인되면 신규 진입을 차단합니다. 완화해도 실계좌 승인이 되지 않습니다.",
        _ when path.Contains("Session")=>"독립 시계로 진입 종료와 청산 요청을 관리합니다. 청산 체결을 보장하지 않으며 실제 시장 세션 자료가 필요합니다.",
        _ when path.Contains("Reentry")=>"새 저점과 새 상향 반등이 있어야 재진입합니다. 신호·주문·체결은 구분됩니다.",
        _ when path.EndsWith("AdvancedTimeframes")=>"지표별 기준은 JSON의 IndicatorTimeframes로 설정할 수 있습니다. 간편 모드에서는 기본 분석 기준과 독립 일봉 MA/매물대를 사용합니다.",
        _=>"실행 전 전체 설정을 검증합니다. OFF 조건은 적용되지 않으며 관련 숫자는 저장해 둡니다. 기간은 함께 표시한 단위를 따릅니다."
    };
    private static readonly Dictionary<string,string> Names=new()
    {
        ["SimpleTimeframe"]="기본 분석 기준",["AdvancedTimeframes"]="지표별 기준 사용",["Evaluation"]="신호 평가 시점",["Size"]="크기",["Unit"]="단위",["Value"]="기간",
        ["Crash"]="급락",["Support"]="MA·매물대 중첩",["Reentry"]="반복 진입",["Exit"]="청산",["Session"]="당일 운용",["Risk"]="계좌 한도",["Quality"]="자료 안전",
        ["DailyEnabled"]="최근 며칠 급락 사용",["MediumEnabled"]="중기 낙폭 사용",["ShortEnabled"]="단기 낙폭 사용",["DailyCombination"]="일봉 급락 결합",["IntradayEnabled"]="장중 급락 사용",
        ["SessionHighEnabled"]="당일 고점 급락 사용",["SessionDrawdownPercent"]="당일 급락 기준",["RollingHighEnabled"]="최근 구간 급락 사용",["RollingWindow"]="장중 고점 기간",["RollingDrawdownPercent"]="구간 급락 기준",["IntradayCombination"]="장중 급락 결합",["DailyIntradayCombination"]="일봉·장중 결합",["CandidateLifetime"]="급락 후보 유효기간",
        ["MovingAverageTimeframe"]="이동평균 기준",["IncludeActiveMovingAverageBar"]="진행봉 MA 포함",["ProfileSource"]="매물대 자료",["ProfileTimeframe"]="매물대 기준",["MaxOverlapDistancePercent"]="MA·매물대 최대 거리",["MaxZoneWidthPercent"]="매물대 최대 폭",["MaxEntryDistanceFromSupportPercent"]="최대 추격 거리",["ReboundCandidateLifetime"]="반등 후보 유효기간",
        ["Mode"]="실행 방식",["MinimumInterval"]="최소 재진입 간격",["AfterProfitCooldown"]="익절 후 대기",["AfterStopCooldown"]="손절 후 대기",["MaxEntriesPerSymbolPerDay"]="종목별 하루 진입 한도",["MaxEntriesPerStrategyPerDay"]="전략별 하루 진입 한도",["MaxEntriesPerAccountPerDay"]="계좌별 하루 진입 한도",
        ["SupportStopEnabled"]="지지 이탈 손절 사용",["PeakBasis"]="추적 최고가 기준",["BreakEvenAfterTakeProfit1"]="1차 익절 후 본전 보호",["RiskEvaluation"]="보호 조건 평가 시점",
        ["DayOnly"]="당일 청산 사용",["NoNewEntryAfter"]="신규 진입 종료",["LiquidationStartAt"]="청산 요청 시작",["TimeZoneId"]="시장 시간대",
        ["MaxOrderCash"]="주문당 최대 금액",["MaxConcurrentExposure"]="전체 투자 한도",["MaxDailyLoss"]="하루 손실 한도",["RiskBudgetPerPosition"]="종목당 손절 위험예산",["MaxConsecutiveStops"]="연속 손절 한도",
        ["MaximumAgeMilliseconds"]="시세 최대 지연",["MaximumBufferedEvents"]="원본 메모리 한도",["MaximumQueuedEvents"]="처리 대기 한도",["RequireKnownSecurityAndRisk"]="당시 종목·위험 확인 필수",["RequireContinuousTrades"]="연속 체결 확인 필수",["RequireVerifiedExchangeTime"]="거래소 시각 확인 필수"
    };
}
