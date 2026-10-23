using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using Stock.Core.Trading;
namespace Stock.App;
public sealed partial class MainViewModel
{
    private void RegisterDataActions()
    {
        Add("샘플",async()=>{await Task.Run(()=>{foreach(var i in new[]{Instrument.Parse("NASDAQ:AAPL"),Instrument.Parse("NASDAQ:NVDA"),Instrument.Parse("KRX:005930")})store.InsertEvents(DemoMarketDataProvider.Sample(i));});RefreshSavedLists();Status="SAMPLE 합성 데이터 3종목 생성 완료 (2026-09-25 13:30~14:00 UTC). 실제 과거 시세가 아닙니다.";},()=>CanConfigure);
        Add("CSV가져오기",async()=>{var dialog=new OpenFileDialog{Filter="STOCK CSV|*.csv"};if(dialog.ShowDialog()==true){var count=await Task.Run(()=>store.InsertEvents(MarketCsv.Import(dialog.FileName)));RefreshSavedLists();Status=$"CSV {count:N0} 이벤트 저장";}},()=>CanConfigure);
        Add("CSV내보내기",async()=>{ApplyEditors();var dialog=new SaveFileDialog{Filter="CSV|*.csv",FileName="stock-events.csv"};if(dialog.ShowDialog()==true){await Task.Run(()=>MarketCsv.Export(dialog.FileName,ReadSelected()));Status="이벤트 CSV 내보내기 완료";}},()=>!Busy);
        Add("재생",async()=>{ApplyEditors();var data=await Task.Run(ReadSelected);if(data.Count==0)throw new InvalidOperationException("재생할 데이터 없음");var i=Instrument.Parse(settings.Backtest.Instrument);exchange=i.Exchange;Symbol=i.Symbol;Changed(nameof(SelectedExchange));Changed(nameof(Currency));StartFeed(new HistoricalMarketDataProvider(data,settings.Data.DemoIntervalMs),"저장 데이터 재생 + 로컬 Paper",false,i.Currency);},()=>CanConfigure);
        Add("백테스트",Backtest,()=>CanConfigure);
        var cancel=new ActionCommand(()=>{backtestCancel?.Cancel();return Task.CompletedTask;},()=>backtestCancel!=null);commands.Add(cancel);Actions["백테스트취소"]=cancel;
        Add("결과내보내기",()=>{if(result==null)throw new InvalidOperationException("백테스트 결과가 없습니다.");var dialog=new SaveFileDialog{Filter="CSV|*.csv",FileName="backtest-trades.csv"};if(dialog.ShowDialog()==true){using var w=new StreamWriter(dialog.FileName,false,new UTF8Encoding(true));w.WriteLine("run_id,time,market,symbol,currency,side,quantity,price,fee,realized_pnl,strategy");foreach(var f in result.Trades)w.WriteLine(string.Join(',',result.RunId,f.Time.ToString("O"),f.Instrument.Exchange,f.Instrument.Symbol,f.Instrument.Currency,f.Buy?"BUY":"SELL",f.Quantity,Serialization.Number(f.Price),Serialization.Number(f.Fee),Serialization.Number(f.RealizedPnl),f.Strategy));File.WriteAllText(Path.ChangeExtension(dialog.FileName,"report.json"),Serialization.Pack(result),Encoding.UTF8);Status="거래 CSV와 자산곡선 포함 JSON 보고서 저장 완료";}return Task.CompletedTask;},()=>!Busy&&result!=null);
    }
    private IReadOnlyList<MarketEvent> ReadSelected()=>store.ReadEvents(settings.Backtest.Instrument,settings.Backtest.Source,DateTimeOffset.Parse(settings.Backtest.From),DateTimeOffset.Parse(settings.Backtest.To));
    private async Task Backtest()
    {
        ApplyEditors();backtestCancel=new();RefreshCommands();Progress=0;BacktestStatus="백테스트 실행 중… 취소할 수 있습니다.";var snapshot=settings.Clone();
        try{result=await Task.Run(()=>new BacktestRunner(store).Run(snapshot,ReadSelected(),backtestCancel.Token,new Progress<int>(p=>dispatcher.BeginInvoke(()=>Progress=p))));Replace(Fills,result.Trades);BacktestStatus=$"{result.Status} · {result.Events:N0} 이벤트 / {result.Fills:N0} 체결 / 순손익 {result.NetPnl:N4} {result.Currency} / 수수료 {result.Fees:N4} / MDD {result.MaxDrawdownPercent:F2}% / 매도체결 승률 {result.WinRate:F2}%\n열린 포지션은 마지막 관측가격 평가, 종료청산 없음. 모델: {TradingEngine.ModelVersion}";DrawChart(result.Equity);Status="백테스트 설정·데이터 해시·판단·결과를 SQLite에 기록했습니다.";}
        finally{backtestCancel.Dispose();backtestCancel=null;RefreshCommands();}
    }
    private void DrawChart(IReadOnlyList<EquityPoint> points)
    {if(points.Count<2){ChartPoints="";Changed(nameof(ChartPoints));return;}var min=points.Min(p=>p.Equity);var max=points.Max(p=>p.Equity);var range=Math.Max(.01m,max-min);var stride=Math.Max(1,points.Count/600);ChartPoints=string.Join(' ',points.Select((p,i)=>(p,i)).Where(x=>x.i%stride==0).Select(x=>$"{(600d*x.i/(points.Count-1)).ToString("F2",CultureInfo.InvariantCulture)},{(180m-(x.p.Equity-min)/range*170m).ToString("F2",CultureInfo.InvariantCulture)}"));Changed(nameof(ChartPoints));}
}
