using System.Collections.Concurrent;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
using Stock.Core.Strategies.Injes;

namespace Stock.App;

public sealed record OversoldBacktestRequest(BarDataset Dataset,OversoldConfig Configuration,DateOnly From,DateOnly To,HistoricalFacts Facts,BarSpecification? BarSpecification=null,StrategyBar[]? StrategyBars=null);

public interface IOversoldBacktestService
{
    Task<OversoldResult> RunAsync(OversoldBacktestRequest request,CancellationToken cancellationToken);
    void Clear();
}

/// <summary>Single application entry point shared by the main chart and screening popup.</summary>
public sealed class OversoldBacktestService(TradingStore? store=null):IOversoldBacktestService
{
    private readonly ConcurrentDictionary<string,OversoldResult> cache=[];
    public Task<OversoldResult> RunAsync(OversoldBacktestRequest request,CancellationToken cancellationToken)
    {
        if(request.Configuration.Bins!=null)
        {
            var observations=store==null?[]:Stock.Core.Strategies.Bins.BinsResearchBridge.Read(store,request.Dataset.Stock,request.From,request.To);
            return Task.Run(()=>Stock.Core.Strategies.Bins.BinsResearchBridge.Run(request.Dataset.Stock,request.Configuration,request.From,request.To,observations,cancellationToken),cancellationToken);
        }
        if(request.Configuration.Injes!=null)
        {
            var observations=store==null?[]:InjesResearchBridge.Read(store,request.Dataset.Stock,request.From,request.To);
            return Task.Run(()=>InjesResearchBridge.Run(request.Dataset.Stock,request.Configuration,request.From,request.To,observations,cancellationToken),cancellationToken);
        }
        throw new InvalidOperationException("기존 전략은 제거되었습니다. Inje's Strategy를 선택하세요.");
    }
    public void Clear()=>cache.Clear();
}
