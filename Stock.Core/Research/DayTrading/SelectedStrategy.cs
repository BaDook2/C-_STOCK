using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Core.Research.DayTrading;

/// <summary>One explicit algorithm switch per saved strategy. Legacy payloads never opt into D implicitly.</summary>
public static class SelectedStrategy
{
    public static StrategyDefinition Definition(OversoldConfig configuration,string name="급락 판단 전략",bool dailyResearch=false)
    {
        var definition=(configuration.CrashStrategy??StrategyDDefaults.Create(DateTimeOffset.UtcNow)).Snapshot();
        var c=definition.Configuration;c.UseCrashStrategy=false;c.CrashStrategy=null;
        c.InitialCapital=configuration.InitialCapital;c.PlannedInvestment=configuration.PlannedInvestment;
        c.CommissionPercent=configuration.CommissionPercent;c.TaxPercent=configuration.TaxPercent;c.SlippagePercent=configuration.SlippagePercent;
        foreach(var (key,kind) in new[]{("RsiPeriod",IndicatorKind.Rsi),("VolumeLookbackDays",IndicatorKind.Volume),("AverageTradingValueLookbackDays",IndicatorKind.Liquidity),("VolumeZoneLookbackDays",IndicatorKind.VolumeProfile)})
            if(c.ParameterTimeframes.TryGetValue(key,out var frame)){definition.DayTrading!.IndicatorTimeframes[kind]=frame;definition.DayTrading.AdvancedTimeframes=true;}
        if(dailyResearch){c.ParameterTimeframes.Clear();definition.DayTrading!.IndicatorTimeframes.Clear();definition.DayTrading.AdvancedTimeframes=false;}
        if(dailyResearch)definition.DayTrading!.SimpleTimeframe=new(1,TimeframeUnit.TradingDay);
        if(dailyResearch){definition.DayTrading!.Support.MovingAverageTimeframe=new(1,TimeframeUnit.TradingDay);definition.DayTrading.Support.ProfileTimeframe=new(1,TimeframeUnit.TradingDay);}
        return (definition with{Preset=name}).Snapshot();
    }
    public static IntradayReplayItem Item(StockItem stock,OversoldConfig config,HistoricalFacts facts,StrategyBar[] daily,StrategyBar[] selected,bool offline)
    {
        var definition=Definition(config,dailyResearch:offline);var policy=definition.DayTrading!;
        var frames=new[]{new TimeframeSpec(1,TimeframeUnit.TradingDay),policy.SimpleTimeframe,policy.Support.MovingAverageTimeframe,policy.Support.ProfileTimeframe}.Concat(policy.IndicatorTimeframes.Values).Concat(definition.Configuration.ParameterTimeframes.Values).Distinct();
        var asOf=daily.Concat(selected).Select(b=>b.ClosedAt).DefaultIfEmpty(DateTimeOffset.UtcNow).Max();
        var seeds=frames.SelectMany(frame=>facts.StrategyFrames.Any(b=>b.Timeframe==frame)?facts.StrategyFrames.Where(b=>b.Timeframe==frame):StrategyFrameSeries.Aggregate(frame.Unit is TimeframeUnit.Minute or TimeframeUnit.Hour?selected:daily,frame,asOf)).Distinct().ToArray();
        return new(new(stock.Symbol,stock.Exchange),definition,new(seeds,facts),definition.Preset);
    }
    public static OversoldResult Backtest(StockItem stock,OversoldConfig config,HistoricalFacts facts,StrategyBar[] daily,StrategyBar[] selected,DateOnly from,DateOnly to,bool offline,CancellationToken token)
    {
        var item=Item(stock,config,facts,daily,selected,offline);var c=item.Strategy.Configuration;
        var begin=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));var end=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)).AddTicks(-1);
        if(end>DateTimeOffset.UtcNow)end=DateTimeOffset.UtcNow;
        var requestedFrame=item.Strategy.DayTrading!.SimpleTimeframe;
        if(!offline&&requestedFrame.Duration!=null)selected=item.Seed.Bars.Where(b=>b.Timeframe==requestedFrame).Select(b=>b.Bar).ToArray();
        var rows=selected.Where(b=>DailyDates.Of(b.Price)>=from&&DailyDates.Of(b.Price)<=to).ToArray();
        var execution=new ExecutionSettings{UseSpread=false,FeePercent=c.CommissionPercent,TaxPercent=c.TaxPercent,SlippagePercent=c.SlippagePercent,LatencyMs=100,TimeoutMilliseconds=offline?604800000:30000,AllowPartial=true};
        var replay=new ClosedBarReplayEngine().Run(new([new(item,rows)],"전략 설정 · 급락 판단 전략",begin,end,DateTimeOffset.UtcNow,config.InitialCapital,execution,item.Strategy.DayTrading!.Risk,config.UsePriceOnlyBacktest,ClockIntervalMilliseconds:60000),token);
        if(offline&&item.Strategy.DayTrading!.Session.DayOnly)replay=replay with{Notices=[..replay.Notices,"오프라인 일봉에서는 당일 운용의 장중 진입·청산을 재현하지 않습니다. 해당 시각 조건을 충족하지 못하면 거래가 생성되지 않습니다."]};
        var trades=replay.Trades.Select(t=>
        {
            var evidence=t.Entry==null?null:Serialization.Unpack<StrategyDSignal>(t.Entry.EvidenceJson);
            var supports=evidence==null?Array.Empty<SupportCandidate>():new[]{new SupportCandidate("D:"+evidence.Id,SupportKind.ApproximateVolumeZone,evidence.Support.Zone.Lower,evidence.Support.Zone.Center,evidence.Support.Zone.Upper,DailyDates.Of(rows[0].Price),DailyDates.Of(rows[0].Price),"급락 판단 전략 진입 당시 지지")};
            var cluster=new SupportCluster(supports.FirstOrDefault()?.Lower??1,supports.FirstOrDefault()?.Center??1,supports.FirstOrDefault()?.Upper??1,supports);
            return new OversoldTrade{Symbol=stock.Symbol,EntrySignalDate=t.Entry?.Time??t.EntryAt,FirstExecutionDate=t.EntryAt,LastExecutionDate=t.ExitAt,AcquisitionCost=t.AcquisitionCost,RealizedPnl=t.RealizedProfit,FinalReturnPercent=t.ReturnPercent,Cluster=cluster,Configuration=config.Clone(),ExitReason=t.Fills.LastOrDefault(f=>!f.Buy)?.Reason??"미청산",Executions=t.Fills.Select(f=>new OversoldExecution(t.Entry?.Time??f.Time,f.Time,f.Buy,f.Quantity,f.Price,f.Fee,f.Reason,0,f.RealizedPnl)).ToList()};
        }).ToList();
        return new OversoldResult{Stock=stock,From=from,To=to,Configuration=config.Clone(),Source=replay.Source,DataHash=replay.DataHash,FactsHash=TradingStore.Hash(Serialization.Pack(facts)),Trades=trades,Performance=replay.Performance,Equity=replay.Equity.ToList(),Cash=replay.Account.Cash,Quantity=replay.Account.Rows.Sum(r=>r.Quantity),Notices=replay.Notices.ToList(),StrategyDResult=replay};
    }
    public static EntryEvaluation Evaluation(StrategyDRuntime runtime,IntradayEntrySignal? signal)
    {
        var o=runtime.Observation!;var state=runtime.SignalState;var c=runtime.Profile.Parameters;
        var supports=o.Supports.Select(z=>new SupportCandidate("D:"+z.StableKey,SupportKind.ApproximateVolumeZone,z.Zone.Lower,z.Zone.Center,z.Zone.Upper,DailyDates.Of(new(o.Time,1,1,1,1,0)),DailyDates.Of(new(o.Time,1,1,1,1,0)),"MA·매물대 중첩")).ToArray();
        var clusters=supports.Select(z=>new SupportCluster(z.Lower,z.Center,z.Upper,[z])).ToArray();
        var checks=new List<ConditionEvaluation>();
        void Add(string name,bool? pass,bool enabled=true)=>checks.Add(new(name,!enabled?ConditionOutcome.Disabled:pass==null?ConditionOutcome.MissingData:pass.Value?ConditionOutcome.Passed:ConditionOutcome.Failed,"","",state.Reason));
        Add("전체",true);Add("상품 제외",o.SecurityTradable);Add("정지·관리 제외",o.SecurityTradable);Add("거래대금",o.AverageTradingValue==null?null:o.AverageTradingValue>=c.MinimumAverageTradingValue);Add("Crash",o.Crash.Passed);Add("RSI",o.Rsi==null?null:o.Rsi<=c.MaximumRsi,c.UseRsiFilter);Add("Volume",o.VolumeRatio==null?null:o.VolumeRatio>=c.CrashVolumeRatio,c.UseVolumeFilter);
        Add("Support 후보",supports.Length>0);Add("Cluster",clusters.Length>0);Add("최소 count",clusters.Length>0);Add("접근",state.Support!=null);Add("반등",signal!=null);Add("FundamentalRisk",o.FundamentalRisk==null?null:!o.FundamentalRisk.Value);Add("최종",signal!=null);Add("MovingAverage",supports.Length>0);Add("ConfirmedPivotHigh",null,false);Add("ApproximateVolumeZone",supports.Length>0);
        return new(signal!=null,signal!=null?OversoldState.Entry1:OversoldState.WaitingForRebound,state.Reason,new(o.Crash.Medium??0,o.Crash.Short??0,o.Rsi,o.VolumeRatio,o.Crash.Types),clusters.FirstOrDefault(),clusters,checks.ToArray(),state.Lowest,null){TechnicalCandidate=signal!=null};
    }
    public static ScreeningObservation Screen(StockItem stock,OversoldConfig config,HistoricalFacts facts,StrategyBar[] daily,StrategyBar[] selected,DateOnly from,DateOnly to,bool offline,CancellationToken token)
    {
        if(stock.Exchange!=Exchange.KRX)return ScreeningObservation.Missing(stock,"급락 판단 전략은 국내 일반주식 전용입니다.");
        var item=Item(stock,config,facts,daily,selected,offline);var runtime=new StrategyDRuntime(item.Instrument,item.Strategy,item.Seed,item.InstanceId,true);
        StrategyDObservation? observation=null,matchedObservation=null;EntryEvaluation? matchedEvaluation=null;IntradayEntrySignal? signal=null;var matching=new List<DateOnly>();
        foreach(var bar in selected.Where(b=>DailyDates.Of(b.Price)>=from&&DailyDates.Of(b.Price)<=to))
        {
            token.ThrowIfCancellationRequested();runtime.OnClosedBar(bar);var found=runtime.Evaluate(new(false,false,0,0,0,null,"",null,0));
            observation=runtime.Observation;if(found!=null){signal=found;matchedObservation=observation;matchedEvaluation=Evaluation(runtime,found);matching.Add(DailyDates.Of(bar.Price));}
        }
        if(observation==null)return ScreeningObservation.Missing(stock,"선택 기간의 확정봉 자료가 없습니다.");
        var o=matchedObservation??observation;var state=runtime.SignalState;
        var evaluation=matchedEvaluation??Evaluation(runtime,signal);
        return new(stock,signal?.Time??o.Time,o.Price,o.Crash.Medium,o.Rsi,o.VolumeRatio,o.Supports.Length,signal!=null,"급락 판단 전략 · "+state.Reason,Serialization.Pack(evaluation),selected.FirstOrDefault()?.OpenTime,o.Time){MatchingDates=matching.ToArray()};
    }
    public static StrategyBar[] DailyBars(BarDataset dataset,HistoricalFacts facts)
    {
        return dataset.CompletedBars.OrderBy(b=>b.Time).Select((b,n)=>{var session=facts.Sessions.FirstOrDefault(s=>s.Date==DailyDates.Of(b))??ResearchMarketSession.For(DailyDates.Of(b),dataset.Stock.Exchange);return new StrategyBar(b with{Time=session.Close},session.Open,session.Close,n+1,facts.TradingValue(dataset.Stock.Symbol,session.Date,session.Close),dataset.Source);}).ToArray();
    }
}
