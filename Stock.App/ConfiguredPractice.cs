using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    private TradingEngine? configuredPaper;
    private MarketEvent[] practiceEvents=[];
    private int practiceEventIndex=-1;
    private readonly List<PriceBar> practiceTrades=[];
    private bool HasConfiguredPractice=>configuredPaper!=null;
    private bool HasNextPractice=>configuredPaper!=null?practiceEventIndex<practiceEvents.Length-1:paper!=null&&replayIndex>=0&&replayIndex<rangeBars.Length-1;
    private bool PaperPending=>configuredPaper?.Broker.Orders.Any(o=>o.Active)??paper?.HasPending==true;
    private int PaperQuantity=>configuredPaper?.Broker.Positions.Values.Sum(p=>p.Quantity)??paper?.Quantity??0;
    public string NextStepCaption=>HasConfiguredPractice?"다음 체결":"다음 거래일";
    private async Task StartConfiguredPractice()
    {
        if(selected==null||From==null||To==null||From>To)throw new InvalidOperationException("종목과 기간을 확인하세요.");
        var snapshot=ConfiguredEngineSettings();FlushCapturedTicks();var from=new DateTimeOffset(From.Value.Date,TimeSpan.FromHours(9));var to=new DateTimeOffset(To.Value.Date.AddDays(1),TimeSpan.FromHours(9));
        await tickWrites;var key=selected.Key;var events=await Task.Run(()=>store.ReadEvents(key,"KIWOOM",from,to.AddTicks(-1)),Token);
        if(events.Count==0)throw new InvalidOperationException("선택 기간의 실제 체결·호가 데이터가 없습니다. 온라인에서 수신·저장 후 연습하세요.");
        configuredPaper=new(snapshot,selected.Currency,InitialCash());practiceEvents=events.ToArray();practiceEventIndex=-1;practiceTrades.Clear();paper=null;paperRun=Guid.NewGuid().ToString("N");paperSource="모의매매";
        StepConfiguredPractice();Changed(nameof(NextStepCaption));Changed(nameof(PaperSummary));Log("저장된 실제 체결로 모의매매 시작 · 다음 체결에서 주문을 처리합니다.");Refresh();
    }
    private void StepConfiguredPractice()
    {
        if(configuredPaper==null||!HasNextPractice)return;
        do
        {
            var e=practiceEvents[++practiceEventIndex];configuredPaper.OnEvent(e,AutoReplay);
            if(e.Kind==EventKind.Trade&&e.TradePrice is decimal price){practiceTrades.Add(new(e.ExchangeTime,price,price,price,price,e.TradeSize??0));break;}
        }while(HasNextPractice);
        // The displayed session candles contain only events already replayed, never future events.
        var days=practiceTrades.GroupBy(b=>b.Time.ToOffset(TimeSpan.FromHours(9)).Date).Select(g=>new PriceBar(g.Last().Time,g.First().Open,g.Max(b=>b.High),g.Min(b=>b.Low),g.Last().Close,g.Sum(b=>b.Volume))).ToArray();ChartBars=days;
        Replace(Trades,configuredPaper.Broker.Fills.AsEnumerable().Reverse().Select(f=>new BarFill(f.Time,f.Buy?"매수":"매도",f.Quantity,f.Price,f.Fee,f.RealizedPnl,string.IsNullOrEmpty(f.Reason)?f.Strategy:f.Reason,f.OrderId)));
        foreach(var f in configuredPaper.Broker.Fills.Select((fill,index)=>(fill,index)))
        {var id="trade-marker:"+paperRun+":"+f.index;store.State(id,new TradeMarker(id,f.fill.Instrument.Key,f.fill.Time,f.fill.Buy,f.fill.Quantity,f.fill.Price,"모의매매"));}
        DataInfo="저장된 실제 체결 · 한국시간 · 재생한 이벤트만 표시합니다.";ReloadTradeMarkers();Changed(nameof(PaperSummary));ResultInfo=$"실제 체결 재생 {practiceEventIndex+1:N0}/{practiceEvents.Length:N0} 이벤트\n평가 {configuredPaper.Broker.Equity:N0} · 체결 {configuredPaper.Broker.Fills.Count:N0}건";
        if(!HasNextPractice){replayTimer.Stop();Replaying=false;}Refresh();
    }
    private void PlaceConfiguredPractice(bool buy,int qty)
    {
        if(configuredPaper==null||selected==null)return;var instrument=new Instrument(selected.Symbol,selected.Exchange);
        if(!configuredPaper.Contexts.TryGetValue(instrument.Key,out var context)||context.Price<=0)throw new InvalidOperationException("체결 가격이 아직 없습니다.");
        var order=configuredPaper.Manual(instrument,buy,qty,context.Price,configuredPaper.Settings.Execution.Kind);
        Status=order==null?configuredPaper.Decisions.LastOrDefault()?.Reason??"주문 조건 미충족":"실제 체결 재생의 다음 이벤트로 모의 주문을 처리합니다.";Changed(nameof(PaperSummary));Refresh();
    }
    private void ResetConfiguredPractice(){configuredPaper=null;practiceEvents=[];practiceEventIndex=-1;practiceTrades.Clear();Changed(nameof(NextStepCaption));}
    private void CancelConfiguredPractice(){if(configuredPaper!=null)foreach(var order in configuredPaper.Broker.Orders.Where(o=>o.Active).ToArray())configuredPaper.Cancel(order.Id);}
    private string ConfiguredPaperSummary=>configuredPaper==null?"":$"실제 체결 모의매매 · 현금 {configuredPaper.Broker.Cash:N0}\n보유 {PaperQuantity:N0}주 · 평가 {configuredPaper.Broker.Equity:N0}\n손익 {configuredPaper.Broker.Equity-configuredPaper.Broker.InitialCash:+#,##0;-#,##0;0} · {(PaperPending?"주문 대기":"주문 없음")}";
}
