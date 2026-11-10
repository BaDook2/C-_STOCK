using System.Collections.ObjectModel;
using System.Globalization;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.Desktop;
public sealed class WatchPick(StockItem stock,bool selected,Action<StockItem,bool> change):Bindable
{
    public StockItem Stock {get;}=stock;public string Name=>Stock.Name;public string Code=>Stock.Symbol;
    private bool chosen=selected;public bool Chosen {get=>chosen;set{if(Set(ref chosen,value))change(Stock,value);}}
    public void Sync(bool value)=>Set(ref chosen,value,nameof(Chosen));
}
public sealed class VirtualItemRow(StockItem stock,string strategy,decimal budget,Func<bool> editable):Bindable
{
    public StockItem Stock {get;}=stock;public string Name=>Stock.Name;public string Code=>Stock.Symbol;
    private string preset=strategy,money=budget.ToString(CultureInfo.InvariantCulture),status="대기";
    public string Strategy {get=>preset;set{if(value!=null&&editable())Set(ref preset,value);}}
    public string Budget {get=>money;set{if(editable())Set(ref money,value);}}
    public string Status {get=>status;private set=>Set(ref status,value);}
    public void Update(string value)=>Status=value;
}
/// <summary>Watchlist editor only. The same app-owned realtime coordinator owns all virtual sessions.</summary>
public sealed class VirtualExecutionWorkspace:Bindable
{
    private readonly StrategyEditor strategies;
    private readonly RealtimeScreeningWorkspace realtime;
    private readonly Func<string> facts;
    private readonly LiveStrategyController? live;
    private readonly DayTradingViewModel? crash;
    private readonly AsyncCommand stop;
    private WatchGroupViewModel? group;
    private VirtualItemRow? selected;
    public ObservableCollection<WatchGroupViewModel> Groups {get;}
    public ObservableCollection<WatchPick> Picks {get;}=[];
    public ObservableCollection<VirtualItemRow> Items {get;}=[];
    public ObservableCollection<string> Names=>strategies.Names;
    public ScreeningWorkspace Analysis {get;}
    public RealtimeScreeningWorkspace Realtime=>realtime;
    public LiveStrategyController? Actual=>live;
    public bool IsActual=>live!=null;
    public bool IsVirtual=>live==null;
    public string WindowTitle=>IsActual?"실제 계좌 전략 실행":"가상 계좌 전략 실행";
    public string Heading=>IsActual?"관심종목으로 실제 전략 실행":"관심종목으로 가상 전략 실행";
    public string Explanation=>IsActual?"선택 계좌의 실제 주문입니다. 명시 활성화와 시작 확인 후 실행하며 팝업을 닫아도 유지됩니다. 검증된 종목·위험·거래대금 자료가 필요합니다.":"관심그룹에서 체크한 종목은 한 번만 등록됩니다. 팝업을 닫아도 앱의 공통 가상 실행은 유지됩니다. 실제 주문은 전송하지 않습니다.";
    public string StartLabel=>IsActual?"실시간 실제매매 시작":"실시간 모의매매 시작";
    public string StopLabel=>IsActual?"실제 전략 전체 중지":"신규 진입 중지 · 보유 보호 유지";
    public string TotalBudget {get=>live?.TotalBudget??realtime.TotalBudget;set{if(live!=null)live.TotalBudget=value;else realtime.TotalBudget=value;}}
    public string DailyLossLimit {get=>live?.DailyLossLimit??realtime.DailyLossLimit;set{if(live!=null)live.DailyLossLimit=value;else realtime.DailyLossLimit=value;}}
    public AsyncCommand Stop=>stop;
    public WatchGroupViewModel? Group {get=>group;set{if(Set(ref group,value))RefreshPicks();}}
    public VirtualItemRow? Selected {get=>selected;set{Set(ref selected,value);Remove.Refresh();}}
    public bool Editable=>live!=null?!live.Locked:!realtime.Busy&&!realtime.Running&&!realtime.HasProtectedPositions&&crash?.Running!=true&&crash?.HasUnsettled!=true;
    public string Status=>live?.Status??(crash?.Running==true?crash.Message:realtime.Status);
    public AsyncCommand AddCurrent {get;}
    public AsyncCommand Remove {get;}
    public AsyncCommand Start {get;}
    public event Action<string>? Notice;
    public VirtualExecutionWorkspace(StrategyEditor strategies,RealtimeScreeningWorkspace realtime,ScreeningWorkspace analysis,ObservableCollection<WatchGroupViewModel> groups,Func<StockItem?> current,Func<string> facts,LiveStrategyController? live=null,DayTradingViewModel? crash=null)
    {
        this.strategies=strategies;this.realtime=realtime;this.facts=facts;this.live=live;this.crash=crash;stop=live?.StopCommand??new AsyncCommand(()=>{if(crash?.Running==true)crash.BlockNewEntries("신규 진입 중지 · 급락 판단 전략 보유 보호 유지");realtime.Stop.Execute(null);return Task.CompletedTask;});Groups=groups;Analysis=analysis;
        AddCurrent=new(()=>{if(current() is {} stock)Pick(stock,true);return Task.CompletedTask;},()=>Editable);Remove=new(()=>{if(Selected is {} item)Pick(item.Stock,false);return Task.CompletedTask;},()=>Editable&&Selected!=null);Start=new(Execute,()=>Editable&&Items.Count>0);
        realtime.PropertyChanged+=(_,e)=>{if(e.PropertyName is nameof(RealtimeScreeningWorkspace.Status) or nameof(RealtimeScreeningWorkspace.Busy))Refresh();};Group=groups.FirstOrDefault();
        if(crash!=null)crash.PropertyChanged+=(_,_)=>Refresh();
        if(live!=null)live.PropertyChanged+=(_,_)=>Refresh();
        if(live==null)realtime.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(RealtimeScreeningWorkspace.RecoveredPlan)&&realtime.RecoveredPlan is {} plan){Items.Clear();foreach(var item in plan.Items)Items.Add(new(item.Stock,item.Preset,item.Budget,()=>Editable));var spec=plan.Items[0].Oversold!.BarSpecification;Analysis.Daily=spec.Unit==BarUnit.Day;if(spec.Unit!=BarUnit.Day){Analysis.Unit=spec.Unit==BarUnit.Hour?"시간":"분";Analysis.Value=spec.Value.ToString();}RefreshPicks();Refresh();}};
    }
    private void RefreshPicks(){Picks.Clear();foreach(var stock in Group?.Stocks??[])Picks.Add(new(stock,Items.Any(r=>r.Stock.Key==stock.Key),Pick));}
    private void Pick(StockItem stock,bool chosen)
    {
        if(!Editable){RefreshPicks();return;}var old=Items.FirstOrDefault(r=>r.Stock.Key==stock.Key);if(chosen&&old==null){if(stock.Exchange!=Exchange.KRX){Notice?.Invoke("현재 실시간 가상 엔진은 국내 종목만 지원합니다.");RefreshPicks();return;}var name=Names.First();Items.Add(new(stock,name,strategies.Registered(name).PlannedInvestment,()=>Editable));}else if(!chosen&&old!=null)Items.Remove(old);
        foreach(var pick in Picks)pick.Sync(Items.Any(r=>r.Stock.Key==pick.Stock.Key));Start.Refresh();
    }
    public void Refresh(){if((Group?.Stocks.Count??0)!=Picks.Count||Picks.Any(p=>Group?.Stocks.Any(s=>s.Key==p.Stock.Key)!=true))RefreshPicks();var states=live?.SessionStatuses()??(crash?.Running==true?crash.LiveRows.ToDictionary(r=>r.Symbol,r=>r.State+(r.Error.Length>0?" · "+r.Error:"")):realtime.SessionStatuses());foreach(var item in Items)item.Update(states.GetValueOrDefault(item.Stock.Key,"대기"));Changed(nameof(Editable));Changed(nameof(Status));AddCurrent.Refresh();Remove.Refresh();Start.Refresh();}
    private async Task Execute()
    {
        try
        {
            if(Items.Count is <1 or >20)throw new InvalidOperationException("실시간 실행은1~20종목을 선택하세요.");
            var plans=Items.Select(row=>new RealtimeItemRequest(row.Stock,row.Strategy,strategies.Registered(row.Strategy),decimal.Parse(row.Budget,NumberStyles.Number,CultureInfo.InvariantCulture))).ToArray();var spec=Analysis.Daily?new BarSpecification(1,BarUnit.Day):new BarSpecification(int.Parse(Analysis.Value),Analysis.Unit=="분"?BarUnit.Minute:BarUnit.Hour);
            var request=new RealtimeRequest(plans.Select(p=>p.Stock).ToArray(),"관심종목 다량 가상 실행",plans[0].Configuration,spec,Serialization.Unpack<HistoricalFacts>(facts()),realtime.PaperAssumptions,decimal.Parse(realtime.TotalBudget,NumberStyles.Number,CultureInfo.InvariantCulture),decimal.Parse(realtime.DailyLossLimit,NumberStyles.Number,CultureInfo.InvariantCulture)){ItemConfigurations=plans};
            if(live!=null)await live.Start(request with{Facts=Serialization.Unpack<HistoricalFacts>(live.FactsJson),PaperAssumptions=false,TotalBudget=decimal.Parse(TotalBudget,NumberStyles.Number,CultureInfo.InvariantCulture),DailyLossLimit=decimal.Parse(DailyLossLimit,NumberStyles.Number,CultureInfo.InvariantCulture)});else if(crash!=null&&plans.Any(p=>p.Configuration.UseCrashStrategy))await crash.StartConfigured(request);else await realtime.StartConfigured(request);Refresh();
        }
        catch(OperationCanceledException){Notice?.Invoke("가상 실행 준비를 취소했습니다.");}
        catch(Exception error) when(error is not OutOfMemoryException){Notice?.Invoke(error.Message);}
    }
}
public sealed partial class MainViewModel
{
    public VirtualExecutionWorkspace VirtualExecution {get;private set;}=null!;
    public VirtualExecutionWorkspace ActualExecution {get;private set;}=null!;
    public LiveStrategyController ActualTrading {get;private set;}=null!;
    public event Action<VirtualExecutionWorkspace>? VirtualPopupRequested;
    public AsyncCommand OpenVirtualExecution {get;private set;}=null!;
    public AsyncCommand OpenActualExecution {get;private set;}=null!;
    public AsyncCommand StopAllStrategies {get;private set;}=null!;
    private void InitializeVirtual(){VirtualExecution=new(Strategies,Realtime,Screening,WatchGroups,()=>SelectedStock,()=>Research.FactsJson,crash:DayTrading);VirtualExecution.Notice+=Notify;OpenVirtualExecution=new(()=>{VirtualExecution.Refresh();VirtualPopupRequested?.Invoke(VirtualExecution);return Task.CompletedTask;});ActualTrading=new(services,Accounts,HistoryService);Accounts.BeforeSessionRevoke=ActualTrading.StopForCredentialReset;ActualTrading.Notice+=Notify;ActualTrading.FillsChanged+=RefreshPaperMarkers;ActualExecution=new(Strategies,Realtime,Screening,WatchGroups,()=>SelectedStock,()=>Research.FactsJson,ActualTrading);ActualExecution.Notice+=Notify;OpenActualExecution=new(()=>{ActualExecution.Refresh();VirtualPopupRequested?.Invoke(ActualExecution);return Task.CompletedTask;});StopAllStrategies=new(async()=>{Realtime.StopAll.Execute(null);if(DayTrading.Running)DayTrading.BlockNewEntries("전체 전략 신규 진입 중지 · 보유 보호 유지");await ActualTrading.Stop();});}
}
