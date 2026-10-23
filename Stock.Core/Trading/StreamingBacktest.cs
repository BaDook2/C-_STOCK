using System.Security.Cryptography;
using System.Text;
namespace Stock.Core.Trading;
/// <summary>Consumes every event from the selected SQLite range; rendering options are never an input.</summary>
public static class StreamingBacktest
{
    public static BacktestResult Run(TradingStore store,TradingSettings settings,IEnumerable<MarketEvent> ordered,CancellationToken token,bool recordDecisions=true)
    {
        settings.Validate();using var iterator=ordered.GetEnumerator();if(!iterator.MoveNext())throw new InvalidOperationException("선택 기간의 원본 체결·호가 자료가 없습니다.");
        var instrument=Instrument.Parse(settings.Backtest.Instrument);string run=store.BeginRun("Backtest:"+TradingEngine.ModelVersion,settings);var engine=new TradingEngine(settings,instrument.Currency,settings.Backtest.InitialCash,recordDecisions?(cat,obj,t)=>store.Audit(run,cat,obj,t):null);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);int processed=0;var quality=new DataQualityGate(settings.Data,settings.Execution);long previous=long.MinValue;string status="완료";
        try
        {
            do{token.ThrowIfCancellationRequested();var e=iterator.Current;if(e.Instrument!=instrument||e.ExchangeTime.UtcTicks<previous)throw new InvalidOperationException("원본 백테스트 종목·시간 순서를 확인하세요.");previous=e.ExchangeTime.UtcTicks;if(processed>0)hash.AppendData([(byte)'\n']);hash.AppendData(Encoding.UTF8.GetBytes(Serialization.Pack(e)));processed++;
                if(quality.Accept(e,false,out var reason))engine.OnEvent(e);else if(recordDecisions)store.Audit(run,"데이터 제외",new{e.Key,reason},e.ExchangeTime);
            }while(iterator.MoveNext());
        }catch(OperationCanceledException){status="취소";}
        var digest=Convert.ToHexString(hash.GetHashAndReset());store.RunHash(run,digest);decimal peak=engine.Broker.InitialCash,mdd=0;foreach(var point in engine.Equity){peak=Math.Max(peak,point.Equity);if(peak>0)mdd=Math.Max(mdd,(peak-point.Equity)/peak*100);}
        var sells=engine.Broker.Fills.Where(f=>!f.Buy).ToArray();var result=new BacktestResult(run,processed,engine.Broker.Fills.Count,engine.Broker.Fills.Sum(f=>f.Fee),engine.Broker.Equity-engine.Broker.InitialCash,mdd,sells.Length==0?0:100m*sells.Count(f=>f.RealizedPnl>0)/sells.Length,instrument.Currency,digest,status,engine.Equity,engine.Broker.Fills);
        store.FinishRun(run,new{Result=result,OpenOrders=engine.Broker.Orders.Where(o=>o.Active),OpenPositions=engine.Broker.Positions.Values.Where(p=>p.Quantity>0),Model=TradingEngine.ModelVersion});return result;
    }
}
