namespace Stock.Core.Trading;

/// <summary>강도별 초기값을 한곳에서 관리합니다. 같은 최종 설정은 모드와 무관하게 동일한 엔진을 사용합니다.</summary>
public static class StrategyPresetMapper
{
    private static readonly decimal[] Volume=[1.2m,1.5m,2m,3m,4m],Momentum=[.1m,.2m,.3m,.5m,1m],Frequency=[.2m,.5m,1m,2m,4m],Percentile=[50,65,75,85,95],ZScore=[0,.5m,1,1.5m,2],Spread=[4,3,2,1,.5m],Score=[40,50,60,70,80],ExitScore=[10,20,30,35,38],Decay=[.1m,.2m,.3m,.5m,.7m],Width=[10,7,5,3,2],Distance=[1,.6m,.3m,.2m,.1m],Crash=[3,5,10,15,20],Bounce=[.5m,1,2,3,5],ExitRetracement=[3,2,1,.5m,.25m];
    private static T At<T>(T[] values,int strength){if(strength is <1 or >5)throw new InvalidOperationException("강도는 1~5로 입력하세요.");return values[strength-1];}
    public static AdvancedStrategyConfig ConvertToAdvanced(StrategyType type,StandardStrategyConfig s,AdvancedStrategyConfig? previous=null)
    {
        foreach(var o in new object[]{s.Common,s.Scalping,s.Box,s.Rebound})StrategyParameterValidation.Object(o);
        var a=previous==null?new AdvancedStrategyConfig():Serialization.Unpack<AdvancedStrategyConfig>(Serialization.Pack(previous));
        a.StrategyType=type;a.Common=Serialization.Unpack<CommonStrategyConfig>(Serialization.Pack(s.Common));
        a.Risk.MaxOpenPositions=s.Common.MaxPositions;a.Risk.ReentryCooldown=s.Common.ReentryCooldown;a.Execution.OrderType=s.Common.OrderType;
        a.Scalping=new(){VolumeLookbackSeconds=120,VolumeRatioThreshold=At(Volume,s.Scalping.VolumeStrength),VolumePercentileThreshold=At(Percentile,s.Scalping.VolumeStrength),VolumeZScoreThreshold=At(ZScore,s.Scalping.VolumeStrength),MomentumWindowSeconds=20,MinimumPriceIncreasePercent=At(Momentum,s.Scalping.MomentumStrength),MinimumTradesPerSecond=At(Frequency,s.Scalping.QuoteActivity),OrderBookImbalance=(s.Scalping.QuoteActivity-3)*.1m,MaximumSpreadPercent=At(Spread,s.Scalping.QuoteActivity),EntryScoreThreshold=At(Score,s.Common.EntryStrength),ExitScoreThreshold=At(ExitScore,s.Common.ExitStrength),VolumeDecayThreshold=At(Decay,s.Common.ExitStrength),ExitOnVolumeDecay=s.Scalping.ExitOnVolumeDecay};
        a.Box=new(){RangeLookbackMinutes=s.Box.LookbackMinutes,MaximumRangePercent=At(Width,s.Box.Strictness),ATRRatioThreshold=.6m/s.Box.Strictness,MinimumSupportTouches=s.Box.Strictness,MinimumResistanceTouches=s.Box.Strictness,SupportDistancePercent=At(Distance,s.Box.SupportStrength),ResistanceDistancePercent=At(Distance,6-s.Common.ExitStrength),LowVolumePercentile=100-At(Percentile,s.Box.Strictness)/2,VolumeReentryRatio=At(Volume,s.Box.VolumeStrength),SupportScoreThreshold=At(Score,s.Common.EntryStrength),RangeScoreThreshold=At(Score,s.Box.Strictness),BreakdownPercent=At(Distance,s.Common.ExitStrength),ExitAtResistance=s.Box.ExitAtResistance,StopOnBreakdown=s.Box.StopOnBreakdown};
        a.Rebound=new(){CrashLookbackSeconds=s.Rebound.LookbackMinutes*60,MinimumCrashPercent=At(Crash,s.Rebound.CrashStrength),MaximumCrashPercent=40,CrashVelocity=At(Crash,s.Rebound.CrashStrength)/(s.Rebound.LookbackMinutes*60),CrashVolumeRatio=At(Volume,s.Rebound.CrashStrength),MinimumReboundPercent=At(Bounce,s.Rebound.ReboundStrength),ReboundConfirmationSeconds=s.Rebound.ReboundStrength,ExitRetracementPercent=At(ExitRetracement,s.Common.ExitStrength),CrashScoreThreshold=At(Score,s.Common.EntryStrength),ReboundScoreThreshold=At(Score,s.Rebound.ReboundStrength),ConfirmVolume=s.Rebound.ConfirmVolume,NewsFilter=s.Rebound.NewsFilter,AutoBuy=s.Rebound.AutoBuy};
        a.Validate();return a;
    }
    public static TradingSettings ToEngine(AdvancedStrategyConfig a,TradingSettings baseline)
    {
        a.Validate();var s=baseline.Clone();s.Advanced=Serialization.Unpack<AdvancedStrategyConfig>(Serialization.Pack(a));
        s.Scalping.Enabled=a.StrategyType==StrategyType.Scalping;s.Box.Enabled=a.StrategyType==StrategyType.Box;s.Rebound.Enabled=a.StrategyType==StrategyType.Rebound;
        var scalp=a.Scalping;s.Scalping.AverageSeconds=scalp.VolumeLookbackSeconds;s.Scalping.CurrentSeconds=scalp.MomentumWindowSeconds;s.Scalping.RelativeVolume=scalp.VolumeRatioThreshold;s.Scalping.TradesPerSecond=scalp.MinimumTradesPerSecond;s.Scalping.MaxSpread=scalp.MaximumSpreadPercent;
        var box=a.Box;s.Box.LookbackSeconds=box.RangeLookbackMinutes*60;s.Box.MinimumSeconds=Math.Min(s.Box.MinimumSeconds,s.Box.LookbackSeconds/2);s.Box.WidthPercent=box.MaximumRangePercent;s.Box.SupportTouches=box.MinimumSupportTouches;s.Box.ResistanceTouches=box.MinimumResistanceTouches;s.Box.BreakPercent=box.BreakdownPercent;
        var rebound=a.Rebound;s.Rebound.Use1=true;s.Rebound.Seconds1=rebound.CrashLookbackSeconds;s.Rebound.Drop1=rebound.MinimumCrashPercent;s.Rebound.Use2=false;s.Rebound.Use3=false;s.Rebound.UseHigh=false;s.Rebound.BouncePercent=rebound.MinimumReboundPercent;
        s.Risk.Sizing=SizingMode.FixedCash;s.Risk.FixedCash=a.Common.Investment;s.Risk.MaxOrderCash=a.Common.Investment;s.Risk.MaxPositions=a.Risk.MaxOpenPositions;s.Risk.MaxDailyLoss=a.Risk.DailyLossLimit;s.Risk.StopMode=DistanceMode.Percent;s.Risk.StopValue=a.Common.StopLossPercent;s.Risk.TakeMode=DistanceMode.Percent;s.Risk.TakeValue=a.Common.TakeProfitPercent;
        s.Risk.MaxSymbolCash=s.Risk.AccountCapital*a.Risk.MaxPositionPercent/100;
        s.Risk.MaxTradeLoss=Math.Max(.01m,a.Common.Investment*(a.Common.StopLossPercent+a.Execution.Commission)/100);s.Risk.MaxTradeLossPercent=Math.Max(.01m,a.Common.StopLossPercent);
        s.Risk.MaxDailyLossPercent=Math.Clamp(a.Risk.DailyLossLimit/s.Risk.AccountCapital*100,.01m,100);
        s.Execution.Kind=a.Execution.OrderType;s.Execution.SlippagePercent=a.Execution.SlippagePercent;s.Execution.FeePercent=a.Execution.Commission;s.Execution.TaxPercent=a.Execution.Tax;s.Execution.TimeoutMilliseconds=a.Execution.OrderTimeoutMilliseconds;
        s.Validate();return s;
    }
}
