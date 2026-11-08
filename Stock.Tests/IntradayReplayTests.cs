using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void IntradayReplayChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var instrument=new Instrument("005930",Exchange.KRX);
        var definition=StrategyDTestConfig();definition.Configuration.PlannedInvestment=100000;
        var prices=new decimal[]{10100,9900,9920,9950,9950,10100,10120,10160,10180,10100,10090};
        var events=prices.Select((p,i)=>new MarketEvent(instrument,EventKind.Trade,open.AddSeconds(i),open.AddSeconds(i),"TEST",i+1){TradePrice=p,TradeSize=1000,CumulativeVolume=(i+1)*1000}).ToArray();
        var execution=new ExecutionSettings{UseSpread=false,FeePercent=definition.Configuration.CommissionPercent,TaxPercent=0,SlippagePercent=0,LatencyMs=0,ParticipationPercent=100,AllowPartial=true,CancelRemainder=false,TimeoutMilliseconds=60000};
        var item=new IntradayReplayItem(instrument,definition,StrategyDSeed(open),"D");
        var request=new IntradayReplayRequest([item],events,new("Synthetic recorded-shape fixture",true,true,true),open,open.AddSeconds(11),open.AddSeconds(11),200000,execution,definition.DayTrading!.Risk);
        var result=new IntradayReplayEngine().Run(request);
        Check(result.Signals.Length==1&&result.Fills.Length==4&&result.Trades.Length==1&&result.Trades[0].ExitAt!=null,"raw replay drives one actual D rebound and the same partial/partial/trailing fill sequence");
        Check(result.Trades[0].RealizedProfit>0&&result.Performance.Trades==1&&result.Performance.WinRate==100&&result.AverageHoldingSeconds==6,"intraday report uses completed roundtrip net costs and exact holding seconds");
        Check(result.Account.Cash==result.Performance.FinalEquity&&result.Account.Cash>request.InitialCash&&result.Account.Rows[0].Quantity==0,"one shared replay account reconciles cash, net result and confirmed flat ownership");
        Check(result.Performance.Cagr==null&&result.Performance.Sharpe==null&&result.Notices.Any(n=>n.Contains("합성")),"short synthetic replay suppresses misleading annualized performance and states its input nature");
        var liveSignals=new List<IntradayEntrySignal>();var paper=new DayTradingPaperAdapter("KRW",request.InitialCash,execution);
        var manual=new DayTradingPortfolioEngine(paper,[new StrategyDRuntime(instrument,definition,StrategyDSeed(open),"D")],definition.DayTrading.Risk);manual.EntrySignalObserved+=liveSignals.Add;manual.SetEntriesEnabled(true,open.AddTicks(-1));
        foreach(var e in events){manual.OnTime(e.ReceivedTime);manual.OnMarket(e,e.ReceivedTime);}manual.OnTime(request.To);
        Check(Serialization.Pack(result.Signals)==Serialization.Pack(liveSignals),"recorded replay and streaming paper share identical signal IDs, times, reasons and frozen evidence");
        Check(Serialization.Pack(result.Fills)==Serialization.Pack(paper.Fills),"same execution adapter and event stream reproduce exact confirmed paper fills");
        var prefix=new IntradayReplayEngine().Run(request with{To=open.AddSeconds(3)});
        Check(prefix.Fills.Length==0&&prefix.Orders.Single().Active&&prefix.Account.Rows[0].Quantity==0,"last-signal prefix remains pending rather than using its own signal event as a fill");
        Check(Serialization.Pack(prefix.Signals)==Serialization.Pack(result.Signals.Where(s=>s.Time<=prefix.To).ToArray()),"later event suffix never changes earlier D signals");
        var future=events[0] with{ExchangeTime=open.AddHours(2),ReceivedTime=open.AddHours(2),Sequence=999,TradePrice=999999,CumulativeVolume=999999};
        var suffix=new IntradayReplayEngine().Run(request with{Events=[..events,future]});
        Check(Serialization.Pack(result.Signals)==Serialization.Pack(suffix.Signals)&&Serialization.Pack(result.Fills)==Serialization.Pack(suffix.Fills),"out-of-range future trades do not alter selected replay signal or fill results");
        Throws<InvalidOperationException>(()=>new IntradayReplayEngine().Run(request with{Provenance=new("OHLC",false,false)}),"OHLC history cannot be passed off as precise raw-trade replay");
        Throws<InvalidOperationException>(()=>new IntradayReplayEngine().Run(request with{Events=[]}),"empty raw interval returns an explicit data error rather than misleading zero results");
        Throws<InvalidOperationException>(()=>new IntradayReplayEngine().Run(request with{To=request.AvailableAt.AddDays(1)}),"future end date beyond available data is rejected");
        Throws<InvalidOperationException>(()=>new IntradayReplayEngine().Run(request with{ClockIntervalMilliseconds=0}),"invalid replay clock interval rejected");
        var costs=Serialization.Unpack<ExecutionSettings>(Serialization.Pack(execution));costs.FeePercent=.2m;
        Throws<InvalidOperationException>(()=>new IntradayReplayEngine().Run(request with{Execution=costs}),"shared execution costs cannot silently disagree with strategy risk assumptions");
        using var cancel=new CancellationTokenSource();cancel.Cancel();Throws<OperationCanceledException>(()=>new IntradayReplayEngine().Run(request,cancel.Token),"raw replay respects cancellation before allocating its execution state");
        var service=new IntradayBacktestService();var cached=service.RunAsync(request,default).GetAwaiter().GetResult();cached.Account.Rows[0]=cached.Account.Rows[0] with{Quantity=999};
        var restored=service.RunAsync(request,default).GetAwaiter().GetResult();Check(restored.Account.Rows[0].Quantity==0&&restored.Id==result.Id,"content-hash cache returns isolated replay results rather than caller-mutated UI objects");
        var revised=definition.Snapshot();revised.Configuration.ReboundPercent=.9m;
        var changed=service.RunAsync(request with{Items=[item with{Strategy=revised}]},default).GetAwaiter().GetResult();
        Check(changed.ConfigurationHash!=result.ConfigurationHash&&changed.Id!=result.Id,"changed strategy snapshot cannot reuse an older backtest cache entry");
        var quoteModel=Serialization.Unpack<ExecutionSettings>(Serialization.Pack(execution));quoteModel.UseSpread=true;
        var noBook=new IntradayReplayEngine().Run(request with{Execution=quoteModel});
        Check(noBook.Fills.Length==0&&noBook.Notices.Any(n=>n.Contains("원본 호가 없음")),"missing quote history does not fabricate spread-based fills");
    }
}
