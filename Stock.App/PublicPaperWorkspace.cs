using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private async Task StartPublicPaperMulti()
    {
        if(MultiSessionExists)throw new InvalidOperationException("이전 실행을 전체 중지한 뒤 시작하세요.");
        if(provider is not ICurrentMarketData quotes)throw new InvalidOperationException("공개 현재가·일봉 공급자가 없습니다.");
        var now=multiClock();if(!LiveBatchSupport.RegularSession(now))throw new InvalidOperationException("공개시세 모의매매는 국내 정규장 09:00~15:20에 시작할 수 있습니다.");
        var today=DailyQuoteMerger.Date(now,Exchange.KRX);var spec=new BarSpecification(1,BarUnit.Day);
        var plan=MultiTrading.CreatePlan(decimal.Parse(MultiTrading.TotalBudget.Replace(",",""),System.Globalization.CultureInfo.InvariantCulture));
        if(plan.Items.Length is <1 or >20)throw new InvalidOperationException("실행할 국내 종목을 1~20개 선택하세요.");
        var prepared=new List<LiveBatchItem>();
        foreach(var item in plan.Items)
        {
            var security=await quotes.CurrentSecurity(item.Stock,ApiToken);
            if(security is {Suspended:true} or {Administrative:true}||security?.Kind is HistoricalSecurityKind.Etf or HistoricalSecurityKind.Etn or HistoricalSecurityKind.Spac or HistoricalSecurityKind.Other)throw new InvalidOperationException(item.Stock.Name+": 제외 종목 또는 거래정지 상태입니다.");
            var source=await PublicStrategyHistory(item.Stock,spec,today.AddDays(-14),today,ApiToken);
            var facts=PublicPaperFacts(item.Stock,source,today);var warmup=PublicPaperBars(source,spec,facts,now);
            if(warmup.Length==0)throw new InvalidOperationException(item.Stock.Name+": 공개 완성 분봉이 없습니다.");
            // The paper profile explicitly records its data assumptions. These records never enter
            // historical facts or real-account plans. Exact KRX traded notional is unavailable here.
            var settings=item.Settings;settings.Execution.UseSpread=false;settings.Execution.TimeoutMilliseconds=60000;
            settings.Advanced!.Execution.OrderTimeoutMilliseconds=60000;
            prepared.Add(item with{Oversold=new(LoadMultiOversoldPreset(item.Preset),spec,warmup,facts,PublicPaper:true)});
        }
        if(!IsOffline||!MultiTrading.UseMock)throw new InvalidOperationException("실행 준비 중 모드가 변경되었습니다.");
        plan=plan with{Items=prepared.ToArray()};var active=new RealtimePaperStrategyBatch(store,multiClock);realtimePaperBatch=active;liveBatch=null;
        active.FillRecorded+=_=>dispatcher.BeginInvoke(()=>{if(!disposed&&ReferenceEquals(active,realtimePaperBatch))ReloadTradeMarkers();});
        active.Start(plan);multiPaperActive=true;ShowTradeMarkers=true;multiStop=new();var token=multiStop.Token;
        MultiStatus="공개시세 실시간 모의 실행 중 · 분봉 거래대금/종목상태 가정 포함";Changed(nameof(MultiStatus));MultiTrading.Apply(active.Snapshot());
        multiTask=Task.Run(async()=>
        {
            var pumps=new List<SelectedQuotePump>();
            try
            {
                foreach(var item in prepared)
                {
                    var pump=new SelectedQuotePump(quotes,fast:true);pumps.Add(pump);
                    pump.Select(item.Stock,s=>{active.OnPublicSnapshot(s);_=dispatcher.BeginInvoke(()=>{if(disposed||token.IsCancellationRequested)return;if(dataset?.Stock.Key==s.Stock.Key){pendingDaily=s;RefreshDailyDisplay();}});},message=>active.FeedFault(message));
                }
                var nextBars=now.AddMinutes(1);
                while(!token.IsCancellationRequested)
                {
                    var time=multiClock();
                    if(time>=nextBars)
                    {
                        foreach(var item in prepared)
                        {
                            var raw=await PublicStrategyHistory(item.Stock,spec,today,today,token);
                            foreach(var bar in PublicPaperBars(raw,spec,item.Oversold!.Facts,time))active.OnPublicBar(item.Stock.Key,bar);
                        }
                        nextBars=time.AddMinutes(1);
                    }
                    await active.Pulse();var snapshot=active.Snapshot();
                    _=dispatcher.BeginInvoke(()=>{if(disposed||!ReferenceEquals(active,realtimePaperBatch))return;MultiTrading.Apply(snapshot);MultiStatus="공개시세 모의 · "+snapshot.Status;Changed(nameof(MultiStatus));ReloadTradeMarkers();Refresh();});
                    if(!snapshot.Running)break;
                    await Task.Delay(500,token);
                }
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            catch{active.FeedFault("공개 분봉/시세 조회 실패 · 가상 주문 중지");}
            finally{foreach(var pump in pumps)pump.Stop();try{await Task.WhenAll(pumps.Select(p=>p.Completion));}catch(OperationCanceledException){}active.Stop();_=dispatcher.BeginInvoke(()=>{if(disposed||!ReferenceEquals(active,realtimePaperBatch))return;MultiTrading.Apply(active.Snapshot());MultiStatus="공개시세 모의 중지 · 보유분 유지";Changed(nameof(MultiStatus));Refresh();});}
        });Refresh();
    }
    private HistoricalFacts PublicPaperFacts(StockItem stock,PriceBar[] bars,DateOnly today)
    {
        var dates=bars.Select(b=>DailyQuoteMerger.Date(b.Time,Exchange.KRX)).Append(today).Distinct().Order().ToArray();
        var sessions=dates.Select(d=>new DailyMarketSession(d,new(d.ToDateTime(new(9,0)),TimeSpan.FromHours(9)),new(d.ToDateTime(new(15,30)),TimeSpan.FromHours(9)))).ToArray();
        var result=new HistoricalFacts{Source="공개시세 모의 전용 가정: KRX 정규장/일반주식/관리 아님, 수동 악재 제외 적용, 분봉 거래대금 OHLC평균×거래량 추정",MarketSessions=sessions,
            StatusRecords=dates.Select(d=>new DailySecurityStatus(stock.Symbol,d,new(d.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)),stock.Board=="KOSDAQ"?HistoricalBoard.Kosdaq:HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false)).ToArray(),
            RiskRecords=dates.Select(d=>new FundamentalRiskRecord(stock.Symbol,d,store.State<bool>("exclude:"+stock.Key),new(d.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9)))).ToArray()};
        result.Validate();return result;
    }
    private static StrategyBar[] PublicPaperBars(PriceBar[] raw,BarSpecification spec,HistoricalFacts facts,DateTimeOffset asOf)
    {
        if(spec.Unit==BarUnit.Day)return raw.Where(b=>b.IsComplete&&b.Time<=asOf).Select((b,i)=>new StrategyBar(b,new DateTimeOffset(DailyDates.Of(b).ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),b.Time,i+1,(b.Open+b.High+b.Low+b.Close)/4*b.Volume,"네이버 KRX 공개 일봉 · 거래대금 추정 · 모의 전용")).ToArray();
        var minutes=raw.Select((b,i)=>new StrategyBar(new(b.Time.AddMinutes(1),b.Open,b.High,b.Low,b.Close,b.Volume,b.IsComplete&&b.Time.AddMinutes(1)<=asOf),b.Time,b.Time.AddMinutes(1),i+1,(b.Open+b.High+b.Low+b.Close)/4*b.Volume,"네이버 KRX 공개 분봉 · 거래대금 추정 · 모의 전용")).ToArray();
        return StrategyBarAggregator.FromMinutes(minutes,spec,facts.Sessions,asOf).Bars;
    }
    /// <summary>Offline analysis always uses completed daily candles; unfinished quotes stay display-only.</summary>
    private async Task<PriceBar[]> PublicStrategyHistory(StockItem stock,BarSpecification spec,DateOnly from,DateOnly to,CancellationToken token)
    {
        if(spec.Unit!=BarUnit.Day&&IsOnline&&provider is IPublicMinuteMarketData minutes)return await minutes.MinuteHistory(stock,from,to,token);
        var cached=store.State<BarDataset>("bars:"+stock.Key);
        if(cached==null||multiClock()-cached.RetrievedAt>TimeSpan.FromMinutes(1))
        {cached=await provider.History(stock,token);store.State("bars:"+stock.Key,cached);}
        return cached.Bars;
    }
}
