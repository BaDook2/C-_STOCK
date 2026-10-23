namespace Stock.Core.Trading;

/// <summary>기존 전략 신호에 명시적으로 활성화한 고급 조건만 적용합니다. 미제공 데이터는 추정하지 않습니다.</summary>
public sealed class AdvancedStrategyGate(ITradingStrategy inner):ITradingStrategy
{
    public string Name=>inner.Name;
    private sealed class State
    {
        public DateOnly Day;public decimal EntryRate,Support,Resistance,ReboundPeak,CrashLow,CrashBid,CrashScore;public DateTimeOffset? CrashAt,ConfirmAt;public StrategySignal? Pending;
    }
    private readonly Dictionary<string,State> states=[];
    public StrategySignal Evaluate(MarketContext c)
    {
        var a=c.Settings.Advanced;if(a==null)return inner.Evaluate(c);
        bool selected=a.StrategyType switch{StrategyType.Scalping=>inner is ScalpingStrategy,StrategyType.Box=>inner is BoxStrategy,_=>inner is ReboundStrategy};
        if(!selected)return new(Name,null,null,"OFF");
        if(!states.TryGetValue(c.Instrument.Key,out var state)||state.Day!=c.Day)states[c.Instrument.Key]=state=new(){Day=c.Day};
        StrategySignal Hold(string reason)=>new(Name,null,null,reason);
        var held=c.Position?.Quantity>0&&c.Position.Owner==Name;
        if(inner is ScalpingStrategy)
        {
            var s=a.Scalping;var current=c.Rate(s.MomentumWindowSeconds);var baseline=c.Rate(s.VolumeLookbackSeconds,s.MomentumWindowSeconds);
            var ratio=baseline>0?current/baseline:0;var w=c.Window(s.MomentumWindowSeconds);var momentum=w.Count>1?(c.Price/w[0].TradePrice!.Value-1)*100:0;
            var frequency=c.Frequency(s.MomentumWindowSeconds);var imbalance=c.QuoteFresh&&c.Quote!.BidSize+c.Quote.AskSize>0?(c.Quote.BidSize-c.Quote.AskSize)/(c.Quote.BidSize+c.Quote.AskSize):null;
            var score=(Score(ratio,s.VolumeRatioThreshold)+Score(momentum,s.MinimumPriceIncreasePercent)+Score(frequency,s.MinimumTradesPerSecond)+Score((imbalance??-1)+1,s.OrderBookImbalance+1))/4;
            if(held)
            {
                if(state.EntryRate<=0)state.EntryRate=current;
                if(score<=s.ExitScoreThreshold||s.ExitOnVolumeDecay&&state.EntryRate>0&&current/state.EntryRate<=s.VolumeDecayThreshold)return new(Name,false,c.Price,"거래량/신호 약화 청산");
            }
            else state.EntryRate=0;
            var signal=inner.Evaluate(c);if(signal.Buy!=true)return signal;
            var distribution=VolumeBins(c,s.VolumeLookbackSeconds,s.MomentumWindowSeconds);
            if(momentum<s.MinimumPriceIncreasePercent)return Hold("최소 상승속도 미충족");
            if(imbalance==null||imbalance<s.OrderBookImbalance)return Hold("매수·매도 호가 불균형 미충족/데이터 없음");
            if(s.VolumePercentileThreshold>0&&Percentile(distribution,current)<s.VolumePercentileThreshold)return Hold("거래량 백분위 미충족");
            if(s.VolumeZScoreThreshold>0&&ZScore(distribution,current)<s.VolumeZScoreThreshold)return Hold("거래량 Z 점수 미충족");
            if(!BuyRatio(w,s.BuyTradeRatio))return Hold("매수 방향 체결 비율 미충족/실제 방향 데이터 없음");
            if(score<s.EntryScoreThreshold)return Hold("진입 종합 점수 미충족");
            return signal;
        }
        if(inner is BoxStrategy)
        {
            var s=a.Box;var history=c.Window(s.RangeLookbackMinutes*60).Where(t=>t.ExchangeTime<c.Now).ToArray();
            if(history.Length<2)return Hold("박스 데이터 준비 중");
            var low=history.Min(t=>t.TradePrice!.Value);var high=history.Max(t=>t.TradePrice!.Value);var width=high-low;
            if(held)
            {
                if(state.Support==0){state.Support=low;state.Resistance=high;}
                if(s.StopOnBreakdown&&c.Price<=state.Support*(1-s.BreakdownPercent/100))return new(Name,false,c.Price,"박스 하단 이탈 손절");
                if(s.ExitAtResistance&&c.Price>=state.Resistance*(1-s.ResistanceDistancePercent/100))return new(Name,false,c.Price,"박스 상단 자동청산");
            }
            else {state.Support=low;state.Resistance=high;}
            var signal=inner.Evaluate(c);
            if(signal.Buy==false&&held)return s.ExitAtResistance?signal:Hold("상단 자동청산 OFF");
            if(signal.Buy!=true)return signal;
            if(width<=0)return Hold("박스 폭 데이터 부족");
            var dist=Math.Abs(c.Price/low-1)*100;
            if(dist>s.SupportDistancePercent)return Hold("지지선 허용 거리 밖");
            var changes=history.Zip(history.Skip(1),(x,y)=>Math.Abs(y.TradePrice!.Value-x.TradePrice!.Value)).ToArray();var atr=changes.Average()/width;
            var bins=VolumeBins(c,s.RangeLookbackMinutes*60,10);var lowPercentile=Percentile(bins,c.Rate(Math.Max(1,s.RangeLookbackMinutes*30),10));
            var volumeBase=c.Rate(Math.Max(1,s.RangeLookbackMinutes*60-10),10);var inflow=volumeBase>0?c.Rate(10)/volumeBase:0;
            if(inflow<s.VolumeReentryRatio)return Hold("지지선 거래량 유입 미충족");
            var touches=SupportResistanceService.Touches(history,low,c.Settings.Box.TolerancePercent,c.Settings.Box.TouchGapSeconds);
            var supportScore=(Score(touches,s.MinimumSupportTouches)+Math.Clamp(100-dist/s.SupportDistancePercent*50,0,100))/2;
            var rangeScore=(Score(s.MaximumRangePercent,width/low*100)+Score(s.ATRRatioThreshold,atr)+Score(touches,s.MinimumSupportTouches)+Score(s.LowVolumePercentile,lowPercentile))/4;
            if(atr>s.ATRRatioThreshold||lowPercentile>s.LowVolumePercentile||supportScore<s.SupportScoreThreshold||rangeScore<s.RangeScoreThreshold)return Hold("박스 변동성·저거래량·점수 미충족");
            return signal;
        }
        {
            var s=a.Rebound;
            if(held)
            {
                state.Pending=null;state.CrashAt=null;state.ConfirmAt=null;
                state.ReboundPeak=Math.Max(Math.Max(state.ReboundPeak,c.Position!.AveragePrice),c.Price);
                if(s.ExitRetracementPercent>0&&c.Price<=state.ReboundPeak*(1-s.ExitRetracementPercent/100))return new(Name,false,c.Price,"반등 약화 청산");
                return inner.Evaluate(c);
            }
            state.ReboundPeak=0;var window=c.Window(s.CrashLookbackSeconds);var anchor=c.Trades.LastOrDefault(t=>t.ExchangeTime<=c.Now.AddSeconds(-s.CrashLookbackSeconds));
            var drop=anchor?.TradePrice>0?(1-c.Price/anchor.TradePrice.Value)*100:0;
            var previous=c.Rate(s.CrashLookbackSeconds,s.CrashLookbackSeconds);var vol=previous>0?c.Rate(s.CrashLookbackSeconds)/previous:0;
            var score=(Score(drop,s.MinimumCrashPercent)+Score(drop/s.CrashLookbackSeconds,s.CrashVelocity)+(s.ConfirmVolume?Score(vol,s.CrashVolumeRatio):100))/3;
            if(drop>=s.MinimumCrashPercent&&drop<=s.MaximumCrashPercent&&drop/s.CrashLookbackSeconds>=s.CrashVelocity&&(!s.ConfirmVolume||vol>=s.CrashVolumeRatio)&&score>=s.CrashScoreThreshold&&state.CrashAt==null)
            {state.CrashAt=c.Now;state.CrashLow=c.Price;state.CrashBid=c.QuoteFresh?c.Quote?.BidSize??0:0;state.CrashScore=score;}
            var original=inner.Evaluate(c);if(state.CrashAt==null)return original.Buy==true?Hold("고급 급락 조건 미충족"):original;
            if((c.Now-state.CrashAt.Value).TotalSeconds>c.Settings.Rebound.CandidateSeconds||drop>s.MaximumCrashPercent){state.CrashAt=null;state.Pending=null;state.ConfirmAt=null;return Hold("급락 후보 만료/최대 급락률 초과");}
            state.CrashLow=Math.Min(state.CrashLow,c.Price);var bounce=(c.Price/state.CrashLow-1)*100;
            if(bounce>=s.MinimumReboundPercent)state.ConfirmAt??=c.Now;else{state.ConfirmAt=null;state.Pending=null;}
            if(original.Buy==true)state.Pending=original;
            if(state.Pending==null)return original;
            var elapsed=state.ConfirmAt==null?0:(decimal)(c.Now-state.ConfirmAt.Value).TotalSeconds;
            if(elapsed<s.ReboundConfirmationSeconds)return Hold("반등 확인시간 대기");
            if(!BuyRatio(window,s.BuyTradeRatio))return Hold("반등 매수 방향 데이터 없음/비율 미충족");
            if(s.BidRecoveryRatio>0&&(!c.QuoteFresh||state.CrashBid<=0||c.Quote?.BidSize is not decimal bid||bid/state.CrashBid<s.BidRecoveryRatio))return Hold("매수호가 회복 데이터 없음/미충족");
            if(s.NewsFilter&&c.Last?.NewsRiskCleared!=true)return Hold("해당 시점의 뉴스/공시 확인 데이터가 없습니다.");
            var reboundScore=(Score(bounce,s.MinimumReboundPercent)+Score(elapsed,s.ReboundConfirmationSeconds)+state.CrashScore)/3;
            if(reboundScore<s.ReboundScoreThreshold)return Hold("반등 종합 점수 미충족");
            state.Pending=null;state.CrashAt=null;state.ConfirmAt=null;
            return new(Name,true,c.Price,s.AutoBuy?"고급 급락·반등 확인":"반등 매수 신호 · 자동매수 OFF");
        }
    }
    private static decimal Score(decimal value,decimal target)=>target<=0?100:Math.Clamp(value/target*50,0,100);
    private static decimal[] VolumeBins(MarketContext c,int seconds,int offset)=>c.Window(seconds,offset).GroupBy(t=>t.ExchangeTime.ToUnixTimeSeconds()).Select(g=>g.Sum(t=>t.TradeSize??0)).ToArray();
    private static decimal Percentile(decimal[] samples,decimal value)=>samples.Length==0?0:100m*samples.Count(n=>n<=value)/samples.Length;
    private static decimal ZScore(decimal[] samples,decimal value){if(samples.Length<2)return 0;var mean=samples.Average();var variance=samples.Average(n=>(n-mean)*(n-mean));return variance==0?(value>mean?100:0):(value-mean)/(decimal)Math.Sqrt((double)variance);}
    private static bool BuyRatio(IReadOnlyList<MarketEvent> events,decimal threshold)=>threshold<=0||events.Count>0&&events.All(e=>e.BuyerInitiated!=null)&&events.Sum(e=>e.TradeSize??0)>0&&events.Where(e=>e.BuyerInitiated==true).Sum(e=>e.TradeSize??0)/events.Sum(e=>e.TradeSize??0)>=threshold;
}
