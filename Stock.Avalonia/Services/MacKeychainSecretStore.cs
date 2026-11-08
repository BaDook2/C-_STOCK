using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Stock.Core.Security;
namespace Stock.Desktop;

/// <summary>Generic-password items in login Keychain; secrets never enter process arguments or files.</summary>
public sealed class MacKeychainSecretStore(string profileDirectory) : ISecretStore
{
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string Foundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private readonly string service = "com.stock.avalonia.broker." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(profileDirectory))))[..24];
    private static readonly Lazy<IntPtr> security = new(() => NativeLibrary.Load(Security));
    private static readonly Lazy<IntPtr> foundation = new(() => NativeLibrary.Load(Foundation));
    private static IntPtr Key(string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(security.Value, name));
    private static IntPtr Core(string name) => Marshal.ReadIntPtr(NativeLibrary.GetExport(foundation.Value, name));
    private IntPtr Query(string name)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("Keychain 저장은 macOS에서만 사용할 수 있습니다.");
        var query = Dictionary();
        try { CFDictionarySetValue(query, Key("kSecClass"), Key("kSecClassGenericPassword")); SetString(query, "kSecAttrService", service); SetString(query, "kSecAttrAccount", name); return query; }
        catch { CFRelease(query); throw; }
    }
    private static IntPtr Dictionary()
    {
        var value = CFDictionaryCreateMutable(IntPtr.Zero, 0, NativeLibrary.GetExport(foundation.Value, "kCFTypeDictionaryKeyCallBacks"), NativeLibrary.GetExport(foundation.Value, "kCFTypeDictionaryValueCallBacks"));
        return value != IntPtr.Zero ? value : throw new InvalidOperationException("Keychain 입력을 만들지 못했습니다.");
    }
    private static void SetString(IntPtr query, string key, string value)
    {
        var text = CFStringCreateWithCString(IntPtr.Zero, value, 0x08000100);
        if (text == IntPtr.Zero) throw new InvalidOperationException("Keychain 입력을 만들지 못했습니다.");
        try { CFDictionarySetValue(query, Key(key), text); } finally { CFRelease(text); }
    }
    private static void Check(int status) { if (status != 0) throw new InvalidOperationException($"Keychain 접근을 완료하지 못했습니다 (OSStatus {status}). 키체인 잠금과 앱 접근 권한을 확인하세요."); }
    public byte[]? Read(string name)
    {
        var query = Query(name); IntPtr result = IntPtr.Zero;
        try
        {
            CFDictionarySetValue(query, Key("kSecReturnData"), Core("kCFBooleanTrue")); CFDictionarySetValue(query, Key("kSecMatchLimit"), Key("kSecMatchLimitOne"));
            var status = SecItemCopyMatching(query, out result); if (status == -25300) return null; Check(status);
            if (result == IntPtr.Zero || CFGetTypeID(result) != CFDataGetTypeID()) throw new InvalidOperationException("Keychain 응답 형식이 올바르지 않습니다.");
            var length = CFDataGetLength(result); if (length < 0 || length > 65_536) throw new InvalidOperationException("저장된 키 형식이 올바르지 않습니다.");
            var bytes = new byte[(int)length]; Marshal.Copy(CFDataGetBytePtr(result), bytes, 0, bytes.Length); return bytes;
        }
        finally { if (result != IntPtr.Zero) CFRelease(result); CFRelease(query); }
    }
    public void Write(string name, byte[] secret)
    {
        var query = Query(name); IntPtr attributes = IntPtr.Zero, data = IntPtr.Zero;
        try
        {
            attributes = Dictionary(); data = CFDataCreate(IntPtr.Zero, secret, secret.Length);
            if (data == IntPtr.Zero) throw new InvalidOperationException("Keychain 입력을 만들지 못했습니다.");
            CFDictionarySetValue(attributes, Key("kSecValueData"), data); var status = SecItemUpdate(query, attributes);
            if (status == -25300) { CFDictionarySetValue(query, Key("kSecValueData"), data); status = SecItemAdd(query, IntPtr.Zero); } Check(status);
        }
        finally { if (data != IntPtr.Zero) CFRelease(data); if (attributes != IntPtr.Zero) CFRelease(attributes); CFRelease(query); }
    }
    public void Delete(string name) { var query = Query(name); try { var status = SecItemDelete(query); if (status != -25300) Check(status); } finally { CFRelease(query); } }
    [DllImport(Security)] private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
    [DllImport(Security)] private static extern int SecItemUpdate(IntPtr query, IntPtr attributes);
    [DllImport(Security)] private static extern int SecItemAdd(IntPtr attributes, IntPtr result);
    [DllImport(Security)] private static extern int SecItemDelete(IntPtr query);
    [DllImport(Foundation)] private static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, nint capacity, IntPtr keys, IntPtr values);
    [DllImport(Foundation)] private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);
    [DllImport(Foundation)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string text, uint encoding);
    [DllImport(Foundation)] private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, nint length);
    [DllImport(Foundation)] private static extern nint CFDataGetLength(IntPtr data);
    [DllImport(Foundation)] private static extern IntPtr CFDataGetBytePtr(IntPtr data);
    [DllImport(Foundation)] private static extern nuint CFGetTypeID(IntPtr data);
    [DllImport(Foundation)] private static extern nuint CFDataGetTypeID();
    [DllImport(Foundation)] private static extern void CFRelease(IntPtr value);
}
