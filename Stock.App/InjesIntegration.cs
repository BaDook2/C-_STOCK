using System.Globalization;
using System.Windows.Threading;
using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Strategies.Injes;
using Stock.Core.Trading;

namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private InjesStreamService? injesStream;private DispatcherTimer? injesTimer;
    private StockItem[] injesTargets=[];
    private InjesStreamService InjesStream
    {
        get
        {
            if(injesStream!=null)return injesStream;injesStream=new(store,multiClock);
            injesStream.FillConfirmed+=fill=>dispatcher.BeginInvoke(()=>
            {
                var id="inje:"+injesStream.RunId+":"+fill.OrderId+":"+fill.Time.UtcTicks+":"+fill.Quantity;
                store.State("trade-marker:"+id,new TradeMarker(id,fill.Instrument.Key,fill.Time,fill.Buy,fill.Quantity,fill.Price,"Inje 가상",Reason:fill.Reason,RealizedPnl:fill.Buy?null:fill.RealizedPnl,AccountScope:"inje-paper:"+injesStream.RunId));ReloadTradeMarkers();
            });
            injesTimer=new(){Interval=TimeSpan.FromSeconds(1)};injesTimer.Tick+=(_,_)=>RefreshInjesScreen();injesTimer.Start();return injesStream;
        }
    }
    private Task StartInjesMulti()
    {
        var selections=MultiTrading.SelectedStrategies();if(selections.Length is <1 or >20)throw new InvalidOperationException("미국 감시 종목1~20개를 선택하세요.");
        var definitions=selections.Select(s=>InjesPresetStore.Require(LoadMultiOversoldPreset(s.Preset))).ToArray();EnsureSingleInjes(definitions);
        var definition=definitions[0];
        var selectedRows=MultiTrading.Rows.Where(r=>r.Selected).ToArray();if(selectedRows.Any(r=>InjesMoney(r.Budget)<definition.CommonParameters.OrderBudget)||selectedRows.Sum(r=>InjesMoney(r.Budget))>InjesMoney(MultiTrading.TotalBudget))throw new InvalidOperationException("종목 예산과 전체 USD 한도를 확인하세요.");
        definition.InitialCash=InjesMoney(MultiTrading.TotalBudget);definition.CommonParameters.MaxDailyLossCash=Math.Min(definition.CommonParameters.MaxDailyLossCash,InjesMoney(MultiTrading.DailyLoss));definition.ExecutionPolicy.MaximumExposureCash=Math.Min(definition.ExecutionPolicy.MaximumExposureCash,definition.InitialCash);
        var resumed=StartInjes(definition,selections.Select(s=>s.Stock).ToArray());if(!resumed)InjesStream.SetEntriesEnabled(true);return Task.CompletedTask;
    }
    private async Task StartInjesScreen()
    {
        var definition=InjesPresetStore.Require(LoadMultiOversoldPreset(SelectedOversoldPreset!));
        StockItem[] targets;
        if(ScreenWatchlist)targets=SelectedScreeningStocks().ToArray();
        else{if(Catalog.Count==0||catalogRegion!=ScreeningMarketSelection)await LoadCatalog();targets=Catalog.Where(s=>MatchesScreeningMarket(s)&&(Sector=="전체"||s.Sector==Sector)).DistinctBy(s=>s.Key).ToArray();}
        var resumed=StartInjes(definition,targets);if(!resumed)InjesStream.SetEntriesEnabled(RealtimeAutoTrade);
    }
    private bool StartInjes(InjesStrategyDefinition definition,StockItem[] targets)
    {
        if(targets.Length is <1 or >20||targets.Any(s=>s.Currency!="USD"))throw new InvalidOperationException("Inje는 미국 종목1~20개를 동일 전략으로 감시합니다.");
        if(!IsOnline||broker is not KiwoomBroker kiwoom)throw new InvalidOperationException("MissingData: 미국 원본 틱·호가 피드 연결이 필요합니다. 일봉/현재가 폴링으로 대체하지 않습니다.");
        var instruments=targets.Select(s=>new Instrument(s.Symbol,s.Exchange)).ToArray();
        var feed=multiFeedFactory?.Invoke(broker,instruments,new(),_=>{})??new KiwoomMarketDataProvider(kiwoom.GetTokenAsync,instruments,new(),_=>{},live:broker.IsLive);
        if(InjesStream.Session is {} restored&&!InjesStream.Running&&InjesStream.HasUnsettled)
        {EnsureSingleInjes([definition,restored.Definition]);if(!instruments.Select(i=>i.Key).Order().SequenceEqual(restored.Capture().Rows.Select(r=>r.Symbol).Order()))throw new InvalidOperationException("보관된 계좌와 같은 종목으로 보호 재개하세요.");InjesStream.ResumeProtection(feed);injesTargets=targets;RefreshInjesScreen();return true;}
        InjesStream.Start(definition,instruments,feed);injesTargets=targets;RealtimeScreeningRows.Clear();RefreshInjesScreen();return false;
    }
    private static decimal InjesMoney(string text)=>decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)&&n>0?n:throw new InvalidOperationException("양의 USD 금액을 입력하세요.");
    private static void EnsureSingleInjes(InjesStrategyDefinition[] definitions)
    {
        string Key(InjesStrategyDefinition d)=>Serialization.Pack(new{d.FamilyId,d.StrategyId,d.StrategyMode,d.CommonParameters,d.StrategyParameters,d.StandardParameters,d.ExecutionPolicy});
        if(definitions.Select(Key).Distinct().Count()!=1)throw new InvalidOperationException("한 실행 세션은 동일한 Inje 전략·파라미터 하나만 사용해야 합니다.");
    }
    private void RefreshInjesScreen()
    {
        if(injesStream==null)return;RealtimeScreenInfo=injesStream.Status;RealtimeScreenConnection="원본 미국 틱/호가 · 실제 주문 없음";
        var snapshot=injesStream.Snapshot();
        if(snapshot!=null&&realtimeScreenAutoTrade!=snapshot.EntriesEnabled){realtimeScreenAutoTrade=snapshot.EntriesEnabled;Changed(nameof(RealtimeAutoTrade));}
        foreach(var stock in injesTargets)
        {
            var result=snapshot?.Evaluations.FirstOrDefault(e=>e.Signal.Symbol==stock.Key);var signal=result?.Signal;var execution=snapshot?.Ledger;
            var row=new RealtimeScreeningDisplayRow(stock.Symbol,stock.Name,signal?.ReferencePrice?.ToString("0.####",CultureInfo.InvariantCulture)??"—","—","—",$"{signal?.Conditions.Count(c=>c.Outcome==EvaluationOutcome.Passed)??0}/{signal?.Conditions.Length??0}",string.Join(" · ",signal?.BlockReasons??[]),result?.State.ToString()??"자료 대기",signal?.SignalTime.ToString("HH:mm:ss.fff")??"—",signal?.SignalReason??"필수 자료 수신 대기",$"보유 {execution?.Positions.FirstOrDefault(p=>p.Instrument.Key==stock.Key)?.Quantity??0} / 미체결 {execution?.Orders.Count(o=>o.Active&&o.Symbol==stock.Key)??0}","");
            var old=RealtimeScreeningRows.FirstOrDefault(r=>r.Code==stock.Symbol);if(old==null)RealtimeScreeningRows.Add(row);else old.Update(row);
        }
        if(multiTrading!=null)multiTrading.Apply(new("Inje USD 가상",injesStream.Running,injesStream.Status,injesTargets.Select(s=>
        {var result=snapshot?.Evaluations.FirstOrDefault(e=>e.Signal.Symbol==s.Key);var ledger=snapshot?.Ledger;var pos=ledger?.Positions.FirstOrDefault(p=>p.Instrument.Key==s.Key);return new LiveBatchRowStatus(s.Key,s.Name,snapshot?.Definition.StrategyName??"Inje",injesStream.Running,result?.Signal.SignalReason??"자료 대기",result?.Signal.SignalType.ToString()??"Hold",pos?.Quantity??0,ledger?.Orders.Where(o=>o.Active&&o.Symbol==s.Key).Sum(o=>o.Remaining)??0,pos?.Unrealized??0,"");}).ToArray()));
        NotifyRealtimeState();
    }
    private StrategyBar[] InjesChartBars(string key,DateTimeOffset? asOf=null)
    {
        var now=asOf??multiClock();return (injesStream?.Trades(key)??[]).Where(e=>e.ExchangeTime<=now&&(e.AvailableAt??e.ReceivedTime)<=now).GroupBy(e=>e.ExchangeTime.UtcTicks/TimeSpan.TicksPerMinute).Select((group,index)=>
        {
            var ordered=group.OrderBy(e=>e.ExchangeTime).ToArray();var open=new DateTimeOffset(group.Key*TimeSpan.TicksPerMinute,TimeSpan.Zero);var close=open.AddMinutes(1);
            return new StrategyBar(new(open,ordered[0].TradePrice!.Value,ordered.Max(e=>e.TradePrice!.Value),ordered.Min(e=>e.TradePrice!.Value),ordered[^1].TradePrice!.Value,ordered.Sum(e=>e.TradeSize!.Value),close<=now),open,close,index+1,null,"원본 체결의 표시용 집계");
        }).ToArray();
    }
    private OversoldScreeningRun? StoreInjesScreeningHistory(string reason)
    {
        var snapshot=injesStream?.Snapshot();if(snapshot==null||injesTargets.Length==0)return null;var now=multiClock();var date=UsSessionClock.MarketDate(now);
        var run=OversoldScreeningRun.Start(injesTargets,InjesPresetStore.Wrap(snapshot.Definition),new(1,BarUnit.Minute),now,date,date,snapshot.Definition.StrategyName) with{Status="Inje 실시간 판단 · "+reason};
        foreach(var stock in injesTargets)
        {
            var evaluation=snapshot.Evaluations.FirstOrDefault(e=>e.Signal.Symbol==stock.Key);var signal=evaluation?.Signal;var bars=InjesChartBars(stock.Key,now);
            if(bars.Length>0){var hash=TradingStore.Hash(Serialization.Pack(bars));store.State("screen-data:"+hash,bars);store.State("screen-bars:"+run.Id+":"+stock.Key+":"+run.BarSpecification,hash);}
            run=run.Append(new(stock,signal?.SignalTime,signal?.ReferencePrice,signal?.Metrics.GetValueOrDefault("dayGainPercent"),null,signal?.Metrics.GetValueOrDefault("rvol"),signal?.SupportCluster?.SupportCount??0,signal is {SignalType:SignalType.Enter,RiskApproved:true},signal?.SignalReason??"MissingData: 원본 이벤트 대기","",bars.FirstOrDefault()?.OpenTime,bars.LastOrDefault()?.ClosedAt){InjesEvaluation=evaluation});
        }
        store.State(run.Id,run);return run;
    }
}
