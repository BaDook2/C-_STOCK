using Stock.Core;
using Stock.Core.Trading;
namespace Stock.App;

public sealed partial class WorkspaceViewModel
{
    private sealed record SuspendedConnection(BrokerConnection Connection,IAccountBroker Broker,RemoteTradingSession Session,string AccountInfo);
    private SuspendedConnection? suspendedConnection;
    /// <summary>Retain verified authentication and reconciliation guards for this process, with no active offline feed or order route.</summary>
    private void SuspendConnection()
    {
        StopTicks();
        if(connection!=null&&broker!=null&&session!=null)suspendedConnection=new(connection,broker,session,AccountInfo);
        else broker?.Dispose();
        broker=null;session=null;Orders.Clear();Holdings.Clear();AccountInfo="실제 계좌 미연결";Changed(nameof(ConnectedAccountText));
    }
}
