using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;
/// <summary>One symbol, sequential point-in-time decisions and next tradable opening fills.</summary>
public sealed class OversoldBacktest
{
    private sealed record Pending(OversoldSignal Signal,bool Buy,int EntryNumber,string[] ExitLegs,DateOnly TriggerDate);
    private sealed class PositionState
    {
        public int Quantity,Entries,PeakQuantity,HarvestBasis,FirstSession;public decimal Average,RemainingCost,LastEntry,Peak;public bool Tp1,Tp2,HarvestStarted;public OversoldTrade? Trade;
    }
    public OversoldResult Run(BarDataset dataset,OversoldConfig requested,DateOnly from,DateOnly to,HistoricalFacts input,CancellationToken token=default,IReadOnlyList<ISupportProvider>? supportProviders=null)
    {
        var c=requested.Clone();c.Validate();if(from>to)throw new InvalidOperationException("시작일은 종료일 이하여야 합니다.");if(dataset.Stock.Exchange!=Exchange.KRX)throw new InvalidOperationException("국내 일반주식 일봉 전용입니다.");var facts=input.Snapshot();
        var result=new OversoldResult{Stock=dataset.Stock,From=from,To=to,Configuration=c,Source=dataset.Source+" / "+facts.Source,DataHash=TradingStore.Hash(Serialization.Pack(dataset.CompletedBars)),FactsHash=TradingStore.Hash(Serialization.Pack(facts)),Cash=c.InitialCapital};
        var bars=dataset.CompletedBars.OrderBy(b=>b.Time).ToArray();foreach(var b in bars)b.Validate();if(bars.GroupBy(DailyDates.Of).Any(g=>g.Count()>1))throw new InvalidOperationException("같은 날짜의 일봉이 중복되었습니다.");
        var declared=facts.Sessions.Where(s=>s.Date>=from&&s.Date<=to).ToDictionary(s=>s.Date);
        var derived=bars.Select(DailyDates.Of).Where(d=>d>=from&&d<=to&&!declared.ContainsKey(d)).Distinct().Select(d=>new DailyMarketSession(d,new DateTimeOffset(d.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(d.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)))).ToArray();
        var sessions=declared.Values.Concat(derived).OrderBy(s=>s.Date).ToArray();
        if(derived.Length>0)result.Notices.Add($"실제 완성 일봉 {derived.Length:N0}개의 날짜로 거래 세션을 구성했습니다. 평일이나 없는 봉은 임의 생성하지 않았습니다.");
        if(sessions.Length==0){result.Notices.Add("선택 기간에 완성 일봉과 검증된 거래 세션이 없습니다.");result.Performance=PerformanceCalculator.Calculate(c.InitialCapital,[],[],c.AnnualRiskFreePercent,c.AnnualTradingDays);return result;}
        if(c.UsePriceOnlyBacktest)result.Notices.Add("가격자료 검증 모드: 누락된 과거 종목상태·위험은 일반주식·거래가능·위험없음으로 가정합니다. 실제 주문과 투자 성과 검증에는 사용할 수 없습니다.");
        var cache=new OversoldIndicatorCache(c);var providers=supportProviders??[new MovingAverageSupportProvider(),new ConfirmedPivotHighProvider(),new ApproximateVolumeZoneProvider()];var pos=new PositionState();Pending? pending=null;OversoldState state=OversoldState.Idle;var entryDetector=new OversoldEntryDetector();int lastExit=-1;decimal? previousCenter=null;int cursor=0;decimal lastClose=0;
        IReadOnlyList<SupportCandidate> candidates=[];PositionRuleEvaluation? positionRules=null;AdditionalEntryEvaluation? additionalEntry=null;
        void Append(PriceBar b){cache.Append(b);candidates=providers.SelectMany(p=>p.Get(cache.History,c)).ToArray();lastClose=b.Close;}
        while(cursor<bars.Length&&DailyDates.Of(bars[cursor])<from)Append(bars[cursor++]);
        var unready=c.MaPeriods().Where(n=>n>cache.History.Count).ToArray();if(c.UseMovingAverageSupport&&unready.Length>0)result.Notices.Add("기간 시작 전 이동평균 준비 부족: "+string.Join(", ",unready)+"봉. 준비될 때까지 해당 후보만 제외합니다. 공개 일봉은 제공처에 따라 약 1,500봉 한계가 있습니다.");
        result.Notices.Add("단일 종목 독립자본 검사. 과거 전체시장 universe/공유자본 포트폴리오 결과가 아닙니다. 실제 거래대금·과거 상태·위험 미지는 진입 보류합니다.");result.Notices.Add("일봉 근사 거래량 구간은 실제 volume profile이 아닙니다. 시가 유동성·장중 경로를 복원하지 않으며 다음 시가 정수수량 모형입니다.");
        void Schedule(DailyMarketSession session,bool buy,int entry,string reason,DailyIndicators indicators,SupportCluster? cluster,params string[] legs)
        {
            var signal=new OversoldSignal(session.Close,buy?"Buy":"Sell",reason,indicators,cluster,state,OversoldPositionRules.Snapshot(session.Close,lastClose,indicators,candidates,c,cluster,additionalEntry,positionRules,reason));result.Signals.Add(signal);pending=new(signal,buy,entry,legs,entryDetector.TriggerDate??session.Date);
        }
        for(int day=0;day<sessions.Length;day++)
        {
            token.ThrowIfCancellationRequested();var session=sessions[day];while(cursor<bars.Length&&DailyDates.Of(bars[cursor])<session.Date)Append(bars[cursor++]);var bar=cursor<bars.Length&&DailyDates.Of(bars[cursor])==session.Date?bars[cursor]:null;
            if(bar!=null&&bar.Time>session.Close)throw new InvalidOperationException("일봉 확정 시각이 입력한 시장 종가 시각보다 늦습니다.");
            DailySecurityStatus? StatusAt(DateTimeOffset at)=>facts.Status(dataset.Stock.Symbol,session.Date,at)??(c.UsePriceOnlyBacktest?new(dataset.Stock.Symbol,session.Date,at,HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false):null);
            bool? RiskAt(DateTimeOffset at)=>facts.Risk(dataset.Stock.Symbol,session.Date,at)??(c.UsePriceOnlyBacktest?false:null);
            var openStatus=StatusAt(session.Open);
            if(pending!=null&&bar!=null&&openStatus is {Suspended:false})
            {
                var order=pending;pending=null;
                if(order.Buy&&(!HistoricalFacts.Tradable(openStatus)||RiskAt(session.Open)!=false))result.Notices.Add(session.Date+" 매수 취소: 시가 시점의 종목 상태/위험 정보를 확인할 수 없거나 위험입니다.");
                else if(order.Buy)
                {
                    var price=bar.Open*(1+c.SlippagePercent/100);var budget=c.PlannedInvestment*c.Ratios()[order.EntryNumber-1]/100;var quantity=decimal.Floor(Math.Min(budget,result.Cash)/(price*(1+c.CommissionPercent/100)));if(quantity>int.MaxValue-pos.Quantity)throw new InvalidOperationException("모형 수량 한도를 초과했습니다.");var qty=(int)quantity;
                    if(qty>0)
                    {
                        var fee=qty*price*c.CommissionPercent/100;var cost=qty*price+fee;
                        if(pos.Quantity==0){pos=new(){FirstSession=day,Peak=price};pos.Trade=new(){Symbol=dataset.Stock.Symbol,TriggerDate=order.TriggerDate,TriggerType=order.Signal.Indicators.Trigger,MediumDrawdown=order.Signal.Indicators.MediumDrawdown,ShortDrawdown=order.Signal.Indicators.ShortDrawdown,EntrySignalDate=order.Signal.SignalTime,FirstExecutionDate=session.Open,EntryRsi=order.Signal.Indicators.Rsi,VolumeRatio=order.Signal.Indicators.VolumeRatio,Cluster=order.Signal.Cluster!,Configuration=c.Clone(),Reentry=previousCenter!=null};result.Trades.Add(pos.Trade);}
                        pos.Average=(pos.Average*pos.Quantity+price*qty)/(pos.Quantity+qty);pos.Quantity+=qty;pos.PeakQuantity=Math.Max(pos.PeakQuantity,pos.Quantity);pos.Entries++;pos.LastEntry=price;pos.RemainingCost+=cost;result.Cash-=cost;pos.Trade!.AcquisitionCost+=cost;pos.Trade.AveragePrice=pos.Average;
                        pos.Trade.Executions.Add(new(order.Signal.SignalTime,session.Open,true,qty,price,fee,"Entry"+pos.Entries,pos.Average,Snapshot:order.Signal.Snapshot));state=pos.Entries switch{1=>OversoldState.Entry1,2=>OversoldState.Entry2,3=>OversoldState.Entry3,_=>OversoldState.Holding};result.Transitions.Add(new(session.Open,state,"Entry"+pos.Entries));
                    }else result.Notices.Add(session.Date+" 매수 0주: 예산·현금으로 1주를 살 수 없습니다. 매수 횟수는 증가하지 않았습니다.");
                }
                else if(pos.Quantity>0)
                {
                    foreach(var leg in order.ExitLegs)
                    {
                        var basis=pos.HarvestBasis>0?pos.HarvestBasis:pos.PeakQuantity;int qty=leg=="TP1"?(int)decimal.Floor(basis*c.TakeProfit1Ratio/100):leg=="TP2"?(int)decimal.Floor(basis*c.TakeProfit2Ratio/100):pos.Quantity;qty=Math.Min(qty,pos.Quantity);
                        if(leg=="TP1")pos.Tp1=true;if(leg=="TP2")pos.Tp2=true;
                        if(qty==0){result.Notices.Add(session.Date+" "+leg+" 0주: 정수 내림으로 체결 없이 단계 완료.");continue;}
                        var price=bar.Open*(1-c.SlippagePercent/100);var fee=qty*price*(c.CommissionPercent+c.TaxPercent)/100;var allocatedCost=pos.RemainingCost*qty/pos.Quantity;pos.RemainingCost-=allocatedCost;pos.Quantity-=qty;result.Cash+=qty*price-fee;pos.Trade!.RealizedPnl+=qty*price-fee-allocatedCost;
                        pos.Trade.Executions.Add(new(order.Signal.SignalTime,session.Open,false,qty,price,fee,leg,pos.Average,qty*price-fee-allocatedCost,order.Signal.Snapshot));
                        if(leg is "TP1" or "TP2"){if(!pos.HarvestStarted){pos.HarvestBasis=basis;pos.Peak=price;}pos.HarvestStarted=true;state=OversoldState.PartialTP;result.Transitions.Add(new(session.Open,state,leg));}
                        if(pos.Quantity==0){pos.Trade.LastExecutionDate=session.Open;pos.Trade.HoldingDays=day-pos.FirstSession+1;pos.Trade.FinalReturnPercent=pos.Trade.RealizedPnl/pos.Trade.AcquisitionCost*100;pos.Trade.ExitReason=leg;previousCenter=pos.Trade.Cluster.Center;lastExit=day;state=OversoldState.Exited;result.Transitions.Add(new(session.Open,state,leg));entryDetector.Reset();break;}
                    }
                }
            }
            if(bar!=null)Append(bars[cursor++]);
            if(pos.Quantity==0)entryDetector.Observe(bar,day,c);
            positionRules=null;additionalEntry=null;var indicator=cache.Current;string reason="자료 부족";SupportCluster? cluster=null;
            if(pos.Quantity>0)
            {
                pos.Trade!.HoldingDays=day-pos.FirstSession+1;state=pos.HarvestStarted?OversoldState.PartialTP:OversoldState.Holding;
                positionRules=OversoldPositionRules.Evaluate(c,bar,pos.Trade.Cluster.Lower,pos.Average,pos.Peak,day-pos.FirstSession+1,pos.Trade.Executions.Any(e=>!e.Buy&&e.Reason=="TP1"));
                var high=c.TouchPolicy==DailyTouchPolicy.CloseOnly?bar?.Close:bar?.High;bool trailing=positionRules.TrailingActive;var exit=positionRules.Exit;
                if(exit!=null){reason=exit;if(pending==null||pending.Buy||pending.ExitLegs.All(l=>l is "TP1" or "TP2"))Schedule(session,false,0,exit,indicator,pos.Trade.Cluster,exit);}
                else if(pending==null&&bar!=null)
                {
                    var legs=new List<string>();if(c.UseTakeProfit1&&!pos.Tp1&&high>=pos.Average*(1+c.TakeProfit1Percent/100))legs.Add("TP1");if(c.UseTakeProfit2&&!pos.Tp2&&high>=pos.Average*(1+c.TakeProfit2Percent/100))legs.Add("TP2");
                    if(legs.Count>0){reason=string.Join("+",legs);Schedule(session,false,0,reason,indicator,pos.Trade.Cluster,legs.ToArray());}
                    else if(!pos.HarvestStarted&&pos.Entries<c.MaxEntries&&bar.Close<=pos.LastEntry*(1+c.AdditionalEntryDropPercent/100))
                    {
                        cluster=SelectCluster();additionalEntry=OversoldPositionRules.Additional(c,pos.LastEntry,bar.Close,cluster,RiskAt(session.Close)==false&&HistoricalFacts.Tradable(StatusAt(session.Close)));if(additionalEntry.FinalDecision){reason="AdditionalEntry";Schedule(session,true,pos.Entries+1,reason,indicator,cluster??pos.Trade.Cluster);}else reason="추가매수 보류: 위험/상태/지지 접근";
                    }else reason="보유 · 추가 조건 대기";
                }else reason="청산 주문 거래가능 시가 대기";
                if(trailing&&high is {} observed)pos.Peak=Math.Max(pos.Peak,observed);
            }
            else if(pending==null&&bar!=null)
            {
                var prior=cache.History.Take(Math.Max(0,cache.History.Count-1)).TakeLast(c.AverageTradingValueLookbackDays).Select(b=>facts.TradingValue(dataset.Stock.Symbol,DailyDates.Of(b),session.Close)).ToArray();
                var evaluation=entryDetector.Evaluate(bar,day,session.Date,c,indicator,candidates,StatusAt(session.Close),RiskAt(session.Close),prior,lastExit,previousCenter,allowPriceOnlyBacktest:c.UsePriceOnlyBacktest);
                state=evaluation.State;reason=evaluation.Reason;cluster=evaluation.Selected;
                if(evaluation.Enter)Schedule(session,true,1,reason,indicator,cluster);
            }else reason=pending!=null?"주문 거래가능 시가 대기":"해당 거래일 일봉 없음";
            result.Transitions.Add(new(session.Close,state,reason));result.Decisions.Add(new(session.Date,state,reason,indicator,cluster,entryDetector.ReboundLow));result.Equity.Add(new(session.Close,result.Cash+pos.Quantity*lastClose));
            SupportCluster? SelectCluster()=>SupportClustering.Build(candidates,c.SupportClusterRangePercent).Where(z=>z.Count>=c.MinimumSupportCount&&z.Approached(bar!.Close,c.SupportDistancePercent)).OrderBy(z=>Math.Abs(z.Center/bar!.Close-1)).ThenBy(z=>z.Center).FirstOrDefault();

        }
        result.Quantity=pos.Quantity;result.PendingOrderAtEnd=pending!=null;if(pending!=null)result.Notices.Add("종료일 신호/거래불가 대기 주문은 미체결로 남겼습니다. 종료일 종가로 소급 체결하지 않습니다.");
        result.Performance=PerformanceCalculator.Calculate(c.InitialCapital,result.Equity,result.Trades,c.AnnualRiskFreePercent,c.AnnualTradingDays,sessions[0].Open);return result;
    }
}
