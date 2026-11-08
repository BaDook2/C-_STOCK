using System.Security.Cryptography;
using System.Text.Json;
namespace Stock.Core.Security;

/// <summary>OS-protected bytes; implementations must never fall back to plaintext.</summary>
public interface ISecretStore
{
    byte[]? Read(string name);
    void Write(string name, byte[] secret);
    void Delete(string name);
}
public interface IBrokerCredentialVault
{
    Brokerage? DefaultBroker { get; }
    BrokerConnection? Load(Brokerage id);
    void Save(BrokerConnection connection);
    void Delete(Brokerage id);
    void DeleteAll();
}
/// <summary>Shared WPF/Avalonia credential format. Only the non-secret default broker is a text file.</summary>
public sealed class BrokerCredentialVault(ISecretStore secrets, string directory) : IBrokerCredentialVault
{
    private string Preference => Path.Combine(directory, "default-broker.txt");
    public Brokerage? DefaultBroker
    {
        get
        {
            if (!File.Exists(Preference)) return null;
            var value = File.ReadAllText(Preference);
            return Enum.TryParse<Brokerage>(value, out var id) && BrokerageInfo.All.Any(b => b.Id == id && b.Available) ? id : null;
        }
    }
    private static string Name(Brokerage id) => Enum.IsDefined(id) ? id.ToString() : throw new ArgumentOutOfRangeException(nameof(id));
    public BrokerConnection? Load(Brokerage id)
    {
        var bytes = secrets.Read(Name(id)); if (bytes is null) return null;
        try
        {
            if (bytes.Length > 65_536) throw new InvalidOperationException("저장된 키 형식이 올바르지 않습니다.");
            var value = JsonSerializer.Deserialize<BrokerConnection>(bytes) ?? throw new InvalidOperationException("저장된 키를 읽지 못했습니다.");
            if (value.Brokerage != id) throw new InvalidOperationException("저장된 증권사가 일치하지 않습니다.");
            value.Validate(); return value;
        }
        catch (JsonException) { throw new InvalidOperationException("저장된 키 형식이 올바르지 않습니다. 키를 다시 입력하세요."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    /// <summary>Caller must authenticate AND read the account successfully before saving.</summary>
    public void Save(BrokerConnection connection)
    {
        connection.Validate(); var bytes = JsonSerializer.SerializeToUtf8Bytes(connection);
        try { secrets.Write(Name(connection.Brokerage), bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
        if (!connection.UseAsDefault) return;
        Directory.CreateDirectory(directory); var temp = Preference + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, Name(connection.Brokerage)); File.Move(temp, Preference, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Delete(Brokerage id) { secrets.Delete(Name(id)); if (DefaultBroker == id) File.Delete(Preference); }
    public void DeleteAll() { foreach (var id in Enum.GetValues<Brokerage>()) secrets.Delete(Name(id)); File.Delete(Preference); }
}
/// <summary>Explicit isolated verifier/test vault. Never selected as a production fallback.</summary>
public sealed class MemorySecretStore : ISecretStore, IDisposable
{
    private readonly Dictionary<string, byte[]> values = [];
    public byte[]? Read(string name) => values.TryGetValue(name, out var bytes) ? bytes.ToArray() : null;
    public void Write(string name, byte[] secret) { Delete(name); values[name] = secret.ToArray(); }
    public void Delete(string name) { if (values.Remove(name, out var bytes)) CryptographicOperations.ZeroMemory(bytes); }
    public void Dispose() { foreach (var name in values.Keys.ToArray()) Delete(name); }
}
