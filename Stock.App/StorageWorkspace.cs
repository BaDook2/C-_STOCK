using System.IO;
using System.Text.Json;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private async Task CheckStorage()
    {
        var report=await Task.Run(()=>StorageDiagnostics.Inspect(store.Path),Token);
        var directory=Path.Combine(Path.GetDirectoryName(store.Path)!,"storage-diagnostics");Directory.CreateDirectory(directory);
        var destination=Path.Combine(directory,$"inspection-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
        File.WriteAllText(destination,JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        var text=$"저장소 {(report.Healthy?"정상":"오류 확인")}\nDB: {report.DatabasePath}\n실제 파일: {report.PhysicalDatabasePath}\nSQLite: {report.Engine}\n무결성: {string.Join(" / ",report.Integrity)}\nJSON 오류: {report.JsonIssues.Count:N0}개\n{string.Join(" · ",report.Rows.Select(r=>$"{r.Key} {r.Value:N0}건"))}\n{report.Error}\n보고서: {destination}\nDB를 삭제하거나 초기화하지 않았습니다.";
        Log($"저장소 {(report.Healthy?"전체 점검 정상":"전체 점검 오류")} · {destination}");alert(text);
    }
}
