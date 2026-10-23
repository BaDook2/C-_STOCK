using Stock.Core.Trading;
namespace Stock.Core;

public enum Brokerage { Mirae, Kiwoom, KoreaInvestment, Samsung, Meritz, Shinhan, Toss }
public sealed record BrokerageInfo(Brokerage Id,string Name,string Notice,bool Available)
{
    public static IReadOnlyList<BrokerageInfo> All {get;} = [
        new(Brokerage.Mirae,"미래에셋증권","주식거래 FIX/API 계약 및 연동 규격이 필요합니다. 현재 이 앱에서는 연결할 수 없습니다.",false),
        new(Brokerage.Kiwoom,"키움증권","키움 REST API 실전 App Key와 Secret Key를 입력하세요.",true),
        new(Brokerage.KoreaInvestment,"한국투자증권","KIS Developers 실전 App Key·Secret과 계좌번호를 입력하세요. 현재 이 앱의 주문은 국내주식만 지원합니다.",true),
        new(Brokerage.Samsung,"삼성증권","개인 주식거래용 API 연동 규격 확인이 필요합니다. 현재 이 앱에서는 연결할 수 없습니다.",false),
        new(Brokerage.Meritz,"메리츠증권","메리츠 Open API 계좌별 App Key·Secret을 입력하세요. 현재 이 앱의 연결과 주문은 국내주식만 지원합니다.",true),
        new(Brokerage.Shinhan,"신한투자증권","신한i indi 설치·서비스 신청과 별도 로그인 연동이 필요합니다. 현재 이 앱에서는 연결할 수 없습니다.",false),
        new(Brokerage.Toss,"토스증권","토스증권 Open API Client ID·Secret을 입력하세요. 허용 IP 등록이 필요합니다. 이 앱에서는 국내주식 주문과 분봉을 지원합니다.",true)
    ];
}
/// <summary>증권사별 인증 정보. ToString으로 비밀을 노출하지 않습니다.</summary>
public sealed class BrokerConnection
{
    public Brokerage Brokerage {get;init;}
    public string AppKey {get;init;}="";
    public string Secret {get;init;}="";
    public string Account {get;init;}="";
    public string Product {get;init;}="01";
    public string MacAddress {get;init;}="";
    public bool UseAsDefault {get;init;}
    public void Validate()
    {
        if(!BrokerageInfo.All.Any(b=>b.Id==Brokerage&&b.Available))throw new InvalidOperationException("이 증권사는 아직 연결을 지원하지 않습니다.");
        if(string.IsNullOrWhiteSpace(AppKey)||string.IsNullOrWhiteSpace(Secret))throw new InvalidOperationException("App Key와 Secret Key를 입력하세요.");
        if(Brokerage==Brokerage.KoreaInvestment)new Credentials{AppKey=AppKey,AppSecret=Secret,Account=Account,Product=Product}.Validate();
        if(Brokerage==Brokerage.Meritz&&!System.Text.RegularExpressions.Regex.IsMatch(MacAddress,"^[0-9A-Fa-f]{12}$"))throw new InvalidOperationException("사용할 네트워크 장치의 MAC 주소 12자리를 입력하세요.");
    }
    public KiwoomCredentials Kiwoom()=>new(){AppKey=AppKey,SecretKey=Secret};
}
public interface IAccountBroker : IBroker,IDisposable
{
    string Id {get;}
    string Name {get;}
    bool IsLive {get;}
    Exchange Exchange {get;}
    string Account {get;}
    string MaskedAccount {get;}
    bool SupportsMarketOrders {get;}
    Task<decimal> CurrentPrice(string symbol);
    Task<OrderReceipt> CancelAsync(string number,string symbol);
}
public sealed record BrokerExecution(string Id,string OrderNumber,string Symbol,bool Buy,int Quantity,decimal Price,DateTimeOffset Time);
public interface IExecutionHistoryBroker
{
    Task<IReadOnlyList<BrokerExecution>> GetExecutionsAsync(DateOnly date);
}
