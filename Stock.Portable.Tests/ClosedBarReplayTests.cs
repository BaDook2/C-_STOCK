using Stock.Core.Research.DayTrading;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

internal static partial class Program
{
    private static void ClosedBarReplayChecks()
    {
        var open=new DateTimeOffset(2026,11,6,9,0,0,TimeSpan.FromHours(9));var instrument=new Instrument("005930",Exchange.KRX);
        var definition=StrategyDTestConfig();definition.Configuration.PlannedInvestment=100000;
        decimal[] prices=[10100,9900,9920,9950,9950,10100,10120,10160,10180,10100,10090];
        var bars=prices.Select((p,i)=>new StrategyBar(new(open.AddMinutes(i+1),p,p,p,p,1000),open.AddMinutes(i),open.AddMinutes(i+1),i+1,p*1000,"OHLC_FIXTURE")).ToArray();
        var costs=new ExecutionSettings{UseSpread=false,FeePercent=definition.Configuration.CommissionPercent,TaxPercent=0,SlippagePercent=0,LatencyMs=0,ParticipationPercent=100,AllowPartial=true,TimeoutMilliseconds=600000};
        var item=new IntradayReplayItem(instrument,definition,StrategyDSeed(open),"D");
        var request=new ClosedBarReplayRequest([new(item,bars)],"Synthetic closed OHLCV",open,open.AddMinutes(11),open.AddMinutes(11),200000,costs,definition.DayTrading!.Risk);
        var engine=new ClosedBarReplayEngine();var result=engine.Run(request);
        Check(result.Signals.Length==1&&result.Signals[0].Time==open.AddMinutes(4),"bar approximation signals only after the actual fourth close, never at an intrabar low");
        Check(result.Fills.Length==4&&result.Trades.Length==1&&result.Trades[0].ExitAt!=null,"closed-bar model reuses the same partial/partial/trailing portfolio execution");
        Check(result.Fills[0].Time==open.AddMinutes(4).AddTicks(1)&&result.Fills[0].Price==9950,"bar signal fills at the following open plus explicit minimum delay");
        Check(result.Precision.Contains("근사")&&result.Notices.Any(n=>n.Contains("장중")),"closed-bar precision and missing intrabar order remain explicit in results");
        var prefix=engine.Run(request with{To=open.AddMinutes(4)});
        Check(prefix.Fills.Length==0&&prefix.Orders.Single().Active,"closed-bar prefix cannot use a future opening to fill a last-close signal");
        Check(Serialization.Pack(prefix.Signals)==Serialization.Pack(result.Signals.Where(s=>s.Time<=prefix.To).ToArray()),"later bars cannot modify earlier closed-bar signals");
        var uncertainSeed=StrategyDSeed(open);uncertainSeed.Facts.StatusRecords=[];uncertainSeed.Facts.RiskRecords=[];
        var uncertain=request with{Items=[new(item with{Seed=uncertainSeed},bars)]};
        Check(engine.Run(uncertain).Signals.Length==0,"unknown historical status or fundamental risk blocks strict closed-bar research");
        var assumed=engine.Run(uncertain with{AssumeUnknownTradableAndNoRisk=true});
        Check(assumed.Signals.Length==1&&assumed.Notices.Any(n=>n.Contains("研究",StringComparison.Ordinal)||n.Contains("연구가정")),"only explicit research assumptions can supply unknown historical eligibility");
        uncertainSeed.Facts.RiskRecords=[new("005930",DateOnly.FromDateTime(open.Date),true,open)];
        Check(engine.Run(uncertain with{AssumeUnknownTradableAndNoRisk=true}).Signals.Length==0,"research defaults never overwrite a known historical risk flag");
        var shock=bars.ToArray();shock[5]=shock[5] with{Price=shock[5].Price with{High=11000,Low=9800}};
        var simultaneous=engine.Run(request with{Items=[new(item,shock)]});
        Check(simultaneous.Orders.Any(o=>!o.Buy&&o.Reason=="FixedStop")&&!simultaneous.Orders.Any(o=>o.Reason=="TP1"),"same OHLC candle hitting stop and profit requests only the protective exit first");
        var unsupported=definition.Snapshot();unsupported.DayTrading!.Support.ProfileSource=VolumeProfileSource.TradeBased;
        Throws<InvalidOperationException>(()=>engine.Run(request with{Items=[new(item with{Strategy=unsupported},bars)]}),"OHLC research cannot masquerade as original-trade volume profile");
        Throws<InvalidOperationException>(()=>engine.Run(request with{Items=[new(item,[])]}),"empty OHLC input reports missing data instead of a successful zero result");
        Throws<InvalidOperationException>(()=>engine.Run(request with{Items=[new(item,[..bars,bars[0]])]}),"duplicate closed-bar input is rejected before any assumed opening fills");
        var rawRuntime=new StrategyDRuntime(instrument,definition,StrategyDSeed(open),"D");
        Throws<InvalidOperationException>(()=>rawRuntime.OnClosedBar(bars[0]),"normal streaming runtime rejects research-only candle injection");
        var approxRuntime=new StrategyDRuntime(instrument,definition,StrategyDSeed(open),"D",true);
        Throws<InvalidOperationException>(()=>approxRuntime.OnMarket(new(instrument,EventKind.Trade,open,open,"TEST",1){TradePrice=10000,TradeSize=1},open),"research runtime rejects mixing constituent raw events into its close approximation");
        var data=new StrategyDDataEngine(instrument,definition,StrategyDSeed(open,true),true);data.OnClosedBar(bars[0]);
        Check(data.Latest!.Supports.All(s=>s.MovingAverage==10000),"bar research retains prior-session MA240 without the future current-session close");
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        Throws<OperationCanceledException>(()=>engine.Run(request,canceled.Token),"closed-bar replay respects cancellation");
        var service=new IntradayBacktestService();var cached=service.RunAsync(request,default).GetAwaiter().GetResult();cached.Account.Rows[0]=cached.Account.Rows[0] with{Quantity=999};
        Check(service.RunAsync(request,default).GetAwaiter().GetResult().Account.Rows[0].Quantity==0,"bar and raw requests share one typed research service with isolated cached snapshots");
        var weekendEnd=open.AddDays(2).AddHours(10);
        var weekend=engine.Run(request with{To=weekendEnd,AvailableAt=weekendEnd,MinimumAnnualizationSessions=2,ClockIntervalMilliseconds=60000});
        Check(weekend.Performance.Cagr==null&&weekend.Performance.Sharpe==null,"weekend clock pulses cannot inflate trading-day observations or enable short-sample annualization");
    }
}
