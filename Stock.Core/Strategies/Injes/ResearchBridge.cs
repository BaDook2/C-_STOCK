using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

/// <summary>Adapts existing memory/CSV tick storage and result tables; never manufactures intrabar paths.</summary>
public static class InjesResearchBridge
{
    public static InjesObservation Observation(MarketEvent market)=>new(market,market.AvailableAt??market.ReceivedTime,market.TradingSession,
        market.StrategyReference??new(market.ReceivedTime){PreviousClose=market.PreviousClose,SessionOpen=market.Open,MarketCap=market.MarketCap,
            FundamentalRisk=market.NewsRiskCleared switch{true=>FundamentalRiskState.Cleared,false=>FundamentalRiskState.RiskDetected,_=>FundamentalRiskState.Unknown}});
    public static InjesObservation[] Read(TradingStore store,StockItem stock,DateOnly from,DateOnly to)=>Read(store,[stock],from,to);
    public static InjesObservation[] Read(TradingStore store,StockItem[] stocks,DateOnly from,DateOnly to)
    {
        if(stocks.Length==0)return [];var keys=stocks.Select(s=>s.Key).ToHashSet(StringComparer.Ordinal);
        var start=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(14)).ToUniversalTime();
        var end=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(-12)).ToUniversalTime();
        // Preserve original source IDs and recorded receive order. Never merge a SAMPLE stream into real data.
        return store.ReadEventsInArrivalOrder(stocks.Length==1?stocks[0].Key:"",start,end).Where(e=>keys.Contains(e.Key)&&e.Source is not("SAMPLE" or "DEMO" or "BAR_FILL_MODEL")&&e.ReceivedTime!=default&&UsSessionClock.MarketDate(e.ExchangeTime)>=from&&UsSessionClock.MarketDate(e.ExchangeTime)<=to).Select(Observation).OrderBy(e=>e.KnownAt).ToArray();
    }
    public static OversoldResult Run(StockItem stock,OversoldConfig config,DateOnly from,DateOnly to,InjesObservation[] observations,CancellationToken token)
    {
        var definition=InjesPresetStore.Require(config);
        if(stock.Currency!="USD")throw new InvalidOperationException("Inje's Strategy는 미국주식 USD 원본 틱·호가 자료를 사용합니다.");
        if(observations.Length==0||!observations.Any(e=>e.Market.Kind==EventKind.Trade))throw new InvalidOperationException("MissingData: 선택 기간의 원본 미국 체결·호가 이벤트가 없습니다. 일봉·분봉을 틱으로 바꾸거나 기존 전략으로 대체하지 않습니다.");
        var earliest=observations.Min(e=>e.KnownAt);var latest=observations.Max(e=>e.KnownAt);if(latest==earliest)latest=latest.AddMilliseconds(1);
        var replay=new InjesReplayEngine().Run(new(definition,[new(stock.Symbol,stock.Exchange)],observations,earliest,latest,definition.InitialCash,string.Join(",",observations.Select(e=>e.Market.Source).Distinct()),true,true),token);
        var trades=new List<OversoldTrade>();var current=new OversoldTrade();int held=0;decimal average=0;
        foreach(var fill in replay.Fills)
        {
            if(fill.Buy&&current.Executions.Count==0)
            {current=new(){Symbol=stock.Symbol,Configuration=config.Clone(),EntrySignalDate=fill.Time,FirstExecutionDate=fill.Time,Cluster=new(fill.Price,fill.Price,fill.Price,[])};trades.Add(current);}
            var order=replay.Orders.Single(o=>o.Id==fill.OrderId);var signal=replay.Signals.FirstOrDefault(s=>s.Symbol==fill.Instrument.Key&&s.SignalTime==order.Created&&s.SignalType==(fill.Buy?SignalType.Enter:SignalType.Exit));
            if(fill.Buy){average=(average*held+fill.Quantity*fill.Price)/(held+fill.Quantity);held+=fill.Quantity;current.EntrySignalDate=signal?.SignalTime??order.Created;}else held-=fill.Quantity;
            current.AveragePrice=average;current.Executions.Add(new OversoldExecution(signal?.SignalTime??order.Created,fill.Time,fill.Buy,fill.Quantity,fill.Price,fill.Fee,fill.Reason,held>0?average:0,fill.RealizedPnl){InjesEvidence=signal});
            if(fill.Buy)current.AcquisitionCost+=fill.Quantity*fill.Price+fill.Fee;else current.RealizedPnl+=fill.RealizedPnl;
            var closed=replay.ClosedTrades.FirstOrDefault(t=>t.EnteredAt==current.FirstExecutionDate&&t.ExitedAt==fill.Time);
            if(closed!=null){current.LastExecutionDate=fill.Time;current.FinalReturnPercent=current.AcquisitionCost>0?current.RealizedPnl/current.AcquisitionCost*100:null;current.ExitReason=fill.Reason;current=new();}
        }
        var p=replay.Performance;var final=replay.Equity.LastOrDefault()?.Equity??definition.InitialCash;
        return new(){Id="inje-result:"+replay.Id,Stock=stock,From=from,To=to,Configuration=config.Clone(),Source=replay.Source,DataHash=replay.DataHash,InjesResult=replay,Trades=trades,
            Performance=new(p.Trades,p.WinRate,null,null,p.AverageHoldingSeconds/86400,p.ProfitFactor,p.InfiniteProfitFactor,p.Expectancy,null,null,p.MaximumDrawdownPercent,null,final,p.OpenPositions),
            Equity=replay.Equity.Select(e=>new BarEquity(e.Time,e.Equity)).ToList(),Cash=definition.InitialCash+replay.Fills.Sum(f=>f.Buy?-f.Quantity*f.Price-f.Fee:f.Quantity*f.Price-f.Fee),Quantity=replay.OpenPositions.Sum(x=>x.Quantity),PendingOrderAtEnd=replay.Orders.Any(o=>o.Active),Notices=replay.Notices.Concat(["Inje's Strategy · "+definition.StrategyId,"원본 틱·호가 재생. 표시 차트의 봉 기준과 독립적으로 판단합니다."]).ToList()};
    }
    public static ScreeningObservation Screen(StockItem stock,OversoldConfig config,InjesObservation[] observations,DateOnly from,DateOnly to,CancellationToken token)
    {
        if(observations.Length==0)return ScreeningObservation.Missing(stock,"MissingData: Inje 원본 미국 틱·호가 자료가 없습니다.");
        var definition=InjesPresetStore.Require(config);if(stock.Currency!="USD")return ScreeningObservation.Missing(stock,"Inje는 미국주식 원본 틱/호가 자료가 필요합니다.");
        var paper=new InjesPaperExecution(definition.InitialCash,definition.ExecutionPolicy);var session=new InjesSession(definition,[new(stock.Symbol,stock.Exchange)],paper);
        StrategyEvaluationResult? matched=null;
        foreach(var observation in observations.OrderBy(e=>e.KnownAt))
        {token.ThrowIfCancellationRequested();session.OnMarket(observation,observation.KnownAt);var e=session.Latest(stock.Key);if(e?.Signal.SignalType==SignalType.Enter&&e.Signal.RiskApproved)matched=e;}
        var evaluation=matched??session.Latest(stock.Key);var signal=evaluation?.Signal;
        return new ScreeningObservation(stock,signal?.SignalTime??observations[^1].KnownAt,signal?.ReferencePrice??observations[^1].Market.TradePrice,signal?.Metrics.GetValueOrDefault("dayGainPercent"),null,signal?.Metrics.GetValueOrDefault("rvol"),(int)(signal?.Metrics.GetValueOrDefault("supportCount")??0),matched!=null,signal?.SignalReason??"Inje 진입 조건/필수 자료 대기","",observations[0].KnownAt,observations[^1].KnownAt){InjesEvaluation=evaluation};
    }
}
