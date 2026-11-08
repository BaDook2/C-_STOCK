using Stock.Core;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void OrderTransmissionGuardChecks()
    {
        var dir=Path.Combine(output,"wal-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var fake=new OrderBroker(DateTimeOffset.UtcNow);var request=new OrderRequest("005930",1,10000,true);var path=Path.Combine(dir,"guard.json");
        foreach(var status in new[]{"전송 중","응답 불명"})
        {
            File.WriteAllText(path,Serialization.Pack(new[]{new LocalOrder("EXISTING",DateTimeOffset.UtcNow,request.Fingerprint,"005930","매수",1,10000,status,"")}));var guard=new OrderGuard(path);var attempt=guard.SubmitAsync(fake,request,canTransmit:()=>true);Pump(()=>attempt.IsCompleted);Check(attempt.IsFaulted&&fake.SubmitCalls==0,"existing "+status+" WAL intent still blocks transmission before any new intent is issued");
            Check(guard.HasUnresolvedOtherThanSending("UNRELATED"),"an unrelated sender ID cannot suppress "+status+" state");
            if(status=="응답 불명")Check(guard.HasUnresolvedOtherThanSending("EXISTING"),"even its own ID never exempts an unknown broker response");
        }
        path=Path.Combine(dir,"final-stop.json");var final=new OrderGuard(path);var calls=0;var stopped=final.SubmitAsync(fake,request,canTransmit:()=>++calls==1);Pump(()=>stopped.IsCompleted);Check(stopped.IsFaulted&&calls==2&&fake.SubmitCalls==0&&final.Entries.Single().Status=="전송 전 중지","stop after the durable write-ahead frame still prevents the actual broker call");
        using var store=new TradingStore(Path.Combine(dir,"identity.db"));var changed=new OrderBroker(DateTimeOffset.UtcNow){PauseBalance=true};var session=new RemoteTradingSession(changed,new TradingSettings(),store,Path.Combine(dir,"identity-guard"));var pending=session.Submit(new("005930",1,1000,true));Pump(()=>changed.BalanceEntered);changed.AccountValue="DUMMY-ANOTHER-ACCOUNT";changed.Release.TrySetResult();Pump(()=>pending.IsCompleted);Check(pending.IsFaulted&&changed.SubmitCalls==0&&!session.TransmissionStateSafe,"manual order also rejects a changed account identity after its final balance refresh");
    }
}
