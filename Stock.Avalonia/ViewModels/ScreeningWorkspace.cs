using System.Collections.ObjectModel;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;

public sealed record ScreeningDisplayRow(ScreeningObservation Observation)
{
    public string Code=>Observation.Stock.Symbol;public string Name=>Observation.Stock.Name;
    public string Region=>!string.IsNullOrEmpty(Observation.Stock.Board)?Observation.Stock.Board switch{"KOSPI"=>"코스피","KOSDAQ"=>"코스닥",_=>Observation.Stock.Board}:Observation.Stock.Exchange.ToString();
    public string Sector=>Observation.Stock.Sector;
    public string Date=>Observation.ObservedAt?.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss")??"—";
    public string Price=>Observation.Price is {} p?DisplayNumber.Price(p)+(Observation.Stock.Exchange==Exchange.KRX?" 원":" USD"):"—";
    public string Drawdown=>Observation.Drawdown?.ToString("0.00")??"—";public string Rsi=>Observation.Rsi?.ToString("0.00")??"—";
    public string Volume=>Observation.VolumeRatio?.ToString("0.00")??"—";public int Supports=>Observation.SupportCount;
    public string Proximity=>Observation.Proximity.ToString("0.00");public string State=>Observation.Status;
}
public sealed class ScreeningWorkspace:Bindable
{
    private readonly AppServices services;
    private readonly StrategyEditor strategies;
    private readonly MarketHistoryService history;
    private readonly Func<bool> online;
    private readonly Func<bool> customAvailable;
    private readonly Func<StockItem[]> watchTargets;
    private readonly Func<string> factsJson;
    private CancellationTokenSource? cancellation;
    private bool busy,daily=true,showAll,useWatchlist;
    private string market="코스피+코스닥",unit="분",value="5",status="전략과 대상·기간을 선택하세요.";
    private ScreeningDisplayRow? selected;
    private StockItem[] catalog=[];
    private string? catalogMarket;
    private int catalogTop;
    private DateTimeOffset catalogAt;
    private string sector="전체";
    public ObservableCollection<string> Sectors {get;}=["전체"];
    public string Sector {get=>sector;set=>Set(ref sector,value??"전체");}
    public string[] Markets {get;}=["코스피","코스닥","코스피+코스닥","S&P500","나스닥100","S&P500+나스닥100"];
    public int[] TopCounts {get;}=[10,25,50,100];
    public int TopCount {get;set;}=10;
    public string Market {get=>market;set{if(value==null)return;if(Set(ref market,value)){Sector="전체";ModeChanged();}}}
    public bool UseWatchlist {get=>useWatchlist;set{Set(ref useWatchlist,value);Changed(nameof(UseMarketTop));}}
    public bool UseMarketTop {get=>!UseWatchlist;set{if(value)UseWatchlist=false;}}
    public StrategyEditor Strategies=>strategies;
    public DateTime? From {get;set;}=DateTime.Today.AddDays(-7);
    public DateTime? To {get;set;}=DateTime.Today;
    public bool CanCustom=>online()&&customAvailable()&&Market is "코스피" or "코스닥" or "코스피+코스닥";
    public bool Daily {get=>!CanCustom||daily;set{daily=value;Changed();Changed(nameof(Custom));}}
    public bool Custom {get=>CanCustom&&!Daily;set{if(value&&!CanCustom){Notice?.Invoke("국내 분봉을 지원하는 온라인 계좌가 필요합니다.");return;}Daily=!value;}}
    public string[] Units {get;}=["분","시간"];
    public string[] Values=>Enumerable.Range(1,unit=="분"?59:12).Select(n=>n.ToString()).ToArray();
    public string Unit {get=>unit;set{if(Set(ref unit,value)){Value="1";Changed(nameof(Values));}}}
    public string Value {get=>value;set=>Set(ref this.value,value);}
    public bool ShowAll {get=>showAll;set{Set(ref showAll,value);RefreshRows();}}
    public bool Busy {get=>busy;private set{if(Set(ref busy,value)){Start.Refresh();LoadCatalog.Refresh();Changed(nameof(SettingsEnabled));}}}
    public bool SettingsEnabled=>!Busy&&!ReadOnly;
    public string Status {get=>status;private set{Set(ref status,value);Progress?.Invoke(value);}}
    public OversoldScreeningRun? Snapshot {get;private set;}
    public ObservableCollection<ScreeningDisplayRow> Rows {get;}=[];
    public ObservableCollection<ScreeningFunnelRow> Funnel {get;}=[];
    public ScreeningDisplayRow? Selected {get=>selected;set{Set(ref selected,value);Changed(nameof(SelectedConditions));OpenResult.Refresh();}}
    public ConditionEvaluation[] SelectedConditions=>Selected?.Observation.Evaluation?.Conditions??[];
    public AsyncCommand Start {get;}
    public AsyncCommand LoadCatalog {get;}
    public AsyncCommand Cancel {get;}
    public AsyncCommand OpenResult {get;}
    public event Action<string>? Notice;
    public event Action<string>? Progress;
    public event Action<ResearchBrowserViewModel>? BrowserRequested;
    public event Action<OversoldScreeningRun>? Completed;
    public bool ReadOnly {get;private set;}
    public bool Editable=>!ReadOnly;
    public string SnapshotLabel=>Snapshot==null?"":Snapshot.StrategyName+" · "+(Snapshot.BarSpecification?.ToString()??"일봉")+" · "+Snapshot.From+" ~ "+Snapshot.To;
    public void LoadSnapshot(OversoldScreeningRun snapshot){Snapshot=snapshot;ReadOnly=true;ShowAll=true;From=snapshot.From?.ToDateTime(TimeOnly.MinValue);To=snapshot.To?.ToDateTime(TimeOnly.MinValue);Status="보관 당시 설정·봉 자료 사용 · "+snapshot.Status;RefreshRows();RefreshFunnel();Changed(nameof(Editable));Changed(nameof(ReadOnly));Changed(nameof(SnapshotLabel));}
    public StockItem[] CurrentTargets=>Snapshot?.FinalCandidates.ToArray()??[];
    public ScreeningWorkspace(AppServices services,StrategyEditor strategies,MarketHistoryService history,Func<bool> online,Func<StockItem[]> watchTargets,Func<string> factsJson,Func<bool>? customAvailable=null)
    {
        this.services=services;this.strategies=strategies;this.history=history;this.online=online;this.watchTargets=watchTargets;this.factsJson=factsJson;this.customAvailable=customAvailable??online;
        Start=new(Execute,()=>!Busy);LoadCatalog=new(LoadCatalogForDisplay,()=>!Busy);Cancel=new(()=>{Stop();return Task.CompletedTask;});OpenResult=new(OpenSelected,()=>Selected!=null);
    }
    // One in-memory list backs both screening modes and the WPF-style sector selector.
    internal async Task<StockItem[]> Catalog(string requestedMarket,int top,CancellationToken token,bool refresh=false)
    {
        var now=services.Clock();if(!refresh&&catalogMarket==requestedMarket&&catalogTop==top&&now>=catalogAt&&now-catalogAt<TimeSpan.FromMinutes(5))return catalog;
        var items=(await services.MarketData.Universe(requestedMarket,top,token)).Where(s=>Matches(s,requestedMarket)).DistinctBy(s=>s.Key).ToArray();token.ThrowIfCancellationRequested();
        catalog=items;catalogMarket=requestedMarket;catalogTop=top;catalogAt=services.Clock();
        if(Market==requestedMarket&&TopCount==top){var priorSector=Sector;Sectors.Clear();Sectors.Add("전체");foreach(var name in items.Select(s=>s.Sector).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct().Order())Sectors.Add(name);sector=Sectors.Contains(priorSector)?priorSector:"전체";Changed(nameof(Sector));}
        return items;
    }
    private async Task LoadCatalogForDisplay()
    {
        cancellation?.Cancel();cancellation=new();var token=cancellation.Token;Busy=true;
        try{var items=await Catalog(Market,TopCount,token,refresh:true);Status=$"{items.Length:N0}개 종목 준비 · 현재 상위/지수 목록 · 과거 편입·편출 미반영";}
        catch(OperationCanceledException) when(token.IsCancellationRequested){Status="목록 조회 취소";}
        catch(Exception ex) when(ex is not OutOfMemoryException){Status=OperationError.Describe(ex);Notice?.Invoke(Status);}
        finally{Busy=false;}
    }
    public void Stop()=>cancellation?.Cancel();
    public void ModeChanged(){if(!CanCustom)daily=true;Changed(nameof(Daily));Changed(nameof(Custom));Changed(nameof(CanCustom));}
    private static bool Matches(StockItem stock,string market)=>market switch{"코스피"=>stock.Board=="KOSPI","코스닥"=>stock.Board=="KOSDAQ","코스피+코스닥"=>stock.Exchange==Exchange.KRX,_=>stock.Exchange!=Exchange.KRX};
    private async Task Execute()
    {
        cancellation?.Dispose();cancellation=new();var token=cancellation.Token;Busy=true;var storageFailure=false;
        try
        {
            if(From==null||To==null||From>To||To.Value.Date>DateTime.Today)throw new InvalidOperationException("스크리닝 기간의 순서와 미래 날짜를 확인하세요.");
            var first=DateOnly.FromDateTime(From.Value);var last=DateOnly.FromDateTime(To.Value);var strategyName=strategies.Selected;var selectedMarket=Market;var selectedSector=Sector;var top=TopCount;var watch=UseWatchlist;var config=strategies.Current();var facts=Serialization.Unpack<HistoricalFacts>(factsJson());facts.Validate();
            BarSpecification? specification=Daily?null:new(int.Parse(Value),Unit=="분"?BarUnit.Minute:BarUnit.Hour);
            Status="대상 종목 조회 중…";var items=(watch?watchTargets():await Catalog(selectedMarket,top,token)).Where(s=>Matches(s,selectedMarket)&&(selectedSector=="전체"||s.Sector==selectedSector)).DistinctBy(s=>s.Key).ToArray();
            if(items.Length==0)throw new InvalidOperationException("선택한 시장/관심그룹에 스크리닝할 종목이 없습니다.");
            Snapshot=OversoldScreeningRun.Start(items,config,specification,DateTimeOffset.UtcNow,first,last,strategyName) with{FactsJson=Serialization.Pack(facts)};Rows.Clear();Selected=null;Funnel.Clear();int done=0;
            using var gate=new SemaphoreSlim(4);
            async Task<(ScreeningObservation Row,PreparedResearchData? Data)> Evaluate(StockItem stock)
            {
                await gate.WaitAsync(token);
                try
                {
                    var data=await history.Get(stock,config,facts,specification,first,last,token);var now=DateTimeOffset.UtcNow;
                    var bars=data.Bars??data.Dataset.CompletedBars.Select((b,index)=>new StrategyBar(b,b.Time,b.Time,index,facts.TradingValue(stock.Symbol,DailyDates.Of(b),b.Time),data.Dataset.Source)).ToArray();
                    var engine=new OversoldScreeningEngine();var result=await Task.Run(()=>specification==null?engine.EvaluateCurrent(stock,bars,config,data.Facts,now,token:token,from:first,to:last):engine.EvaluateObserved(stock,bars,config,data.Facts,now,token:token,from:first,to:last),token);
                    return(result,data);
                }
                catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch(StorageException){throw;}
                catch(Exception ex) when(ex is not OutOfMemoryException){return(ScreeningObservation.Missing(stock,OperationError.Describe(ex)),null);}
                finally{gate.Release();}
            }
            var pending=items.Select(Evaluate).ToArray();
            try
            {
                await foreach(var completed in Task.WhenEach(pending).WithCancellation(token))
                {
                    var current=await completed;token.ThrowIfCancellationRequested();Snapshot=Snapshot.Append(current.Row);done++;
                    if(current.Data!=null)services.Store.State("desktop-screen-data:"+Snapshot.Id+":"+current.Row.Stock.Key,current.Data);
                    RefreshRows();RefreshFunnel();Status=$"전체 {items.Length:N0}개 중 {done:N0}번째 · {current.Row.Stock.Name} · 후보 {Snapshot.FinalCandidates.Length:N0}개";
                }
            }
            catch{cancellation.Cancel();throw;}
            finally{try{await Task.WhenAll(pending);}catch{ /* Preserve the original completion/cancel/storage failure. */ }}
            Snapshot=Snapshot with{Status="완료"};Status+=" · 완료";
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){if(Snapshot!=null)Snapshot=Snapshot with{Status="취소 · 완료한 종목 유지"};Status="스크리닝 취소 · 완료한 종목은 열람할 수 있습니다.";}
        catch(StorageException ex){storageFailure=true;Status="저장소 오류 · 스크리닝 중지";Notice?.Invoke(ex.Message);}
        catch(Exception ex) when(ex is not OutOfMemoryException){if(Snapshot!=null)Snapshot=Snapshot with{Status="실패 · 완료한 종목 유지"};Status=OperationError.Describe(ex);Notice?.Invoke(Status);}
        finally{Busy=false;if(Snapshot!=null&&!storageFailure)try{services.Store.State("desktop-screen:"+Snapshot.Id,Snapshot);Changed(nameof(SnapshotLabel));Completed?.Invoke(Snapshot);}catch(StorageException ex){Status="저장소 오류 · 결과 보관 실패";Notice?.Invoke(ex.Message);}}
    }
    private void RefreshRows()
    {
        if(Snapshot==null){Rows.Clear();return;}var wanted=Snapshot.Results.Where(r=>ShowAll||r.Candidate).ToArray();
        foreach(var old in Rows.Where(r=>!wanted.Any(w=>w.Stock.Key==r.Observation.Stock.Key)).ToArray())Rows.Remove(old);
        foreach(var row in wanted)if(!Rows.Any(r=>r.Observation.Stock.Key==row.Stock.Key))Rows.Add(new(row));
    }
    private void RefreshFunnel(){Funnel.Clear();if(Snapshot!=null)foreach(var step in Snapshot.Funnel())Funnel.Add(step);}
    private async Task OpenSelected()
    {
        var row=Selected;var snapshot=Snapshot;if(row==null||snapshot==null)return;
        try
        {
            var targets=Rows.Select(r=>r.Observation.Stock).ToArray();var browser=new ResearchBrowserViewModel(targets,async (stock,token)=>
            {
                var data=services.Store.State<PreparedResearchData>("desktop-screen-data:"+snapshot.Id+":"+stock.Key)??throw new InvalidOperationException(stock.Name+": 완료한 종목의 당시 자료를 찾을 수 없습니다.");
                var first=snapshot.From!.Value;var last=snapshot.To!.Value;var result=await services.Backtests.RunAsync(new(data.Dataset,snapshot.Configuration,first,last,data.Facts,snapshot.BarSpecification,data.Bars),token);
                var prices=(data.Bars?.Select(b=>b.Price with{Time=b.OpenTime})??data.Dataset.CompletedBars).Where(b=>DailyDates.Of(b)>=first&&DailyDates.Of(b)<=last).ToArray();return new(result,prices);
            },"스크리닝 당시 설정 사용 · "+snapshot.StrategyName+" · "+(snapshot.BarSpecification?.ToString()??"일봉")+" · "+snapshot.From+" ~ "+snapshot.To);browser.Initialize();browser.Notice+=message=>Notice?.Invoke(message);BrowserRequested?.Invoke(browser);await browser.Go(Array.FindIndex(targets,s=>s.Key==row.Observation.Stock.Key));
        }
        catch(Exception ex) when(ex is not OutOfMemoryException){Notice?.Invoke(ex.Message);}
    }
}
