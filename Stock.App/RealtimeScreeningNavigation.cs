using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Capture completed bars and confirmed fills from the running research session, without starting another strategy or placing orders.</summary>
    public Task<ScreeningChartSnapshot> LoadRealtimeScreeningChart(string code,CancellationToken token=default)=>Task.Run(()=>
    {
        token.ThrowIfCancellationRequested();var stream=injesStream??throw new InvalidOperationException("Inje 감시 세션이 없습니다.");var snapshot=stream.Snapshot()??throw new InvalidOperationException("Inje 원장이 없습니다.");
        var stock=injesTargets.FirstOrDefault(s=>s.Symbol==code)??throw new InvalidOperationException("감시 종목이 없습니다.");var bars=InjesChartBars(stock.Key);
        if(bars.Length==0)throw new InvalidOperationException("MissingData: 표시할 원본 체결이 아직 없습니다.");
        var now=multiClock();var first=Stock.Core.Strategies.Injes.UsSessionClock.MarketDate(bars[0].OpenTime);var last=Stock.Core.Strategies.Injes.UsSessionClock.MarketDate(now);
        var run=OversoldScreeningRun.Start(injesTargets,Stock.Core.Strategies.Injes.InjesPresetStore.Wrap(snapshot.Definition),null,now,first,last,snapshot.Definition.StrategyName) with{Status="실시간 감시 스냅샷"};
        var journal=store.State<Stock.Core.Strategies.Injes.InjesSessionJournal>("inje-live:"+stream.RunId);
        var fills=journal?.Ledger.Fills.Where(f=>f.Instrument.Key==stock.Key).ToArray()??[];
        var markers=fills.Select(f=>new TradeMarker("inje:"+stream.RunId+":"+f.OrderId+":"+f.Time.UtcTicks+":"+f.Quantity,stock.Key,f.Time,f.Buy,f.Quantity,f.Price,"Inje 가상",Reason:f.Reason,RealizedPnl:f.Buy?null:f.RealizedPnl,AccountScope:"inje-paper:"+stream.RunId)).ToArray();
        var trades=fills.Select(f=>new ScreeningChartTrade(f.Time,f.Buy,f.Quantity,f.Price,f.Reason,"Inje 가상",RealizedPnl:f.Buy?null:f.RealizedPnl){InjesEvidence=journal?.Checkpoint?.OrderEvidence.GetValueOrDefault(f.OrderId)}).ToArray();
        var zones=trades.Where(t=>t.Buy&&t.InjesEvidence?.SupportCluster!=null).Select(t=>{var c=t.InjesEvidence!.SupportCluster!;return new ScreeningSupportZone(t.InjesEvidence.SignalTime,now,c.ClusterLower,c.ClusterUpper,true,c.SupportCount,string.Join(", ",c.SupportIds));}).ToArray();
        return new ScreeningChartSnapshot(stock,[new("1분","1분",bars.Select(b=>b.Price with{Time=b.OpenTime}).ToArray(),"원본 체결 표시용 집계 · 판단과 독립")],markers,trades,zones,"원본 틱 판단 · 확인된 가상 체결과 진입 당시 근거 · 실제 주문 없음",null,run);
    },token);

    public async Task OpenRealtimeScreeningStock(RealtimeScreeningDisplayRow row)
    {
        if(screeningNavigationBusy||Busy)return;screeningNavigationBusy=true;
        try
        {
            var codes=RealtimeScreeningRows.Select(r=>r.Code).ToArray();var snapshot=await LoadRealtimeScreeningChart(row.Code);ScreeningChartDialog? dialog=null;
            Task<ScreeningChartSnapshot?> Navigate(int delta,CancellationToken token)
            {
                var current=Array.IndexOf(codes,dialog!.Snapshot.Stock.Symbol);var next=Math.Clamp(current+delta,0,codes.Length-1);
                return Load(codes[next],token);
                async Task<ScreeningChartSnapshot?> Load(string code,CancellationToken cancellation)=>await LoadRealtimeScreeningChart(code,cancellation);
            }
            dialog=new(snapshot,Navigate,CreateScreeningChartViewModel(snapshot));
            if(System.Windows.Application.Current?.MainWindow is {} owner&&owner.IsVisible)dialog.Owner=owner;
            dialog.Show();Status=row.Name+" 실시간 전략 차트를 새 창으로 열었습니다.";
        }
        catch(OperationCanceledException){}
        catch(InvalidOperationException ex){alert("실시간 종목 차트를 열 수 없습니다. "+ex.Message);}
        catch(Exception){alert("실시간 차트 자료를 읽지 못했습니다. 저장소 또는 자료 형식 오류를 확인하세요. 프로그램과 기존 데이터는 유지됩니다.");}
        finally{screeningNavigationBusy=false;}
    }
}
