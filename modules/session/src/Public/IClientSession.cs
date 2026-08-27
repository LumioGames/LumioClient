using System.Threading;

namespace Lumio.Client.Session
{
    public interface IClientSession
    {
        SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken);

        SessionTickResult Tick(in ClientOwnerTick tick);

        SessionCommandResult RequestClose(in SessionCloseRequest request);

        ClientSessionSnapshot GetSnapshot();
    }
}
