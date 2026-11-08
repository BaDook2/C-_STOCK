using System.Runtime.InteropServices;
namespace Stock.Core.Security;

/// <summary>Existing WPF DPAPI CurrentUser format, extracted without changing ciphertext paths or scope.</summary>
public sealed class WindowsDpapiSecretStore(string directory) : ISecretStore
{
    private string FileName(string name)
    {
        if (!Enum.TryParse<Brokerage>(name, out var id) || !Enum.IsDefined(id)) throw new ArgumentException("지원하지 않는 키 이름입니다.");
        return Path.Combine(directory, id + ".protected");
    }
    public byte[]? Read(string name)
    {
        var path = FileName(name); if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 1_048_576) throw new InvalidOperationException("저장된 키 파일이 올바르지 않습니다.");
        return Protect(File.ReadAllBytes(path), false);
    }
    public void Write(string name, byte[] secret)
    {
        var encrypted = Protect(secret, true); Directory.CreateDirectory(directory);
        var path = FileName(name); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, encrypted); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Delete(string name) => File.Delete(FileName(name));
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Protect(byte[] bytes, bool encrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows 보안 저장은 Windows 계정에서만 사용할 수 있습니다.");
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) }; Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            var ok = encrypt ? CryptProtectData(ref input, "STOCK broker key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new InvalidOperationException("저장된 키를 이 Windows 계정에서 읽거나 저장할 수 없습니다. 키를 다시 입력하세요.");
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            for (var i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0); Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) { for (var i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0); LocalFree(output.Data); }
        }
    }
}
