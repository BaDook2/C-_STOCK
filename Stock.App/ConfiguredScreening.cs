using Stock.Core;
using Stock.Core.Pipeline;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    public string ScreeningExplanation=>"일봉은 공개 가격·거래량을 평가합니다. 온라인 분·시간봉은 연결 증권사의 공식 1분봉을 집계하며 거래대금은 OHLC 평균×거래량 추정임을 결과에 표시합니다. 틱봉은 검증된 연속 원본이 필요합니다. 상태·위험 미확인은 후보에 명시하며 자동주문하지 않습니다."; private string LegacyScreeningExplanation=>UseConfiguredStrategy?"선택한 Standard / Advanced 전략을 저장된 실제 체결·호가의 마지막 시점에 적용합니다. 데이터가 없는 종목은 조회 제외로 표시합니다.":"기존 일봉 전략 조건을 최근 확정 일봉에 적용합니다. 차트 표시 체크는 매수 조건을 바꾸지 않습니다.";
    private async Task ScreenConfigured(IReadOnlyList<StockItem> items,string source)
    {
        if(From==null||To==null||From>To)throw new InvalidOperationException("차트의 시작·종료 기간을 확인하세요.");
        var snapshot=ConfiguredEngineSettings();FlushCapturedTicks();var start=new DateTimeOffset(From.Value.Date,TimeSpan.FromHours(9));var end=new DateTimeOffset(To.Value.Date.AddDays(1),TimeSpan.FromHours(9)).AddTicks(-1);int done=0,missing=0;
        if(snapshot.Pipeline!=null){await ScreenPipeline(items,snapshot,source);return;}
        foreach(var item in items)
        {
            Token.ThrowIfCancellationRequested();var data=store.ReadEvents(item.Key,"KIWOOM",start,end);
            if(data.Count==0){missing++;}
            else
            {
                var cancellation=Token;
                var row=await Task.Run(()=>
                {
                    var context=new MarketContext(new(item.Symbol,item.Exchange),snapshot);ITradingStrategy inner=snapshot.Advanced!.StrategyType switch{StrategyType.Scalping=>new ScalpingStrategy(),StrategyType.Box=>new BoxStrategy(),_=>new ReboundStrategy()};var strategy=new AdvancedStrategyGate(inner);StrategySignal? last=null;var scanner=new CommonScanner(snapshot);var quality=new DataQualityGate(snapshot.Data,snapshot.Execution);
                    foreach(var e in data){cancellation.ThrowIfCancellationRequested();if(!quality.Accept(e,false,out _))continue;context.Add(e);if(e.Kind==EventKind.Trade)last=scanner.Reject(context)==null?strategy.Evaluate(context):null;}
                    return last?.Buy==true&&context.Last!=null?new ScreeningRow(item,context.Price,context.Last.PreviousClose>0?(context.Price/context.Last.PreviousClose.Value-1)*100:0,last.Reason,context.Last.ExchangeTime.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy.MM.dd HH:mm:ss")):null;
                },cancellation);
                if(row!=null)Matches.Add(row);
            }
            ScreenInfo=$"{++done:N0}/{items.Count:N0}종목 · 조건 일치 {Matches.Count:N0} · 실제 체결 데이터 없음 {missing:N0} · {source}";
        }
        Log("저장된 실제 체결의 마지막 시점으로 스크리닝을 마쳤습니다.");
    }
}
