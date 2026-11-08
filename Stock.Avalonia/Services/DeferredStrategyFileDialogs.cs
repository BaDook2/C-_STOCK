namespace Stock.Desktop;
public sealed class DeferredStrategyFileDialogs(Func<IStrategyFileDialogs?> current):IStrategyFileDialogs
{
    private IStrategyFileDialogs Current=>current()??throw new InvalidOperationException("파일 대화상자가 연결되지 않았습니다.");
    public Task<string?> Import()=>Current.Import();
    public Task<string?> Export(string name)=>Current.Export(name);
}
