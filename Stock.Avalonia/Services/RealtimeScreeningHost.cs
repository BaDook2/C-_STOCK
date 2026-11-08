using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed record RealtimeItemRequest(StockItem Stock,string Name,OversoldConfig Configuration,decimal Budget);
public sealed record RealtimeRequest(StockItem[] Stocks,string Name,OversoldConfig Configuration,BarSpecification Specification,HistoricalFacts Facts,bool PaperAssumptions,decimal TotalBudget,decimal DailyLossLimit)
{public RealtimeItemRequest[]? ItemConfigurations {get;init;}}
/// <summary>
/// Application-lifetime feed coordinator. The existing detector and paper matcher own all decisions.
/// REST observations are explicitly an approximate paper model, never raw ticks or live broker orders.
/// </summary>
public sealed class RealtimeScreeningHost(AppServices services,MarketHistoryService history,Func<IAccountBroker?> account):IAsyncDisposable
{
    private readonly RealtimeOversoldScreeningService research=new(services.Clock);
    private readonly RealtimePaperStrategyBatch paper=new(services.Store,services.Clock);
    private CancellationTokenSource? stop;
    private Task loop=Task.CompletedTask;
    private RealtimeRequest? request;
    private LiveBatchItem[] prepared=[];
    private bool paperStarted;
    private string error="";
    private RealtimeScreeningStatusRow[] preparationFailures=[];
    public RealtimeScreeningSnapshot Snapshot {get{var snapshot=research.Snapshot();return snapshot with{Rows=[..snapshot.Rows,..preparationFailures]};}}
    public LiveBatchStatus PaperSnapshot=>paper.Snapshot();
    public bool HasProtectedPositions=>paper.Snapshot().Rows.Any(r=>r.Owned>0||r.Pending>0);
    public bool Active=>!loop.IsCompleted;
    public bool RestoreAvailable=>paper.RestoreAvailable;
    public LiveBatchPlan? RecoveredPlan=>paper.RestoredPlan;
    public string PaperRunId=>paper.RunId;
    public string Error=>error;
    public event Action<string>? Progress;
    public event Action<TradeMarker>? FillRecorded {add=>paper.FillRecorded+=value;remove=>paper.FillRecorded-=value;}
    public RealtimeScreeningChartData ChartData(string key){var data=research.ChartData(key);return data with{StrategyName=prepared.FirstOrDefault(i=>i.Stock.Key==key)?.Preset??data.StrategyName};}
    public async Task Start(RealtimeRequest value,CancellationToken token)
    {
        if(Active||HasProtectedPositions)throw new InvalidOperationException("기존 실시간 감시/가상 보유분을 먼저 확인하세요. 기존 상태를 새 실행으로 덮어쓰지 않습니다.");
        if(!LiveBatchSupport.RegularSession(services.Clock()))throw new InvalidOperationException("현재는 장 외 시간입니다. 국내 정규 연속매매 구간(09:00~15:20)에 시작하세요.");
        value.Configuration.Validate();value.Specification.Validate();value.Facts.Validate();
        if(value.Stocks.Length is <1 or >100||value.Stocks.Any(s=>s.Exchange!=Exchange.KRX))throw new InvalidOperationException("실시간 감시는 중복 없는 국내 일반주식1~100종목을 지원합니다.");
        if(value.TotalBudget<=0||value.TotalBudget>1e12m||value.DailyLossLimit<=0||value.DailyLossLimit>value.TotalBudget)throw new InvalidOperationException("전체 가상 투자 한도와 일 손실 한도를 확인하세요.");
        var overrides=value.ItemConfigurations?.Select(i=>i with{Configuration=i.Configuration.Clone()}).ToArray();if(overrides!=null){if(overrides.Length!=value.Stocks.Length||overrides.Select(i=>i.Stock.Key).Distinct().Count()!=overrides.Length||overrides.Any(i=>!value.Stocks.Any(s=>s.Key==i.Stock.Key)||i.Budget<=0||i.Configuration.PlannedInvestment>i.Budget)||overrides.Sum(i=>i.Budget)>value.TotalBudget)throw new InvalidOperationException("종목별 전략·예산과 전체 가상 투자 한도를 확인하세요.");foreach(var item in overrides)item.Configuration.Validate();}
        var quotes=QuoteSource();var firstQuote=await quotes.Current(value.Stocks[0],token);if(firstQuote.Quote.State!="OPEN")throw new InvalidOperationException("현재는 장 외 시간 또는 시세 지연 상태입니다. 제공처의 장중 시세를 확인한 뒤 시작하세요.");
        request=value with{Stocks=value.Stocks.DistinctBy(s=>s.Key).ToArray(),Configuration=value.Configuration.Clone(),Facts=value.Facts.Snapshot()};paperStarted=false;error="";var now=services.Clock();var today=DailyQuoteMerger.Date(now,Exchange.KRX);
        preparationFailures=[];using var gate=new SemaphoreSlim(4);var completed=0;
        var pending=request.Stocks.Select(async stock=>
        {
            await gate.WaitAsync(token);try
            {
                var requested=overrides?.First(i=>i.Stock.Key==stock.Key);var config=requested?.Configuration.Clone()??request.Configuration;var data=await history.Get(stock,config,request.Facts,request.Specification.Unit==BarUnit.Day?null:request.Specification,today.AddDays(-7),today,token);
                var facts=PrepareFacts(stock,data,request.Facts,request.PaperAssumptions,today);var bars=ToBars(data,facts,now);
                if(bars.Length==0)throw new InvalidOperationException(stock.Name+": 완성된 준비 봉이 없습니다.");
                if(bars.Length>20000)throw new InvalidOperationException(stock.Name+": 준비 봉이20,000개를 넘습니다. 더 큰 봉 기준을 선택하세요.");
                var budget=requested?.Budget??config.PlannedInvestment;var settings=PaperSettings(config,budget);return(Item:(LiveBatchItem?)new LiveBatchItem(stock,requested?.Name??request.Name,settings,budget){Oversold=new(config.Clone(),request.Specification,bars,facts,ResearchOnly:true,PublicPaper:true)},Stock:stock,Error:"");
            }
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch(StorageException){throw;}
            catch(Exception ex) when(ex is not OutOfMemoryException){return(Item:(LiveBatchItem?)null,Stock:stock,Error:ex is OperationCanceledException or TimeoutException?OperationError.ResponseTimeout:ex is InvalidOperationException?ex.Message:"과거 자료 준비 실패");}
            finally{gate.Release();var count=Interlocked.Increment(ref completed);Progress?.Invoke($"실시간 자료 준비 {count:N0}/{request.Stocks.Length:N0} · {stock.Name}");}
        }).ToArray();
        // Await every preparation before releasing its gate, including failed/cancelled runs.
        var downloaded=await Task.WhenAll(pending);token.ThrowIfCancellationRequested();var items=downloaded.Where(r=>r.Item!=null).Select(r=>r.Item!).ToArray();preparationFailures=downloaded.Where(r=>r.Item==null).Select(r=>new RealtimeScreeningStatusRow(r.Stock.Key,r.Stock.Name,r.Stock.Symbol,null,null,0,0,0,r.Error,"자료 준비 실패",null,"자료 부족",r.Error,false,false)).ToArray();
        if(items.Length==0)throw new InvalidOperationException("모든 종목의 준비 자료가 부족합니다. "+string.Join(" / ",downloaded.Select(r=>r.Stock.Name+": "+r.Error)));prepared=items;request=request with{Stocks=items.Select(i=>i.Stock).ToArray()};
        await Task.Run(()=>research.Start(new(items,request.Name,request.Specification)),token);research.SetConnectionState(account()==null?"공개 관측시세 연결 중":"연결 API 관측시세 · 원본 체결 아님");
        stop?.Dispose();stop=new();var feedToken=stop.Token;loop=Task.Run(()=>RunFeed(quotes,request,today,feedToken),feedToken);
    }
    public void SetAutomatic(bool enabled)
    {
        if(enabled)
        {
            if(!research.Running||request==null)throw new InvalidOperationException("먼저 실시간 스크리닝을 시작하세요.");
            if(!paperStarted)
            {
                if(prepared.Length>20)throw new InvalidOperationException("기존 가상 체결 엔진의 동시 실행 한도는20종목입니다. 감시는100종목까지 가능하며 자동매매는 대상을 줄여 시작하세요.");
                var items=prepared.Select(i=>i with{Oversold=i.Oversold! with{Warmup=research.ChartData(i.Stock.Key).Bars}}).ToArray();
                paper.Start(new(items,request.TotalBudget,request.DailyLossLimit));paperStarted=true;
            }
        }
        paper.SetEntryEnabled(enabled);research.SetAutoTrading(enabled);
    }
    public void StopResearch()
    {research.SetAutoTrading(false);paper.SetEntryEnabled(false);research.Stop("감시 중지 · 기존 가상 보유/미체결 보호는 유지");if(!HasProtectedPositions)stop?.Cancel();}
    public void Emergency(string reason="긴급중지")
    {research.Stop(reason+" · 보유분 임의청산 없음");try{paper.RequestStop(reason:reason+" · 가상 주문 중지 · 보유분 임의청산 없음");}catch(Exception ex) when(ex is not OutOfMemoryException){error=reason+"는 적용됐지만 가상 상태 보관에 실패했습니다: "+ex.GetType().Name;}finally{stop?.Cancel();}}
    private ICurrentMarketData QuoteSource()=>account() is {} broker?new BrokerCurrentMarketData(broker,_=>null):services.MarketData as ICurrentMarketData??throw new InvalidOperationException("현재가 자료 공급자가 없습니다.");
    public void RestorePaper()
    {
        if(Active||HasProtectedPositions)throw new InvalidOperationException("실행 중인 감시/보유를 먼저 확인하세요.");paper.RestoreLatest();prepared=paper.CurrentPlan()!.Items;paperStarted=true;
        var first=prepared[0];request=new(prepared.Select(i=>i.Stock).ToArray(),"보관된 가상 실행",first.Oversold!.Configuration,first.Oversold.BarSpecification,first.Oversold.Facts,false,paper.RestoredPlan!.TotalBudget,paper.RestoredPlan.DailyLossLimit);
    }
    public async Task ResumePaper(bool assumptions,CancellationToken token)
    {
        if(Active||request==null||!paperStarted)throw new InvalidOperationException("중지 상태로 복원한 가상 계좌가 필요합니다.");if(!LiveBatchSupport.RegularSession(services.Clock()))throw new InvalidOperationException("장 외 시간입니다. 장중에 보호 감시를 재개하세요.");
        var quotes=QuoteSource();var first=await quotes.Current(prepared[0].Stock,token);if(first.Quote.State!="OPEN"||(services.Clock()-first.Quote.TradeTime).Duration()>TimeSpan.FromSeconds(20))throw new InvalidOperationException("신선한 장중 시세를 확인하지 못했습니다.");
        request=request with{PaperAssumptions=assumptions};await RollPublicDay(DailyQuoteMerger.Date(services.Clock(),Exchange.KRX),token);paper.ResumeProtection();
        stop?.Dispose();stop=new();var feedToken=stop.Token;loop=Task.Run(()=>RunFeed(quotes,request,DailyQuoteMerger.Date(services.Clock(),Exchange.KRX),feedToken),feedToken);
    }
    private async Task RollPublicDay(DateOnly day,CancellationToken token)
    {
        var updated=new List<LiveBatchItem>();
        foreach(var item in paperStarted?paper.CurrentPlan()!.Items:prepared)
        {
            var old=item.Oversold!;var data=new PreparedResearchData(new(item.Stock,old.Warmup.Select(b=>b.Price).ToArray(),"보관된 전략 판단 봉",services.Clock()),old.BarSpecification,old.Warmup,old.Facts);
            var facts=PrepareFacts(item.Stock,data,old.Facts,request!.PaperAssumptions,day);if(paperStarted)paper.ExtendPublicFacts(item.Stock.Key,facts);
            updated.Add(item with{Oversold=old with{Facts=facts}});
        }
        prepared=updated.ToArray();research.Stop("거래일 갱신 · 기존 가상 포지션 유지");await Task.Run(()=>research.Start(new(prepared,request!.Name,request.Specification)),token);research.SetAutoTrading(paperStarted&&paper.EntryEnabled);
    }
    private async Task RunFeed(ICurrentMarketData quotes,RealtimeRequest plan,DateOnly date,CancellationToken token)
    {
        var nextQuotes=DateTimeOffset.MinValue;var nextBars=services.Clock().AddMinutes(1);var failures=0;var lastPaperPulse=DateTimeOffset.MinValue;
        var quoteTimes=new Dictionary<string,DateTimeOffset>();var barTimes=prepared.ToDictionary(i=>i.Stock.Key,i=>i.Oversold!.Warmup.Last().ClosedAt);
        try
        {
            while(!token.IsCancellationRequested&&(research.Running||paperStarted&&paper.Snapshot().Running||HasProtectedPositions))
            {
                var now=services.Clock();var changed=false;if(DailyQuoteMerger.Date(now,Exchange.KRX)!=date){if(plan.Specification.Unit!=BarUnit.Day){research.FeedFault("거래일 변경 · 자료를 다시 준비하세요.");paper.FeedFault("거래일 변경 · 상태 확인 필요");break;}if(!LiveBatchSupport.RegularSession(now)){research.SetConnectionState("장 외 대기 · 일봉 상태 유지");await Task.Delay(1000,token);continue;}var opening=await quotes.Current(plan.Stocks[0],token);if(opening.Quote.State!="OPEN"){research.SetConnectionState("장중 시세 확인 대기 · 새 주문 차단");await Task.Delay(1000,token);continue;}date=DailyQuoteMerger.Date(now,Exchange.KRX);await RollPublicDay(date,token);nextQuotes=DateTimeOffset.MinValue;nextBars=now;quoteTimes.Clear();}
                if(now>=nextQuotes)
                {
                    var intervals=new List<TimeSpan>();var successes=0;
                    foreach(var group in plan.Stocks.Chunk(20))
                    {
                        token.ThrowIfCancellationRequested();try
                        {
                            CurrentMarketSnapshot[] data;
                            if(account()==null&&services.MarketData is IFastPublicCurrentBatchData fast)data=await fast.CurrentBatchFast(group,token);
                            else if(account()==null&&services.MarketData is IPublicCurrentBatchData batch)data=await batch.CurrentBatch(group,token);
                            else{var values=new List<CurrentMarketSnapshot>();foreach(var stock in group)try{values.Add(await quotes.Current(stock,token));}catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch(Exception ex) when(ex is not OutOfMemoryException){failures++;}data=values.ToArray();}
                            foreach(var item in data){if(item.Quote.TradeTime>quoteTimes.GetValueOrDefault(item.Stock.Key)){quoteTimes[item.Stock.Key]=item.Quote.TradeTime;changed=true;}research.OnPublicSnapshot(item);if(paperStarted)paper.OnPublicSnapshot(item);intervals.Add(item.PollInterval);successes++;}
                        }
                        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch(Exception ex) when(ex is not OutOfMemoryException){failures++;}
                    }
                    if(successes==0&&failures>=3)throw new InvalidOperationException("현재가 조회가 반복 실패했습니다. 신규 진입을 차단합니다.");
                    if(successes>0)failures=0;research.SetConnectionState(successes==plan.Stocks.Length?"관측시세 수신 · 지연 가능 · 원본 체결 아님":successes==0?"시세 미수신 · 신규 진입 차단 · 재시도 대기":"일부 시세 누락 · 마지막 수신시각 확인");var interval=intervals.DefaultIfEmpty(TimeSpan.FromSeconds(7)).Min();nextQuotes=now+(interval<TimeSpan.FromSeconds(1)?TimeSpan.FromSeconds(1):interval);
                }
                if(now>=nextBars)
                {
                    foreach(var item in prepared)
                    {
                        token.ThrowIfCancellationRequested();try
                        {
                            PreparedResearchData data;if(plan.Specification.Unit==BarUnit.Day){var daily=await services.MarketData.History(item.Stock,token);services.Store.State("bars:"+item.Stock.Key,daily);data=new(daily,null,null,item.Oversold!.Facts);}else data=await history.Get(item.Stock,item.Oversold!.Configuration,item.Oversold.Facts,plan.Specification,date,date,token);
                            foreach(var bar in ToBars(data,item.Oversold!.Facts,now).Where(b=>b.ClosedAt>barTimes[item.Stock.Key])){research.OnPublicBar(item.Stock.Key,bar);if(paperStarted)paper.OnPublicBar(item.Stock.Key,bar);barTimes[item.Stock.Key]=bar.ClosedAt;changed=true;}
                        }
                        catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch(Exception ex) when(ex is not OutOfMemoryException){error=item.Stock.Name+": "+OperationError.Describe(ex);}
                    }
                    nextBars=now.AddMinutes(1);
                }
                research.Pulse();if(paperStarted&&(changed||!LiveBatchSupport.RegularSession(now)||now-lastPaperPulse>=TimeSpan.FromSeconds(20))){await paper.Pulse();lastPaperPulse=now;}await Task.Delay(250,token);
            }
        }
        catch(OperationCanceledException)when(token.IsCancellationRequested){}
        catch(Exception ex) when(ex is not OutOfMemoryException){error=ex is InvalidOperationException?ex.Message:"시세 감시 오류";research.FeedFault(error);try{paper.FeedFault(error);}catch(Exception storage) when(storage is not OutOfMemoryException){error+=" · 가상 상태 보관 실패: "+storage.GetType().Name;}}
    }
    private static StrategyBar[] ToBars(PreparedResearchData data,HistoricalFacts facts,DateTimeOffset now)
    {
        if(data.Bars!=null)return data.Bars.Where(b=>b.Price.IsComplete&&b.ClosedAt<=now).ToArray();
        return data.Dataset.CompletedBars.Select((bar,index)=>{var session=facts.Sessions.FirstOrDefault(s=>s.Date==DailyDates.Of(bar))??ResearchMarketSession.For(DailyDates.Of(bar),data.Dataset.Stock.Exchange);return new StrategyBar(bar with{Time=session.Close},session.Open,session.Close,index+1,facts.TradingValue(data.Dataset.Stock.Symbol,session.Date,session.Close)??(bar.Open+bar.High+bar.Low+bar.Close)/4*bar.Volume,data.Dataset.Source+" · 거래대금 OHLC 평균×량 추정");}).Where(b=>b.ClosedAt<=now).ToArray();
    }
    private static HistoricalFacts PrepareFacts(StockItem stock,PreparedResearchData data,HistoricalFacts source,bool assumptions,DateOnly today)
    {
        var result=source.Snapshot();var dates=data.Dataset.CompletedBars.Select(DailyDates.Of).Append(today).Distinct().ToArray();var sessions=result.Sessions.ToDictionary(s=>s.Date);foreach(var day in dates)if(!sessions.ContainsKey(day))sessions[day]=ResearchMarketSession.For(day,stock.Exchange);result.MarketSessions=sessions.Values.OrderBy(s=>s.Open).ToArray();
        if(assumptions)
        {
            result.StatusRecords=[..result.StatusRecords,..dates.Where(d=>!result.StatusRecords.Any(r=>r.Symbol==stock.Symbol&&r.Date==d)).Select(d=>new DailySecurityStatus(stock.Symbol,d,sessions[d].Open,stock.Board=="KOSDAQ"?HistoricalBoard.Kosdaq:HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false))];
            result.RiskRecords=[..result.RiskRecords,..dates.Where(d=>!result.RiskRecords.Any(r=>r.Symbol==stock.Symbol&&r.Date==d)).Select(d=>new FundamentalRiskRecord(stock.Symbol,d,false,sessions[d].Open))];
        }
        result.Source+=(assumptions?" · 사용자가 허용한 가상모형: 미확인 분류/위험 일반주식·악재없음 가정":" · 관측 봉의 정규장 시각, 분류/위험 미확인은 유지");result.Validate();return result;
    }
    internal static TradingSettings PaperSettings(OversoldConfig config,decimal budget,bool publicPaper=true)
    {
        // Same existing mapper and execution/risk shape as the WPF batch adapter.
        var advanced=new StrategyConfiguration{Enabled=true,StrategyType=StrategyType.Box}.Resolve();advanced.Common.Investment=config.PlannedInvestment;advanced.Common.TradingStart="09:00";advanced.Common.TradingEnd="15:20";advanced.Risk.MaxPositionPercent=100;advanced.Risk.MaxOpenPositions=20;advanced.Risk.DailyLossLimit=budget;advanced.Execution.OrderType=OrderKind.Limit;advanced.Execution.OrderTimeoutMilliseconds=20000;advanced.Execution.Commission=config.CommissionPercent;advanced.Execution.Tax=config.TaxPercent;
        var settings=new TradingSettings();settings.Risk.AccountCapital=budget;settings.Risk.MaxOrderCash=budget;settings.Risk.MaxSymbolCash=budget;settings.Risk.StopMode=DistanceMode.Percent;settings.Risk.StopValue=Math.Abs(config.EmergencyStopLossPercent);settings.Risk.MaxTradeLoss=budget*(Math.Abs(config.EmergencyStopLossPercent)+config.CommissionPercent)/100+1;settings.Risk.MaxTradeLossPercent=Math.Min(100,Math.Abs(config.EmergencyStopLossPercent)+config.CommissionPercent+1);settings.Risk.MaxDailyLoss=budget;settings.Execution.StaleSeconds=10;settings.Execution.Retry=false;settings.Execution.UseSpread=!publicPaper;settings.Risk.FlattenOnStop=false;settings.Filters.ForEach(f=>f.Enabled=false);return StrategyPresetMapper.ToEngine(advanced,settings);
    }
    public async ValueTask DisposeAsync(){Emergency();try{await loop.ConfigureAwait(false);}catch(OperationCanceledException){}stop?.Dispose();}
}
