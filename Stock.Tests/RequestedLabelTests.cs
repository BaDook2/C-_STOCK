using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void RequestedLabelChecks()
    {
        var labels=new Dictionary<string,string>
        {
            ["AverageTradingValueLookbackDays"]="평균 거래대금 기간",
            ["MinimumAverageTradingValue"]="최소 거래대금",
            ["CrashLookbackDays"]="중기 낙폭 기간",
            ["MinimumDrawdownPercent"]="중기 낙폭 기준",
            ["ShortCrashLookbackDays"]="단기 낙폭 기간",
            ["ShortCrashPercent"]="단기 낙폭 기준",
            ["UseRsiFilter"]="RSI 사용",
            ["RsiPeriod"]="RSI 기간",
            ["MaximumRsi"]="최대 RSI",
            ["UseVolumeFilter"]="거래량 조건 사용",
            ["VolumeLookbackDays"]="평균 거래량 기간",
            ["CrashVolumeRatio"]="거래량 증가 기준",
            ["UseMovingAverageSupport"]="이동평균선 사용",
            ["MovingAveragePeriods"]="이동평균선 기간",
            ["UsePivotSupport"]="과거 고점 사용",
            ["PivotLeftBars"]="고점 이전 비교 기간",
            ["PivotRightBars"]="고점 확정 기간",
            ["UseApproximateVolumeZone"]="근사 매물대 사용",
            ["VolumeZoneLookbackDays"]="매물대 분석 기간",
            ["VolumeZoneBinSize"]="가격 구간 폭",
            ["VolumeZoneMaxBins"]="최대 가격 구간 수",
            ["VolumeZoneThresholdPercent"]="매물대 거래량 기준",
            ["VolumeZoneMergeRangePercent"]="매물대 병합 거리",
            ["SupportClusterRangePercent"]="지지 군집 범위",
            ["MinimumSupportCount"]="최소 지지 개수",
            ["SupportDistancePercent"]="지지구간 접근 거리",
            ["ClusterChangeTolerancePercent"]="지지 군집 변경 기준",
            ["RequireReboundConfirmation"]="반등 확인 사용",
            ["ReboundPercent"]="반등 기준",
            ["CandidateLifetimeDays"]="진입 후보 유지 기간",
            ["PlannedInvestment"]="총 투자금",
            ["MaxEntries"]="최대 분할매수 횟수",
            ["EntryRatios"]="분할매수 비율",
            ["AdditionalEntryDropPercent"]="추가매수 하락 기준",
            ["RequireSupportForAdditionalEntry"]="추가매수 시 지지 확인",
            ["SupportBreakPercent"]="지지구간 이탈 기준",
            ["UseEmergencyStop"]="비상 손절 사용",
            ["EmergencyStopLossPercent"]="비상 손절 기준",
            ["UseTakeProfit1"]="1차 익절 사용",
            ["TakeProfit1Percent"]="1차 익절 기준",
            ["TakeProfit1Ratio"]="1차 익절 비율",
            ["UseTakeProfit2"]="2차 익절 사용",
            ["TakeProfit2Percent"]="2차 익절 기준",
            ["TakeProfit2Ratio"]="2차 익절 비율",
            ["UseTrailingStop"]="추적 손절 사용",
            ["TrailingStopPercent"]="추적 손절 폭",
            ["TrailingActivation"]="추적 손절 시작 기준",
            ["MaxHoldingDays"]="최대 보유기간",
            ["ReentryCooldownDays"]="재진입 대기기간",
            ["MinimumNewClusterDistancePercent"]="새 지지군집 거리",
            ["AllowEarlyReentryWithNewCluster"]="새 지지군집 발견 시 조기 재진입",
        };
        var properties=ParameterCatalog.Properties(new OversoldConfig()).ToDictionary(p=>p.Name);
        Check(labels.Count==51&&labels.All(pair=>{var metadata=ParameterCatalog.For(properties[pair.Key]);return metadata.DisplayName==pair.Value&&metadata.ShortDescription.Contains(pair.Value);}),"all51 requested Korean labels match card and tooltip metadata exactly");
    }
}

