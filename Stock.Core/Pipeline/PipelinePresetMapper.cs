using Stock.Core.Trading;
namespace Stock.Core.Pipeline;
/// <summary>모든 1~5 강도 변환표를 중앙에서 관리합니다.</summary>
public static class PipelinePresetMapper
{
    private static readonly decimal[] RankVelocity=[0,0.5m,1,2,5],Turnover=[10000,50000,100000,500000,1000000],Volume=[0.5m,1,1.5m,2,3],Width=[50,30,20,10,5],Vwap=[20,15,10,5,2],Distance=[20,15,10,5,2],Spread=[4,2,1,.5m,.2m],Depth=[10,50,100,500,1000],Strength=[50,75,100,150,200],Volatility=[30,20,10,5,2],Decay=[.1m,.25m,.5m,.7m,.9m],Score=[40,50,60,70,80],Invalidation=[10,20,30,40,50],Trailing=[30,25,20,15,10];
    private static readonly int[] Rank=[200,100,50,25,10];
    private static T At<T>(T[] a,int n)=>n is >=1 and <=5?a[n-1]:throw new InvalidOperationException("강도는 1~5로 입력하세요.");
    public static PipelineAdvancedConfig Convert(PipelineStandardConfig s,PipelineAdvancedConfig? previous=null)
    {
        if(s==null)throw new InvalidOperationException("Standard 설정이 없습니다.");
        foreach(var (_,section) in s.Sections())StrategyParameterValidation.Object(section??throw new InvalidOperationException("설정 영역이 없습니다."));
        var a=previous==null?new PipelineAdvancedConfig():Serialization.Unpack<PipelineAdvancedConfig>(Serialization.Pack(previous));
        a.Universe.Market=s.Universe.Market;a.Universe.MaxPrice=s.Universe.MaxPrice;a.Universe.MaxMarketCap=s.Universe.MaxMarketCap;a.Universe.RankingSource=s.Universe.RankingSource;a.Universe.UseTopSearchSymbols=s.Universe.UseTopSearchSymbols;
        a.Attention=new(){SearchRankVelocityMin=At(RankVelocity,s.Attention.SearchAttentionStrength),CurrentSearchRankMax=At(Rank,s.Attention.SearchAttentionStrength),DayTurnoverMin=At(Turnover,s.Attention.TurnoverStrength),DayVolumeTo20DayAverageMin=At(Volume,s.Attention.RelativeVolumeStrength),Intraday5MinVolumeVsSameTimeAverageMin=At(Volume,s.Attention.RelativeVolumeStrength),AttentionScoreThreshold=At(Score,s.Attention.SearchAttentionStrength)};
        a.PriceAction=new(){DayChangePercentMin=-At(Width,s.PriceAction.DayChangeFilter),DayChangePercentMax=At(Width,s.PriceAction.DayChangeFilter)*2,GapPercentMin=-At(Width,s.PriceAction.GapFilter),GapPercentMax=At(Width,s.PriceAction.GapFilter),RecentNDayDrawdownMin=0,RecentNDayDrawdownMax=100-10*s.PriceAction.RecentDrawdownCondition,VwapDeviationPercentMin=-At(Vwap,s.PriceAction.VwapDistanceCondition),VwapDeviationPercentMax=At(Vwap,s.PriceAction.VwapDistanceCondition),DistanceToPrevLowMax=At(Distance,s.PriceAction.SupportResistancePosition),DistanceToResistanceMin=s.PriceAction.SupportResistancePosition*.2m,PriceActionScoreThreshold=At(Score,s.PriceAction.DayChangeFilter)};
        a.Microstructure=new(){MaxBidAskSpreadPercent=At(Spread,s.Microstructure.SpreadTolerance),MinBidDepth=At(Depth,s.Microstructure.LiquidityQuality),MinAskDepth=At(Depth,s.Microstructure.LiquidityQuality),MinOrderBookImbalance=(s.Microstructure.LiquidityQuality-3)*.1m,ExecutionStrengthMin=At(Strength,s.Microstructure.ExecutionStrength),RealizedVolatility1mMin=0,RealizedVolatility1mMax=At(Volatility,s.Microstructure.VolatilityStability),VolumeDecayRateThreshold=At(Decay,s.Microstructure.VolumeDecaySensitivity),MicrostructureScoreThreshold=At(Score,s.Microstructure.LiquidityQuality)};
        a.TickScalping.EntryScoreThreshold=At(Score,s.TickScalping.EntryStrictness);a.TickScalping.TakeProfitPercent=s.TickScalping.TakeProfitPercent;a.TickScalping.StopLossPercent=s.TickScalping.StopLossPercent;a.TickScalping.MaxHoldSeconds=s.TickScalping.MaxHoldSeconds;a.TickScalping.LiquidityDropExit=s.TickScalping.LiquidityDropExit;a.TickScalping.SupportBreakExit=s.TickScalping.SupportBreakExit;
        a.Farmer.InitialPositionPercent=s.Farmer.InitialPositionPercent;a.Farmer.TrendContinuationScoreThreshold=At(Score,s.Farmer.TrendHoldingStrength);a.Farmer.ThesisInvalidationScoreThreshold=At(Invalidation,s.Farmer.ThesisInvalidationStrictness);a.Farmer.TrailingStopPercent=At(Trailing,s.Farmer.ThesisInvalidationStrictness);a.Farmer.PartialHarvestEnabled=s.Farmer.PartialHarvestEnabled;a.Farmer.FullHarvestEnabled=s.Farmer.FullHarvestEnabled;a.Farmer.FarmerSignalOnlyMode=!s.Farmer.AutoBuy;
        a.Portfolio.MaxPennyPortfolioPercent=s.Portfolio.MaxPennyPortfolioPercent;a.Portfolio.ProfitTransferEnabled=s.Portfolio.ProfitTransferEnabled;a.Portfolio.ProfitTransferRatio=s.Portfolio.ProfitTransferRatio;
        a.Validate();return a;
    }
    public static TradingSettings ToEngine(PipelineAdvancedConfig a,TradingSettings baseline)
    {
        a.Validate();var s=baseline.Clone();s.Advanced=null;s.Pipeline=Serialization.Unpack<PipelineAdvancedConfig>(Serialization.Pack(a));
        s.Scalping.Enabled=s.Box.Enabled=s.Rebound.Enabled=false;
        s.Risk.MaxPositions=a.Risk.MaxOpenPositions;s.Risk.MaxDailyLoss=a.Risk.DailyLossLimit;s.Risk.MaxDailyLossPercent=Math.Clamp(a.Risk.DailyLossLimit/s.Risk.AccountCapital*100,.01m,100);
        s.Risk.MaxSymbolCash=s.Risk.AccountCapital*a.Risk.MaxPositionPercent/100;s.Risk.MaxOrderCash=s.Risk.MaxSymbolCash;
        s.Risk.StopMode=DistanceMode.Percent;s.Risk.StopValue=a.Strategy==PipelineStrategy.TickScalping?-a.TickScalping.StopLossPercent:a.Farmer.TrailingStopPercent;s.Risk.MaxTradeLossPercent=s.Risk.StopValue;s.Risk.MaxTradeLoss=s.Risk.MaxOrderCash*(s.Risk.StopValue+a.Execution.Commission)/100;
        s.Execution.Kind=a.Execution.OrderType;s.Execution.SlippagePercent=a.Execution.SlippagePercent;s.Execution.FeePercent=a.Execution.Commission;s.Execution.TaxPercent=a.Execution.Tax;s.Execution.TimeoutMilliseconds=a.Execution.OrderTimeoutMilliseconds;
        s.Validate();return s;
    }
}
public static class PipelineValidation
{
    public static void Validate(PipelineAdvancedConfig a)
    {
        if(a==null||!Enum.IsDefined(a.Strategy))throw new InvalidOperationException("실행할 파이프라인 전략을 선택하세요.");
        foreach(var (name,section) in a.Sections()){if(section==null)throw new InvalidOperationException(name+" 설정이 없습니다.");StrategyParameterValidation.Object(section);}
        void Range(decimal min,decimal max,string name){if(min>max)throw new InvalidOperationException(name+": 최소값은 최대값보다 클 수 없습니다.");}
        Range(a.Universe.MinPrice,a.Universe.MaxPrice,"가격");Range(a.Universe.MinMarketCap,a.Universe.MaxMarketCap,"시가총액");
        Range(a.PriceAction.DayChangePercentMin,a.PriceAction.DayChangePercentMax,"등락률");Range(a.PriceAction.GapPercentMin,a.PriceAction.GapPercentMax,"갭");Range(a.PriceAction.RecentNDayDrawdownMin,a.PriceAction.RecentNDayDrawdownMax,"최근 낙폭");Range(a.PriceAction.VwapDeviationPercentMin,a.PriceAction.VwapDeviationPercentMax,"VWAP 거리");Range(a.Microstructure.RealizedVolatility1mMin,a.Microstructure.RealizedVolatility1mMax,"1분 변동성");
        if(a.Farmer.ThesisInvalidationScoreThreshold>=a.Farmer.TrendContinuationScoreThreshold)throw new InvalidOperationException("가설 훼손 점수는 추세 유지 점수보다 낮아야 합니다.");
        if(a.Farmer.PartialHarvestEnabled&&a.Farmer.FullHarvestEnabled&&a.Farmer.PartialHarvestTriggerPercent>=a.Farmer.FullHarvestTriggerPercent)throw new InvalidOperationException("일부 수확 목표는 전량 수확 목표보다 낮아야 합니다.");
        if(a.Farmer.InitialPositionPercent>a.Risk.MaxPositionPercent||a.Farmer.InitialPositionPercent>a.Portfolio.MaxPennyPortfolioPercent)throw new InvalidOperationException("농부 초기 비중은 종목/포트폴리오 한도 이하여야 합니다.");
        if(a.Portfolio.ProfitTransferEnabled&&!a.Portfolio.CorePortfolioDestinationEnabled)throw new InvalidOperationException("수익 배정에는 Core 목적지를 켜야 합니다.");
        if(!a.Universe.UseTopSearchSymbols&&!a.Universe.UseWatchlistSymbols)throw new InvalidOperationException("검색 상위 또는 관심종목 중 하나는 사용해야 합니다.");
    }
}
