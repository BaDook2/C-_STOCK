using Stock.Core.Research.Oversold;
using Stock.Core.Trading;

namespace Stock.Core.Research.DayTrading;

public sealed record ClosedBarReplayItem(IntradayReplayItem Item,StrategyBar[] Bars);
public sealed record ClosedBarReplayRequest(ClosedBarReplayItem[] Items,string Source,DateTimeOffset From,DateTimeOffset To,DateTimeOffset AvailableAt,
    decimal InitialCash,ExecutionSettings Execution,AccountRiskPolicy AccountLimits,bool AssumeUnknownTradableAndNoRisk=false,
    int MaximumAssumedOpeningShares=10000,int ClockIntervalMilliseconds=1000,int MinimumAnnualizationSessions=20);

/// <summary>
/// Explicit OHLCV close approximation. Strategy/portfolio rules are shared with raw streaming;
/// only observation timing and next-open liquidity are research assumptions.
/// </summary>
public sealed class ClosedBarReplayEngine
{
    public IntradayReplayResult Run(ClosedBarReplayRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);cancellationToken.ThrowIfCancellationRequested();
        var items=request.Items.Select(input=>
        {
            var item=input.Item;var facts=item.Seed.Facts.Snapshot();
            if(request.AssumeUnknownTradableAndNoRisk)
            {
                facts.Source+=" · 연구가정: 미확인 일반주식/정지아님/위험없음";
                foreach(var session in facts.Sessions.Where(s=>s.Close>=request.From&&s.Open<=request.To))
                {
                    if(facts.Status(item.Instrument.Symbol,session.Date,session.Open)==null)facts.StatusRecords=[..facts.StatusRecords,new(item.Instrument.Symbol,session.Date,session.Open,HistoricalBoard.Kospi,HistoricalSecurityKind.Common,false,false)];
                    if(facts.Risk(item.Instrument.Symbol,session.Date,session.Open)==null)facts.RiskRecords=[..facts.RiskRecords,new(item.Instrument.Symbol,session.Date,false,session.Open)];
                    facts.Validate();
                }
            }
            return item with{Strategy=item.Strategy.Snapshot(),Seed=new(item.Seed.Bars.ToArray(),facts)};
        }).ToArray();
        var runtimes=items.Select(i=>new StrategyDRuntime(i.Instrument,i.Strategy,i.Seed,i.InstanceId,true)).ToArray();
        var paper=new DayTradingPaperAdapter(items[0].Instrument.Currency,request.InitialCash,request.Execution);
        var portfolio=new DayTradingPortfolioEngine(paper,runtimes,request.AccountLimits);
        var report=new IntradayReportBuilder(items,request.From,request.To,request.InitialCash,request.MinimumAnnualizationSessions);report.Attach(portfolio);
        portfolio.SetEntriesEnabled(true,request.From.AddTicks(-1));
        var latency=TimeSpan.FromTicks(Math.Max(1,(long)request.Execution.LatencyMs*TimeSpan.TicksPerMillisecond));
        var events=request.Items.SelectMany((input,index)=>input.Bars.Where(b=>b.ClosedAt>=request.From&&b.ClosedAt<=request.To).SelectMany(bar=>new[]{
            new BarStep(bar.OpenTime+latency,true,index,input.Item.Instrument.Key,bar),new BarStep(bar.ClosedAt,false,index,input.Item.Instrument.Key,bar)}))
            .Where(e=>e.Time>=request.From&&e.Time<=request.To).OrderBy(e=>e.Time).ThenBy(e=>e.Opening?1:0).ThenBy(e=>e.Index).ToArray();
        var clock=request.From;var interval=TimeSpan.FromMilliseconds(request.ClockIntervalMilliseconds);
        foreach(var step in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while(clock<=step.Time){cancellationToken.ThrowIfCancellationRequested();portfolio.OnTime(clock);report.Mark(clock,paper.Equity);clock+=interval;}
            if(step.Opening)portfolio.ProcessAssumedBarOpening(step.Symbol,step.Bar,request.MaximumAssumedOpeningShares);
            else portfolio.OnClosedBar(step.Symbol,step.Bar,request.MaximumAssumedOpeningShares,false);
            report.Mark(step.Time,paper.Equity);
        }
        while(clock<=request.To){cancellationToken.ThrowIfCancellationRequested();portfolio.OnTime(clock);report.Mark(clock,paper.Equity);clock+=interval;}
        portfolio.OnTime(request.To);report.Mark(request.To,paper.Equity);
        var hash=TradingStore.Hash(Serialization.Pack(request.Items.Select(i=>new{i.Item.Instrument,i.Bars})));
        var config=TradingStore.Hash(Serialization.Pack(request with{AvailableAt=request.To}));
        var notices=new List<string>{"완성봉 종가에서만 반등 평가 · 실제 장중 첫 접근/틱 저점/신호 시각 재현 아님","같은 봉의 손절/익절 동시 도달은 손절 우선 · 현재 봉 고가로 과거 저가의 추적손절을 소급하지 않음",
            $"다음 봉 시가 체결 가정 · 시가+최소지연 시각 · 최대 가정 유동성 {request.MaximumAssumedOpeningShares:N0}주/시가 기회 · 실제 호가/거래량 미재현"};
        if(request.AssumeUnknownTradableAndNoRisk)notices.Add("미확인 상태/위험의 연구가정 사용 · 실계좌 승인으로 전이 불가");
        return report.Finish(portfolio,paper,hash,config,"봉 확정 기반 근사 · 틱 재현 아님",request.Source,request.Items.Sum(i=>i.Bars.Length),0,notices);
    }
    private sealed record BarStep(DateTimeOffset Time,bool Opening,int Index,string Symbol,StrategyBar Bar);
    private static void Validate(ClosedBarReplayRequest r)
    {
        if(r.Items==null||r.Items.Length==0||string.IsNullOrWhiteSpace(r.Source)||r.From>=r.To||r.To>r.AvailableAt||r.From==DateTimeOffset.MinValue||r.InitialCash<=0||r.Execution==null||r.Execution.UseSpread||r.AccountLimits==null||r.MaximumAssumedOpeningShares<=0||r.ClockIntervalMilliseconds is <1 or >60000||r.MinimumAnnualizationSessions<2)
            throw new InvalidOperationException("확정봉 연구의 기간/자료/가상계좌/비호가 체결가정/시계를 확인하세요.");
        foreach(var input in r.Items)
        {
            input.Item.Strategy.Validate();input.Item.Seed.Facts.Validate();var policy=input.Item.Strategy.DayTrading!;
            if(policy.Support.ProfileSource!=VolumeProfileSource.Approximate)throw new InvalidOperationException("OHLCV만 있는 연구에서는 정밀 체결 매물대를 사용할 수 없습니다.");
            if(policy.SimpleTimeframe.Unit is TimeframeUnit.Trade or TimeframeUnit.Second)throw new InvalidOperationException("봉 근사는 제공된 분/시간/일봉만 지원합니다. 체결/초 기준에는 원본 이벤트가 필요합니다.");
            if(input.Bars==null||input.Bars.Length==0)throw new InvalidOperationException("선택 종목의 확정봉 자료가 없습니다.");
            foreach(var bar in input.Bars)
            {
                bar.Price.Validate();
                if(!bar.Price.IsComplete||bar.OpenTime>=bar.ClosedAt||bar.ClosedAt-bar.OpenTime<=TimeSpan.FromMilliseconds(r.Execution.LatencyMs))throw new InvalidOperationException("봉 완성시각과 체결가정 지연을 확인하세요.");
                if(policy.SimpleTimeframe.Duration is {} duration&&bar.ClosedAt-bar.OpenTime!=duration)throw new InvalidOperationException("분/시간봉 길이가 전략의 기본 분석 기준과 일치하지 않습니다.");
            }
            if(input.Bars.GroupBy(b=>b.OpenTime).Any(g=>g.Count()>1))throw new InvalidOperationException("중복된 확정봉 시작시각입니다.");
            var c=input.Item.Strategy.Configuration;
            if(c.CommissionPercent!=r.Execution.FeePercent||c.TaxPercent!=r.Execution.TaxPercent||c.SlippagePercent!=r.Execution.SlippagePercent)throw new InvalidOperationException("공유 봉 모형의 비용은 전략 가정과 일치해야 합니다.");
        }
    }
}
