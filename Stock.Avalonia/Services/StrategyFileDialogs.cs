using Avalonia.Controls;
using Avalonia.Platform.Storage;
namespace Stock.Desktop;
public interface IStrategyFileDialogs
{
    Task<string?> Import();
    Task<string?> Export(string suggestedName);
}
public sealed class StrategyFileDialogs(Func<TopLevel?> owner):IStrategyFileDialogs
{
    private static readonly FilePickerFileType Json=new("전략 JSON"){Patterns=["*.json"]};
    public async Task<string?> Import()
    {var source=owner()?.StorageProvider??throw new InvalidOperationException("파일 선택 창을 열 수 없습니다.");var files=await source.OpenFilePickerAsync(new(){Title="전략 JSON 불러오기",AllowMultiple=false,FileTypeFilter=[Json]});return files.FirstOrDefault()?.TryGetLocalPath();}
    public async Task<string?> Export(string suggestedName)
    {var source=owner()?.StorageProvider??throw new InvalidOperationException("파일 저장 창을 열 수 없습니다.");var file=await source.SaveFilePickerAsync(new(){Title="전략 JSON 내보내기",SuggestedFileName=suggestedName,DefaultExtension="json",FileTypeChoices=[Json]});return file?.TryGetLocalPath();}
}
