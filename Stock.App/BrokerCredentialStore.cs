using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Stock.Core;
namespace Stock.App;

/// <summary>Windows DPAPI CurrentUser로 암호화한 증권사별 연결 정보 저장소.</summary>
public sealed class BrokerCredentialStore(string directory)
{
    public Brokerage DefaultBroker
    {
        get{try{var value=File.ReadAllText(Path.Combine(directory,"default-broker.txt"));if(Enum.TryParse<Brokerage>(value,out var id)&&BrokerageInfo.All.Any(b=>b.Id==id&&b.Available))return id;}catch(IOException){}return Brokerage.Kiwoom;}
    }
    private string FileName(Brokerage id)
    {if(!Enum.IsDefined(id))throw new ArgumentOutOfRangeException(nameof(id));return Path.Combine(directory,id+".protected");}
    public BrokerConnection? Load(Brokerage id)
    {
        var path=FileName(id);if(!File.Exists(path))return null;
        var plain=Protect(File.ReadAllBytes(path),false);
        try{var c=JsonSerializer.Deserialize<BrokerConnection>(plain)??throw new InvalidOperationException("저장된 키를 읽지 못했습니다.");if(c.Brokerage!=id)throw new InvalidOperationException("저장된 증권사가 일치하지 않습니다.");c.Validate();return c;}
        finally{CryptographicOperations.ZeroMemory(plain);}
    }
    /// <summary>호출자는 실전 인증과 계좌 조회가 성공한 뒤에만 저장합니다.</summary>
    public void Save(BrokerConnection connection)
    {
        connection.Validate();var plain=JsonSerializer.SerializeToUtf8Bytes(connection);byte[] encrypted;
        try{encrypted=Protect(plain,true);}finally{CryptographicOperations.ZeroMemory(plain);}
        Directory.CreateDirectory(directory);var target=FileName(connection.Brokerage);var temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(temp,encrypted);File.Move(temp,target,true);if(connection.UseAsDefault){var defaultPath=Path.Combine(directory,"default-broker.txt");File.WriteAllText(temp,connection.Brokerage.ToString());File.Move(temp,defaultPath,true);}}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public void Delete(Brokerage id)
    {
        File.Delete(FileName(id));
        var preference=Path.Combine(directory,"default-broker.txt");
        if(File.Exists(preference)&&File.ReadAllText(preference)==id.ToString())File.Delete(preference);
    }
    /// <summary>Only known credential files and the default selection; never user trading databases.</summary>
    public void DeleteAll()
    {
        foreach(var id in Enum.GetValues<Brokerage>())File.Delete(FileName(id));
        File.Delete(Path.Combine(directory,"default-broker.txt"));
    }
    [StructLayout(LayoutKind.Sequential)]private struct Blob {public int Length;public IntPtr Data;}
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Protect(byte[] bytes,bool encrypt)
    {
        var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            var ok=encrypt?CryptProtectData(ref input,"STOCK broker key",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new InvalidOperationException("저장된 키를 이 Windows 계정에서 읽거나 저장할 수 없습니다. 키를 다시 입력하세요.");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally
        {
            for(var i=0;i<input.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);
            if(output.Data!=IntPtr.Zero){for(var i=0;i<output.Length;i++)Marshal.WriteByte(output.Data,i,0);LocalFree(output.Data);}
        }
    }
}
