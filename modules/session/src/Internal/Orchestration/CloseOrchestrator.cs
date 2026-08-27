using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    internal sealed class CloseOrchestrator
    {
        private readonly bool _owned = true;
        private readonly List<string> _calls = new List<string>();

        private static readonly string[] Order =
        {
            "input", "prediction", "replica", "voxel", "ecs", "scope", "handshake", "connection"
        };

        public string[] CallOrder
        {
            get { return _calls.ToArray(); }
        }

        public void Release(
            SessionResourceLedger ledger,
            RuntimeHandleLedger handles,
            GameplayScopeActivationGate gate,
            IClientGameplayScopeActivator scope,
            IInputSampleIngress input,
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

            _calls.Clear();
            if (input != null)
            {
                input.DrainAccepted();
                _calls.Add("input");
            }

            if (prediction != null)
            {
                prediction.ResetForNewSession(new PredictionResetRequest(generation, 8));
                _calls.Add("prediction");
            }

            if (replica != null)
            {
                replica.ResetForNewSession(new ReplicaResetRequest(generation));
                _calls.Add("replica");
            }

            handles.DestroyVoxelThenEcs();
            _calls.Add("voxel");
            _calls.Add("ecs");
            if (gate.Activated)
            {
                ValueTask<GameplayScopeReleaseResult> released = scope.ReleaseAsync(new GameplayScopeLease(generation), CancellationToken.None);
                _ = released.IsCompleted;
                _calls.Add("scope");
            }

            gate.Reset();
            if (handshake != null)
            {
                handshake.Cancel();
                _calls.Add("handshake");
            }

            if (connection != null)
            {
                connection.RequestClose(ConnectionCloseReason.OwnerRequest);
                _calls.Add("connection");
            }

            ledger.ReleaseInOrder(Order);
            ledger.ReleaseRemaining();
        }
    }
}
