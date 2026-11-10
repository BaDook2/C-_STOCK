using System.Collections.Concurrent;
using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Desktop;

public sealed record OversoldBacktestRequest(BarDataset Dataset,OversoldConfig Configuration,DateOnly From,DateOnly To,HistoricalFacts Facts,BarSpecification? BarSpecification=null,StrategyBar[]? StrategyBars=null);

public interface IOversoldBacktestService
{
    Task<OversoldResult> RunAsync(OversoldBacktestRequest request,CancellationToken cancellationToken);
    void Clear();
}

/// <summary>Single application entry point shared by the main chart and screening popup.</summary>
public sealed class OversoldBacktestService:IOversoldBacktestService
{
    private readonly ConcurrentDictionary<string,OversoldResult> cache=[];
    public Task<OversoldResult> RunAsync(OversoldBacktestRequest request,CancellationToken cancellationToken)
    {
        request.BarSpecification?.Validate();
        // A realtime daily snapshot uses the same daily engine/cache as a normal daily run.
        if(request.BarSpecification?.Unit==BarUnit.Day)request=request with{BarSpecification=null,StrategyBars=null};
        request.Configuration.Validate();request.Facts.Validate();
        if((request.BarSpecification==null&&request.StrategyBars!=null)||(request.BarSpecification!=null&&request.StrategyBars==null))throw new InvalidOperationException("지정봉 설정과 가격 자료를 함께 전달하세요.");
        request.BarSpecification?.Validate();var dataHash=request.StrategyBars==null
            ?TradingStore.Hash(Serialization.Pack(request.Dataset.CompletedBars))
            :TradingStore.Hash(Serialization.Pack(request.StrategyBars));
        var configHash=TradingStore.Hash(Serialization.Pack(request.Configuration));
        var factsHash=TradingStore.Hash(Serialization.Pack(request.Facts));
        var key=$"{request.Dataset.Stock.Key}|{request.From:yyyyMMdd}|{request.To:yyyyMMdd}|{request.BarSpecification}|{dataHash}|{configHash}|{factsHash}";
        if(cache.TryGetValue(key,out var saved))return Task.FromResult(Serialization.Unpack<OversoldResult>(Serialization.Pack(saved)));
        return Task.Run(()=>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var daily=Stock.Core.Research.DayTrading.SelectedStrategy.DailyBars(request.Dataset,request.Facts);
            var facts=request.Facts.Snapshot();
            if(facts.MarketSessions.Length==0){facts.MarketSessions=daily.Select(b=>new DailyMarketSession(DailyDates.Of(b.Price),b.OpenTime,b.ClosedAt)).ToArray();facts.Source="확정 일봉 날짜의 연구 세션";facts.Validate();}
            var result=request.Configuration.UseCrashStrategy
                ?Stock.Core.Research.DayTrading.SelectedStrategy.Backtest(request.Dataset.Stock,request.Configuration,facts,daily,request.StrategyBars??daily,request.From,request.To,request.BarSpecification==null,cancellationToken)
                :request.BarSpecification is {} specification
                ?new OversoldBacktest().Run(request.Dataset.Stock,request.StrategyBars!,specification,request.Configuration,request.From,request.To,request.Facts,cancellationToken)
                :new OversoldBacktest().Run(request.Dataset,request.Configuration,request.From,request.To,request.Facts,cancellationToken);
            if(request.Configuration.UseCrashStrategy)result.BarSpecification=request.BarSpecification;
            result.IndicatorHistory=request.StrategyBars?.Select(b=>b.Price with{Time=b.OpenTime}).ToArray()??request.Dataset.CompletedBars;result.DailyIndicatorHistory=request.Dataset.CompletedBars;
            cache[key]=Serialization.Unpack<OversoldResult>(Serialization.Pack(result));
            return result;
        },cancellationToken);
    }
    public void Clear()=>cache.Clear();
}
