using System.Text;
using Stock.Core;
using Stock.Core.Security;
using Stock.Desktop;
internal static partial class Program
{
    private static IBrokerCredentialVault MemoryCredentials()=>new BrokerCredentialVault(new MemorySecretStore(),Path.Combine(output,"credential-metadata",Guid.NewGuid().ToString("N")));
    private static void CredentialChecks()
    {
        var dir=Path.Combine(output,"credential-checks",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var backend=new MemorySecretStore();var vault=new BrokerCredentialVault(backend,dir);
        BrokerConnection Connection(Brokerage id=Brokerage.Kiwoom,bool primary=true)=>new(){Brokerage=id,AppKey="DUMMY-KEY-SECURE-CHECK",Secret="DUMMY-SECRET-SECURE-CHECK",UseAsDefault=primary};
        vault.Save(Connection());Check(vault.Load(Brokerage.Kiwoom)?.Secret==Connection().Secret&&new BrokerCredentialVault(backend,dir).DefaultBroker==Brokerage.Kiwoom,"credential service round-trips verified settings and default without database secrets");
        vault.Save(Connection(Brokerage.Toss,false));vault.Delete(Brokerage.Toss);Check(vault.DefaultBroker==Brokerage.Kiwoom&&vault.Load(Brokerage.Kiwoom)!=null,"resetting another broker preserves the default and its protected key");
        backend.Write("Toss",Encoding.UTF8.GetBytes("{}"));var invalid=false;try{vault.Load(Brokerage.Toss);}catch(InvalidOperationException){invalid=true;}Check(invalid,"malformed or mismatched credential records are rejected rather than returning empty valid keys");
        vault.DeleteAll();Check(vault.DefaultBroker==null&&vault.Load(Brokerage.Kiwoom)==null,"all-key reset removes only known keys and the default preference");
        if(OperatingSystem.IsWindows())
        {
            var windows=new BrokerCredentialVault(new WindowsDpapiSecretStore(dir),dir);windows.Save(Connection());var file=Path.Combine(dir,"Kiwoom.protected");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains(Connection().Secret)&&new BrokerCredentialVault(new WindowsDpapiSecretStore(dir),dir).Load(Brokerage.Kiwoom)?.AppKey==Connection().AppKey,"real Windows CurrentUser DPAPI encrypts isolated dummy keys and decrypts them after vault recreation");
            var bytes=File.ReadAllBytes(file);bytes[^1]^=0x7F;File.WriteAllBytes(file,bytes);invalid=false;try{windows.Load(Brokerage.Kiwoom);}catch(InvalidOperationException){invalid=true;}Check(invalid,"DPAPI tampering fails closed and never produces a usable connection");windows.DeleteAll();
        }
        if(OperatingSystem.IsMacOS())
        {
            var macDir=Path.Combine(dir,"mac-native",Guid.NewGuid().ToString("N"));
            var native=new MacKeychainSecretStore(macDir);
            var mac=new BrokerCredentialVault(native,macDir);
            try
            {
                Check(native.Read("Kiwoom")==null,"unique macOS Keychain test scope starts without an existing user credential");
                mac.Save(Connection());
                var restored=new BrokerCredentialVault(new MacKeychainSecretStore(macDir),macDir);
                Check(restored.Load(Brokerage.Kiwoom)?.Secret==Connection().Secret&&restored.DefaultBroker==Brokerage.Kiwoom,"native macOS Keychain round-trips dummy credentials and restored default account");
                var changed=new BrokerConnection{Brokerage=Brokerage.Kiwoom,AppKey=Connection().AppKey,Secret="DUMMY-UPDATED-MAC-SECRET",UseAsDefault=true};mac.Save(changed);
                Check(mac.Load(Brokerage.Kiwoom)?.Secret==changed.Secret&&Directory.EnumerateFiles(macDir).All(path=>!File.ReadAllText(path).Contains("DUMMY-")),"macOS Keychain updates the dummy secret without plaintext credential files");
                mac.DeleteAll();
                Check(native.Read("Kiwoom")==null&&mac.DefaultBroker==null,"macOS Keychain dummy reset removes only its isolated record and metadata");
            }
            finally { mac.DeleteAll(); }
        }
        var now=new DateTimeOffset(2026,11,6,10,30,0,TimeSpan.FromHours(9));var failed=new ReadOnlyBroker(now){BalanceTimeout=true};
        using(var services=new AppServices(Path.Combine(dir,"failed"),new MarketFixture(),brokerFactory:(_,_)=>failed,credentials:vault))
        using(var account=new AccountWorkspace(services))
        {
            account.Initialize();account.AppKey=Connection().AppKey;account.Secret=Connection().Secret;account.Connect.Execute(null);Pump(()=>!account.Busy);
            Check(!account.Online&&vault.Load(Brokerage.Kiwoom)==null&&vault.DefaultBroker==null,"failed account verification saves neither key nor default preference");
        }
        var creates=0;
        using(var services=new AppServices(Path.Combine(dir,"verified"),new MarketFixture(),brokerFactory:(_,_)=>{creates++;return new ReadOnlyBroker(now);},credentials:vault))
        {
            using(var first=new AccountWorkspace(services)){first.Initialize();first.AppKey=Connection().AppKey;first.Secret=Connection().Secret;first.Connect.Execute(null);Pump(()=>!first.Busy&&first.Online);Check(vault.Load(Brokerage.Kiwoom)!=null&&first.AppKey==""&&!first.Reveal,"only a verified account is saved and all secret editor values are cleared");}
            using var second=new AccountWorkspace(services);second.Initialize();var dialogs=0;second.ConnectionRequested+=()=>dialogs++;second.OnlineMode.Execute(null);Pump(()=>!second.Busy&&second.Online);
            Check(creates==2&&dialogs==0,"recreated account workspace verifies the saved default without opening the key dialog");
            second.Confirm=_=>Task.FromResult(false);second.ResetSelected.Execute(null);Pump(()=>!second.Busy);Check(second.Online&&vault.Load(Brokerage.Kiwoom)!=null,"cancelled key reset preserves the verified session and OS-protected credentials");
            second.Confirm=_=>Task.FromResult(true);second.ResetSelected.Execute(null);Pump(()=>!second.Busy&&!second.Online);Check(vault.Load(Brokerage.Kiwoom)==null&&vault.DefaultBroker==null,"confirmed reset disconnects before deleting the saved key and default");
        }
        backend.Dispose();
        using var delayed=new DelayedCredentialVault(Connection());using var slowServices=new AppServices(Path.Combine(dir,"delayed-editor"),new MarketFixture(),credentials:delayed);using var editor=new AccountWorkspace(slowServices);editor.Initialize();editor.LoadSaved.Execute(null);Pump(()=>delayed.Entered);editor.AppKey="DUMMY-NEW-USER-EDIT";delayed.Release.Set();Pump(()=>!editor.Busy);Check(editor.AppKey=="DUMMY-NEW-USER-EDIT","late OS-vault load cannot overwrite newer credential input");
        delayed.Release.Reset();delayed.Entered=false;editor.LoadSaved.Execute(null);Pump(()=>delayed.Entered);editor.ClearInputs();delayed.Release.Set();Pump(()=>!editor.Busy);Check(editor.AppKey==""&&editor.Secret==""&&!editor.Reveal,"closing or clearing an editor invalidates its pending secret load");
    }
    private sealed class DelayedCredentialVault(BrokerConnection value):IBrokerCredentialVault,IDisposable
    {
        public volatile bool Entered;public ManualResetEventSlim Release {get;}=new(false);public Brokerage? DefaultBroker=>null;
        public BrokerConnection? Load(Brokerage id){Entered=true;Release.Wait();return value;}
        public void Save(BrokerConnection connection)=>throw new NotSupportedException();public void Delete(Brokerage id)=>throw new NotSupportedException();public void DeleteAll()=>throw new NotSupportedException();public void Dispose()=>Release.Dispose();
    }
}
