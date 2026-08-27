using System.Threading;
using System.Threading.Tasks;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    internal sealed class CloseOrchestrator
    {
        private readonly bool _owned = true;

        private static readonly string[] Order =
        {
            "input", "prediction", "replica", "voxel", "ecs", "scope", "handshake", "connection"
        };

        public void Release(
            SessionResourceLedger ledger,
            RuntimeHandleLedger handles,
            GameplayScopeActivationGate gate,
            IClientGameplayScopeActivator scope,
            IClientHandshake handshake,
            IClientConnection connection,
            IClientReplica replica,
            IClientPrediction prediction,
            ulong generation)
        {
            if (!_owned)
            {
                return;
            }

            if (replica != null)
            {
                replica.ResetForNewSession(new ReplicaResetRequest(generation));
            }

            if (prediction != null)
            {
                prediction.ResetForNewSession(new PredictionResetRequest(generation, 8));
            }

            handles.DestroyVoxelThenEcs();
            if (gate.Activated)
            {
                ValueTask<GameplayScopeReleaseResult> released = scope.ReleaseAsync(new GameplayScopeLease(generation), CancellationToken.None);
                _ = released.IsCompleted;
            }

            gate.Reset();
            if (handshake != null)
            {
                handshake.Cancel();
            }

            if (connection != null)
            {
                connection.RequestClose(ConnectionCloseReason.OwnerRequest);
            }

            ledger.ReleaseInOrder(Order);
            ledger.ReleaseRemaining();
        }
    }
}
