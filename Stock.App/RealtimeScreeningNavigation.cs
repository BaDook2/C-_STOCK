using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Capture completed bars and confirmed fills from the running research session, without starting another strategy or placing orders.</summary>
    public Task<ScreeningChartSnapshot> LoadRealtimeScreeningChart(string code,CancellationToken token=default)=>Task.Run(async()=>
    {
        token.ThrowIfCancellationRequested();
        var data=RealtimeScreening.ChartData("KRX:"+code);
        if(data.Bars.Length==0)throw new InvalidOperationException("이 종목의 완성된 지정 봉이 아직 없습니다.");
        var bars=data.Bars.OrderBy(b=>b.ClosedAt).ThenBy(b=>b.Sequence).ToArray();
        var now=multiClock();var first=DailyDates.Of(bars[0].Price);var last=DailyQuoteMerger.Date(now,Exchange.KRX);
        var targets=RealtimeScreening.Targets;
        var run=OversoldScreeningRun.Start(targets,data.Configuration,data.Specification,now,first,last,data.StrategyName) with{FactsJson=Serialization.Pack(data.Facts),Status="실시간 감시 스냅샷"};
        var from=new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));
        var markers=new List<TradeMarker>();
        var savedMarkers=ChartTradeHistory.Read(()=>store.States("trade-marker:"),out var historyUnavailable);
        foreach(var row in savedMarkers)
        {
            try{var marker=Serialization.Unpack<TradeMarker>(row.Json);if(marker.StockKey==data.Stock.Key&&marker.Price.HasValue&&marker.Time>=from&&marker.Time<=now)markers.Add(marker);}
            catch(System.Text.Json.JsonException){}
        }
        var trades=markers.OrderBy(m=>m.Time).Select(m=>new ScreeningChartTrade(m.Time,m.Buy,m.Quantity,m.Price!.Value,m.Reason,m.Source,TradeNumber:m.TradeNumber,RealizedPnl:m.RealizedPnl)).ToList();
        var series=new ScreeningChartSeries(data.Specification.ToString(),data.Specification.ToString(),bars.Select(b=>b.Price with{Time=b.OpenTime}).ToArray(),string.Join(", ",bars.Select(b=>b.Source).Distinct()));
        var notices=new List<string>{"창을 연 시점의 완성 봉과 확인된 실제·가상 체결을 표시합니다. 같은 전략의 과거 백테스트 모형체결은 출처로 구분하며 실제 주문/체결로 기록하지 않습니다."};
        if(historyUnavailable)notices.Add("저장 DB 읽기 오류로 실제·가상 체결 기록을 읽지 못했습니다. 해당 기록은 표시하지 않으며 기존 데이터는 보존했습니다. 테스트 기록의 저장소 점검으로 진단을 확인하세요. 아래 모형체결은 실제 거래 기록이 아닙니다.");
        OversoldResult? result=null;
        try
        {
            var dataset=new BarDataset(data.Stock,bars.Select(b=>b.Price).ToArray(),series.Source,now);
            result=await oversoldBacktests.RunAsync(new(dataset,data.Configuration,first,last,data.Facts,data.Specification,bars),token);
            foreach(var pair in result.Trades.Select((trade,index)=>(trade,number:index+1)))
                foreach(var fill in pair.trade.Executions)
                {
                    const string source="실시간 전략 검토 · 백테스트 모형체결";
                    markers.Add(new("realtime-backtest:"+result.Id+":"+markers.Count,data.Stock.Key,fill.ExecutionTime,fill.Buy,fill.Quantity,fill.Price,source,TradeNumber:pair.number,Reason:fill.Reason,RealizedPnl:fill.Buy?null:fill.RealizedPnl));
                    trades.Add(new(fill.ExecutionTime,fill.Buy,fill.Quantity,fill.Price,fill.Reason,source,fill,pair.number));
                }
        }
        catch(OperationCanceledException){throw;}
        catch(InvalidOperationException ex){notices.Add("과거 모형체결 계산: "+ex.Message);}
        var zones=(result?.Trades??[]).SelectMany(t=>t.Executions.Where(e=>e.Buy&&e.Snapshot!=null).SelectMany(e=>e.Snapshot!.Clusters.Select(z=>new ScreeningSupportZone(e.SignalTime,t.LastExecutionDate??now,z.Lower,z.Upper,z.UsedForEntry,z.Count,string.Join(", ",z.Supports.Select(s=>s.Id)))))).ToArray();
        return new ScreeningChartSnapshot(data.Stock,[series],markers.ToArray(),trades.OrderBy(t=>t.Time).ToArray(),zones,string.Join(" · ",notices),result,run);
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
