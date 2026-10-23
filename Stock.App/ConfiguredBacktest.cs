using Stock.Core;
using Stock.Core.Research;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class WorkspaceViewModel
{
    private readonly List<MarketEvent> capturedTicks=[];private Task tickWrites=Task.CompletedTask;
    private void FlushCapturedTicks()
    {if(capturedTicks.Count==0)return;var batch=capturedTicks.ToArray();capturedTicks.Clear();tickWrites=tickWrites.ContinueWith(previous=>{previous.GetAwaiter().GetResult();store.InsertEvents(batch);},CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default);}
    private async Task RunConfiguredBacktest()
    {
        if(selected==null||From==null||To==null||From>To)throw new InvalidOperationException("종목과 시작·종료 기간을 확인하세요.");
        ResetConfiguredPractice();var item=selected;var snapshot=ConfiguredEngineSettings();var from=new DateTimeOffset(From.Value.Date,TimeSpan.FromHours(9));var to=new DateTimeOffset(To.Value.Date.AddDays(1),TimeSpan.FromHours(9));
        FlushCapturedTicks();await tickWrites;
        snapshot.Backtest.Instrument=item.Key;snapshot.Backtest.InitialCash=InitialCash();snapshot.Backtest.From=from.ToString("O");snapshot.Backtest.To=to.ToString("O");
        var token=Token;var result=await Task.Run(()=>StreamingBacktest.Run(store,snapshot,store.EnumerateEvents(item.Key,"KIWOOM",from,to.AddTicks(-1),token),token),token);
        var saved=new SavedBacktest{Id="chart-run:"+result.RunId,At=DateTimeOffset.UtcNow,Stock=item,Period="실제 체결 · "+strategyConfiguration.StrategyType,DataHash=result.DataHash,Initial=snapshot.Backtest.InitialCash,Profit=result.NetPnl,MaxDrawdown=result.MaxDrawdownPercent,Fills=result.Trades.Select(f=>new BarFill(f.Time,f.Buy?"매수":"매도",f.Quantity,f.Price,f.Fee,f.RealizedPnl,string.IsNullOrEmpty(f.Reason)?f.Strategy:f.Reason,f.OrderId)).ToArray(),Equity=result.Equity.Select(p=>new BarEquity(p.Time,p.Equity)).ToArray(),AdvancedConfiguration=snapshot.Advanced,PipelineConfiguration=snapshot.Pipeline};
        saved.Quantity=result.Trades.Sum(f=>f.Buy?f.Quantity:-f.Quantity);saved.Cash=snapshot.Backtest.InitialCash+result.Trades.Sum(f=>(f.Buy?-1:1)*f.Price*f.Quantity-f.Fee);
        store.State(saved.Id,saved);LatestResult=saved;SelectedRun=saved;History.Insert(0,saved);Changed(nameof(LatestResult));ConfigurationMessage=$"{result.Status} · {result.Events:N0}개 실제 이벤트 · 체결 {result.Fills:N0}건";ResultInfo=saved.Summary;Log(ConfigurationMessage);ActiveTab=0;
    }
}
