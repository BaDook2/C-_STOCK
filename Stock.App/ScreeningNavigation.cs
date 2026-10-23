using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
namespace Stock.App;

public sealed record ScreeningChartSeries(string Label,string Period,PriceBar[] Bars,string Source);
public sealed record ScreeningChartTrade(DateTimeOffset Time,bool Buy,decimal Quantity,decimal Price,string Reason,string Source,OversoldExecution? Execution=null,int? TradeNumber=null)
{
    public string TimeText=>Time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss");
    public string Side=>(TradeNumber is {} number?$"#{number} ":"")+(Buy?"매수":"매도");
    public string PriceText=>Price.ToString("N2");
    public string QuantityText=>Quantity.ToString("N0");
    public string Detail=>Execution==null?$"{Side} · {TimeText}\n가격 {Price:N2} · 수량 {Quantity:N0}\n사유 {Reason}\n출처 {Source}":$"{Side} · {TimeText}\n가격 {Price:N2} · 수량 {Quantity:N0} · 평균단가 {Execution.AveragePriceAfter:N2}\n실현손익 {Execution.RealizedPnl:N2} · 사유 {Reason}\n신호 {Execution.SignalTime.ToOffset(TimeSpan.FromHours(9)):yyyy.MM.dd HH:mm:ss}\n"+DecisionSnapshotText.Format(Execution.Snapshot);
}
public sealed record ScreeningSupportZone(DateTimeOffset From,DateTimeOffset To,decimal Lower,decimal Upper,bool UsedForEntry,int Count,string Supports);
public sealed record ScreeningChartSnapshot(StockItem Stock,ScreeningChartSeries[] Series,TradeMarker[] Markers,ScreeningChartTrade[] Trades,ScreeningSupportZone[] SupportZones,string Notice,OversoldResult? Result,OversoldScreeningRun Run)
{
    public ScreeningChartSeries Initial=>Series.First();
}

public sealed partial class WorkspaceViewModel
{
    private bool screeningNavigationBusy;
    /// <summary>스크리닝 당시 범위와 설정으로 독립 차트 자료를 만들며 메인 종목/차트 상태는 변경하지 않는다.</summary>
    public async Task<ScreeningChartSnapshot> LoadScreeningChart(StockItem stock,OversoldScreeningRun? source=null,CancellationToken cancellationToken=default)
    {
        if(stock.Exchange!=Exchange.KRX)throw new InvalidOperationException("국내 종목만 상세 전략 차트를 지원합니다.");
        var run=source??LatestScreening??throw new InvalidOperationException("먼저 스크리닝을 실행하세요.");
        if(!run.Targets.Any(s=>s.Key==stock.Key))throw new InvalidOperationException("이 스크리닝에 포함된 종목이 아닙니다.");
        if(run.From is not {} first||run.To is not {} last)throw new InvalidOperationException("스크리닝 기간이 저장되지 않았습니다.");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,ApiToken,cancellationToken);var token=linked.Token;var facts=run.FactsJson.Length>0?Serialization.Unpack<HistoricalFacts>(run.FactsJson):oversoldFacts.Snapshot();
        var series=new List<ScreeningChartSeries>();var notices=new List<string>();var executions=new List<(OversoldExecution Execution,string Source,int? TradeNumber)>();OversoldResult? backtestResult=null;
        if(run.BarSpecification==null)
        {
            var daily=await provider.History(stock,token);token.ThrowIfCancellationRequested();
            var selectedDaily=daily.Bars.Where(b=>{var day=DailyDates.Of(b);return day>=first&&day<=last;}).ToArray();
            if(selectedDaily.Length==0)throw new InvalidOperationException("스크리닝 기간에 확정된 일봉이 없습니다.");
            series.Add(new("일봉","일봉",selectedDaily,daily.Source));
            try
            {
                backtestResult=await oversoldBacktests.RunAsync(new(daily,run.Configuration,first,last,facts),token);
                foreach(var pair in backtestResult.Trades.Select((trade,index)=>(trade,number:index+1)))executions.AddRange(pair.trade.Executions.Select(e=>(e,"스크리닝 기간 모형체결",(int?)pair.number)));
                if(backtestResult.Trades.Count==0)notices.Add("선택 기간의 전략 매수·매도 체결은 없습니다.");
            }
            catch(InvalidOperationException ex){notices.Add("일봉 전략 체결 계산: "+ex.Message);}
        }
        else
        {
            var specification=run.BarSpecification;
            StrategyBar[] rows;
            try{rows=await ScreeningBars(stock,specification,facts,run.ScreeningTime,token,first,last);}
            catch(InvalidOperationException ex){throw new InvalidOperationException($"스크리닝 당시 {specification} 가격 자료를 불러오지 못했습니다. {ex.Message}",ex);}
            var exact=rows.Where(b=>b.Price.IsComplete&&DailyDates.Of(b.Price)>=first&&DailyDates.Of(b.Price)<=last).OrderBy(b=>b.ClosedAt).ThenBy(b=>b.Sequence).ToArray();
            if(exact.Length==0)throw new InvalidOperationException($"스크리닝 기간 {first:yyyy-MM-dd}~{last:yyyy-MM-dd}에 완성된 {specification}이 없습니다.");
            var exactSource=string.Join(", ",exact.Select(b=>b.Source).Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct());
            series.Add(new(specification.ToString(),specification.ToString(),exact.Select(b=>b.Price).ToArray(),exactSource));
            notices.Add("스크리닝 당시 지정 봉과 기간을 그대로 표시합니다. 지정 봉 스크리닝은 신호 평가이며, 저장된 실제·모의 체결만 함께 표시합니다.");
            try
            {
                var daily=await provider.History(stock,token);var selectedDaily=daily.Bars.Where(b=>DailyDates.Of(b)>=first&&DailyDates.Of(b)<=last).ToArray();
                if(selectedDaily.Length>0)series.Add(new("일봉","일봉",selectedDaily,daily.Source));
            }
            catch(OperationCanceledException){throw;}
            catch(InvalidOperationException ex){notices.Add("보조 일봉: "+ex.Message);}
        }

        foreach(var history in History.Where(h=>h.Stock.Key==stock.Key&&h.OversoldResultId!=null))
        {
            if(store.State<OversoldResult>(history.OversoldResultId!) is not {} saved)continue;
            foreach(var pair in saved.Trades.Select((trade,index)=>(trade,number:index+1)))executions.AddRange(pair.trade.Executions.Select(e=>(e,"저장된 "+e.ExecutionMode,(int?)pair.number)));
        }
        var rangeStart=new DateTimeOffset(first.ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));var rangeEnd=new DateTimeOffset(last.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.FromHours(9));
        var distinct=executions.Where(x=>x.Execution.ExecutionTime>=rangeStart&&x.Execution.ExecutionTime<rangeEnd).GroupBy(x=>(x.Execution.ExecutionTime,x.Execution.Buy,x.Execution.Quantity,x.Execution.Price,x.Execution.Reason)).Select(g=>g.OrderByDescending(x=>x.TradeNumber.HasValue).First()).OrderBy(x=>x.Execution.ExecutionTime).ToArray();
        var markers=distinct.Select((x,i)=>new TradeMarker("screen-detail:"+i,stock.Key,x.Execution.ExecutionTime,x.Execution.Buy,x.Execution.Quantity,x.Execution.Price,x.Source,TradeNumber:x.TradeNumber)).ToList();
        var trades=distinct.Select(x=>new ScreeningChartTrade(x.Execution.ExecutionTime,x.Execution.Buy,x.Execution.Quantity,x.Execution.Price,x.Execution.Reason,x.Source,x.Execution,x.TradeNumber)).ToList();
        foreach(var row in store.States("trade-marker:"))try
        {
            var marker=Serialization.Unpack<TradeMarker>(row.Json);if(marker.StockKey!=stock.Key||marker.Price==null||marker.Time<rangeStart||marker.Time>=rangeEnd)continue;
            if(!markers.Any(m=>m.Time==marker.Time&&m.Buy==marker.Buy&&m.Quantity==marker.Quantity&&m.Price==marker.Price)){markers.Add(marker);trades.Add(new(marker.Time,marker.Buy,marker.Quantity,marker.Price.Value,marker.Source,marker.Source));}
        }catch{/* 손상된 한 행은 다른 기록을 숨기지 않는다. */}
        var markerRows=markers.Where(m=>m.Price.HasValue).GroupBy(m=>(m.Time,m.Buy,m.Quantity,m.Price,m.Source)).Select(g=>g.First()).OrderBy(m=>m.Time).ToArray();
        var zones=(backtestResult?.Trades??[]).SelectMany(t=>t.Executions.Where(e=>e.Buy&&e.Snapshot!=null).SelectMany(e=>e.Snapshot!.Clusters.Select(z=>new ScreeningSupportZone(e.SignalTime,t.LastExecutionDate??new DateTimeOffset(last.ToDateTime(new TimeOnly(15,30)),TimeSpan.FromHours(9)),z.Lower,z.Upper,z.UsedForEntry,z.Count,string.Join(", ",z.Supports.Select(s=>s.Id)))))).ToArray();
        return new(stock,series.ToArray(),markerRows,trades.OrderBy(t=>t.Time).ToArray(),zones,string.Join(" · ",notices.Distinct()),backtestResult,run);
    }
    public async Task OpenScreeningStock(StockItem stock,OversoldScreeningRun? source=null)
    {
        if(screeningNavigationBusy||Busy)return;screeningNavigationBusy=true;
        try
        {
            var run=source??LatestScreening??throw new InvalidOperationException("먼저 스크리닝을 실행하세요.");var snapshot=await LoadScreeningChart(stock,run);var stocks=run.Results.Select(r=>r.Stock).DistinctBy(s=>s.Key).ToArray();ScreeningChartDialog? dialog=null;
            async Task<ScreeningChartSnapshot?> Navigate(int delta,CancellationToken token){var current=Array.FindIndex(stocks,s=>s.Key==dialog!.Snapshot.Stock.Key);if(current<0)return null;var next=Math.Clamp(current+delta,0,stocks.Length-1);return await LoadScreeningChart(stocks[next],run,token);}
            dialog=new ScreeningChartDialog(snapshot,Navigate);
            if(System.Windows.Application.Current?.MainWindow is {} owner&&owner.IsVisible)dialog.Owner=owner;
            dialog.Show();Status=stock.Name+" 상세 차트를 새 창으로 열었습니다.";
        }
        catch(OperationCanceledException){Status="선택 종목 상세 차트 열기가 취소됐습니다.";}
        catch(InvalidOperationException ex){Status="선택 종목 상세 차트를 열 수 없습니다. "+ex.Message;}
        finally{screeningNavigationBusy=false;}
    }
    private IReadOnlyList<TradeMarker> BacktestTradeMarkers()
    {
        var run=SelectedRun;if(!ShowBacktestTradeMarkers||run==null||run.Stock.Key!=selected?.Key||run.Period.StartsWith("정기매수",StringComparison.Ordinal))return [];
        if(run.OversoldResultId is {} id&&store.State<OversoldResult>(id) is {} result)
            return result.Trades.SelectMany((trade,tradeIndex)=>trade.Executions.Select((e,executionIndex)=>new TradeMarker("backtest:"+run.Id+":"+tradeIndex+":"+executionIndex,run.Stock.Key,e.ExecutionTime,e.Buy,e.Quantity,e.Price,"백테스트 체결",TradeNumber:tradeIndex+1))).ToArray();
        return run.Fills.Select((f,i)=>new TradeMarker("backtest:"+run.Id+":"+i,run.Stock.Key,f.Time,f.Side.Contains("매수",StringComparison.Ordinal),f.Quantity,f.Price,"백테스트 체결")).ToArray();
    }
}
