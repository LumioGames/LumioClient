using System.Threading;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    public interface IClientSession
    {
        SessionCommandResult RequestConnect(in SessionConnectRequest request, CancellationToken cancellationToken);

        SessionCommandResult Login(in SessionConnectRequest request, CancellationToken cancellationToken);

        SessionTickResult Tick(in ClientOwnerTick tick);

        SessionCommandResult RequestClose(in SessionCloseRequest request);

        ClientSessionSnapshot GetSnapshot();

        bool TryDequeueSuperseded(out SessionSupersededNotice notice);

        bool TryGetReplicaWorld(out IReplicaWorld world);
    }
}
