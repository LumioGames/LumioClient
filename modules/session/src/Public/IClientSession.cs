using System.Threading;

namespace Lumio.Client.Session
{
    public enum ClientSessionState
    {
        Disconnected,
        Connecting,
        Negotiating,
        Synchronizing,
        Active,
        Resyncing,
        Reconnecting,
        Closed,
        Faulted
    }

    public interface IClientSession
    {
        SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken);

        SessionTickResult Tick(in ClientOwnerTick tick);

        SessionCommandResult RequestClose(in SessionCloseRequest request);

        ClientSessionSnapshot GetSnapshot();
    }
}
