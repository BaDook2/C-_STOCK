using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Stock.Core;

public record LocalOrder(string Id, DateTimeOffset Time, string Fingerprint, string Symbol, string Side,
    int Quantity, decimal Price, string Status, string Number,string Market="KRX",string Currency="KRW");

// Write-ahead journal: a crash between transmission and acknowledgement remains unresolved.
public sealed class OrderGuard
{
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<LocalOrder> entries;
    public IReadOnlyList<LocalOrder> Entries => entries.AsReadOnly();
    public bool HasUnresolved => entries.Any(e => e.Status is "전송 중" or "응답 불명");
    public OrderGuard(string path)
    {
        this.path = path;
        entries = File.Exists(path) ? JsonSerializer.Deserialize<List<LocalOrder>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("주문 기록을 읽을 수 없어 주문을 차단합니다.") : [];
    }
    public static string AccountScope(Credentials c) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(c.Account + ":" + c.Product)))[..24];
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, entries);
            stream.Flush(true);
        }
        File.Move(temp, path, true);
    }
    private void Replace(LocalOrder value)
    {
        entries[entries.FindIndex(e => e.Id == value.Id)] = value;
        Save();
    }
    public async Task<OrderReceipt> SubmitAsync(IBroker broker, OrderRequest order,string? clientOrderId=null,Func<bool>? canTransmit=null)
    {
        order.Validate();
        if (!await gate.WaitAsync(0)) throw new InvalidOperationException("주문 처리 중입니다. 중복 전송을 차단했습니다.");
        try
        {
            if (HasUnresolved) throw new InvalidOperationException("응답 불명 주문을 먼저 확인하세요. 신규 주문이 차단되었습니다.");
            if(clientOrderId!=null&&entries.Any(e=>e.Id==clientOrderId))throw new InvalidOperationException("이미 기록된 자동 주문 ID입니다. 재전송하지 않습니다.");
            if (clientOrderId==null&&entries.Any(e => e.Fingerprint == order.Fingerprint && e.Status != "거부" &&
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(e.Time,"Korea Standard Time").DateTime) == KoreaClock.Today))
                throw new InvalidOperationException("오늘 동일한 종목·방향·가격·수량의 주문 기록이 있습니다. 중복 주문을 차단했습니다.");
            if(canTransmit?.Invoke()==false)throw new InvalidOperationException("전송 전 중지/시세 만료로 차단했습니다.");
            var entry = new LocalOrder(clientOrderId??Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, order.Fingerprint, order.Symbol,
                order.Buy ? "매수" : "매도", order.Quantity, order.Price, "전송 중", "",order.Market.ToString(),new Trading.Instrument(order.Symbol,order.Market).Currency);
            entries.Add(entry);
            Save(); // Failure here must prevent transmission.
            if(canTransmit?.Invoke()==false){Replace(entry with{Status="전송 전 중지"});throw new InvalidOperationException("전송 전 중지/시세 만료로 차단했습니다.");}
            try
            {
                var receipt = await broker.SubmitAsync(order);
                Replace(entry with { Status = "접수", Number = receipt.Number });
                return receipt;
            }
            catch (BrokerRejectedException)
            {
                Replace(entry with { Status = "거부" });
                throw;
            }
            catch
            {
                // Includes local persistence failure AFTER a successful remote submission.
                try { Replace(entry with { Status = "응답 불명" }); } catch { /* Existing write-ahead record stays on disk. */ }
                throw new UnknownOrderException();
            }
        }
        finally { gate.Release(); }
    }
    public void AcknowledgeExternalReview()
    {
        if (!gate.Wait(0)) throw new InvalidOperationException("주문 처리 중에는 확인할 수 없습니다.");
        try
        {
            var previous = entries;
            entries = entries.Select(e => e.Status is "전송 중" or "응답 불명" ? e with { Status = "외부 확인 완료" } : e).ToList();
            try { Save(); }
            catch { entries = previous; throw; }
        }
        finally { gate.Release(); }
    }
}
