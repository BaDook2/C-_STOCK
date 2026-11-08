namespace Stock.Desktop;
internal static class StartupDiagnostics
{
    // Record type/stack only. Broker responses, credentials and arbitrary exception messages are excluded.
    public static string? Record(Exception error)
    {
        try{var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Stock.Avalonia","diagnostics");Directory.CreateDirectory(root);var path=Path.Combine(root,"startup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".log");File.WriteAllText(path,DateTimeOffset.UtcNow+"\n"+error.GetType().FullName+"\n"+error.StackTrace);return path;}catch{return null;}
    }
}
