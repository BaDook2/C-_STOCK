using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Stock.Core.Trading;

/// <summary>Resolve the file actually opened, including Windows packaged-app AppData redirection.</summary>
public static class StoragePaths
{
    public static string PhysicalPath(string path)
    {
        if(!OperatingSystem.IsWindows())return System.IO.Path.GetFullPath(path);
        using var handle=File.OpenHandle(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        var buffer=new StringBuilder(32768);
        if(GetFinalPathNameByHandle(handle,buffer,(uint)buffer.Capacity,0)==0)throw new IOException("DB의 실제 파일 경로를 확인하지 못했습니다.");
        var resolved=buffer.ToString();
        return resolved.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase)?@"\\"+resolved[8..]:resolved.StartsWith(@"\\?\",StringComparison.Ordinal)?resolved[4..]:resolved;
    }

    [DllImport("kernel32.dll",EntryPoint="GetFinalPathNameByHandleW",CharSet=CharSet.Unicode,SetLastError=true,ExactSpelling=true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint length,uint flags);
}
