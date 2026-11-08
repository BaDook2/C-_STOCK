using System.IO;
using Stock.Core.Trading;
internal static partial class Program
{
    private static int passed;
    private static readonly string Dir=Path.Combine(Path.GetTempPath(),"StockAvaloniaChecks-"+Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Epoch=DateTimeOffset.Parse("2026-09-25T13:30:00Z");
    private static int Main()
    {
        Directory.CreateDirectory(Dir);
        try{DayTradingChecks();Console.WriteLine($"PASS: {passed} portable core checks; no WPF, broker calls or account transactions");return 0;}
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);passed++;Console.WriteLine("PASS: "+message);}
    private static void Throws<T>(Action action,string message)where T:Exception{try{action();}catch(T){Check(true,message);return;}throw new Exception("FAIL expected "+typeof(T).Name+": "+message);}
    private static async Task ThrowsAsync<T>(Func<Task> action,string message)where T:Exception{try{await action();}catch(T){Check(true,message);return;}throw new Exception("FAIL expected "+typeof(T).Name+": "+message);}
}
