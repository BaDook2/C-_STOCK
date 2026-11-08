using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

/// <summary>Common cost-inclusive fill collector/report for raw replay, bar research and streamed paper sessions.</summary>
internal sealed class IntradayReportBuilder(IntradayReplayItem[] items,DateTimeOffset from,DateTimeOffset to,decimal initialCash,int minimumAnnualizationSessions)
{
    private sealed class Trip
    {
        public int Number,Quantity;public string Symbol="";public DateTimeOffset Entry;public decimal Cost,Pnl;
        public readonly List<Fill> Fills=[];public IntradayEntrySignal? Signal;
        public IntradayRoundTrip Finish(DateTimeOffset? exit)=>new(Number,Symbol,Entry,exit,Cost,Pnl,exit!=null&&Cost>0?Pnl/Cost*100:null,exit==null?null:(exit.Value-Entry).TotalSeconds,Fills.ToArray(),Signal);
    }
    private readonly List<IntradayEntrySignal> signals=[];
    private readonly List<IntradayRoundTrip> trades=[];
    private readonly Dictionary<string,Trip> opened=[];
    private readonly List<BarEquity> curve=[new(from,initialCash)];
    private int tradeNumber;
    public void Attach(DayTradingPortfolioEngine portfolio)
    {
        portfolio.EntrySignalObserved+=signals.Add;
        portfolio.FillConfirmed+=(fill,evidence)=>
        {
            var key=fill.Instrument.Key;
            if(fill.Buy&&!opened.ContainsKey(key))opened[key]=new(){Number=++tradeNumber,Symbol=key,Entry=fill.Time,Signal=evidence};
            if(!opened.TryGetValue(key,out var trip))throw new InvalidOperationException("진입이 없는 가상 매도 체결입니다.");
            trip.Fills.Add(fill);trip.Quantity+=fill.Buy?fill.Quantity:-fill.Quantity;
            if(fill.Buy)trip.Cost+=fill.Quantity*fill.Price+fill.Fee;else trip.Pnl+=fill.RealizedPnl;
            if(trip.Quantity==0){trades.Add(trip.Finish(fill.Time));opened.Remove(key);}
        };
    }
    public void Mark(DateTimeOffset at,decimal equity)=>curve.Add(new(at,equity));
    public IntradayReplayResult Finish(DayTradingPortfolioEngine portfolio,IDayTradingExecutionAdapter execution,string dataHash,string configurationHash,
        string precision,string source,int inputEvents,int rejectedEvents,IEnumerable<string> inputNotices)
    {
        var all=trades.Concat(opened.Values.Select(t=>t.Finish(null))).OrderBy(t=>t.Number).ToArray();
        var equity=curve.GroupBy(p=>p.Time).Select(g=>g.Last()).OrderBy(p=>p.Time).ToArray();
        // Clock pulses on weekends/after hours are not trading-day return observations.
        var sessions=items.SelectMany(i=>i.Seed.Facts.Sessions).Where(s=>s.Open<=to&&s.Close>=from).GroupBy(s=>s.Date).ToDictionary(g=>g.Key,g=>g.Last());
        var daily=equity.GroupBy(p=>DailyQuoteMerger.Date(p.Time,items[0].Instrument.Exchange))
            .Where(g=>sessions.ContainsKey(g.Key)).Select(g=>g.LastOrDefault(p=>p.Time<=sessions[g.Key].Close)??g.Last()).ToArray();
        var metricTrades=all.Select(t=>new OversoldTrade{Symbol=t.Symbol,FirstExecutionDate=t.EntryAt,LastExecutionDate=t.ExitAt,
            AcquisitionCost=t.AcquisitionCost,RealizedPnl=t.RealizedProfit,FinalReturnPercent=t.ReturnPercent,
            HoldingDays=t.ExitAt==null?0:items.Single(i=>i.Instrument.Key==t.Symbol).Seed.Facts.Sessions.Count(s=>s.Close>=t.EntryAt&&s.Open<=t.ExitAt)}).ToArray();
        var shared=items[0].Strategy.Configuration;
        var stats=PerformanceCalculator.Calculate(initialCash,daily,metricTrades,shared.AnnualRiskFreePercent,shared.AnnualTradingDays,from);
        var high=initialCash;decimal drawdown=0;foreach(var point in equity){high=Math.Max(high,point.Value);if(high>0)drawdown=Math.Max(drawdown,(high-point.Value)/high*100);}
        stats=stats with{MaxDrawdown=drawdown,Cagr=daily.Length<minimumAnnualizationSessions?null:stats.Cagr,Sharpe=daily.Length<minimumAnnualizationSessions?null:stats.Sharpe};
        var notices=new List<string>(inputNotices){"공유 가상계좌 · 실제 주문 없음","당일 청산은 주문 시도이며 미체결/미청산을 보장하지 않음"};
        if(daily.Length<minimumAnnualizationSessions)notices.Add("짧은 표본의 CAGR/Sharpe 연율화 미표시");
        if(items.Any(i=>i.Strategy.DayTrading!.Support.ProfileSource==VolumeProfileSource.Approximate))notices.Add("OHLCV 근사 매물대 사용 · 정밀 체결 Volume Profile과 구분");
        var account=portfolio.Snapshot();if(account.NeedsReconciliation)notices.Add("미청산/미확인 주문 · 결과의 계좌 경고 확인 필요");
        var closed=all.Where(t=>t.ExitAt!=null).ToArray();
        return new("intraday-run:"+TradingStore.Hash(dataHash+configurationHash),dataHash,configurationHash,precision,source,from,to,initialCash,account,
            signals.ToArray(),execution.Fills.ToArray(),all,equity,stats,closed.Length>0?(decimal)closed.Average(t=>t.HoldingSeconds!.Value):null,
            notices.Distinct().ToArray(),portfolio.ExitSnapshots,inputEvents,rejectedEvents,execution.Orders.ToArray());
    }
}
