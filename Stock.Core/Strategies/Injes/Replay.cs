using Stock.Core.Research.DayTrading;
using Stock.Core.Trading;

namespace Stock.Core.Strategies.Injes;

public sealed record InjesReplayRequest(InjesStrategyDefinition Definition,Instrument[] Instruments,InjesObservation[] Events,
    DateTimeOffset From,DateTimeOffset To,decimal InitialCash,string Source,bool OriginalTradeEvents,bool OriginalReceiveTimes,
    bool SyntheticFixture=false,int ClockIntervalMilliseconds=250);
public sealed record InjesClosedTrade(string Symbol,DateTimeOffset EnteredAt,DateTimeOffset ExitedAt,decimal RealizedProfit,double HoldingSeconds);
public sealed record InjesPerformance(int Trades,int OpenPositions,decimal RealizedProfit,decimal UnrealizedProfit,decimal NetReturnPercent,
    decimal? WinRate,decimal? AverageWin,decimal? AverageLossAbsolute,decimal? PayoffRatio,decimal? ProfitFactor,bool InfiniteProfitFactor,
    decimal? Expectancy,decimal MaximumDrawdownPercent,decimal? AverageHoldingSeconds,decimal? AverageSlippagePercent,
    decimal? OrderFillRatio,decimal? PartialFillRatio,decimal? UnfilledOrCancelledRatio,int EntrySignals,IReadOnlyDictionary<string,int> FilterRejections);
public sealed record InjesReplayResult(string Id,string ConfigurationHash,string DataHash,string Source,string ExecutionModel,
    bool SyntheticFixture,InjesStrategyDefinition Definition,StrategySignal[] Signals,Fill[] Fills,ManagedOrder[] Orders,
    Position[] OpenPositions,InjesClosedTrade[] ClosedTrades,EquityPoint[] Equity,InjesPerformance Performance,string[] Notices);

public sealed class InjesReplayEngine
{
    public InjesReplayResult Run(InjesReplayRequest request,CancellationToken token=default)
    {
        var r=request;var definition=r.Definition.Snapshot();
        if(r.From>=r.To||r.To>DateTimeOffset.UtcNow||r.InitialCash<=0||r.ClockIntervalMilliseconds is <1 or >60000||!r.OriginalTradeEvents||!r.OriginalReceiveTimes||string.IsNullOrWhiteSpace(r.Source))throw new InvalidOperationException("원본 체결·수신시각과 유효한 기간·현금·자료 출처가 필요합니다. OHLCV를 틱으로 대체하지 않습니다.");
        var execution=new InjesPaperExecution(r.InitialCash,definition.ExecutionPolicy);var session=new InjesSession(definition,r.Instruments,execution);
        session.SetEntriesEnabled(true,r.From.AddTicks(-1));var now=r.From;var interval=TimeSpan.FromMilliseconds(r.ClockIntervalMilliseconds);
        foreach(var row in r.Events.Select((e,i)=>(e,i)).OrderBy(x=>x.e.KnownAt).ThenBy(x=>x.i))
        {
            token.ThrowIfCancellationRequested();var e=row.e;if(e.KnownAt<r.From||e.KnownAt>r.To)continue;
            while(now<e.KnownAt)
            {
                token.ThrowIfCancellationRequested();
                if(!session.RequiresClockPulse(now)){now+=TimeSpan.FromTicks(((e.KnownAt-now).Ticks+interval.Ticks-1)/interval.Ticks*interval.Ticks);break;}
                session.OnTime(now);now+=interval;
            }
            session.OnMarket(e,e.KnownAt);
        }
        while(now<=r.To){token.ThrowIfCancellationRequested();session.OnTime(now);if(!session.RequiresClockPulse(now))break;now+=interval;}session.OnTime(r.To);
        return Build(session,r.InitialCash,r.Source,r.SyntheticFixture,TradingStore.Hash(InjesStrategyJson.Export(definition)),TradingStore.Hash(Serialization.Pack(r.Events)));
    }
    public static InjesReplayResult Build(InjesSession session,decimal initialCash,string source,bool fixture,string configurationHash,string dataHash)
    {
        var execution=session.Execution;var fills=execution.Fills.ToArray();var orders=execution.Orders.ToArray();var closed=new List<InjesClosedTrade>();
        var rounds=new Dictionary<string,(int Quantity,DateTimeOffset First,decimal Profit)>();
        foreach(var fill in fills)
        {
            var row=rounds.GetValueOrDefault(fill.Instrument.Key);if(fill.Buy){if(row.Quantity==0)row.First=fill.Time;row.Quantity+=fill.Quantity;}
            else{row.Quantity-=fill.Quantity;row.Profit+=fill.RealizedPnl;if(row.Quantity==0){closed.Add(new(fill.Instrument.Key,row.First,fill.Time,row.Profit,(fill.Time-row.First).TotalSeconds));row.Profit=0;}}
            rounds[fill.Instrument.Key]=row;
        }
        var wins=closed.Where(t=>t.RealizedProfit>0).ToArray();var losses=closed.Where(t=>t.RealizedProfit<0).ToArray();
        decimal? winRate=closed.Count>0?(decimal)wins.Length/closed.Count:null;
        decimal? averageWin=wins.Length>0?wins.Average(t=>t.RealizedProfit):null,averageLoss=losses.Length>0?-losses.Average(t=>t.RealizedProfit):null;
        var grossProfit=wins.Sum(t=>t.RealizedProfit);var grossLoss=-losses.Sum(t=>t.RealizedProfit);
        decimal peak=initialCash,mdd=0;foreach(var p in session.Equity){peak=Math.Max(peak,p.Equity);if(peak>0)mdd=Math.Max(mdd,(peak-p.Equity)/peak*100);}
        decimal? Ratio(int count)=>orders.Length>0?(decimal)count/orders.Length:null;
        var slips=fills.Select(f=>orders.First(o=>o.Id==f.OrderId) is {} order?(f.Price/order.Price-1)*100*(f.Buy?1:-1):0).ToArray();
        var performance=new InjesPerformance(closed.Count,execution.Positions.Values.Count(p=>p.Quantity>0),fills.Where(f=>!f.Buy).Sum(f=>f.RealizedPnl),execution.Positions.Values.Sum(p=>p.Unrealized),(execution.Equity-initialCash)/initialCash*100,
            winRate*100,averageWin,averageLoss,averageLoss>0?averageWin/averageLoss:null,grossLoss>0?grossProfit/grossLoss:null,grossLoss==0&&grossProfit>0,
            closed.Count>0?closed.Average(t=>t.RealizedProfit):null,mdd,closed.Count>0?(decimal)closed.Average(t=>t.HoldingSeconds):null,slips.Length>0?slips.Average():null,
            Ratio(orders.Count(o=>o.Filled>0)),Ratio(orders.Count(o=>o.Filled>0&&(o.Filled<o.Quantity||fills.Count(f=>f.OrderId==o.Id)>1))),Ratio(orders.Count(o=>o.Remaining>0||o.State==OrderState.Cancelled)),session.Signals.Count(s=>s.SignalType==SignalType.Enter),session.Rejections);
        return new(Guid.NewGuid().ToString("N"),configurationHash,dataHash,source,"QueuePositionUnknown · 보수적 후속 체결량·지연·부분체결·취소 확인 · 호가 대기열 재현 아님",fixture,session.Definition,session.Signals,fills,orders,execution.Positions.Values.Where(p=>p.Quantity>0).ToArray(),closed.ToArray(),session.Equity,performance,
            [fixture?"합성 검사 자료: 시장 수익성 검증 아님":"기록 가용시각순 재생", "미청산 포지션/미체결 주문은 유지하며 승리 거래에 포함하지 않음", "실제 돈 주문 없음; 미국 틱·호가·당시 기준 자료의 품질에 따른 결과"]);
    }
}
