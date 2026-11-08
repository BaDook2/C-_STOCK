using Stock.Core.Trading;
namespace Stock.Core.Research.Oversold;

public sealed record OversoldOwnedState(int Quantity,int Entries,int PeakQuantity,int HarvestBasis,int FirstDay,int LastExit,decimal Average,decimal LastEntry,decimal Peak,decimal? PriorCenter,bool Tp1,bool Tp2,bool Harvest,SupportCluster? InitialCluster);
/// <summary>Durable strategy state and decision context, never raw ticks or a quote cache.</summary>
public sealed record OversoldLiveCheckpoint(int Version,string Key,PriceBar[] History,decimal?[] TradingValues,StrategyBar[] CompletedBars,EntryDetectorState Detector,OversoldOwnedState Owned,DateTimeOffset LastBarTime,long LastBarSequence,long LiveSequence,OversoldSignal? Pending,EntryEvaluation? LatestEntry,string[] EntryOrders,string[] ExecutionIds,string Error,HistoricalFacts Facts);

public sealed partial class OversoldLiveStrategy
{
    public OversoldLiveCheckpoint CapturePublicState()
    {
        if(!publicPaper)throw new InvalidOperationException("원본 체결 스트림은 이 공개시세 복원 형식으로 저장하지 않습니다.");
        return new(1,instrument.Key,cache.History.ToArray(),values.ToArray(),CompletedBars,detector.Capture(),new(quantity,entries,peakQuantity,harvestBasis,firstDay,lastExit,average,lastEntry,peak,priorCenter,tp1,tp2,harvest,initialCluster),lastBarTime,lastBarSequence,liveSequence,pendingSignal,latestEntryEvaluation,filledEntryOrders.ToArray(),executionIds.ToArray(),error,facts.Snapshot());
    }
    public static OversoldLiveStrategy RestorePublic(Instrument instrument,OversoldLiveConfiguration configuration,OversoldLiveCheckpoint state)
    {
        if(!configuration.PublicPaper||state.Version!=1||state.Key!=instrument.Key||state.History.Length!=state.TradingValues.Length||state.History.Length>1_000_000)throw new InvalidOperationException("보관된 공개시세 전략의 버전·종목·지표 이력이 일치하지 않습니다.");
        var owned=state.Owned;
        if(owned.Quantity<0||owned.Entries<0||owned.Quantity>0&&(owned.InitialCluster==null||owned.Average<=0||owned.Entries<1)||owned.PeakQuantity<owned.Quantity||owned.HarvestBasis<0||owned.FirstDay<0||owned.LastExit< -1||state.Pending?.SignalTime>state.LastBarTime)throw new InvalidOperationException("보관된 포지션·신호 상태가 올바르지 않습니다.");
        var result=new OversoldLiveStrategy(instrument,configuration with{Warmup=[],Facts=state.Facts});
        foreach(var bar in state.History){if(bar.Time>state.LastBarTime)throw new InvalidOperationException("보관된 지표에 미래 봉이 있습니다.");result.cache.Append(bar);}
        if(owned.Quantity>0&&owned.FirstDay>=result.sessions.Length)throw new InvalidOperationException("보관된 첫 체결 거래일이 없습니다.");
        result.values.AddRange(state.TradingValues);result.completedBars.AddRange(state.CompletedBars);result.detector.Restore(state.Detector);
        result.quantity=owned.Quantity;result.entries=owned.Entries;result.peakQuantity=owned.PeakQuantity;result.harvestBasis=owned.HarvestBasis;result.firstDay=owned.FirstDay;result.lastExit=owned.LastExit;result.average=owned.Average;result.lastEntry=owned.LastEntry;result.peak=owned.Peak;result.priorCenter=owned.PriorCenter;result.tp1=owned.Tp1;result.tp2=owned.Tp2;result.harvest=owned.Harvest;result.initialCluster=owned.InitialCluster;
        result.lastBarTime=state.LastBarTime;result.lastBarSequence=state.LastBarSequence;result.liveSequence=state.LiveSequence;result.pendingSignal=state.Pending;result.readySignal=null;result.latestEntryEvaluation=state.LatestEntry;result.error=state.Error;
        result.filledEntryOrders.UnionWith(state.EntryOrders);result.executionIds.UnionWith(state.ExecutionIds);result.candidates=result.providers.SelectMany(p=>p.Get(result.cache.History,result.config)).ToArray();return result;
    }
    /// <summary>Append an observed new trading session. Earlier sessions and their indices are immutable.</summary>
    public void ExtendPublicFacts(HistoricalFacts updated)
    {
        if(!publicPaper)throw new InvalidOperationException("실계좌 세션을 공개시세 가정으로 확장할 수 없습니다.");
        var next=updated.Snapshot();
        if(sessions.Any(old=>!next.Sessions.Contains(old))||next.Sessions.Any(s=>!sessions.Contains(s)&&sessions.Length>0&&s.Date<=sessions[^1].Date))throw new InvalidOperationException("이미 판단한 시장 세션은 변경할 수 없습니다.");
        facts.Source=next.Source;facts.MarketSessions=next.MarketSessions;facts.StatusRecords=next.StatusRecords;facts.RiskRecords=next.RiskRecords;facts.TradingValues=next.TradingValues;facts.Validate();sessions=facts.Sessions.OrderBy(s=>s.Open).ToArray();
    }
}
