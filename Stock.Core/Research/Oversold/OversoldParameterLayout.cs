using Stock.Core.Trading;
using Config=Stock.Core.Research.Oversold.OversoldConfig;
namespace Stock.Core.Research.Oversold;

public sealed record ParameterLayoutRow(ParameterUsageScope Scope,string Group,IReadOnlyList<string?> Keys);
/// <summary>Explicit semantic rows; null is an intentional empty cell, never filled by a later field.</summary>
public static partial class OversoldParameterMetadata
{
    public static IReadOnlyList<ParameterLayoutRow> Rows {get;}=Array.AsReadOnly(new ParameterLayoutRow[]
    {
        new(ParameterUsageScope.Backtest,"모형 계좌 · 체결 비용",[nameof(Config.InitialCapital),null,null]),
        new(ParameterUsageScope.Backtest,"모형 계좌 · 체결 비용",[nameof(Config.CommissionPercent),nameof(Config.TaxPercent),nameof(Config.SlippagePercent)]),
        new(ParameterUsageScope.Backtest,"일봉 감지 가정",[nameof(Config.TouchPolicy),null,null]),
        new(ParameterUsageScope.Backtest,"일봉 감지 가정",[nameof(Config.UsePriceOnlyBacktest),null,null]),
        new(ParameterUsageScope.Backtest,"성과 통계",[nameof(Config.AnnualRiskFreePercent),nameof(Config.AnnualTradingDays),null]),
        new(ParameterUsageScope.CommonStrategy,"종목 선별 · 급락",[nameof(Config.AverageTradingValueLookbackDays),nameof(Config.MinimumAverageTradingValue),null]),
        new(ParameterUsageScope.CommonStrategy,"종목 선별 · 급락",[nameof(Config.CrashLookbackDays),nameof(Config.MinimumDrawdownPercent),null]),
        new(ParameterUsageScope.CommonStrategy,"종목 선별 · 급락",[nameof(Config.ShortCrashLookbackDays),nameof(Config.ShortCrashPercent),null]),
        new(ParameterUsageScope.CommonStrategy,"종목 선별 · 급락",[nameof(Config.UseRsiFilter),nameof(Config.RsiPeriod),nameof(Config.MaximumRsi)]),
        new(ParameterUsageScope.CommonStrategy,"종목 선별 · 급락",[nameof(Config.UseVolumeFilter),nameof(Config.VolumeLookbackDays),nameof(Config.CrashVolumeRatio)]),
        new(ParameterUsageScope.CommonStrategy,"지지 · 이동평균선",[nameof(Config.UseMovingAverageSupport),nameof(Config.MovingAveragePeriods),null]),
        new(ParameterUsageScope.CommonStrategy,"지지 · 과거 고점",[nameof(Config.UsePivotSupport),nameof(Config.PivotLeftBars),nameof(Config.PivotRightBars)]),
        new(ParameterUsageScope.CommonStrategy,"지지 · 근사 매물대",[nameof(Config.UseApproximateVolumeZone),nameof(Config.VolumeZoneLookbackDays),nameof(Config.VolumeZoneBinSize)]),
        new(ParameterUsageScope.CommonStrategy,"지지 · 근사 매물대",[nameof(Config.VolumeZoneMaxBins),nameof(Config.VolumeZoneThresholdPercent),nameof(Config.VolumeZoneMergeRangePercent)]),
        new(ParameterUsageScope.CommonStrategy,"지지 군집",[nameof(Config.SupportClusterRangePercent),nameof(Config.MinimumSupportCount),null]),
        new(ParameterUsageScope.CommonStrategy,"지지 군집",[nameof(Config.SupportDistancePercent),nameof(Config.ClusterChangeTolerancePercent),null]),
        new(ParameterUsageScope.CommonStrategy,"반등 확인",[nameof(Config.RequireReboundConfirmation),nameof(Config.ReboundPercent),nameof(Config.CandidateLifetimeDays)]),
        new(ParameterUsageScope.CommonStrategy,"분할매수",[nameof(Config.PlannedInvestment),nameof(Config.MaxEntries),nameof(Config.EntryRatios)]),
        new(ParameterUsageScope.CommonStrategy,"분할매수",[nameof(Config.AdditionalEntryDropPercent),nameof(Config.RequireSupportForAdditionalEntry),null]),
        new(ParameterUsageScope.CommonStrategy,"손절",[nameof(Config.SupportBreakPercent),null,null]),
        new(ParameterUsageScope.CommonStrategy,"손절",[nameof(Config.UseEmergencyStop),nameof(Config.EmergencyStopLossPercent),null]),
        new(ParameterUsageScope.CommonStrategy,"익절",[nameof(Config.UseTakeProfit1),nameof(Config.TakeProfit1Percent),nameof(Config.TakeProfit1Ratio)]),
        new(ParameterUsageScope.CommonStrategy,"익절",[nameof(Config.UseTakeProfit2),nameof(Config.TakeProfit2Percent),nameof(Config.TakeProfit2Ratio)]),
        new(ParameterUsageScope.CommonStrategy,"추적 손절",[nameof(Config.UseTrailingStop),nameof(Config.TrailingStopPercent),nameof(Config.TrailingActivation)]),
        new(ParameterUsageScope.CommonStrategy,"보유 · 재진입",[nameof(Config.MaxHoldingDays),nameof(Config.ReentryCooldownDays),null]),
        new(ParameterUsageScope.CommonStrategy,"보유 · 재진입",[nameof(Config.MinimumNewClusterDistancePercent),nameof(Config.AllowEarlyReentryWithNewCluster),null]),
    });
}

