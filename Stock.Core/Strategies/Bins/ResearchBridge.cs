using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Bins;

public sealed record BinsScreenEvaluation(BinsSymbolSnapshot State,BinsDecision? Signal,ConditionOutcome[] Outcomes);
/// <summary>UI adapter for the independent Bin engine. Original observations and dated provider rules remain mandatory.</summary>
public static class BinsResearchBridge
{
    public static BinsMarketInput Observation(MarketEvent e)=>new(e,e.AvailableAt??e.ReceivedTime,e.BinSession,e.BinMetadata,e.BinContinuityVerified);
    public static BinsMarketInput[] Read(TradingStore store,StockItem stock,DateOnly from,DateOnly to)
    {
        var start=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(14)).ToUniversalTime();
        var end=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(-12)).ToUniversalTime();
        return store.ReadEventsInArrivalOrder(stock.Key,start,end).Where(e=>e.Source is not("SAMPLE" or "DEMO" or "BAR_FILL_MODEL")&&MarketCalendars.TradingDate(e.Instrument.Exchange,e.ExchangeTime)>=from&&MarketCalendars.TradingDate(e.Instrument.Exchange,e.ExchangeTime)<=to).Select(Observation).ToArray();
    }
    public static BinsStrategyConfig Require(OversoldConfig config)
    {config.Validate();return config.Bins?.Snapshot()??throw new InvalidOperationException("Bin's Strategy 설정을 선택하세요.");}
    private static decimal Cash(StockItem stock,OversoldConfig config)=>stock.Currency=="KRW"?config.BinsInitialCashKrw:config.BinsInitialCashUsd;
    private static CompletedBar[] Bars(BinsMarketInput[] rows)=>rows.SelectMany(e=>e.Event.BinCompletedBars).Distinct().ToArray();
    private static HistoricalTrade[] Profile(BinsMarketInput[] rows)=>rows.SelectMany(e=>e.Event.BinProfileTrades).Distinct().ToArray();
    public static OversoldResult Run(StockItem stock,OversoldConfig config,DateOnly from,DateOnly to,BinsMarketInput[] observations,CancellationToken token)
    {
        var definition=Require(config);if(!definition.Market.EnabledExchanges.Contains(stock.Exchange))throw new InvalidOperationException("Bin 설정에서 이 거래소를 사용하도록 선택하세요.");
        if(observations.Length==0||!observations.Any(e=>e.Event.Kind==EventKind.Trade))throw new InvalidOperationException("MissingData: 선택 기간의 Bin 원본 체결·호가 이벤트가 없습니다. 일봉을 틱으로 변환하지 않습니다.");
        var first=observations.Min(e=>e.AvailableAt);var last=observations.Max(e=>e.AvailableAt);if(first==last)last=last.AddTicks(1);
        var cash=Cash(stock,config);var replay=new BinsReplayEngine().Run(new(definition,[new(stock.Symbol,stock.Exchange)],observations,Bars(observations),Profile(observations),first,last,new Dictionary<string,decimal>{{stock.Currency,cash}},string.Join(",",observations.Select(e=>e.Event.Source).Distinct()),ReplayPrecision.OriginalTicks,true,true,DurationSpec.Seconds(1),observations.Any(e=>e.Event.Source.Contains("SYNTHETIC",StringComparison.OrdinalIgnoreCase)||e.Event.Source.Contains("FIXTURE",StringComparison.OrdinalIgnoreCase))),token);
        var transport=config.Clone();transport.InitialCapital=cash;var trades=new List<OversoldTrade>();OversoldTrade? current=null;int held=0;decimal average=0;
        foreach(var fill in replay.Fills)
        {
            var order=replay.Orders.Single(o=>o.Id==fill.OrderId);var evidence=replay.Decisions.FirstOrDefault(d=>d.Kind=="OrderIntent"&&d.OrderId==fill.OrderId);
            if(fill.Buy&&current==null){current=new(){Symbol=stock.Symbol,Configuration=transport.Clone(),EntrySignalDate=evidence?.Time??order.Created,FirstExecutionDate=fill.Time,Cluster=new(fill.Price,fill.Price,fill.Price,[])};trades.Add(current);}
            if(current==null)throw new InvalidOperationException("Bin replay sell has no confirmed entry.");
            if(fill.Buy){average=(average*held+fill.Price*fill.Quantity)/(held+fill.Quantity);held+=fill.Quantity;current.AcquisitionCost+=fill.Quantity*fill.Price+fill.Fee;}else{held-=fill.Quantity;current.RealizedPnl+=fill.RealizedPnl;}
            current.AveragePrice=average;current.Executions.Add(new(evidence?.Time??order.Created,fill.Time,fill.Buy,fill.Quantity,fill.Price,fill.Fee,fill.Reason,held>0?average:0,fill.RealizedPnl){BinsEvidence=evidence});
            if(held==0){current.LastExecutionDate=fill.Time;current.ExitReason=fill.Reason;current.FinalReturnPercent=current.AcquisitionCost>0?100*current.RealizedPnl/current.AcquisitionCost:null;current.HoldingDays=(int)(fill.Time-current.FirstExecutionDate).TotalDays;current=null;}
        }
        var p=replay.Currencies.Single();var wins=replay.ClosedTrades.Count(t=>t.Profit>0);var profit=replay.ClosedTrades.Where(t=>t.Profit>0).Sum(t=>t.Profit);var loss=-replay.ClosedTrades.Where(t=>t.Profit<0).Sum(t=>t.Profit);
        return new(){Id="bin-result:"+Guid.NewGuid().ToString("N"),Stock=stock,From=from,To=to,Configuration=transport,Source=replay.Source,DataHash=replay.DataHash,BinsResult=replay,Trades=trades,
            Performance=new(p.ClosedTrades,p.ClosedTrades==0?null:100m*wins/p.ClosedTrades,null,null,null,loss>0?profit/loss:null,loss==0&&profit>0,p.ClosedTrades==0?null:replay.ClosedTrades.Average(t=>t.Profit),null,null,p.MaximumDrawdownPct,null,p.FinalEquity,p.OpenPositions),
            Equity=replay.Equity.Where(e=>e.Currency==stock.Currency).Select(e=>new BarEquity(e.Time,e.Equity)).ToList(),Cash=cash+replay.Fills.Sum(f=>f.Buy?-f.Quantity*f.Price-f.Fee:f.Quantity*f.Price-f.Fee),Quantity=replay.FinalStates.Sum(s=>s.Quantity),PendingOrderAtEnd=replay.Orders.Any(o=>o.Active),Notices=replay.Notices.Concat(["Bin's Strategy · "+stock.Currency+" 독립 가상 계좌","원본 틱·호가 판단 · 화면 표시 봉과 독립 · 실제 주문 없음"]).ToList()};
    }
    public static ScreeningObservation Screen(StockItem stock,OversoldConfig config,BinsMarketInput[] observations,CancellationToken token)
    {
        var definition=Require(config);if(observations.Length==0)return ScreeningObservation.Missing(stock,"MissingData: Bin 원본 체결·호가 자료가 없습니다.");
        if(!definition.Market.EnabledExchanges.Contains(stock.Exchange))return ScreeningObservation.Missing(stock,"Bin 설정에서 사용하지 않는 거래소입니다.");
        var paper=new BinsPaperExecution(stock.Currency,Cash(stock,config));var engine=new BinsStrategyEngine(definition,[new(stock.Symbol,stock.Exchange)],[paper],evaluationOnly:true);
        var rows=observations.Select((e,n)=>(At:e.AvailableAt,N:n,Input:(BinsMarketInput?)e,Bar:(CompletedBar?)null,Trade:(HistoricalTrade?)null))
            .Concat(Bars(observations).Select((b,n)=>(b.KnownAt,n,(BinsMarketInput?)null,(CompletedBar?)b,(HistoricalTrade?)null)))
            .Concat(Profile(observations).Select((t,n)=>(t.KnownAt,n,(BinsMarketInput?)null,(CompletedBar?)null,(HistoricalTrade?)t)))
            .Where(x=>x.Item1<=observations.Max(e=>e.AvailableAt)).OrderBy(x=>x.Item1).ThenBy(x=>x.Item3==null?0:1).ThenBy(x=>x.Item2);
        foreach(var row in rows){token.ThrowIfCancellationRequested();if(row.Item4!=null)engine.LoadCompletedBar(row.Item4,row.Item1);else if(row.Item5!=null)engine.LoadProfileTrade(row.Item5,row.Item1);else if(row.Item3!=null)engine.OnMarket(row.Item3,row.Item1);}
        var state=engine.Snapshot(stock.Key);var matched=engine.Decisions.LastOrDefault(d=>d.Kind=="ScreenMatched");var entry=matched??engine.Decisions.LastOrDefault(d=>d.Kind=="EntrySignal");
        var last=observations.MaxBy(e=>e.AvailableAt)!;var missing=last.Session?.ValidAt(last.Event.Instrument,last.Event.ExchangeTime)!=true||last.Metadata?.VerifiedAt(last.AvailableAt)!=true||state.Status.Contains("Missing",StringComparison.OrdinalIgnoreCase)||state.Status.Contains("Insufficient",StringComparison.OrdinalIgnoreCase)||state.Regime.Validity.Contains("Missing",StringComparison.OrdinalIgnoreCase);
        ConditionOutcome[] outcomes=[missing?ConditionOutcome.MissingData:ConditionOutcome.Passed,state.SelectedFamily==null?ConditionOutcome.NotEvaluated:ConditionOutcome.Passed,entry==null?ConditionOutcome.NotEvaluated:ConditionOutcome.Passed,matched!=null?ConditionOutcome.Passed:entry!=null?ConditionOutcome.Failed:ConditionOutcome.NotEvaluated];
        return new(stock,last.AvailableAt,last.Event.TradePrice??last.Event.LastPrice,null,null,null,0,matched!=null,matched?.Reason??state.Status,"",observations.Min(e=>e.AvailableAt),last.AvailableAt){BinsEvaluation=new(state,matched??entry,outcomes)};
    }
}
