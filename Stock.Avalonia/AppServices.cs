using Stock.Core.Research;
using Stock.Core.Research.Oversold;
using Stock.Core.Research.DayTrading;
using Stock.Core.Trading;
using Stock.Core;
using Stock.Core.Security;
namespace Stock.Desktop;

/// <summary>Explicit composition root. Independent data directory and one shared research service per algorithm.</summary>
public sealed class AppServices:IDisposable
{
    public TradingStore Store {get;}
    public IPublicMarketData MarketData {get;}
    public IOversoldBacktestService Backtests {get;}
    public IntradayBacktestService IntradayBacktests {get;}=new();
    public string DataDirectory {get;}
    public Func<BrokerConnection,Exchange,IAccountBroker> BrokerFactory {get;}
    public Func<DateTimeOffset> Clock {get;}
    public TimeSpan QueryTimeout {get;}
    public IBrokerCredentialVault Credentials {get;}
    public Func<IAccountBroker,Instrument[],DataSettings,Action<string>,IMarketDataProvider> LiveFeedFactory {get;}
    public AppServices(string dataDirectory,IPublicMarketData? market=null,IOversoldBacktestService? backtests=null,Func<BrokerConnection,Exchange,IAccountBroker>? brokerFactory=null,Func<DateTimeOffset>? clock=null,TimeSpan? queryTimeout=null,IBrokerCredentialVault? credentials=null,Func<IAccountBroker,Instrument[],DataSettings,Action<string>,IMarketDataProvider>? liveFeedFactory=null)
    {
        QueryTimeout=queryTimeout??TimeSpan.FromSeconds(45);if(QueryTimeout<=TimeSpan.Zero||QueryTimeout>TimeSpan.FromMinutes(5))throw new ArgumentOutOfRangeException(nameof(queryTimeout));
        DataDirectory=Path.GetFullPath(dataDirectory);Directory.CreateDirectory(DataDirectory);Store=new(Path.Combine(DataDirectory,"workspace.db"),memoryMarketData:true);
        var vaultPath=Path.Combine(DataDirectory,"credentials");
        Credentials=credentials??new BrokerCredentialVault(OperatingSystem.IsWindows()?new WindowsDpapiSecretStore(vaultPath):OperatingSystem.IsMacOS()?new MacKeychainSecretStore(vaultPath):throw new PlatformNotSupportedException("이 OS의 안전한 키 저장소를 지원하지 않습니다."),vaultPath);
        LiveFeedFactory=liveFeedFactory??((broker,instruments,settings,status)=>broker is KiwoomBroker kiwoom?new KiwoomMarketDataProvider(kiwoom.GetTokenAsync,instruments,settings,status,live:true):throw new InvalidOperationException("검증된 키움 실제 체결·호가 피드가 필요합니다."));
        MarketData=market??new PublicMarketData();Backtests=backtests??new OversoldBacktestService();BrokerFactory=brokerFactory??CreateBroker;Clock=clock??(()=>DateTimeOffset.UtcNow);
    }
    private static IAccountBroker CreateBroker(BrokerConnection connection,Exchange exchange)
    {
        connection.Validate();return connection.Brokerage switch
        {Brokerage.Kiwoom=>new KiwoomBroker(connection.Kiwoom(),exchange,live:true),Brokerage.KoreaInvestment=>new KisBroker(new(){AppKey=connection.AppKey,AppSecret=connection.Secret,Account=connection.Account,Product=connection.Product},live:true),Brokerage.Meritz=>new MeritzBroker(connection),Brokerage.Toss=>new TossBroker(connection),_=>throw new InvalidOperationException("지원하지 않는 증권사입니다.")};
    }
    public void Dispose(){if(MarketData is IDisposable disposable)disposable.Dispose();Store.Dispose();}
}
