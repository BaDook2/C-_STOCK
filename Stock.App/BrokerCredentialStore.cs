using Stock.Core;
using Stock.Core.Security;
namespace Stock.App;

/// <summary>Preserves the original WPF API and DPAPI files through the shared credential service.</summary>
public sealed class BrokerCredentialStore(string directory)
{
    private readonly BrokerCredentialVault vault = new(new WindowsDpapiSecretStore(directory), directory);
    public Brokerage DefaultBroker => vault.DefaultBroker ?? Brokerage.Kiwoom;
    public BrokerConnection? Load(Brokerage id) => vault.Load(id);
    public void Save(BrokerConnection connection) => vault.Save(connection);
    public void Delete(Brokerage id) => vault.Delete(id);
    public void DeleteAll() => vault.DeleteAll();
}
