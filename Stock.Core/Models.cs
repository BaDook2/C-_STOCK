using System.Globalization;
using System.Text.RegularExpressions;

namespace Stock.Core;

public sealed class Credentials
{
    public required string AppKey { get; init; }
    public required string AppSecret { get; init; }
    public required string Account { get; init; }
    public required string Product { get; init; }
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AppKey) || string.IsNullOrWhiteSpace(AppSecret) ||
            !Regex.IsMatch(Account, "^[0-9]{8}$") || !Regex.IsMatch(Product, "^[0-9]{2}$"))
            throw new InvalidOperationException("로컬 연결 설정을 확인하세요. 계좌 앞 8자리와 상품코드 2자리가 필요합니다.");
    }
    public static Credentials FromEnvironment() => new()
    {
        AppKey = Environment.GetEnvironmentVariable("STOCK_KIS_APP_KEY") ?? "",
        AppSecret = Environment.GetEnvironmentVariable("STOCK_KIS_APP_SECRET") ?? "",
        Account = Environment.GetEnvironmentVariable("STOCK_KIS_ACCOUNT") ?? "",
        Product = Environment.GetEnvironmentVariable("STOCK_KIS_PRODUCT") ?? "01"
    };
}
public record OrderRequest(string Symbol, int Quantity, decimal Price, bool Buy, Trading.Exchange Market = Trading.Exchange.KRX, Trading.OrderKind Kind = Trading.OrderKind.Limit)
{
    public void Validate()
    {
        new Trading.Instrument(Symbol,Market).Validate();
        if (Quantity <= 0 || Quantity > 1_000_000) throw new InvalidOperationException("수량은 1~1,000,000주여야 합니다.");
        if (Price <= 0 || Price > 100_000_000 || Market == Trading.Exchange.KRX && decimal.Truncate(Price) != Price || decimal.Round(Price,4)!=Price)
            throw new InvalidOperationException("가격은 양수이며 KRW 정수 / USD 소수 4자리 이하입니다.");
        var tick=new Trading.Instrument(Symbol,Market).Tick(Price);
        if(Kind==Trading.OrderKind.Limit && Price%tick!=0)throw new InvalidOperationException("지정가가 해당 가격대의 호가 단위와 맞지 않습니다.");
    }
    public string Fingerprint => $"{Market}:{Symbol}:{Quantity}:{Price.ToString(CultureInfo.InvariantCulture)}:{Buy}:{Kind}";
}
public record OrderReceipt(string Number);
public record Holding(string Symbol, string Name, decimal Quantity, decimal AveragePrice, decimal CurrentPrice, decimal Profit,string Currency="KRW",string Market="KRX");
public record Balance(IReadOnlyList<Holding> Holdings, decimal Cash, decimal Total,string Currency="KRW",decimal? OrderableCash=null);
public record BrokerOrder(string Date, string Number, string Symbol, string Name, string Side,
    decimal Quantity, decimal Price, decimal Filled, decimal Remaining, bool Cancelled, bool Rejected,string Currency="KRW",string Market="KRX",string BrokerStatus="",string OriginalNumber="",decimal? AverageFillPrice=null,DateTimeOffset? LastFillTime=null)
{
    public string Status => Rejected ? "거부" : Cancelled ? (Filled > 0 ? "부분체결 후 취소" : "취소") :
        Quantity > 0 && Filled >= Quantity ? "체결 완료" : Filled > 0 ? "부분 체결" : "접수 / 미체결";
}
public interface IBroker
{
    Task ConnectAsync();
    Task<OrderReceipt> SubmitAsync(OrderRequest order);
    Task<Balance> GetBalanceAsync();
    Task<IReadOnlyList<BrokerOrder>> GetOrdersAsync(DateOnly date);
}
public sealed class BrokerRejectedException(string message) : Exception(message);
public sealed class UnknownOrderException() : Exception("주문 응답 불명: 자동 재전송하지 않습니다. 주문 내역 및 모의투자 HTS에서 확인하세요.");
public static class KoreaClock
{
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Korea Standard Time").DateTime);
}
