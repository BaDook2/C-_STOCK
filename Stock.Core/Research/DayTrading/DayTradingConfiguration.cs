using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public enum TimeframeUnit { Trade, Second, Minute, Hour, TradingDay, Week, Month }
/// <summary>A trade is an event count, never an elapsed second. Trading days follow supplied sessions.</summary>
public sealed record TimeframeSpec(int Size,TimeframeUnit Unit)
{
    public void Validate()
    {
        if(Size<=0||Size>1_000_000||!Enum.IsDefined(Unit)||Unit is TimeframeUnit.TradingDay or TimeframeUnit.Week or TimeframeUnit.Month&&Size!=1)
            throw new InvalidOperationException("분석 기준은 양의 크기와 체결/초/분/시간/거래일이어야 합니다. 일봉은 1거래일입니다.");
    }
    [System.Text.Json.Serialization.JsonIgnore] public TimeSpan? Duration=>Unit switch
    {
        TimeframeUnit.Second=>TimeSpan.FromSeconds(Size),TimeframeUnit.Minute=>TimeSpan.FromMinutes(Size),
        TimeframeUnit.Hour=>TimeSpan.FromHours(Size),_=>null
    };
    public override string ToString()=>$"{Size} {Unit switch{TimeframeUnit.Trade=>"체결",TimeframeUnit.Second=>"초",TimeframeUnit.Minute=>"분",TimeframeUnit.Hour=>"시간",TimeframeUnit.Week=>"주봉",TimeframeUnit.Month=>"월봉",_=>"일봉"}}";
}
public sealed class DataQualityPolicy
{
    public int MaximumAgeMilliseconds {get;set;}=2_000;
    public int MaximumBufferedEvents {get;set;}=200_000;
    public int MaximumQueuedEvents {get;set;}=4_096;
    public bool RequireKnownSecurityAndRisk {get;set;}=true;
    public bool RequireContinuousTrades {get;set;}=true;
    public bool RequireVerifiedExchangeTime {get;set;}=true;
}

/// <summary>Versioned additive policies. No legacy config property or meaning is changed.</summary>
