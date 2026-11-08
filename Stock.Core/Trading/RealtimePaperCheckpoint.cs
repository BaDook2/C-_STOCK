using Stock.Core.Research.Oversold;
namespace Stock.Core.Trading;

public sealed record PaperIntentCheckpoint(string Id,string Reason,OversoldDecisionSnapshot? Snapshot);
public sealed record PaperRowCheckpoint(string Key,PaperBrokerCheckpoint Broker,OversoldLiveCheckpoint Strategy,PaperIntentCheckpoint[] Intents,DateTimeOffset? SuppressedBuySignal);
public sealed record PaperBatchCheckpoint(int Version,string RunId,LiveBatchPlan Plan,int OrderSequence,PaperRowCheckpoint[] Rows,DateOnly RiskDate,decimal OpeningEquity);
public sealed record PaperCheckpointEnvelope(string Payload,string Sha256);

public sealed partial class RealtimePaperStrategyBatch
{
    public string RunId=>runId;
    public bool EntryEnabled {get{lock(sync)return entryEnabled;}}
    public LiveBatchPlan? RestoredPlan {get;private set;}
    public LiveBatchPlan? CurrentPlan(){lock(sync)return plan==null?null:plan with{Items=rows.Values.Select(r=>r.Item with{Oversold=r.Item.Oversold! with{Warmup=r.Strategy.CompletedBars,Facts=r.Strategy.CapturePublicState().Facts}}).ToArray()};}
    public bool RestoreAvailable=>store.State<string>("paper-live-batch-latest") is {} id&&store.State<PaperCheckpointEnvelope>("paper-live-checkpoint:"+id)!=null;
    private PaperBatchCheckpoint CaptureCheckpoint()=>new(2,runId,plan!,orderSequence,rows.Select(pair=>new PaperRowCheckpoint(pair.Key,pair.Value.Broker.Capture(),pair.Value.Strategy.CapturePublicState(),pair.Value.Intents.Select(i=>new PaperIntentCheckpoint(i.Key,i.Value.Reason,i.Value.Snapshot)).ToArray(),pair.Value.SuppressedBuySignal)).ToArray(),riskDate,openingEquity);
    /// <summary>Loads a consistent paper ledger in a paused state. Never replays prices or submits orders.</summary>
    public void RestoreLatest()
    {
        lock(sync)
        {
            if(armed||rows.Count>0)throw new InvalidOperationException("빈 실행에만 보관된 가상 계좌를 복원할 수 있습니다.");
            var id=store.State<string>("paper-live-batch-latest")??throw new InvalidOperationException("보관된 가상 실행이 없습니다.");
            var envelope=store.State<PaperCheckpointEnvelope>("paper-live-checkpoint:"+id)??throw new InvalidOperationException("이전 버전 기록에는 복원용 원장이 없습니다. 새 계좌로 자동 재개하지 않습니다.");
            if(TradingStore.Hash(envelope.Payload)!=envelope.Sha256)throw new InvalidOperationException("가상 복원 원장의 무결성 검사가 실패했습니다.");
            var saved=Serialization.Unpack<PaperBatchCheckpoint>(envelope.Payload);
            if(saved.Version!=2||saved.RunId!=id||saved.Plan.Items.Length is <1 or >20||saved.Rows.Length!=saved.Plan.Items.Length||saved.Plan.Items.Any(i=>i.Oversold?.PublicPaper!=true)||saved.Rows.Select(r=>r.Key).Distinct().Count()!=saved.Rows.Length||saved.OrderSequence<0||saved.RiskDate==default||saved.OpeningEquity<0)throw new InvalidOperationException("가상 복원 원장의 버전·종목 구성이 올바르지 않습니다.");
            var restored=new Dictionary<string,Row>();
            foreach(var item in saved.Plan.Items)
            {
                item.Settings.Validate();item.Oversold!.Configuration.Validate();var checkpoint=saved.Rows.Single(r=>r.Key==item.Stock.Key);
                var row=new Row(item,checkpoint.Strategy){Enabled=false,Status="보관 상태 · 보호 감시 재개 필요",SuppressedBuySignal=checkpoint.Strategy.Pending?.SignalTime};row.Broker.Restore(checkpoint.Broker);
                var position=row.Broker.Positions.GetValueOrDefault(item.Stock.Key);if(row.Strategy.Position.Quantity!=(position?.Quantity??0)||row.Strategy.Position.Quantity>0&&row.Strategy.Position.Average!=position!.AveragePrice)throw new InvalidOperationException("가상 계좌와 전략 보유 상태가 일치하지 않습니다.");
                foreach(var intent in checkpoint.Intents)row.Intents.Add(intent.Id,(intent.Reason,intent.Snapshot));if(row.Broker.Orders.Any(o=>!row.Intents.ContainsKey(o.Id)))throw new InvalidOperationException("보관된 가상 주문의 당시 판단 정보가 없습니다.");row.AppliedFills=row.Broker.Fills.Count;restored.Add(item.Stock.Key,row);
            }
            foreach(var pair in restored)rows.Add(pair.Key,pair.Value);runId=saved.RunId;plan=saved.Plan;RestoredPlan=saved.Plan;orderSequence=saved.OrderSequence;riskDate=saved.RiskDate;openingEquity=saved.OpeningEquity;armed=false;entryEnabled=false;status="보관된 가상 보유/미체결 복원 · 주문 없음 · 보호 감시 재개 필요";
        }
    }
    public void ExtendPublicFacts(string key,HistoricalFacts facts)
    {lock(sync){if(!rows.TryGetValue(key,out var row))throw new InvalidOperationException("가상 실행 종목이 없습니다.");row.Strategy.ExtendPublicFacts(facts);Save();}}
    /// <summary>Explicit resume, still with new/scale-in buys disabled and no cached quote considered fresh.</summary>
    public void ResumeProtection()
    {
        lock(sync)
        {
            if(plan==null||armed)throw new InvalidOperationException("중지 상태의 복원된 가상 계좌가 필요합니다.");
            if(!LiveBatchSupport.RegularSession(clock()))throw new InvalidOperationException("장 외 시간입니다. 장중 새 시세로 보호 감시를 재개하세요.");
            foreach(var row in rows.Values){row.Enabled=true;row.PublicSnapshot=null;row.LastEvaluation=default;row.Error="";row.SuppressedBuySignal=row.Strategy.PendingSignalTime;row.Status="보호 감시 재개 · 신선한 관측 대기";}
            armed=true;entryEnabled=false;status="보호 감시 재개 · 신규/추가매수 꺼짐";Save();
        }
    }
}
