using Stock.Core;
namespace Stock.App;

/// <summary>Confirmation precedes stopping the affected account and removing encrypted credentials.</summary>
public sealed class CredentialResetService(BrokerCredentialStore store,Func<Brokerage?,Task> beforeDelete)
{
    public async Task<bool> Reset(Brokerage? selected,Func<string,bool> confirm)
    {
        var scope=selected is {} id?BrokerageInfo.All.Single(b=>b.Id==id).Name:"모든 증권사 (선택 목록에서 제외된 증권사 포함)";
        if(!confirm(scope+"의 저장된 API 키와 현재 입력값을 초기화합니다. 해당 계좌의 자동매매와 연결을 종료합니다. 거래 내역·전략·시세 자료는 유지됩니다. 초기화할까요?"))return false;
        await beforeDelete(selected);
        if(selected is {} broker)store.Delete(broker);else store.DeleteAll();
        return true;
    }
}
