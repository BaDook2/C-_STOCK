using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private const string OversoldPresetPrefix="oversold-preset:";
    private const string OversoldPresetListKey="oversold-presets-v1";
    private const string ActiveOversoldPresetKey="oversold-active-preset-v1";
    private string? activeOversoldPreset;
    private string? selectedOversoldPreset;
    public string? SelectedOversoldPreset
    {
        get=>selectedOversoldPreset;
        set
        {
            if(value==null||value==selectedOversoldPreset)return;
            if(!CanConfigure){Changed();return;}
            LoadNamedOversoldPreset(value);
        }
    }
    // Retirement is a product boundary, not a user-editable preference or saved DB flag.
    private static bool LegacyStrategiesAvailable=>false;
    private static void EnsureLegacyUnavailable(){if(!LegacyStrategiesAvailable)throw new InvalidOperationException("기존 전략은 사용 중지되었습니다. 국내 과매도·낙주 반등 일봉 백테스트만 사용할 수 있습니다.");}
    private void InitializeOnlyOversold()
    {
        InitializeScreeningResults();
        Presets.Clear();foreach(var p in store.State<string[]>(OversoldPresetListKey)??["기본"])if(!string.IsNullOrWhiteSpace(p)&&!Presets.Contains(p))Presets.Add(p);
        if(Presets.Count==0)Presets.Add("기본");
        if(!Presets.Contains(StrategyDiagnosticPreset.Name))
        {
            Presets.Add(StrategyDiagnosticPreset.Name);
            store.State(OversoldPresetPrefix+StrategyDiagnosticPreset.Name,StrategyDiagnosticPreset.Configuration());
            store.State(OversoldPresetListKey,Presets.ToArray());
        }
        foreach(var preset in StrategyDiagnosticPreset.BacktestVerificationPresets())
        {
            if(Presets.Contains(preset.Key))continue;
            preset.Value.Validate();Presets.Add(preset.Key);store.State(OversoldPresetPrefix+preset.Key,preset.Value);
        }
        store.State(OversoldPresetListKey,Presets.ToArray());
        activeOversoldPreset=store.State<string>(ActiveOversoldPresetKey);if(activeOversoldPreset==null||!Presets.Contains(activeOversoldPreset))activeOversoldPreset=Presets[0];
        if(store.State<OversoldConfig>(OversoldPresetPrefix+activeOversoldPreset)==null)store.State(OversoldPresetPrefix+activeOversoldPreset,oversoldConfig.Clone());
        selectedOversoldPreset=activeOversoldPreset;Preset=activeOversoldPreset;
        Add("백테스트",RunOversold,()=>CanConfigure&&dataset?.Stock.Exchange==Exchange.KRX&&!IsTick);
        Add("연습시작",StartPractice,()=>CanConfigure&&IsOffline&&dataset!=null&&!IsTick);
        Add("스크리닝",ScreenOversold,()=>CanConfigure);
        Add("결과열기",()=>SelectedMatch==null?Task.CompletedTask:OpenScreeningStock(SelectedMatch.Stock),()=>CanConfigure&&SelectedMatch!=null);
        Add("저장",()=>{var name=RequireActiveOversoldName();if(!string.Equals(Preset.Trim(),name,StringComparison.Ordinal))throw new InvalidOperationException("전략 이름을 바꾸려면 이름 변경 버튼을 사용하세요.");SaveOversold();store.State(OversoldPresetPrefix+name,oversoldConfig.Clone());PersistOversoldPresetSelection(name);Log("전략 설정 저장: "+name);return Task.CompletedTask;},()=>CanConfigure);
        Add("추가",()=>{ApplyOversoldFields();var requested=Preset.Trim();if(requested.Length==0||string.Equals(requested,activeOversoldPreset,StringComparison.Ordinal))requested=UniqueOversoldName("새 전략");ValidateOversoldName(requested);if(Presets.Contains(requested))throw new InvalidOperationException("같은 이름의 전략이 이미 있습니다.");SaveOversold();store.State(OversoldPresetPrefix+requested,oversoldConfig.Clone());Presets.Add(requested);PersistOversoldPresetSelection(requested);Log("전략 추가: "+requested);return Task.CompletedTask;},()=>CanConfigure);
        Add("이름변경",()=>{var previous=RequireActiveOversoldName();var renamed=Preset.Trim();ValidateOversoldName(renamed);if(string.Equals(previous,renamed,StringComparison.Ordinal)){Preset=previous;Changed(nameof(Preset));Log("전략 이름이 이미 "+previous+"입니다.");return Task.CompletedTask;}if(Presets.Contains(renamed))throw new InvalidOperationException("같은 이름의 전략이 이미 있습니다.");SaveOversold();store.State(OversoldPresetPrefix+renamed,oversoldConfig.Clone());store.DeleteState(OversoldPresetPrefix+previous);var index=Presets.IndexOf(previous);if(index>=0)Presets[index]=renamed;else Presets.Add(renamed);PersistOversoldPresetSelection(renamed);Log($"전략 이름 변경: {previous} → {renamed}");return Task.CompletedTask;},()=>CanConfigure);
        Add("복구",()=>{var name=RequireActiveOversoldName();var cfg=store.State<OversoldConfig>(OversoldPresetPrefix+name)??store.State<OversoldConfig>("oversold-config-v1")??new();cfg.Validate();oversoldConfig=cfg.Clone();Preset=name;selectedOversoldPreset=name;BuildOversoldGroups();Changed(nameof(Preset));Changed(nameof(SelectedOversoldPreset));Log("저장 이후의 입력을 취소하고 이전 설정으로 복구했습니다.");return Task.CompletedTask;},()=>CanConfigure);
        Add("불러오기",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="전략 설정 JSON|*.json",DefaultExt=".json",CheckFileExists=true,Title="전략 JSON 불러오기"};if(dialog.ShowDialog()!=true)return Task.CompletedTask;var imported=OversoldPresetExport.Read(dialog.FileName);var basis=string.IsNullOrWhiteSpace(imported.Preset)?"가져온 전략":imported.Preset.Trim();var name=UniqueOversoldName(basis);oversoldConfig=imported.Configuration.Clone();BuildOversoldGroups();SaveOversold();store.State(OversoldPresetPrefix+name,oversoldConfig.Clone());Presets.Add(name);PersistOversoldPresetSelection(name);Log("전략 JSON 불러오기: "+name);return Task.CompletedTask;},()=>CanConfigure);
        Add("다운로드",()=>{foreach(var field in OversoldGroups.SelectMany(g=>g.Fields))field.Apply();var snapshot=OversoldPresetExport.Snapshot(Preset,oversoldConfig,DateTimeOffset.UtcNow);var dialog=new Microsoft.Win32.SaveFileDialog{Filter="전략 설정 JSON|*.json",DefaultExt=".json",AddExtension=true,InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),FileName=OversoldPresetExport.SafeName(Preset,KoreaClock.Today),Title="전략 설정 다운로드"};if(dialog.ShowDialog()==true&&OversoldPresetExport.Write(dialog.FileName,snapshot))Status="전략 설정 JSON을 저장했습니다.";return Task.CompletedTask;},()=>CanConfigure);
        Add("기본값",()=>{oversoldConfig=new();BuildOversoldGroups();Log("낙주 전략 기본값을 불러왔습니다. 저장 후 적용하세요.");return Task.CompletedTask;},()=>CanConfigure);
        foreach(var key in new[]{"전략설정저장","틱설정저장","틱백테스트","순위자료적용","순위CSV","파이프라인Mock"})Add(key,()=>{EnsureLegacyUnavailable();return Task.CompletedTask;},()=>false);
    }
    private void ApplyOversoldFields(){foreach(var field in OversoldGroups.SelectMany(g=>g.Fields))field.Apply();oversoldConfig.Validate();}
    private static void ValidateOversoldName(string name){if(string.IsNullOrWhiteSpace(name)||name.Length>80)throw new InvalidOperationException("전략 이름은 1~80자로 입력하세요.");}
    private string RequireActiveOversoldName(){var name=activeOversoldPreset;if(string.IsNullOrWhiteSpace(name))throw new InvalidOperationException("선택한 전략이 없습니다.");return name;}
    private string UniqueOversoldName(string basis)
    {
        basis=string.IsNullOrWhiteSpace(basis)?"새 전략":basis.Trim();if(!Presets.Contains(basis))return basis;
        for(var i=2;i<int.MaxValue;i++){var candidate=$"{basis} {i:N0}";if(!Presets.Contains(candidate))return candidate;}
        throw new InvalidOperationException("새 전략 이름을 만들 수 없습니다.");
    }
    private void PersistOversoldPresetSelection(string name)
    {
        activeOversoldPreset=selectedOversoldPreset=name;Preset=name;store.State(OversoldPresetListKey,Presets.ToArray());store.State(ActiveOversoldPresetKey,name);Changed(nameof(Preset));Changed(nameof(SelectedOversoldPreset));
    }
    private void LoadNamedOversoldPreset(string name)
    {
        var cfg=store.State<OversoldConfig>(OversoldPresetPrefix+name)??(name==activeOversoldPreset?store.State<OversoldConfig>("oversold-config-v1"):null)??throw new InvalidOperationException("저장된 낙주 전략 설정이 없습니다.");
        cfg.Validate();oversoldConfig=cfg.Clone();activeOversoldPreset=selectedOversoldPreset=name;Preset=name;store.State(ActiveOversoldPresetKey,name);store.State("oversold-config-v1",oversoldConfig);BuildOversoldGroups();Changed(nameof(Preset));Changed(nameof(SelectedOversoldPreset));Log("전략 선택: "+name);
    }
    private async Task ScreenOversold()
    {
        var (first,last)=ScreenPeriod();var spec=ScreenDaily?null:BarSpecification.Parse(ScreenBarValue,ScreenBarUnit switch{"틱"=>BarUnit.Tick,"분"=>BarUnit.Minute,"시"=>BarUnit.Hour,_=>throw new InvalidOperationException("틱/분/시를 선택하세요.")});SaveOversold(!ScreenDaily);var cfg=oversoldConfig.Clone();var facts=oversoldFacts.Snapshot();StockItem[] items;string source;
        if(ScreenWatchlist)
        {
            var groups=WatchGroups.Where(g=>g.IncludeInScreening).ToArray();if(groups.Length==0)throw new InvalidOperationException("스크리닝할 관심그룹을 하나 이상 선택하세요.");store.State("screen-watch-groups",groups.Select(g=>g.Id).ToArray());
            var sectorEntries=groups.SelectMany(g=>g.Items).Select(e=>e.Stock).Where(s=>Sector=="전체"||s.Sector==Sector).ToArray();
            var marketEntries=sectorEntries.Where(MatchesScreeningMarket).ToArray();items=marketEntries.DistinctBy(s=>s.Key).ToArray();
            var duplicateCount=marketEntries.Length-items.Length;var outsideMarketCount=sectorEntries.Length-marketEntries.Length;
            source=$"관심그룹 {groups.Length:N0}개 · 중복 {duplicateCount:N0}개 제외 · 선택시장 밖 {outsideMarketCount:N0}개 제외";
        }
        else{if(Catalog.Count==0||catalogRegion!=ScreeningMarketSelection||catalogSize!=ScreeningSize)await LoadCatalog();items=Catalog.Where(s=>MatchesScreeningMarket(s)&&(Sector=="전체"||s.Sector==Sector)).DistinctBy(s=>s.Key).ToArray();source=ScreeningMarketSelection+" 현재 상위 목록 · 과거 전체시장 아님";}
        store.State("oversold-screen-bars",new ScreeningBarPreference(ScreenDaily,ScreenBarValue,ScreenBarUnit));store.State("oversold-screen-period",new ScreeningPeriodPreference(ScreenFrom!.Value,ScreenTo!.Value));Matches.Clear();var token=Token;int done=0,missing=0,unsupported=0;long dataMilliseconds=0,calculationMilliseconds=0;var generation=++screeningGeneration;var elapsed=System.Diagnostics.Stopwatch.StartNew();
        var run=OversoldScreeningRun.Start(items,cfg,spec,DateTimeOffset.UtcNow,first,last,SelectedOversoldPreset) with{FactsJson=Serialization.Pack(facts)};PublishScreening(run);
        try
        {
            const int concurrency=4;
            foreach(var batch in items.Chunk(concurrency))
            {
                token.ThrowIfCancellationRequested();var begin=done+1;var finish=Math.Min(items.Length,done+batch.Length);ScreenInfo=$"전체 {items.Length:N0}개 중 {begin:N0}~{finish:N0}번째 조회 중 · 조건 일치 {Matches.Count:N0}개";Status=ScreenInfo;
                var rows=await Task.WhenAll(batch.Select(EvaluateItem));
                foreach(var result in rows)
                {
                    token.ThrowIfCancellationRequested();if(generation!=screeningGeneration)return;var item=result.Item;var row=result.Row;if(result.Unsupported)unsupported++;if(result.Missing)missing++;dataMilliseconds+=result.DataMilliseconds;calculationMilliseconds+=result.CalculationMilliseconds;
                    run=run.Append(row);done++;PublishScreening(run,done%8==0);
                    if(row.Candidate)Matches.Add(new(item,row.Price!.Value,0,(SelectedOversoldPreset??run.StrategyName)+" · "+row.Status,row.ObservedAt!.Value.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss")){Drawdown=row.Drawdown,Rsi=row.Rsi,VolumeRatio=row.VolumeRatio,SupportCount=row.SupportCount,Status=row.Status});
                    ScreenInfo=$"전체 {items.Length:N0}개 중 {done:N0}번째 · {item.Name} · 조건 일치 {Matches.Count:N0}개 · {elapsed.Elapsed:mm\\:ss}";Status=ScreenInfo;
                }
            }
            run=run with{Status="완료"};
        }
        catch(OperationCanceledException){run=run with{Status="취소 · 완료한 종목만 기록"};throw;}
        catch{run=run with{Status="실패 · 완료한 종목만 기록"};throw;}
        finally{elapsed.Stop();if(generation==screeningGeneration){PublishScreening(run);var average=done==0?0:elapsed.Elapsed.TotalSeconds/done;var dataAverage=done==0?0:dataMilliseconds/1000d/done;var calculationAverage=done==0?0:calculationMilliseconds/(double)done;ScreenInfo=$"전체 {items.Length:N0}개 중 {done:N0}개 완료 · 조건 일치 {Matches.Count:N0}개 · 조회 실패/봉 없음 {missing:N0}개 · 선택시장 밖 {unsupported:N0}개 · {source} · {run.Status} · {elapsed.Elapsed:mm\\:ss} ({average:N2}초/종목 · 자료조회 {dataAverage:N2}초 · 전략계산 {calculationAverage:N0}ms)";Status=ScreenInfo;}}
        Logs.Insert(0,$"{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss}  {ScreenInfo}");

        async Task<(StockItem Item,ScreeningObservation Row,bool Missing,bool Unsupported,long DataMilliseconds,long CalculationMilliseconds)> EvaluateItem(StockItem item)
        {
            if(item.Exchange!=Exchange.KRX)return(item,ScreeningObservation.Missing(item,"선택시장 밖 · 국내 일반주식 전략"),false,true,0,0);
            var dataWatch=System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var bars=await ScreeningBars(item,spec,facts,run.ScreeningTime,token,first,last);var itemFacts=facts;DailySecurityStatus? currentStatus=null;CurrentMarketSnapshot? currentQuote=null;
                if(spec!=null)
                {
                    var dates=bars.Select(b=>DateOnly.FromDateTime(b.OpenTime.ToOffset(TimeSpan.FromHours(9)).DateTime)).Distinct().ToArray();
                    var known=facts.Sessions.ToDictionary(s=>s.Date);var added=dates.Where(d=>!known.ContainsKey(d)).Select(d=>new DailyMarketSession(d,new DateTimeOffset(d.ToDateTime(new TimeOnly(9,0)),TimeSpan.FromHours(9)),new DateTimeOffset(d.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9))));
                    itemFacts=facts.Snapshot();itemFacts.MarketSessions=[..facts.Sessions.Concat(added).OrderBy(s=>s.Open)];if(string.IsNullOrWhiteSpace(itemFacts.Source)&&itemFacts.MarketSessions.Length>0)itemFacts.Source="연결 증권사 분봉의 KRX 정규장 세션";itemFacts.Validate();
                }
                if(provider is ICurrentMarketData current)
                {
                    async Task<DailySecurityStatus?> Status(){try{return await current.CurrentSecurity(item,token);}catch(OperationCanceledException){throw;}catch{return null;}}
                    async Task<CurrentMarketSnapshot?> Quote(){try{return await current.Current(item,token);}catch(OperationCanceledException){throw;}catch{return null;}}
                    var statusTask=Status();
                    if(spec==null){var quoteTask=Quote();await Task.WhenAll(statusTask,quoteTask);currentQuote=quoteTask.Result;}else await statusTask;
                    currentStatus=statusTask.Result;
                }
                var observed=DateTimeOffset.UtcNow;
                if(currentQuote!=null)
                {
                    var merged=DailyQuoteMerger.Merge(bars.Select(b=>b.Price).ToArray(),currentQuote,observed);
                    bars=merged.Select((b,i)=>new StrategyBar(b,b.Time,b.Time,i,facts.TradingValue(item.Symbol,DailyDates.Of(b),b.Time),currentQuote.Source)).ToArray();
                }
                bool? risk=store.State<bool>("exclude:"+item.Key)?true:store.State<string>("review:"+item.Key)==KoreaClock.Today.ToString()?false:null;
                dataWatch.Stop();var calculationWatch=System.Diagnostics.Stopwatch.StartNew();
                var engine=new OversoldScreeningEngine();
                var row=await Task.Run(()=>spec==null?engine.EvaluateCurrent(item,bars,cfg,itemFacts,observed,currentStatus,risk,token,first,last):engine.EvaluateObserved(item,bars,cfg,itemFacts,run.ScreeningTime,currentStatus,risk,token,first,last),token);
                calculationWatch.Stop();
                if(spec!=null&&bars.Any(b=>b.Source.Contains("거래대금 OHLC 평균×거래량 추정",StringComparison.Ordinal))&&row.Evaluation is {} intradayEvaluation)
                {
                    intradayEvaluation=intradayEvaluation with{Conditions=[..intradayEvaluation.Conditions,new("온라인 분봉 거래대금",ConditionOutcome.Passed,"OHLC 평균×거래량","증권사 분봉에 실제 거래대금 필드 없음 · 추정값 사용",bars[0].Source,true)]};
                    row=row with{EvaluationJson=Serialization.Pack(intradayEvaluation)};
                }
                if(currentQuote!=null&&row.Evaluation is {} evaluation)
                {
                    var turnover=currentQuote.TradingValue;evaluation=evaluation with{Conditions=[..evaluation.Conditions,new("현재 당일 거래대금",turnover.HasValue?ConditionOutcome.Passed:ConditionOutcome.MissingData,turnover?.ToString("N0")??"미제공","참고 관측값 · 과거 평균 거래대금 대체 아님",currentQuote.Source,true)]};row=row with{EvaluationJson=Serialization.Pack(evaluation)};
                }
                // Missing facts inside an otherwise evaluated row remain visible in the funnel. The
                // progress summary reserves "조회 실패/봉 없음" for rows that could not be evaluated.
                return(item,row,row.Evaluation==null,false,dataWatch.ElapsedMilliseconds,calculationWatch.ElapsedMilliseconds);
            }
            catch(OperationCanceledException){throw;}
            catch(InvalidOperationException ex){dataWatch.Stop();return(item,ScreeningObservation.Missing(item,ex.Message),true,false,dataWatch.ElapsedMilliseconds,0);}
            catch{dataWatch.Stop();return(item,ScreeningObservation.Missing(item,"자료 부족 · 시세 조회 실패"),true,false,dataWatch.ElapsedMilliseconds,0);}
        }
    }
}
