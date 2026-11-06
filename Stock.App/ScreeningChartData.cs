using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private ScreeningChartViewModel CreateScreeningChartViewModel(ScreeningChartSnapshot snapshot)=>new(snapshot,LoadScreeningChartBase,
        ()=>CanReadScreeningMinutes(snapshot.Stock),ReadScreeningChartQuote,alert,multiClock);

    private bool CanReadScreeningMinutes(StockItem stock)=>!disposed&&!apiPaused&&(IsOffline
        ?stock.Exchange==Exchange.KRX&&provider is IPublicMinuteMarketData
        :broker is IMinuteHistoryBroker&&broker.Exchange==stock.Exchange);

    /// <summary>Display-only daily/minute sources share the existing providers and preserve the screening interval.</summary>
    private async Task<ScreeningChartSeries> LoadScreeningChartBase(ScreeningChartSnapshot snapshot,bool minute,CancellationToken cancellation)
    {
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,ApiToken,cancellation);linked.CancelAfter(TimeSpan.FromSeconds(30));
        var token=linked.Token;var stock=snapshot.Stock;
        var first=snapshot.Run.From??throw new InvalidOperationException("스크리닝 시작일이 없습니다.");
        var last=snapshot.Run.To??throw new InvalidOperationException("스크리닝 종료일이 없습니다.");
        PriceBar[] rows;string origin;
        if(!minute){var daily=await provider.History(stock,token);rows=daily.Bars;origin=daily.Source;}
        else if(IsOffline&&stock.Exchange==Exchange.KRX&&provider is IPublicMinuteMarketData publicMinutes)
        {rows=await publicMinutes.MinuteHistory(stock,first,last,token);origin="네이버 KRX 공개 1분봉";}
        else if(IsOnline&&broker is IMinuteHistoryBroker minutes&&broker.Exchange==stock.Exchange)
        {
            origin=broker.Name+" 공식 1분봉";
            rows=minutes is IRangedMinuteHistoryBroker ranged?await ranged.MinuteHistory(stock.Symbol,first,last,token):await minutes.MinuteHistory(stock.Symbol,token);
        }
        else throw new InvalidOperationException("선택한 종목의 분봉을 제공하는 시세 API가 없습니다.");
        token.ThrowIfCancellationRequested();
        rows=await Task.Run(()=>rows.Where(b=>DailyDates.Of(b)>=first&&DailyDates.Of(b)<=last).OrderBy(b=>b.Time).DistinctBy(b=>b.Time).ToArray(),token);
        if(rows.Length==0)throw new InvalidOperationException($"{first:yyyy-MM-dd} ~ {last:yyyy-MM-dd}에 표시할 {(minute?"분봉":"일봉")} 자료가 없습니다.");
        return new(minute?"1분":"일봉",minute?"1분":"일봉",rows,origin);
    }

    private ScreeningChartQuote? ReadScreeningChartQuote(string key)
    {
        var row=lastRealtimeScreeningSnapshot?.Rows.FirstOrDefault(r=>r.Key==key);
        return row?.Price is >0?new(key,row.Price.Value,row.ChangePercent,row.LastQuoteTime,row.DataState,realtimeScreening?.Running==true):null;
    }
}
