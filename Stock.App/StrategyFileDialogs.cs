namespace Stock.App;

public interface IStrategyFileDialogs
{
    string? Import();
    string? Export(string suggestedName);
}

public sealed class StrategyFileDialogs:IStrategyFileDialogs
{
    public string? Import()
    {var dialog=new Microsoft.Win32.OpenFileDialog{Filter="전략 설정 JSON|*.json",DefaultExt=".json",CheckFileExists=true,Title="전략 JSON 불러오기"};return dialog.ShowDialog()==true?dialog.FileName:null;}
    public string? Export(string suggestedName)
    {var dialog=new Microsoft.Win32.SaveFileDialog{Filter="전략 설정 JSON|*.json",DefaultExt=".json",AddExtension=true,InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),FileName=suggestedName,Title="전략 설정 다운로드"};return dialog.ShowDialog()==true?dialog.FileName:null;}
}
