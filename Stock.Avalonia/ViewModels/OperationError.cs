namespace Stock.Desktop;

/// <summary>Only the caller token proves a user cancellation. HttpClient timeout also throws OperationCanceledException.</summary>
internal static class OperationError
{
 internal const string ResponseTimeout="요청한 작업의 응답 시간이 초과됐습니다. 연결 상태를 확인한 뒤 다시 시도하세요.";
 internal static string Describe(Exception error)=>error is OperationCanceledException or TimeoutException?ResponseTimeout:error.Message;
}
