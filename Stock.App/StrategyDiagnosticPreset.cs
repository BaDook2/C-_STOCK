using Stock.Core.Research.Oversold;

namespace Stock.App;

/// <summary>A saved strategy preset that makes disabled screening conditions easy to distinguish from missing and skipped conditions.</summary>
public static class StrategyDiagnosticPreset
{
    public const string Name="전략 진단";
    public const string HoldOneDayName="백테스트 검증 1 · 1일 보유";
    public const string QuickProfitName="백테스트 검증 2 · 초단기 익절";
    public const string SupportBreakName="백테스트 검증 3 · 지지이탈";

    public static OversoldConfig Configuration()=>new()
    {
        UseRsiFilter=false,
        UseVolumeFilter=false,
        UseMovingAverageSupport=true,
        MovingAveragePeriods="5",
        UsePivotSupport=false,
        UseApproximateVolumeZone=false,
        MinimumSupportCount=1,
        RequireReboundConfirmation=false
    };

    /// <summary>Public-daily engine checks. They deliberately relax entry filters and are never order authorization.</summary>
    public static IReadOnlyDictionary<string,OversoldConfig> BacktestVerificationPresets()
    {
        var basis=VerificationBase();
        var hold=basis.Clone();hold.UseTakeProfit1=hold.UseTakeProfit2=hold.UseTrailingStop=hold.UseEmergencyStop=false;hold.SupportBreakPercent=-99.999m;hold.MaxHoldingDays=1;
        var profit=basis.Clone();profit.UseTakeProfit1=true;profit.TakeProfit1Percent=.001m;profit.TakeProfit1Ratio=100;profit.UseTakeProfit2=profit.UseTrailingStop=profit.UseEmergencyStop=false;profit.SupportBreakPercent=-99.999m;profit.MaxHoldingDays=50;
        var stop=basis.Clone();stop.UseTakeProfit1=stop.UseTakeProfit2=stop.UseTrailingStop=stop.UseEmergencyStop=false;stop.SupportBreakPercent=-.001m;stop.MaxHoldingDays=50;
        return new Dictionary<string,OversoldConfig>{{HoldOneDayName,hold},{QuickProfitName,profit},{SupportBreakName,stop}};
    }

    private static OversoldConfig VerificationBase()=>new()
    {
        AverageTradingValueLookbackDays=1,MinimumAverageTradingValue=0,
        CrashLookbackDays=1000,MinimumDrawdownPercent=-.001m,ShortCrashLookbackDays=1,ShortCrashPercent=-.001m,
        UseRsiFilter=false,RsiPeriod=2,MaximumRsi=100,
        UseVolumeFilter=false,VolumeLookbackDays=1,CrashVolumeRatio=.001m,
        UseMovingAverageSupport=true,MovingAveragePeriods="1",UsePivotSupport=false,UseApproximateVolumeZone=false,
        SupportClusterRangePercent=100,MinimumSupportCount=1,SupportDistancePercent=100,
        RequireReboundConfirmation=false,ReboundPercent=.001m,CandidateLifetimeDays=1000,ClusterChangeTolerancePercent=100,
        InitialCapital=10_000_000,PlannedInvestment=1_000_000,MaxEntries=1,EntryRatios="100",AdditionalEntryDropPercent=-.001m,RequireSupportForAdditionalEntry=false,
        CommissionPercent=0,TaxPercent=0,SlippagePercent=0,UsePriceOnlyBacktest=true,
        ReentryCooldownDays=0,MinimumNewClusterDistancePercent=.001m,AllowEarlyReentryWithNewCluster=true
    };
}
