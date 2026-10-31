using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

/// <summary>Completed minute/hour bars, close-time decisions, and next completed bar opening fills.</summary>
public sealed partial class OversoldBacktest
{
    public OversoldResult Run(StockItem stock,IReadOnlyList<StrategyBar> input,BarSpecification specification,OversoldConfig requested,DateOnly from,DateOnly to,HistoricalFacts inputFacts,CancellationToken token=default,IReadOnlyList<ISupportProvider>? supportProviders=null)
    {
        specification.Validate();if(specification.Unit is not(BarUnit.Minute or BarUnit.Hour))throw new InvalidOperationException("지정봉 백테스트는 분봉 또는 시간봉만 지원합니다.");if(specification.Unit==BarUnit.Minute&&specification.Value>59)throw new InvalidOperationException("분봉 숫자는 1~59로 선택하세요.");if(specification.Unit==BarUnit.Hour&&specification.Value>12)throw new InvalidOperationException("시간봉 숫자는 1~12로 선택하세요.");
        var c=requested.Clone();c.Validate();if(from>to)throw new InvalidOperationException("시작일은 종료일 이하여야 합니다.");if(stock.Exchange!=Exchange.KRX)throw new InvalidOperationException("국내 일반주식 장중봉 전용입니다.");var facts=inputFacts.Snapshot();
        var expectedDuration=specification.Unit==BarUnit.Hour?TimeSpan.FromHours(specification.Value):TimeSpan.FromMinutes(specification.Value);
        var all=input.Where(b=>b.Price.IsComplete).OrderBy(b=>b.ClosedAt).ThenBy(b=>b.Sequence).ToArray();foreach(var b in all){b.Price.Validate();if(b.OpenTime>=b.ClosedAt||b.ClosedAt-b.OpenTime!=expectedDuration)throw new InvalidOperationException($"{specification} 시작·종료 시각을 확인하세요.");}
        if(all.GroupBy(b=>(b.ClosedAt,b.Sequence)).Any(g=>g.Count()>1))throw new InvalidOperationException("지정 봉 순서가 중복되었습니다.");
        var result=new OversoldResult{Stock=stock,From=from,To=to,Configuration=c,BarSpecification=specification,Source=string.Join(" / ",all.Select(b=>b.Source).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct())+" / "+facts.Source,DataHash=TradingStore.Hash(Serialization.Pack(all)),FactsHash=TradingStore.Hash(Serialization.Pack(facts)),Cash=c.InitialCapital};
        var known=facts.Sessions.ToDictionary(s=>s.Date);var dates=all.Select(b=>DailyDates.Of(b.Price)).Distinct().Order().ToArray();
        var sessions=dates.Select(d=>known.GetValueOrDefault(d)??new DailyMarketSession(d,new DateTimeOffset(d.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(d.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)))).OrderBy(s=>s.Open).ToArray();
        var sessionByDate=sessions.ToDictionary(s=>s.Date);if(all.Any(b=>!sessionByDate.TryGetValue(DailyDates.Of(b.Price),out var s)||b.OpenTime<s.Open||b.ClosedAt>s.Close))throw new InvalidOperationException("정규장 밖이거나 시장 세션과 맞지 않는 지정 봉이 있습니다.");
        var bars=all.Where(b=>DailyDates.Of(b.Price)<=to).ToArray();var active=bars.Where(b=>DailyDates.Of(b.Price)>=from).ToArray();
        if(active.Length==0){result.Notices.Add("선택 기간에 완성된 지정 봉이 없습니다.");result.Performance=PerformanceCalculator.Calculate(c.InitialCapital,[],[],c.AnnualRiskFreePercent,c.AnnualTradingDays);return result;}
        if(c.UsePriceOnlyBacktest)result.Notices.Add("가격자료 검증 모드: 누락된 과거 종목상태·위험은 일반주식·거래가능·위험없음으로 가정합니다. 실제 주문과 투자 성과 검증에는 사용할 수 없습니다.");
        result.Notices.Add($"{specification} 완성봉 종가로 판단하고 다음 완성봉 시가에 체결합니다. 같은 봉의 고가·저가 순서는 알 수 없어 Stop을 익절보다 우선합니다.");
        result.Notices.Add("키움 원본 분봉에는 실제 체결대금 필드가 없어 OHLC 평균×거래량 추정값을 사용합니다. 장중봉 백테스트 결과에 이 한계가 포함됩니다.");
        var cache=new OversoldIndicatorCache(c,true);var providers=supportProviders??[new MovingAverageSupportProvider(),new ConfirmedPivotHighProvider(true),new ApproximateVolumeZoneProvider()];var detector=new OversoldEntryDetector();var values=new List<decimal?>();var position=new MinutePosition();MinutePending? pending=null;var state=OversoldState.Idle;var candidates=Array.Empty<SupportCandidate>();int lastExit=-1;decimal? previousCenter=null;decimal lastClose=0;
        void Append(StrategyBar row){cache.Append(row.Price);candidates=providers.SelectMany(p=>p.Get(cache.History,c)).ToArray();values.Add(row.TradingValue);lastClose=row.Price.Close;}
        foreach(var warmup in bars.Where(b=>DailyDates.Of(b.Price)<from)){token.ThrowIfCancellationRequested();Append(warmup);}
        var unready=c.MaPeriods().Where(n=>n>cache.History.Count).ToArray();if(c.UseMovingAverageSupport&&unready.Length>0)result.Notices.Add("기간 시작 전 이동평균 준비 부족: "+string.Join(", ",unready)+"봉. 준비될 때까지 해당 후보만 제외합니다.");
        var dayOrdinals=bars.Select(b=>DailyDates.Of(b.Price)).Distinct().Order().Select((d,i)=>(d,i)).ToDictionary(x=>x.d,x=>x.i);
        foreach(var row in active)
        {
            token.ThrowIfCancellationRequested();var date=DailyDates.Of(row.Price);var day=dayOrdinals[date];var session=sessionByDate[date];
            DailySecurityStatus? StatusAt(DateTimeOffset at)=>facts.Status(stock.Symbol,date,at)??(c.UsePriceOnlyBacktest?new(stock.Symbol,date,at,HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false):null);
            bool? RiskAt(DateTimeOffset at)=>facts.Risk(stock.Symbol,date,at)??(c.UsePriceOnlyBacktest?false:null);
            var openStatus=StatusAt(row.OpenTime);
            if(pending!=null&&openStatus is {Suspended:false})
            {
                var order=pending;pending=null;
                if(order.Buy&&(!HistoricalFacts.Tradable(openStatus)||RiskAt(row.OpenTime)!=false))result.Notices.Add($"{row.OpenTime:yyyy-MM-dd HH:mm} 매수 취소: 체결 시점 상태/위험을 확인할 수 없거나 위험입니다.");
                else if(order.Buy)
                {
                    var price=row.Price.Open*(1+c.SlippagePercent/100);var budget=c.PlannedInvestment*c.Ratios()[order.EntryNumber-1]/100;var quantity=decimal.Floor(Math.Min(budget,result.Cash)/(price*(1+c.CommissionPercent/100)));if(quantity>int.MaxValue-position.Quantity)throw new InvalidOperationException("모형 수량 한도를 초과했습니다.");var qty=(int)quantity;
                    if(qty>0)
                    {
                        var fee=qty*price*c.CommissionPercent/100;var cost=qty*price+fee;
                        if(position.Quantity==0){position=new(){FirstDay=day,Peak=price};position.Trade=new(){Symbol=stock.Symbol,TriggerDate=order.TriggerDate,TriggerType=order.Signal.Indicators.Trigger,MediumDrawdown=order.Signal.Indicators.MediumDrawdown,ShortDrawdown=order.Signal.Indicators.ShortDrawdown,EntrySignalDate=order.Signal.SignalTime,FirstExecutionDate=row.OpenTime,EntryRsi=order.Signal.Indicators.Rsi,VolumeRatio=order.Signal.Indicators.VolumeRatio,Cluster=order.Signal.Cluster!,Configuration=c.Clone()};position.Trade.Reentry=previousCenter!=null;result.Trades.Add(position.Trade);}
                        position.Average=(position.Average*position.Quantity+price*qty)/(position.Quantity+qty);position.Quantity+=qty;position.PeakQuantity=Math.Max(position.PeakQuantity,position.Quantity);position.Entries++;position.LastEntry=price;position.RemainingCost+=cost;result.Cash-=cost;position.Trade!.AcquisitionCost+=cost;position.Trade.AveragePrice=position.Average;
                        position.Trade.Executions.Add(new(order.Signal.SignalTime,row.OpenTime,true,qty,price,fee,"Entry"+position.Entries,position.Average,Snapshot:order.Signal.Snapshot,ExecutionMode:specification.ToString()+" Backtest"));state=position.Entries switch{1=>OversoldState.Entry1,2=>OversoldState.Entry2,3=>OversoldState.Entry3,_=>OversoldState.Holding};result.Transitions.Add(new(row.OpenTime,state,"Entry"+position.Entries));
                    }else result.Notices.Add($"{row.OpenTime:yyyy-MM-dd HH:mm} 매수 0주: 예산·현금으로 1주를 살 수 없습니다.");
                }
                else if(position.Quantity>0)
                {
                    foreach(var leg in order.ExitLegs)
                    {
                        var basis=position.HarvestBasis>0?position.HarvestBasis:position.PeakQuantity;int qty=leg=="TP1"?(int)decimal.Floor(basis*c.TakeProfit1Ratio/100):leg=="TP2"?(int)decimal.Floor(basis*c.TakeProfit2Ratio/100):position.Quantity;qty=Math.Min(qty,position.Quantity);if(leg=="TP1")position.Tp1=true;if(leg=="TP2")position.Tp2=true;if(qty==0)continue;
                        var price=row.Price.Open*(1-c.SlippagePercent/100);var fee=qty*price*(c.CommissionPercent+c.TaxPercent)/100;var allocated=position.RemainingCost*qty/position.Quantity;position.RemainingCost-=allocated;position.Quantity-=qty;result.Cash+=qty*price-fee;position.Trade!.RealizedPnl+=qty*price-fee-allocated;
                        position.Trade.Executions.Add(new(order.Signal.SignalTime,row.OpenTime,false,qty,price,fee,leg,position.Average,qty*price-fee-allocated,order.Signal.Snapshot,specification.ToString()+" Backtest"));
                        if(leg is "TP1" or "TP2"){if(!position.HarvestStarted){position.HarvestBasis=basis;position.Peak=price;}position.HarvestStarted=true;state=OversoldState.PartialTP;result.Transitions.Add(new(row.OpenTime,state,leg));}
                        if(position.Quantity==0){position.Trade.LastExecutionDate=row.OpenTime;position.Trade.HoldingDays=day-position.FirstDay+1;position.Trade.FinalReturnPercent=position.Trade.RealizedPnl/position.Trade.AcquisitionCost*100;position.Trade.ExitReason=leg;previousCenter=position.Trade.Cluster.Center;lastExit=day;state=OversoldState.Exited;result.Transitions.Add(new(row.OpenTime,state,leg));detector.Reset();break;}
                    }
                }
            }
            var priorValues=values.TakeLast(c.AverageTradingValueLookbackDays).ToArray();Append(row);detector.Observe(position.Quantity==0?row.Price:null,day,c);var indicator=cache.Current;string reason="자료 부족";SupportCluster? selected=null;PositionRuleEvaluation? rules=null;AdditionalEntryEvaluation? additional=null;
            void Schedule(bool buy,int entry,string why,SupportCluster? cluster,params string[] legs){var signal=new OversoldSignal(row.ClosedAt,buy?"Buy":"Sell",why,indicator,cluster,state,OversoldPositionRules.Snapshot(row.ClosedAt,row.Price.Close,indicator,candidates,c,cluster,additional,rules,why));result.Signals.Add(signal);pending=new(signal,buy,entry,legs,detector.TriggerDate??date);}
            SupportCluster? SelectCluster()=>SupportClustering.Build(candidates,c.SupportClusterRangePercent).Where(z=>z.Count>=c.MinimumSupportCount&&z.Approached(row.Price.Close,c.SupportDistancePercent)).OrderBy(z=>Math.Abs(z.Center/row.Price.Close-1)).ThenBy(z=>z.Center).FirstOrDefault();
            if(position.Quantity>0)
            {
                position.Trade!.HoldingDays=day-position.FirstDay+1;state=position.HarvestStarted?OversoldState.PartialTP:OversoldState.Holding;rules=OversoldPositionRules.Evaluate(c,row.Price,position.Trade.Cluster.Lower,position.Average,position.Peak,position.Trade.HoldingDays,position.Tp1);var high=c.TouchPolicy==DailyTouchPolicy.CloseOnly?row.Price.Close:row.Price.High;var exit=rules.Exit;
                if(exit!=null){reason=exit;if(pending==null||pending.Buy||pending.ExitLegs.All(l=>l is "TP1" or "TP2"))Schedule(false,0,exit,position.Trade.Cluster,exit);}
                else if(pending==null)
                {
                    var legs=new List<string>();if(c.UseTakeProfit1&&!position.Tp1&&high>=position.Average*(1+c.TakeProfit1Percent/100))legs.Add("TP1");if(c.UseTakeProfit2&&!position.Tp2&&high>=position.Average*(1+c.TakeProfit2Percent/100))legs.Add("TP2");
                    if(legs.Count>0){reason=string.Join("+",legs);Schedule(false,0,reason,position.Trade.Cluster,legs.ToArray());}
                    else if(!position.HarvestStarted&&position.Entries<c.MaxEntries&&row.Price.Close<=position.LastEntry*(1+c.AdditionalEntryDropPercent/100)){selected=SelectCluster();additional=OversoldPositionRules.Additional(c,position.LastEntry,row.Price.Close,selected,RiskAt(row.ClosedAt)==false&&HistoricalFacts.Tradable(StatusAt(row.ClosedAt)));if(additional.FinalDecision){reason="AdditionalEntry";Schedule(true,position.Entries+1,reason,selected??position.Trade.Cluster);}else reason="추가매수 보류: 위험/상태/지지 접근";}else reason="보유 · 추가 조건 대기";
                }else reason="청산 주문 다음 지정 봉 시가 대기";if(rules.TrailingActive)position.Peak=Math.Max(position.Peak,high);
            }
            else if(pending==null)
            {
                var evaluation=detector.Evaluate(row.Price,day,date,c,indicator,candidates,StatusAt(row.ClosedAt),RiskAt(row.ClosedAt),priorValues,lastExit,previousCenter,cache.History.Count,currentResearch:false,allowPriceOnlyBacktest:c.UsePriceOnlyBacktest);state=evaluation.State;reason=evaluation.Reason;selected=evaluation.Selected;if(evaluation.Enter)Schedule(true,1,reason,selected);
            }else reason="주문 다음 지정 봉 시가 대기";
            result.Transitions.Add(new(row.ClosedAt,state,reason));result.Decisions.Add(new(date,state,reason,indicator,selected,detector.ReboundLow));result.Equity.Add(new(row.ClosedAt,result.Cash+position.Quantity*lastClose));
        }
        result.Quantity=position.Quantity;result.PendingOrderAtEnd=pending!=null;if(pending!=null)result.Notices.Add("종료 시점 신호는 다음 완성봉이 없어 미체결로 남겼습니다.");
        var minutesPerBar=checked(specification.Value*(specification.Unit==BarUnit.Hour?60:1));var periods=Math.Max(1,390/minutesPerBar)*c.AnnualTradingDays;result.Performance=PerformanceCalculator.Calculate(c.InitialCapital,result.Equity,result.Trades,c.AnnualRiskFreePercent,periods,active[0].OpenTime);return result;
    }
    private sealed record MinutePending(OversoldSignal Signal,bool Buy,int EntryNumber,string[] ExitLegs,DateOnly TriggerDate);
    private sealed class MinutePosition
    {public int Quantity,Entries,PeakQuantity,HarvestBasis,FirstDay;public decimal Average,RemainingCost,LastEntry,Peak;public bool Tp1,Tp2,HarvestStarted;public OversoldTrade? Trade;}
}
