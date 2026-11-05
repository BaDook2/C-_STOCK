using System.Collections.ObjectModel;
using System.IO;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.App;

public sealed class RealtimeScreeningDisplayRow(
    string Code,string Name,string Price,string Change,string Proximity,string Conditions,
    string Unmet,string StrategyState,string LastAnalysis,string DataState,string OrderState,string Error):Observable
{
    public string Code {get;}=Code;
    public string Name {get;}=Name;
    private string[] values=[Price,Change,Proximity,Conditions,Unmet,StrategyState,LastAnalysis,DataState,OrderState,Error];
    public string Price=>values[0]; public string Change=>values[1]; public string Proximity=>values[2];
    public string Conditions=>values[3]; public string Unmet=>values[4]; public string StrategyState=>values[5];
    public string LastAnalysis=>values[6]; public string DataState=>values[7]; public string OrderState=>values[8]; public string Error=>values[9];
    public void Update(RealtimeScreeningDisplayRow row)
    {if(!values.SequenceEqual(row.values)){values=row.values;Changed(null);}}
}

public sealed partial class WorkspaceViewModel
{
    private RealtimeOversoldScreeningService? realtimeScreening;
    private readonly List<RealtimePaperStrategyBatch> realtimeScreenPapers=[];
    private CancellationTokenSource? realtimeScreenStop;
    private Task? realtimeScreenTask;
    private bool realtimeScreenMode,realtimeScreenAutoTrade;
    private string realtimeScreenInfo="실시간 모드를 선택하면 현재 전략 조건에 가까워지는 종목을 계속 감시합니다.";
    private string realtimeScreenConnection="중지";
    private bool realtimeScreenStrategyChanged;
    private CancellationTokenSource? realtimeStrategyChange;
    private long realtimeStrategyChangeId;
    public bool RealtimeScreeningStrategyChanging {get;private set;}
    private RealtimeScreeningDisplayRow? selectedRealtimeScreeningRow;

    public ObservableCollection<RealtimeScreeningDisplayRow> RealtimeScreeningRows {get;}=[];
    public RealtimeScreeningDisplayRow? SelectedRealtimeScreeningRow {get=>selectedRealtimeScreeningRow;set{selectedRealtimeScreeningRow=value;Changed();}}
    public bool RegularScreeningMode {get=>!realtimeScreenMode;set{if(value)RealtimeScreeningMode=false;}}
    public bool RealtimeScreeningMode
    {
        get=>realtimeScreenMode;
        set
        {
            if(realtimeScreenMode==value)return;
            if(RealtimeScreeningRunning||RealtimeScreeningProtectionActive){Changed();return;}
            realtimeScreenMode=value;
            if(value&&ScreenDaily)ScreenCustomBars=true;
            Changed();Changed(nameof(RegularScreeningMode));Changed(nameof(ShowScreenCustomBars));Changed(nameof(ScreenDaily));Changed(nameof(ScreenCustomBars));NotifyRealtimeState();
        }
    }
    public bool RealtimeAutoTrade
    {
        get=>realtimeScreenAutoTrade;
        set
        {
            if(realtimeScreenAutoTrade==value)return;
            if(value&&realtimeScreenStrategyChanged){alert("전략을 변경한 뒤에는 실시간 스크리닝을 중지하고 다시 시작해야 새 전략의 가상 자동매매를 켤 수 있습니다. 기존 보유분은 이전 전략으로 계속 보호합니다.");Changed();return;}
            if(value&&!RealtimeMockSupported)
            {alert("자동매매는 키움 국내 실시간 체결·호가 연결이 필요합니다. 기본 계좌는 가상 계좌이며 실제 주문은 전송하지 않습니다.");Changed();return;}
            realtimeScreenAutoTrade=value;
            realtimeScreening?.SetAutoTrading(value);
            foreach(var paper in realtimeScreenPapers)paper.SetEntryEnabled(value);
            RealtimeScreenInfo=value?"자동매매 활성 · 지금 이후 새 신호만 가상 주문으로 전달합니다.":"자동매매 꺼짐 · 신규/추가매수 차단, 기존 가상 보유 보호는 유지합니다.";
            Changed();
        }
    }
    public bool RealtimeScreeningRunning=>realtimeScreening?.Snapshot().Running==true;
    public bool RealtimeScreeningProtectionActive=>realtimeScreenTask is {IsCompleted:false}&&!RealtimeScreeningRunning;
    public bool CanStartRealtimeScreening=>RealtimeScreeningMode&&!RealtimeScreeningRunning&&!RealtimeScreeningProtectionActive&&CanConfigure&&ScreenCustomBars&&(IsOffline&&provider is ICurrentMarketData&&provider is IPublicMinuteMarketData&&provider is IPublicCurrentBatchData||RealtimeMockSupported);
    public bool CanAutoTradeRealtimeScreening=>RealtimeMockSupported;
    public bool CanStopRealtimeScreening=>RealtimeScreeningRunning||RealtimeScreeningProtectionActive;
    public string RealtimeScreenInfo {get=>realtimeScreenInfo;private set{realtimeScreenInfo=value;Changed();}}
    public string RealtimeScreenConnection {get=>realtimeScreenConnection;private set{realtimeScreenConnection=value;Changed();}}

    private RealtimeOversoldScreeningService RealtimeScreening
    {
        get
        {
            if(realtimeScreening!=null)return realtimeScreening;
            realtimeScreening=new(multiClock);
            realtimeScreening.SignalRaised+=signal=>dispatcher.BeginInvoke(()=>
            {
                if(disposed)return;
                var suffix=signal.AutoTradingEligible?" · 가상 자동매매 전달":" · 신호 표시만";
                Log($"실시간 스크리닝 신호 · {signal.Name} · {signal.Reason}{suffix}");
                Status=$"{signal.Name} 매수 신호 발생{suffix}";
            });
            return realtimeScreening;
        }
    }

    private void InitializeRealtimeScreening()
    {
        Actions["실시간스크리닝시작"]=new ActionCommand(()=>Run(StartRealtimeScreening),()=>CanStartRealtimeScreening);
        Actions["실시간스크리닝중지"]=new ActionCommand(()=>StopRealtimeScreening(false),()=>CanStopRealtimeScreening);
    }

    private async Task StartRealtimeScreening()
    {
        realtimeScreenStrategyChanged=false;
        if(realtimeScreenTask is {IsCompleted:true})
        {
            try{await realtimeScreenTask;}catch{}
            realtimeScreenTask=null;realtimeScreenStop?.Dispose();realtimeScreenStop=null;
            foreach(var oldPaper in realtimeScreenPapers)oldPaper.Stop();
            realtimeScreenPapers.Clear();
        }
        if(IsOffline){await StartPublicRealtimeScreening();return;}
        if(!RealtimeMockSupported||credentials==null||broker==null)throw new InvalidOperationException("온라인 투자에서 키움 국내 실시간 계좌를 먼저 연결하세요.");
        if(MultiSessionExists)throw new InvalidOperationException("기존 다량 전략 실행을 먼저 전체 중지하세요.");
        if(!ScreenCustomBars)throw new InvalidOperationException("실시간 스크리닝은 완성 시점을 확인할 수 있는 분봉 또는 시간봉을 선택하세요.");
        if(!LiveBatchSupport.RegularSession(multiClock()))throw new InvalidOperationException("실시간 스크리닝은 국내 정규 연속매매시간(09:00~15:20)에 시작할 수 있습니다.");
        var specification=TimeBarSelection.Parse(ScreenBarValue,ScreenBarUnit);
        var strategyName=SelectedOversoldPreset??throw new InvalidOperationException("적용할 전략을 선택하세요.");
        var config=LoadMultiOversoldPreset(strategyName);
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

        var now=multiClock();var today=DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(9)).DateTime);
        var baseFacts=oversoldFacts.Snapshot();
        if(!baseFacts.Sessions.Any(s=>s.Date==today))
        {
            baseFacts.MarketSessions=[..baseFacts.MarketSessions,new(today,new DateTimeOffset(today.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(today.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)))];
            if(string.IsNullOrWhiteSpace(baseFacts.Source))baseFacts.Source="키움 국내 정규장 실시간 감시";
            baseFacts.Validate();
        }
        var currentData=provider as ICurrentMarketData;
        var prepared=new List<LiveBatchItem>();var failures=new List<RealtimeScreeningDisplayRow>();int completed=0;
        foreach(var batch in targets.Chunk(4))
        {
            Token.ThrowIfCancellationRequested();
            RealtimeScreenInfo=$"실시간 감시 준비 {completed:N0}/{targets.Length:N0} · 과거 봉과 현재 상태 확인 중";
            var results=await Task.WhenAll(batch.Select(Prepare));
            foreach(var result in results)
            {
                completed++;
                if(result.Item!=null)prepared.Add(result.Item);
                else failures.Add(new(result.Stock.Symbol,result.Stock.Name,"—","—","0.0점","0/0",result.Error,"자료 준비 실패","—","자료 부족","자동매매 불가",result.Error));
            }
        }
        if(prepared.Count==0)throw new InvalidOperationException("실시간 감시에 필요한 준비 봉을 가져온 종목이 없습니다.");

        var plan=new RealtimeScreeningPlan(prepared.ToArray(),strategyName,specification);
        RealtimeScreening.Start(plan);
        realtimeScreenPapers.Clear();
        try
        {
            foreach(var chunk in prepared.Where(item=>LiveBatchSupport.Reason(item)==null
                &&HistoricalFacts.Tradable(item.Oversold!.Facts.Status(item.Stock.Symbol,today,now))
                &&item.Oversold.Facts.Risk(item.Stock.Symbol,today,now)==false).Chunk(20))
            {
                var items=chunk.ToArray();if(items.Length==0)continue;
                var total=items.Sum(i=>i.Budget);var paper=new RealtimePaperStrategyBatch(store,multiClock);
                paper.FillRecorded+=_=>dispatcher.BeginInvoke(()=>{if(!disposed){ReloadTradeMarkers();Refresh();}});
                paper.Start(new(items,total,Math.Max(1,total)));
                paper.SetEntryEnabled(realtimeScreenAutoTrade);
                realtimeScreenPapers.Add(paper);
            }
        }
        catch
        {foreach(var paper in realtimeScreenPapers)paper.Stop();realtimeScreenPapers.Clear();RealtimeScreening.Stop("가상 자동매매 준비 실패");throw;}
        if(realtimeScreenAutoTrade&&realtimeScreenPapers.Count==0)
        {
            realtimeScreenAutoTrade=false;RealtimeScreening.SetAutoTrading(false);Changed(nameof(RealtimeAutoTrade));
            alert("실시간 감시는 시작했지만 자동매매 가능한 종목이 없습니다. 오늘의 종목 상태·위험 확인과 비상 손절 설정을 확인하세요.");
        }
        else RealtimeScreening.SetAutoTrading(realtimeScreenAutoTrade);

        realtimeScreenStop=new();var token=realtimeScreenStop.Token;
        var feeds=new List<IMarketDataProvider>();
        try
        {
            foreach(var chunk in prepared.Select(i=>new Instrument(i.Stock.Symbol,i.Stock.Exchange)).Chunk(20))
            {
                Action<string> state=message=>
                {
                    RealtimeScreening.SetConnectionState(message);
                    if(message.Contains("단절")||message.Contains("오류")||message.Contains("실패"))
                    {RealtimeScreening.FeedFault(message);foreach(var paper in realtimeScreenPapers)paper.FeedFault(message);realtimeScreenStop?.Cancel();}
                };
                feeds.Add(multiFeedFactory?.Invoke(broker,chunk.ToArray(),prepared[0].Settings.Data,state)??new KiwoomMarketDataProvider(((KiwoomBroker)broker).GetTokenAsync,chunk.ToArray(),prepared[0].Settings.Data,state,live:true));
            }
        }
        catch
        {
            foreach(var feed in feeds)await feed.DisposeAsync();
            foreach(var paper in realtimeScreenPapers)paper.Stop();
            realtimeScreenPapers.Clear();RealtimeScreening.Stop("피드 준비 실패");realtimeScreenStop.Dispose();realtimeScreenStop=null;throw;
        }

        RealtimeScreeningRows.Clear();foreach(var failed in failures)RealtimeScreeningRows.Add(failed);
        RealtimeScreenInfo=$"{prepared.Count:N0}종목 감시 시작 · {failures.Count:N0}종목 자료 부족 · 접근도는 확률이 아닌 조건 거리입니다.";
        RealtimeScreenConnection="연결 중";NotifyRealtimeState();
        realtimeScreenTask=Task.Run(async()=>
        {
            async Task Read(IMarketDataProvider feed)
            {
                var events=new List<MarketEvent>();
                try
                {
                    await foreach(var e in feed.Stream(token))
                    {
                        RealtimeScreening.OnMarket(e);foreach(var paper in realtimeScreenPapers)paper.OnMarket(e);events.Add(e);
                        if(events.Count>=128){store.InsertEvents(events);events.Clear();}
                    }
                    if(!token.IsCancellationRequested)throw new IOException("실시간 시세 수신 종료");
                }
                catch(OperationCanceledException)when(token.IsCancellationRequested){}
                catch(Exception ex)
                {
                    RealtimeScreening.FeedFault(ex.Message);foreach(var paper in realtimeScreenPapers)paper.FeedFault(ex.Message);
                    realtimeScreenStop?.Cancel();
                }
                finally
                {
                    try{if(events.Count>0)store.InsertEvents(events);}catch{RealtimeScreening.FeedFault("원본 체결 저장 실패");}
                    await feed.DisposeAsync();
                }
            }
            async Task Poll()
            {
                using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(500));
                try
                {
                    while(await timer.WaitForNextTickAsync(token))
                    {
                        RealtimeScreening.Pulse();foreach(var paper in realtimeScreenPapers)await paper.Pulse();
                        var snapshot=RealtimeScreening.Snapshot();var paperRows=realtimeScreenPapers.SelectMany(p=>p.Snapshot().Rows).ToDictionary(r=>r.Key,StringComparer.Ordinal);
                        _=dispatcher.BeginInvoke(()=>ApplyRealtimeSnapshot(snapshot,paperRows,failures));
                        if(!snapshot.Running&&paperRows.Values.All(r=>r.Owned==0&&r.Pending==0)){realtimeScreenStop?.Cancel();break;}
                    }
                }
                catch(OperationCanceledException)when(token.IsCancellationRequested){}
            }
            await Task.WhenAll(feeds.Select(Read).Append(Poll()));
            _=dispatcher.BeginInvoke(()=>{RealtimeScreenConnection="중지";NotifyRealtimeState();});
        });
        NotifyRealtimeState();

        async Task<(StockItem Stock,LiveBatchItem? Item,string Error)> Prepare(StockItem stock)
        {
            try
            {
                var facts=baseFacts.Snapshot();DailySecurityStatus? currentStatus=null;
                if(currentData!=null)try{currentStatus=await currentData.CurrentSecurity(stock,Token);}catch(OperationCanceledException){throw;}catch{}
                if(currentStatus!=null)facts.StatusRecords=[..facts.StatusRecords.Where(s=>s.Symbol!=stock.Symbol||s.Date!=today),currentStatus];
                bool? risk=store.State<bool>("exclude:"+stock.Key)?true:store.State<string>("review:"+stock.Key)==KoreaClock.Today.ToString()?false:null;
                if(risk.HasValue)facts.RiskRecords=[..facts.RiskRecords.Where(r=>r.Symbol!=stock.Symbol||r.Date!=today),new(stock.Symbol,today,risk.Value,now)];
                facts.Validate();
                var warmup=await ScreeningBars(stock,specification,facts,now,Token);
                if(warmup.Length==0)throw new InvalidOperationException("검증된 준비 봉이 없습니다.");
                if(warmup.Length>20000)warmup=warmup.TakeLast(20000).ToArray();
                facts=LiveBatchSupport.WithObservedWarmupSessions(facts,warmup);
                var budget=Math.Max(config.PlannedInvestment,1);var settings=BuildOversoldTradingSettings(config,budget,false);
                var item=new LiveBatchItem(stock,strategyName,settings,budget){Oversold=new(config.Clone(),specification,warmup,facts)};
                return(stock,item,"");
            }
            catch(OperationCanceledException){throw;}
            catch(InvalidOperationException ex){return(stock,null,ex.Message);}
            catch{return(stock,null,"시세 또는 준비 자료 조회 실패");}
        }
    }

    private void ApplyRealtimeSnapshot(RealtimeScreeningSnapshot snapshot,IReadOnlyDictionary<string,LiveBatchRowStatus> orders,IReadOnlyList<RealtimeScreeningDisplayRow> failures)
    {
        if(disposed||RealtimeScreeningStrategyChanging||snapshot.Version!=realtimeScreening?.Version)return;
        var updated=new List<RealtimeScreeningDisplayRow>();
        foreach(var row in snapshot.Rows)
        {
            var order=orders.GetValueOrDefault(row.Key);
            var orderText=IsOffline?"공개시세 · 신호만":order==null?(snapshot.AutoTradingEnabled?"자동매매 불가 · 상태/위험 확인":"자동매매 꺼짐"):order.Status+(string.IsNullOrWhiteSpace(order.Error)?"":" · "+order.Error);
            updated.Add(new(row.Code,row.Name,row.Price is {} p?$"{DisplayNumber.Price(p)}원":"—",row.ChangePercent is {} c?$"{c:+0.00;-0.00;0.00}%":"—",$"{row.Proximity:N1}점",$"{row.MetConditions:N0}/{row.TotalConditions:N0}",row.UnmetConditions,row.StrategyState,row.LastAnalyzedAt?.ToOffset(TimeSpan.FromHours(9)).ToString("HH:mm:ss")??"—",row.DataState,orderText,row.Error));
        }
        updated.AddRange(failures);
        // Retain item identity and ordering while quotes change: no Reset/Remove event can
        // clear a DataGrid selection or invalidate the row between two clicks.
        var existing=RealtimeScreeningRows.ToDictionary(r=>r.Code,StringComparer.Ordinal);
        foreach(var row in updated)
            if(existing.TryGetValue(row.Code,out var current))current.Update(row);else RealtimeScreeningRows.Add(row);
        var keys=updated.Select(r=>r.Code).ToHashSet(StringComparer.Ordinal);
        foreach(var old in RealtimeScreeningRows.Where(r=>!keys.Contains(r.Code)).ToArray())RealtimeScreeningRows.Remove(old);
        RealtimeScreenConnection=snapshot.ConnectionState;
        RealtimeScreenInfo=$"{snapshot.Status} · {snapshot.Rows.Count:N0}종목 · 집중 {snapshot.Rows.Count(r=>r.Focused):N0} · 신호 {snapshot.Rows.Count(r=>r.BuySignal):N0} · 접근도는 발동 확률이 아닙니다."+(IsOffline?" · 공개시세 신호 표시만 · 체결/호가 아님 · 지연 가능":"");
        if(realtimeScreenAutoTrade!=snapshot.AutoTradingEnabled){realtimeScreenAutoTrade=snapshot.AutoTradingEnabled;Changed(nameof(RealtimeAutoTrade));}
        NotifyRealtimeState();
    }

    private void ChangeRealtimeScreeningStrategy(string name,OversoldConfig configuration)
    {
        if(!RealtimeScreeningRunning)return;
        realtimeStrategyChange?.Cancel();realtimeStrategyChange?.Dispose();realtimeStrategyChange=new();
        var cancellation=realtimeStrategyChange.Token;var changeId=++realtimeStrategyChangeId;
        var requestedConfiguration=configuration.Clone();
        foreach(var paper in realtimeScreenPapers)paper.SetEntryEnabled(false);
        RealtimeScreening.SetAutoTrading(false);
        realtimeScreenStrategyChanged=true;realtimeScreenAutoTrade=false;Changed(nameof(RealtimeAutoTrade));
        RealtimeScreeningStrategyChanging=true;Changed(nameof(RealtimeScreeningStrategyChanging));
        SelectedRealtimeScreeningRow=null;RealtimeScreeningRows.Clear();
        RealtimeScreenInfo="새 전략으로 감시를 준비하는 중입니다. 기존 가상 보유분은 이전 전략으로 보호하며 자동매매는 해제했습니다.";
        NotifyRealtimeState();
        _=Reconfigure();
        async Task Reconfigure()
        {
            try{await Task.Run(()=>RealtimeScreening.Reconfigure(requestedConfiguration,name,cancellation),cancellation);}
            catch(OperationCanceledException){}
            catch{if(!disposed&&changeId==realtimeStrategyChangeId){RealtimeScreening.Stop("전략 변경 실패 · 다시 시작하세요.");alert("새 전략 준비에 실패했습니다. 실시간 스크리닝을 다시 시작하세요. 기존 보유분의 보호 관리는 유지합니다.");}}
            finally{if(!disposed&&changeId==realtimeStrategyChangeId){RealtimeScreeningStrategyChanging=false;Changed(nameof(RealtimeScreeningStrategyChanging));NotifyRealtimeState();}}
        }
    }

    private async Task StopRealtimeScreening(bool emergency)
    {
        realtimeStrategyChangeId++;realtimeStrategyChange?.Cancel();RealtimeScreeningStrategyChanging=false;Changed(nameof(RealtimeScreeningStrategyChanging));
        if(realtimeScreening==null&&realtimeScreenTask==null)return;
        realtimeScreening?.Stop(emergency?"긴급중지":"사용자 중지 · 신규 종목 탐색과 진입 중지");
        foreach(var paper in realtimeScreenPapers)
        {
            if(emergency)paper.Stop();else paper.SetEntryEnabled(false);
        }
        realtimeScreenAutoTrade=false;Changed(nameof(RealtimeAutoTrade));
        var protectedPositions=!emergency&&realtimeScreenPapers.SelectMany(p=>p.Snapshot().Rows).Any(r=>r.Owned>0||r.Pending>0);
        if(protectedPositions)
        {
            RealtimeScreenInfo="스크리닝 중지 · 신규/추가매수 차단 · 기존 가상 포지션과 미체결 보호 관리는 계속됩니다.";
            NotifyRealtimeState();return;
        }
        realtimeScreenStop?.Cancel();
        var task=realtimeScreenTask;if(task!=null)try{await task.WaitAsync(TimeSpan.FromSeconds(5));}catch(OperationCanceledException){}catch(TimeoutException){}catch{}
        realtimeScreenTask=null;realtimeScreenStop?.Dispose();realtimeScreenStop=null;realtimeScreenPapers.Clear();
        RealtimeScreenConnection="중지";RealtimeScreenInfo=emergency?"긴급중지 · 실시간 감시와 가상 신규 주문을 중단했습니다.":"실시간 스크리닝을 중지했습니다.";NotifyRealtimeState();
    }

    private void DisposeRealtimeScreening()
    {
        realtimeStrategyChangeId++;realtimeStrategyChange?.Cancel();realtimeStrategyChange?.Dispose();realtimeStrategyChange=null;
        realtimeScreening?.Stop("프로그램 종료");foreach(var paper in realtimeScreenPapers)paper.Stop();realtimeScreenStop?.Cancel();
        try{realtimeScreenTask?.Wait(TimeSpan.FromSeconds(5));}catch{}
        realtimeScreenTask=null;realtimeScreenStop?.Dispose();realtimeScreenStop=null;realtimeScreenPapers.Clear();
    }

    private void NotifyRealtimeState()
    {
        Changed(nameof(RealtimeScreeningRunning));Changed(nameof(RealtimeScreeningProtectionActive));Changed(nameof(CanStartRealtimeScreening));Changed(nameof(CanStopRealtimeScreening));
        Actions.GetValueOrDefault("실시간스크리닝시작")?.Refresh();Actions.GetValueOrDefault("실시간스크리닝중지")?.Refresh();Refresh();
    }
}
