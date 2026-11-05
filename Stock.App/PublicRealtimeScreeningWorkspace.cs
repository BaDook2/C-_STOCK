using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Keyless KRX research uses public completed candles and quote observations, never a broker feed or real order.</summary>
    private async Task StartPublicRealtimeScreening()
    {
        if(provider is not ICurrentMarketData quotes||provider is not IPublicMinuteMarketData minutes||provider is not IPublicCurrentBatchData batchQuotes)
            throw new InvalidOperationException("공개 현재가·분봉 공급자가 없습니다.");
        if(MultiSessionExists)throw new InvalidOperationException("기존 다량 전략 실행을 먼저 전체 중지하세요.");
        if(!ScreenCustomBars)throw new InvalidOperationException("공개시세 실시간 감시는 분봉 또는 시간봉을 선택하세요.");
        var specification=TimeBarSelection.Parse(ScreenBarValue,ScreenBarUnit);
        var strategyName=SelectedOversoldPreset??throw new InvalidOperationException("적용할 전략을 선택하세요.");
        var configuration=LoadMultiOversoldPreset(strategyName);
        StockItem[] targets;
        if(ScreenWatchlist)
        {
            targets=SelectedScreeningStocks().Take(100).ToArray();
            if(targets.Length==0)throw new InvalidOperationException("실시간 감시할 관심그룹 종목이 없습니다.");
        }
        else
        {
            if(Catalog.Count==0||catalogRegion!=ScreeningMarketSelection||catalogSize!=ScreeningSize)await LoadCatalog();
            targets=Catalog.Where(s=>MatchesScreeningMarket(s)&&(Sector=="전체"||s.Sector==Sector)).DistinctBy(s=>s.Key).Take(100).ToArray();
            if(targets.Length==0)throw new InvalidOperationException("선택 시장의 실시간 감시 종목이 없습니다.");
        }
        var now=multiClock();var today=DailyQuoteMerger.Date(now,Exchange.KRX);
        var prepared=new List<LiveBatchItem>();var failures=new List<RealtimeScreeningDisplayRow>();
        foreach(var group in targets.Chunk(4))
        {
            Token.ThrowIfCancellationRequested();
            RealtimeScreenInfo=$"공개시세 감시 준비 {prepared.Count+failures.Count:N0}/{targets.Length:N0} · 완성 분봉 확인 중";
            var downloads=await Task.WhenAll(group.Select(async stock=>
            {
                try
                {
                    var bars=await minutes.MinuteHistory(stock,today.AddDays(-14),today,Token);
                    DailySecurityStatus? status=null;
                    try{status=await quotes.CurrentSecurity(stock,Token);}catch(OperationCanceledException){throw;}catch{}
                    return(stock,Bars:bars,Status:status,Error:"");
                }
                catch(OperationCanceledException){throw;}
                catch(InvalidOperationException ex){return(stock,Bars:Array.Empty<PriceBar>(),Status:(DailySecurityStatus?)null,Error:ex.Message);}
                catch{return(stock,Bars:Array.Empty<PriceBar>(),Status:(DailySecurityStatus?)null,Error:"공개 분봉 조회 실패");}
            }));
            foreach(var download in downloads)
            {
                try
                {
                    if(download.Error!="")throw new InvalidOperationException(download.Error);
                    if(download.Status is {Suspended:true} or {Administrative:true}||download.Status?.Kind is HistoricalSecurityKind.Etf or HistoricalSecurityKind.Etn or HistoricalSecurityKind.Spac or HistoricalSecurityKind.Other)
                        throw new InvalidOperationException("거래정지 또는 제외 상품입니다.");
                    var facts=PublicPaperFacts(download.stock,download.Bars,today);
                    var warmup=PublicPaperBars(download.Bars,specification,facts,now);
                    if(warmup.Length==0)throw new InvalidOperationException("공개 완성 분봉이 없습니다.");
                    if(warmup.Length>20000)warmup=warmup.TakeLast(20000).ToArray();
                    var budget=Math.Max(configuration.PlannedInvestment,1);
                    var settings=BuildOversoldTradingSettings(configuration,budget,false);
                    prepared.Add(new(download.stock,strategyName,settings,budget)
                    {Oversold=new(configuration.Clone(),specification,warmup,facts,ResearchOnly:true,PublicPaper:true)});
                }
                catch(InvalidOperationException ex)
                {failures.Add(new(download.stock.Symbol,download.stock.Name,"—","—","0점","0/0",ex.Message,"자료 준비 실패","—","자료 부족","신호만",ex.Message));}
            }
        }
        if(prepared.Count==0)throw new InvalidOperationException("공개 분봉을 준비한 종목이 없습니다. 잠시 후 다시 시도하세요.");
        if(!IsOffline)throw new InvalidOperationException("실행 준비 중 계좌 모드가 변경되었습니다.");
        RealtimeScreening.Start(new(prepared.ToArray(),strategyName,specification));
        RealtimeScreening.SetConnectionState("공개 시세 연결 중");
        realtimeScreenAutoTrade=false;Changed(nameof(RealtimeAutoTrade));
        RealtimeScreeningRows.Clear();foreach(var failure in failures)RealtimeScreeningRows.Add(failure);
        RealtimeScreenInfo=$"공개시세 {prepared.Count:N0}종목 감시 · 신호 표시만 · 실제 체결/호가가 아니며 지연될 수 있습니다.";
        realtimeScreenStop=new();var token=realtimeScreenStop.Token;
        realtimeScreenTask=Task.Run(async() =>
        {
            var active=prepared.Select(i=>i.Stock).ToArray();
            var nextBars=now.AddMinutes(1);var nextQuotes=DateTimeOffset.MinValue;
            var snapshotFailures=0;
            try
            {
                while(!token.IsCancellationRequested)
                {
                    var time=multiClock();
                    if(DailyQuoteMerger.Date(time,Exchange.KRX)!=today)
                    {RealtimeScreening.Stop("거래일 변경 · 공개 분봉을 다시 준비해 시작하세요.");break;}
                    if(time>=nextBars)
                    {
                        foreach(var group in active.Chunk(4))
                        {
                            var rows=await Task.WhenAll(group.Select(async stock=>
                            {
                                try{return(stock,Bars:await minutes.MinuteHistory(stock,today,today,token));}
                                catch(OperationCanceledException){throw;}
                                catch{return(stock,Bars:Array.Empty<PriceBar>());}
                            }));
                            foreach(var row in rows)
                            {
                                var item=prepared.First(i=>i.Stock.Key==row.stock.Key);
                                foreach(var bar in PublicPaperBars(row.Bars,specification,item.Oversold!.Facts,time))RealtimeScreening.OnPublicBar(row.stock.Key,bar);
                            }
                        }
                        nextBars=time.AddMinutes(1);
                    }
                    if(time>=nextQuotes)
                    {
                        var intervals=new List<TimeSpan>();var successes=0;
                        foreach(var group in active.Chunk(20))
                        {
                            try
                            {
                                var snapshots=await batchQuotes.CurrentBatch(group,token);
                                foreach(var quote in snapshots){RealtimeScreening.OnPublicSnapshot(quote);intervals.Add(quote.PollInterval);successes++;}
                            }
                            catch(OperationCanceledException){throw;}
                            catch{snapshotFailures++;}
                        }
                        if(successes==0&&snapshotFailures>=3)
                        {RealtimeScreening.FeedFault("공개 현재가 조회가 반복 실패했습니다. 인터넷 연결을 확인하세요.");break;}
                        if(successes>0)snapshotFailures=0;
                        RealtimeScreening.SetConnectionState(successes==active.Length?"공개 시세 수신 · 지연 가능":"공개 시세 일부 조회 실패");
                        nextQuotes=multiClock()+(intervals.Count==0?TimeSpan.FromSeconds(7):intervals.Min());
                    }
                    RealtimeScreening.Pulse();
                    var snapshot=RealtimeScreening.Snapshot();
                    _=dispatcher.BeginInvoke(()=>ApplyRealtimeSnapshot(snapshot,new Dictionary<string,LiveBatchRowStatus>(),failures));
                    if(!snapshot.Running)break;
                    await Task.Delay(500,token);
                }
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){}
            catch{RealtimeScreening.FeedFault("공개시세 감시가 중단됐습니다. 인터넷 연결을 확인하세요.");}
            finally
            {
                var snapshot=RealtimeScreening.Snapshot();
                _=dispatcher.BeginInvoke(()=>{if(!disposed){ApplyRealtimeSnapshot(snapshot,new Dictionary<string,LiveBatchRowStatus>(),failures);NotifyRealtimeState();}});
            }
        });
        NotifyRealtimeState();
    }
}
