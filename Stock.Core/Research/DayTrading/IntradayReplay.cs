using System.Collections.Concurrent;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record IntradayReplayItem(Instrument Instrument,StrategyDefinition Strategy,StrategyDDataSeed Seed,string InstanceId);
public sealed record ReplayProvenance(string Source,bool OriginalTradeEvents,bool OriginalReceiveTimes,bool SyntheticFixture=false);
public sealed record IntradayReplayRequest(IntradayReplayItem[] Items,MarketEvent[] Events,ReplayProvenance Provenance,
    DateTimeOffset From,DateTimeOffset To,DateTimeOffset AvailableAt,decimal InitialCash,ExecutionSettings Execution,AccountRiskPolicy AccountLimits,
    int ClockIntervalMilliseconds=1000,int MinimumAnnualizationSessions=20);
public sealed record IntradayRoundTrip(int Number,string Symbol,DateTimeOffset EntryAt,DateTimeOffset? ExitAt,decimal AcquisitionCost,
    decimal RealizedProfit,decimal? ReturnPercent,double? HoldingSeconds,Fill[] Fills,IntradayEntrySignal? Entry);
public sealed record IntradayReplayResult(string Id,string DataHash,string ConfigurationHash,string Precision,string Source,DateTimeOffset From,DateTimeOffset To,
    decimal InitialCash,DayTradingPortfolioSnapshot Account,IntradayEntrySignal[] Signals,Fill[] Fills,IntradayRoundTrip[] Trades,BarEquity[] Equity,
    PerformanceSummary Performance,decimal? AverageHoldingSeconds,string[] Notices,IReadOnlyDictionary<string,string> ExitSnapshots,int InputEvents,int RejectedEvents,ManagedOrder[] Orders);

/// <summary>Replays recorded receive order through the exact same signal/portfolio runtime used by paper streaming.</summary>
public sealed class IntradayReplayEngine
{
    public IntradayReplayResult Run(IntradayReplayRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);cancellationToken.ThrowIfCancellationRequested();
        var items=request.Items.Select(i=>i with{Strategy=i.Strategy.Snapshot(),Seed=new(i.Seed.Bars.ToArray(),i.Seed.Facts.Snapshot())}).ToArray();
        var dataHash=TradingStore.Hash(Serialization.Pack(request.Events));
        var configHash=TradingStore.Hash(Serialization.Pack(new{Items=items,request.Execution,request.AccountLimits,request.InitialCash,request.From,request.To,request.ClockIntervalMilliseconds,request.MinimumAnnualizationSessions,request.Provenance}));
        var runtime=items.Select(i=>new StrategyDRuntime(i.Instrument,i.Strategy,i.Seed,i.InstanceId)).ToArray();
        var paper=new DayTradingPaperAdapter(items[0].Instrument.Currency,request.InitialCash,request.Execution);
        var audits=new List<DayTradingAudit>();var portfolio=new DayTradingPortfolioEngine(paper,runtime,request.AccountLimits,audits.Add);
        var report=new IntradayReportBuilder(items,request.From,request.To,request.InitialCash,request.MinimumAnnualizationSessions);report.Attach(portfolio);
        var before=request.From==DateTimeOffset.MinValue?request.From:request.From.AddTicks(-1);portfolio.SetEntriesEnabled(true,before);
        var clock=request.From;var interval=TimeSpan.FromMilliseconds(request.ClockIntervalMilliseconds);
        void Pulse(DateTimeOffset now){portfolio.OnTime(now);report.Mark(now,paper.Equity);}
        // Recorded receive order preserves late/out-of-order exchange events so live quality checks behave identically.
        foreach(var item in request.Events.Select((e,index)=>(Event:e,Index:index)).OrderBy(x=>x.Event.ReceivedTime).ThenBy(x=>x.Index))
        {
            cancellationToken.ThrowIfCancellationRequested();var e=item.Event;
            if(e.ReceivedTime<request.From||e.ReceivedTime>request.To)continue;
            while(clock<=e.ReceivedTime){cancellationToken.ThrowIfCancellationRequested();Pulse(clock);clock+=interval;}
            portfolio.OnMarket(e,e.ReceivedTime);report.Mark(e.ReceivedTime,paper.Equity);
        }
        while(clock<=request.To){cancellationToken.ThrowIfCancellationRequested();Pulse(clock);clock+=interval;}
        Pulse(request.To);
        var notices=new List<string>{"기록 수신순서 재생 · 신호와 체결 시각 분리 · 신호 이벤트 즉시 체결 없음",request.Execution.UseSpread?"후속 실제 호가·잔량/지연/부분체결 모형":"후속 실제 체결량 참여율 모형 · 호가 스프레드 미재현"};
        if(request.Provenance.SyntheticFixture)notices.Add("합성 검사 입력 · 시장 수익성 평가 자료 아님");
        if(request.Execution.UseSpread&&!request.Events.Any(e=>e.Kind==EventKind.Quote))notices.Add("원본 호가 없음: 호가 체결모형은 미체결로 남을 수 있음");
        return report.Finish(portfolio,paper,dataHash,configHash,request.Provenance.SyntheticFixture?"합성 원본형 이벤트 검사":"기록 체결/수신시각 재생",request.Provenance.Source,request.Events.Length,audits.Count(a=>a.Kind=="자료 차단"),notices);
    }
    private static void Validate(IntradayReplayRequest r)
    {
        if(r.Items==null||r.Items.Length==0||r.Events==null||r.Provenance==null||r.Execution==null||r.AccountLimits==null||r.From>=r.To||r.To>r.AvailableAt||r.InitialCash<=0||r.ClockIntervalMilliseconds is <1 or >60000||r.MinimumAnnualizationSessions<2)
            throw new InvalidOperationException("재생 종목/자료/기간/가용시각/계좌/시계 간격을 확인하세요.");
        if(!r.Provenance.OriginalTradeEvents||!r.Provenance.OriginalReceiveTimes||string.IsNullOrWhiteSpace(r.Provenance.Source))
            throw new InvalidOperationException("원본 체결/수신시각 자료가 없습니다. OHLCV를 정밀 틱 재생으로 대체하지 않습니다.");
        if(!r.Events.Any(e=>e.Kind==EventKind.Trade&&e.ReceivedTime>=r.From&&e.ReceivedTime<=r.To))throw new InvalidOperationException("선택 기간의 원본 체결 자료가 없습니다.");
        foreach(var i in r.Items)
        {
            i.Instrument.Validate();i.Strategy.Validate();i.Seed.Facts.Validate();
            if(i.Strategy.Configuration.CommissionPercent!=r.Execution.FeePercent||i.Strategy.Configuration.TaxPercent!=r.Execution.TaxPercent||i.Strategy.Configuration.SlippagePercent!=r.Execution.SlippagePercent)
                throw new InvalidOperationException("공유계좌 체결 비용은 각 전략의 비용 가정과 일치해야 합니다.");
        }
        foreach(var e in r.Events)e.Validate();
    }
}

/// <summary>One research entry point for all UI routes. Immutable content hashes include data, facts, policies and costs.</summary>
public sealed class IntradayBacktestService
{
    private readonly ConcurrentDictionary<string,IntradayReplayResult> cache=[];
    public Task<IntradayReplayResult> RunAsync(IntradayReplayRequest request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(request.To>request.AvailableAt)throw new InvalidOperationException("사용 가능한 시각 이후의 백테스트 기간입니다.");
        request=Serialization.Unpack<IntradayReplayRequest>(Serialization.Pack(request));
        var key=TradingStore.Hash(Serialization.Pack(request with{AvailableAt=request.To}));
        if(cache.TryGetValue(key,out var cached))return Task.FromResult(Serialization.Unpack<IntradayReplayResult>(Serialization.Pack(cached)));
        return Task.Run(()=>
        {
            cancellationToken.ThrowIfCancellationRequested();var result=new IntradayReplayEngine().Run(request,cancellationToken);
            if(cache.Count>=32)cache.Clear();cache[key]=Serialization.Unpack<IntradayReplayResult>(Serialization.Pack(result));return result;
        },cancellationToken);
    }
    public void Clear()=>cache.Clear();
    public Task<IntradayReplayResult> RunAsync(ClosedBarReplayRequest request,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(request.To>request.AvailableAt)throw new InvalidOperationException("사용 가능한 시각 이후의 백테스트 기간입니다.");
        request=Serialization.Unpack<ClosedBarReplayRequest>(Serialization.Pack(request));
        var key="bar:"+TradingStore.Hash(Serialization.Pack(request with{AvailableAt=request.To}));
        if(cache.TryGetValue(key,out var cached))return Task.FromResult(Serialization.Unpack<IntradayReplayResult>(Serialization.Pack(cached)));
        return Task.Run(()=>
        {
            cancellationToken.ThrowIfCancellationRequested();var result=new ClosedBarReplayEngine().Run(request,cancellationToken);
            if(cache.Count>=32)cache.Clear();cache[key]=Serialization.Unpack<IntradayReplayResult>(Serialization.Pack(result));return result;
        },cancellationToken);
    }
}
