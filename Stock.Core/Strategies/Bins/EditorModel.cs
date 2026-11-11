using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Stock.Core.Trading;
using TradingMetadata = Stock.Core.Trading.ParameterMetadata;

namespace Stock.Core.Strategies.Bins;

public sealed record BinsParameterChoice(string Value,string Label);
public sealed record BinsParameterGroup(string Name,BinsParameterField[] Fields);
/// <summary>Typed draft editor. Parsing never mutates an executing configuration; Apply returns a validated deep snapshot.</summary>
public sealed class BinsParameterField : INotifyPropertyChanged,IDataErrorInfo
{
    private readonly Type type;
    private readonly Action<JsonNode?> write;
    private readonly Action<string>? writeUnit;
    private string text,unit;
    public string Path {get;}
    public TradingMetadata Metadata {get;}
    public string Label=>Metadata.DisplayName;
    public bool IsBool=>type==typeof(bool);
    public bool IsChoice=>type.IsEnum;
    public bool IsText=>!IsBool&&!IsChoice;
    public bool HasTimeUnit=>writeUnit!=null;
    public bool ReadOnly {get;}
    public string Unit {get=>unit;set{if(!HasTimeUnit||string.IsNullOrEmpty(value)||value==unit)return;if(!UnitChoices.Any(x=>x.Value==value))throw new InvalidOperationException("시간 단위를 확인하세요.");unit=value;writeUnit!(value);Notify();}}
    public BinsParameterChoice[] UnitChoices {get;}
    public BinsParameterChoice[] Choices=>IsChoice?Enum.GetNames(type).Select(s=>new BinsParameterChoice(s,BinsLabels.Choice(s))).ToArray():[];
    public string Text {get=>text;set{if(value==null&&(IsBool||IsChoice))return;value??="";if(text==value)return;text=value;Notify();Notify(nameof(BoolValue));Notify(nameof(Error));}}
    public bool BoolValue {get=>bool.TryParse(text,out var b)&&b;set=>Text=value.ToString();}
    public string Help=>Metadata.DetailedDescription+(HasTimeUnit?"\n값과 단위를 함께 저장합니다.":"");
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([System.Runtime.CompilerServices.CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
    public BinsParameterField(string path,string label,string description,string displayUnit,Type valueType,JsonNode? value,Action<JsonNode?> write,
        BinParameterAttribute? range=null,string? timeUnit=null,bool barUnit=false,Action<string>? writeUnit=null,bool readOnly=false)
    {
        Path=path;type=System.Nullable.GetUnderlyingType(valueType)??valueType;this.write=write;this.writeUnit=writeUnit;unit=timeUnit??displayUnit;ReadOnly=readOnly;
        var numeric=type==typeof(int)||type==typeof(decimal);
        Metadata=new(path,label,description,description,"","",displayUnit,numeric&&range!=null?(double)range.Minimum:null,numeric&&range!=null?(double)range.Maximum:null,InputKind:type==typeof(int)?ParameterInputKind.Integer:type==typeof(decimal)?ParameterInputKind.Decimal:ParameterInputKind.Text);
        text=value==null?"":value.ToString();
        UnitChoices=writeUnit==null?[]:barUnit?[new("Second","초"),new("Minute","분"),new("Hour","시"),new("TradingDay","거래일 봉")]:[new("Second","초"),new("Minute","분"),new("Hour","시"),new("Day","일")];
        Nullable=System.Nullable.GetUnderlyingType(valueType)!=null;
    }
    public bool Nullable {get;}
    private JsonNode? Parse()
    {
        if(Nullable&&string.IsNullOrWhiteSpace(text))return null;
        if(type==typeof(string))return JsonValue.Create(text);
        if(type==typeof(bool))return JsonValue.Create(bool.Parse(text));
        if(type.IsEnum){if(!Enum.IsDefined(type,text))throw new FormatException();return JsonValue.Create(text);}
        if(type==typeof(DateOnly)){var d=DateOnly.ParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture);return JsonValue.Create(d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));}
        if(type==typeof(DateTimeOffset))
        {if(!System.Text.RegularExpressions.Regex.IsMatch(text,@"(Z|[+-]\d{2}:\d{2})$"))throw new FormatException();return JsonValue.Create(DateTimeOffset.Parse(text,CultureInfo.InvariantCulture).ToString("O",CultureInfo.InvariantCulture));}
        var n=decimal.Parse(text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
        if(Metadata.Min is {} min&&n<(decimal)min||Metadata.Max is {} max&&n>(decimal)max)throw new FormatException();
        return type==typeof(int)?n==decimal.Truncate(n)?JsonValue.Create(checked((int)n)):throw new FormatException():JsonValue.Create(n);
    }
    public string Error {get{try{_ =Parse();return "";}catch(Exception e)when(e is FormatException or OverflowException or ArgumentException){return Label+"의 값·형식·범위를 확인하세요.";}}}
    public string this[string name]=>Error;
    public void Apply(){if(Error!="")throw new InvalidOperationException(Error);write(Parse());}
}
public sealed class BinsEditorModel
{
    private readonly JsonObject draft;
    public BinsParameterGroup[] Groups {get;private set;}=[];
    public BinsParameterField[] Fields=>Groups.SelectMany(g=>g.Fields).ToArray();
    public event Action? GroupsChanged;
    public BinsEditorModel(BinsStrategyConfig config){draft=JsonNode.Parse(BinsJson.Export(config))!.AsObject();Build();}
    public BinsStrategyConfig Apply(bool enabled)
    {foreach(var field in Fields)field.Apply();draft["enabled"]=enabled;return BinsJson.Import(draft.ToJsonString(BinsJson.Options));}
    public void AddFee(string country)
    {
        foreach(var field in Fields)field.Apply();var array=draft["market"]!["countryProfiles"]![country]!["fees"]!.AsArray();
        var today=DateOnly.FromDateTime(DateTime.Today);var start=new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);
        var fee=new FeeRate(today,today.AddYears(1),start,"사용자 입력 가상 체결비용 가정",0,0,0,0);
        array.Add(JsonSerializer.SerializeToNode(fee,BinsJson.Options));Build();GroupsChanged?.Invoke();
    }
    public void RemoveFee(string country,int index)
    {foreach(var field in Fields)field.Apply();draft["market"]!["countryProfiles"]![country]!["fees"]!.AsArray().RemoveAt(index);Build();GroupsChanged?.Invoke();}
    private void Build()
    {
        var groups=new List<BinsParameterGroup>();
        groups.Add(new("설정 이름",[new("strategyName","전략 이름","Bin 설정의 이름입니다.","",typeof(string),draft["strategyName"],v=>draft["strategyName"]=v)]));
        void Group(string path,string title,Type t)
        {var fields=new List<BinsParameterField>();Walk(Get(path)!,t,path,fields);groups.Add(new(title,fields.ToArray()));}
        Group("selection","전략 가족 선택",typeof(SelectionConfig));Group("regime","시장 상태 분류",typeof(RegimeConfig));Group("stage1","1단계 · 지지구간 반등 허가",typeof(StageOneConfig));
        Group("entries.downtrendRebound","하락 반등",typeof(DowntrendReboundConfig));Group("entries.uptrendBreakout","상승 · 이전 고점 돌파",typeof(BreakoutConfig));Group("entries.uptrendPullback","상승 · 눌림목 반등",typeof(PullbackConfig));Group("entries.rangeLowerBounce","박스 · 하단 반등",typeof(RangeBounceConfig));Group("entries.rangeUpperBreakout","박스 · 상단 돌파",typeof(BreakoutConfig));
        var priorities=new List<BinsParameterField>();var priority=draft["entries"]!["patternPriority"]!.AsArray();
        for(int n=0;n<priority.Count;n++){var index=n;priorities.Add(new("entries.patternPriority."+n,"진입 우선순위 "+(n+1),"다섯 패턴을 중복 없이 선택합니다.","",typeof(EntryPattern),priority[n],value=>priority[index]=value));}groups.Add(new("동시 신호 우선순위",priorities.ToArray()));
        Group("sharedExit","공통 · 매도·손절·익절",typeof(CommonExitConfig));Group("sharedReentry","공통 · 재진입",typeof(CommonReentryConfig));Group("sharedRisk","공통 · 계좌 위험 한도",typeof(CommonRiskConfig));Group("quality","시세 품질·준비 자료",typeof(QualityConfig));Group("execution","주문·가상 체결",typeof(ExecutionPolicy));
        var market=new List<BinsParameterField>();var node=draft["market"]!.AsObject();market.Add(new("market.regularSessionOnly","정규장만 사용","장전·장후도 공급자의 확인 권한이 있어야 합니다.","",typeof(bool),node["regularSessionOnly"],v=>node["regularSessionOnly"]=v));
        foreach(var exchange in Enum.GetValues<Exchange>())
        {
            var name=exchange.ToString();market.Add(new("market.enabledExchanges."+name,name+" 사용","감시할 거래소를 선택합니다.","",typeof(bool),JsonValue.Create(node["enabledExchanges"]!.AsArray().Any(v=>v!.GetValue<string>()==name)),v=>{var enabled=node["enabledExchanges"]!.AsArray();var old=enabled.FirstOrDefault(n=>n!.GetValue<string>()==name);if(v!.GetValue<bool>()&&old==null)enabled.Add(name);else if(!v.GetValue<bool>()&&old!=null)enabled.Remove(old);}));
        }
        groups.Add(new("시장·거래소",market.ToArray()));
        foreach(var country in new[]{"krx","us"})
        {
            var path="market.countryProfiles."+country;Group(path,country=="krx"?"한국 · KRW 예산·유동성":"미국 · USD 예산·유동성",typeof(CountryProfile));
            var fees=Get(path)!["fees"]!.AsArray();for(int i=0;i<fees.Count;i++)Group(path+".fees."+i,(country=="krx"?"KRW":"USD")+" 비용표 "+(i+1),typeof(FeeRate));
        }
        Groups=groups.ToArray();
    }
    private JsonNode? Get(string path)
    {JsonNode? node=draft;foreach(var segment in path.Split('.'))node=node is JsonArray array?array[int.Parse(segment,CultureInfo.InvariantCulture)]:node![segment];return node;}
    private void Walk(JsonNode node,Type targetType,string path,List<BinsParameterField> fields)
    {
        foreach(var p in targetType.GetProperties().Where(p=>p.GetCustomAttribute<JsonIgnoreAttribute>()==null))
        {
            var key=JsonNamingPolicy.CamelCase.ConvertName(p.Name);var child=node[key];var a=p.GetCustomAttribute<BinParameterAttribute>();var fieldPath=path+"."+key;var label=BinsLabels.Field(p.Name);var type=System.Nullable.GetUnderlyingType(p.PropertyType)??p.PropertyType;
            if(type==typeof(DurationSpec)||type==typeof(BarTimeframeSpec))
            {
                var value=child!.AsObject();var bar=type==typeof(BarTimeframeSpec);
                fields.Add(new(fieldPath,label,a?.Description??label,bar?"봉 크기":"기간",bar?typeof(int):typeof(decimal),value["value"],v=>value["value"]=v,new BinParameterAttribute(label,minimum:bar?1:0),value["unit"]!.GetValue<string>(),bar,u=>value["unit"]=u));continue;
            }
            if(type.IsArray)continue;
            if(type==typeof(string)||type.IsEnum||type==typeof(bool)||type==typeof(int)||type==typeof(decimal)||type==typeof(DateOnly)||type==typeof(DateTimeOffset))
            {
                var fixedPolicy=fieldPath is "sharedRisk.maxConcurrentPositionsPerSymbol" or "sharedRisk.allowPyramiding" or "execution.allowUnverifiedLiveExecution" or "execution.emergencyMarketFallback" or "execution.entryOrderKind" or "execution.exitOrderKind";
                fields.Add(new(fieldPath,label,(a?.Description??label)+(type==typeof(DateOnly)?" · YYYY-MM-DD":type==typeof(DateTimeOffset)?" · UTC 또는 +09:00 등 시차 포함":""),a?.Unit??"",p.PropertyType,child,v=>node[key]=v,a,readOnly:fixedPolicy));
            }
            else if(child!=null)Walk(child,type,fieldPath,fields);
        }
    }
}
