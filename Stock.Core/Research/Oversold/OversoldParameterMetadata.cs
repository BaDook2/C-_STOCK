using Stock.Core.Trading;
using Config=Stock.Core.Research.Oversold.OversoldConfig;
namespace Stock.Core.Research.Oversold;

public sealed record ParameterUsageSection(ParameterUsageScope Scope,string Name,string Description,string Status,string EmptyMessage="");

/// <summary>Presentation metadata only. Explicit membership never changes config values or engine behavior.</summary>
public static partial class OversoldParameterMetadata
{
    public static IReadOnlyList<ParameterUsageSection> Sections {get;}=Array.AsReadOnly(new[]
    {
        new ParameterUsageSection(ParameterUsageScope.Backtest,"백테스트 전용",
            "과거 성과를 계산하기 위한 자금·비용·일봉 감지·통계 가정입니다. 실제 계좌의 잔고나 수수료를 설정하는 값이 아닙니다.",
            "스크리닝도 전체 설정을 검증하지만 과거 봉부터 진입/반등 후보 상태를 순서대로 확인합니다. 체결·청산·성과 가정으로 후보를 판정하지 않습니다."),
        new ParameterUsageSection(ParameterUsageScope.CommonStrategy,"공통 전략 조건",
            "어떤 종목을 고르고 언제 진입·추가 매수·청산할지 정하는 전략 규칙입니다. 백테스트 환경 가정과 구분해 관리합니다.",
            "현재 연결: 일봉 백테스트 / 선택 봉의 순차 진입 스크리닝. 실계좌 자동매매에는 아직 적용되지 않습니다. 스크리닝은 체결을 만들지 않으며 실제 보유·청산은 계산하지 않습니다."),
        new ParameterUsageSection(ParameterUsageScope.Online,"온라인 투자 전용",
            "실제 계좌 주문에만 적용되는 전략 파라미터를 구분하는 영역입니다.",
            "기존 수동 주문 수량·1회 매수 한도는 종목·차트 → 실제 계좌 주문에서 설정합니다.",
            "현재 이 전략의 온라인 자동매매는 미지원이며 전용 파라미터가 없습니다.")
    });

    private static readonly Lazy<IReadOnlyDictionary<string,ParameterLayoutRow>> MembershipMap=new(()=>Rows.SelectMany(row=>row.Keys.Where(k=>k!=null).Select(k=>(Key:k!,Row:row))).ToDictionary(x=>x.Key,x=>x.Row));
    private static IReadOnlyDictionary<string,ParameterLayoutRow> ByKey=>MembershipMap.Value;
    public static IReadOnlyCollection<string> Keys=>ByKey.Keys.ToArray();

    public static ParameterMetadata Describe(ParameterMetadata metadata)
    {
        if(!ByKey.TryGetValue(metadata.Key,out var member))throw new InvalidOperationException("사용범위 메타데이터가 없습니다: "+metadata.Key);
        var section=Sections.Single(s=>s.Scope==member.Scope);
        var usage=metadata.Key switch
        {
            nameof(Config.InitialCapital)=>"독립 모형 계좌의 현금과 성과 기준입니다. 스크리닝에서도 초기 현금이 총 예정 투자금 이상인지 검증합니다(초기 현금 ≥ 예정 투자금). 스크리닝의 진입 후보 신호에는 현금 체결 계산을 사용하지 않습니다.",
            nameof(Config.CommissionPercent) or nameof(Config.TaxPercent) or nameof(Config.SlippagePercent)=>"백테스트의 모형 체결가격·정수 매수 수량·잔액·손익에 적용합니다. 현재 스크리닝은 체결 없이 진입 신호만 확인하므로 후보 판정에 사용하지 않습니다. 실계좌의 실제 청구 비용을 변경하지 않습니다.",
            nameof(Config.TouchPolicy)=>"손절·익절 수치를 정하는 전략 조건과 달리, 그 수치에 도달했는지를 일봉 고저가 또는 확정 종가로 판단하는 모형 가정입니다. 비상손절·익절·추적 손절 감지에 적용하며, 최초 지지 하단 이탈은 언제나 종가로 판단합니다. 현재 스크리닝은 보유 포지션이 없어 이 정책을 사용하지 않습니다. 실제 장중 체결 감지나 주문 방식 설정이 아닙니다.",
            nameof(Config.AnnualRiskFreePercent) or nameof(Config.AnnualTradingDays)=>"PerformanceCalculator의 Sharpe 통계에만 사용합니다. 매수·매도 신호와 체결가격에는 영향을 주지 않습니다. 스크리닝에서 성과 계산 함수는 재사용하지만 후보 선별에는 이 통계를 사용하지 않습니다.",
            nameof(Config.PlannedInvestment)=>"총 전략 예산이며 실제 계좌 수동 주문 한도와 별개입니다. 분할 비율과 함께 백테스트 매수 수량을 정합니다. 스크리닝에서도 초기 현금 이하의 값인지 검증하지만 현재 스크리닝 진입 신호에는 체결 수량을 사용하지 않습니다.",
            _=>"백테스트에서 전략 판단에 사용하는 값입니다. 스크리닝은 과거 봉부터 진입·반등 상태를 평가하며 체결을 생성하지 않으므로 추가 매수·보유·청산·재진입 상태는 실행하지 않습니다. 공통이라는 분류는 현재 실계좌에 적용된다는 뜻이 아닙니다."
        };
        return metadata with {Category=section.Name,Group=member.Group,UsageScope=member.Scope,UsageDescription=usage,InputKind=metadata.Key==nameof(Config.EntryRatios)?ParameterInputKind.DecimalList:metadata.Key==nameof(Config.MovingAveragePeriods)?ParameterInputKind.IntegerList:metadata.InputKind,ShortDescription=metadata.DisplayName+" · "+metadata.ShortDescription+"\n사용범위: "+section.Name+" · 현재 실계좌 자동매매 미지원"};
    }
}
