using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Stock.Core.Trading;
using Stock.Core.Pipeline;
namespace Stock.App;

public sealed record ParameterChoice(string Value,string Label);
public sealed record ParameterCell(ParameterField? Field){public bool IsEmpty=>Field==null;}
public sealed record ParameterRow(IReadOnlyList<ParameterCell> Cells);
public sealed record ParameterGroup(string Name,IReadOnlyList<ParameterField> Fields,IReadOnlyList<ParameterRow>? Rows=null);
public sealed class ParameterField:Observable,IDataErrorInfo
{
    private readonly object target;private readonly PropertyInfo property;private readonly Action edited;private string value;
    public ParameterMetadata Metadata {get;}
    public ParameterField(object target,PropertyInfo property,Action edited){this.target=target;this.property=property;this.edited=edited;Metadata=ParameterCatalog.For(property);value=NumericDisplay.Format(property.GetValue(target));}
    public string Value {get=>value;set{if(this.value==value)return;this.value=value;Changed();Changed(nameof(InputText));Changed(nameof(InputTooltip));Changed(nameof(Error));edited();}}
    public string InputText {get=>FixedDecimalEdit.Display(value,Metadata.InputKind);set=>Value=value;}
    public bool IsList=>Metadata.InputKind is ParameterInputKind.DecimalList or ParameterInputKind.IntegerList;
    public string InputTooltip=>Metadata.ShortDescription+(FixedDecimalEdit.Enabled(Metadata.InputKind)?"\n"+FixedDecimalEdit.Help+(FixedDecimalEdit.Rounded(value,Metadata.InputKind)?"\n표시 반올림 · 정확한 저장값: "+value+". 편집하지 않으면 이 값을 보존합니다.":""):"");
    public bool BoolValue {get=>bool.TryParse(value,out var b)&&b;set{Value=value.ToString();Changed();}}
    public bool IsBool=>property.PropertyType==typeof(bool);
    public bool IsEnum=>property.PropertyType.IsEnum;
    public bool IsText=>!IsBool&&!IsEnum;
    public ParameterChoice[] Choices=>IsEnum?Enum.GetNames(property.PropertyType).Select(n=>new ParameterChoice(n,n switch{"Market"=>"시장가","Limit"=>"지정가","ConservativeHighLow"=>"고저가 감지 · 손절 우선","CloseOnly"=>"확정 종가만 감지","AfterEntry"=>"첫 매수 체결 후","AfterTakeProfit1"=>"1차 익절 체결 후","US"=>"해외 (미국)","Korea"=>"국내","All"=>"전체",_=>n})).ToArray():[];
    public string Range=>Metadata.Min is {} min?$"{Metadata.Unit} · {min:#,##0.########} ~ {Metadata.Max:#,##0.########}":Metadata.Unit;
    public string Error {get{try{_ = Parse();return "";}catch{return Metadata.DisplayName+": 값과 허용 범위를 확인하세요.";}}}
    public string this[string name]=>name is nameof(Value) or nameof(InputText)?Error:"";
    private object Parse()
    {
        var type=property.PropertyType;if(type==typeof(string))return value;if(type==typeof(bool))return bool.Parse(value);
        if(type.IsEnum){var item=Enum.Parse(type,value);return Enum.IsDefined(type,item)?item:throw new FormatException();}
        var number=decimal.Parse(value,NumberStyles.Number,CultureInfo.InvariantCulture);if(number<(decimal)Metadata.Min!.Value||number>(decimal)Metadata.Max!.Value)throw new FormatException();
        if(type==typeof(int))return number==decimal.Truncate(number)?checked((int)number):throw new FormatException();return number;
    }
    public void Apply(){if(Error!="")throw new InvalidOperationException(Error);property.SetValue(target,Parse());}
}

public sealed partial class WorkspaceViewModel
{
    private StrategyConfiguration strategyConfiguration=new();
    private string configurationMessage="저장된 실제 체결 데이터에 적용합니다. 기존 일봉 전략은 아래에서 계속 설정할 수 있어요.";
    public ObservableCollection<ParameterGroup> ParameterGroups {get;}=[];
    public ParameterChoice[] StrategyChoices {get;}=[new("Oversold","국내주식 과매도·낙주 반등")];
    public string StrategyChoice {get=>"Oversold";set{if(!LegacyStrategiesAvailable)return;if(!CanConfigure||string.IsNullOrEmpty(value)||value==StrategyChoice)return;try{ApplyParameterFields();strategyConfiguration.StrategyType=Enum.Parse<StrategyType>(value);if(StandardMode&&value!="Pipeline")strategyConfiguration.Advanced=strategyConfiguration.Resolve();BuildParameterGroups();Changed();Changed(nameof(IsPipeline));Changed(nameof(PipelineStrategyChoice));}catch(InvalidOperationException ex){ConfigurationMessage=ex.Message;}}}
    public bool UseConfiguredStrategy {get=>false;set{Changed();}}
    public bool StandardMode {get=>strategyConfiguration.Mode==StrategyConfigMode.Standard;set{if(value)SetConfigurationMode(StrategyConfigMode.Standard);}}
    public bool AdvancedMode {get=>!StandardMode;set{if(value)SetConfigurationMode(StrategyConfigMode.Advanced);}}
    public string ConfigurationState=>strategyConfiguration.State.ToString();
    public string ConfigurationMessage {get=>configurationMessage;private set{configurationMessage=value;Changed();}}
    private void InitializeStrategyEditor()
    {
        strategyConfiguration=store.State<StrategyConfiguration>("strategy-configuration-v1")??new();InitializePipeline();BuildParameterGroups();
        Add("전략설정저장",()=>{SaveStrategyConfiguration();Log("Standard / Advanced 설정을 저장했습니다.");return Task.CompletedTask;},()=>CanConfigure);
        Add("틱백테스트",RunConfiguredBacktest,()=>CanConfigure&&IsOffline&&selected!=null&&UseConfiguredStrategy);
    }
    private void SetConfigurationMode(StrategyConfigMode mode)
    {
        if(mode==strategyConfiguration.Mode||!CanConfigure)return;
        try{ApplyParameterFields();if(mode==StrategyConfigMode.Advanced){if(IsPipeline)strategyConfiguration.Pipeline.Advanced=strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode);else strategyConfiguration.Advanced=strategyConfiguration.Resolve();}strategyConfiguration.Mode=mode;BuildParameterGroups();ConfigurationMessage=mode==StrategyConfigMode.Advanced?"실제 엔진에 전달되는 값입니다. 직접 수정하면 Custom으로 저장됩니다.":"핵심 값을 Advanced 파라미터로 변환합니다. 기존 Custom 세부값은 Standard 변환 시 다시 계산됩니다.";Changed(nameof(StandardMode));Changed(nameof(AdvancedMode));}
        catch(InvalidOperationException ex){ConfigurationMessage=ex.Message;Changed(nameof(StandardMode));Changed(nameof(AdvancedMode));}
    }
    private void ApplyParameterFields(){foreach(var f in ParameterGroups.SelectMany(g=>g.Fields))f.Apply();if(IsPipeline)strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode).Validate();else strategyConfiguration.Resolve().Validate();}
    private void SaveStrategyConfiguration()
    {ApplyParameterFields();if(IsPipeline)strategyConfiguration.Pipeline.Advanced=strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode);else strategyConfiguration.Advanced=strategyConfiguration.Resolve();store.State("strategy-configuration-v1",strategyConfiguration);ConfigurationMessage="설정을 저장했습니다. 재실행 시 같은 모드와 값을 복원합니다.";}
    private TradingSettings ConfiguredEngineSettings()
    {EnsureLegacyUnavailable();ApplyParameterFields();foreach(var f in TickFields)f.Apply();var baseline=tickSettings.Clone();baseline.Risk.AccountCapital=InitialCash();if(IsPipeline){var config=strategyConfiguration.Pipeline.Resolve(strategyConfiguration.Mode);var result=PipelinePresetMapper.ToEngine(config,baseline);result.PipelineObservations=PipelineObservationRows();return result;}return StrategyPresetMapper.ToEngine(strategyConfiguration.Resolve(),baseline);}
    private void BuildParameterGroups()
    {
        ParameterGroups.Clear();
        void Group(string name,object target,Func<PropertyInfo,bool>? filter=null)
        {var props=ParameterCatalog.Properties(target).Where(p=>filter?.Invoke(p)??true);ParameterGroups.Add(new(name,props.Select(p=>new ParameterField(target,p,()=>{strategyConfiguration.State=StandardMode?StrategyConfigState.Preset:StrategyConfigState.Custom;Changed(nameof(ConfigurationState));})).ToArray()));}
        if(IsPipeline)
        {
            var cfg=strategyConfiguration.Pipeline;
            if(StandardMode){foreach(var (name,target) in cfg.Standard.Sections())Group(name,target);Group("Risk",cfg.Advanced.Risk);Group("Execution",cfg.Advanced.Execution);}
            else foreach(var (name,target) in cfg.Advanced.Sections())Group(name,target);
            Changed(nameof(ConfigurationState));return;
        }
        if(StandardMode)
        {
            Group("공통 설정",strategyConfiguration.Standard.Common);
            Group("전략 핵심 설정",strategyConfiguration.StrategyType switch{StrategyType.Scalping=>(object)strategyConfiguration.Standard.Scalping,StrategyType.Box=>strategyConfiguration.Standard.Box,_=>strategyConfiguration.Standard.Rebound});
        }
        else
        {
            var a=strategyConfiguration.Advanced;
            Group("Entry · Exit 공통",a.Common,p=>p.Name is "Investment" or "StopLossPercent" or "TakeProfitPercent" or "MaxHoldingSeconds" or "TradingStart" or "TradingEnd");
            object signal=strategyConfiguration.StrategyType switch{StrategyType.Scalping=>a.Scalping,StrategyType.Box=>a.Box,_=>a.Rebound};
            bool Exit(PropertyInfo p)=>p.Name.Contains("Exit")||p.Name.Contains("Decay")||p.Name.Contains("Breakdown")||p.Name is "ResistanceDistancePercent";
            bool Entry(PropertyInfo p)=>p.Name.Contains("Score")||p.Name is "SupportDistancePercent" or "MinimumReboundPercent" or "ReboundConfirmationSeconds" or "BuyTradeRatio" or "AutoBuy" or "NewsFilter";
            Group("Signal · 신호 계산",signal,p=>!Exit(p)&&!Entry(p));Group("Entry · 진입",signal,p=>!Exit(p)&&Entry(p));Group("Exit · 청산",signal,Exit);
            Group("Risk · 위험 관리",a.Risk);Group("Execution · 주문과 비용",a.Execution);
        }
        Changed(nameof(ConfigurationState));
    }
}
