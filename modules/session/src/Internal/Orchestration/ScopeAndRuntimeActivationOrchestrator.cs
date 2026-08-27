using System.Threading;

namespace Lumio.Client.Session
{
    internal sealed class ScopeAndRuntimeActivationOrchestrator
    {
        private readonly bool _owned = true;

        public bool Activate(IClientGameplayScopeActivator scope, GameplayScopeActivationGate gate, RuntimeHandleLedger handles, ulong generation)
        {
            if (!_owned)
            {
                return false;
            }

            if (!gate.Prepared)
            {
                GameplayScopePrepareRequest prepare = new GameplayScopePrepareRequest(generation);
                var pending = scope.PrepareAsync(in prepare, CancellationToken.None);
                if (!pending.IsCompleted || !pending.Result.Succeeded)
                {
                    return false;
                }

                gate.TryPrepare();
            }

            if (!gate.Activated)
            {
                if (!scope.ActivateAtTickBarrier(new GameplayScopeActivationRequest(generation)).Succeeded)
                {
                    return false;
                }

                if (!gate.TryActivate())
                {
                    return false;
                }
            }

            if (!gate.CanCreateWorldHandles())
            {
                return false;
            }

            if (!handles.TryCreateEcs())
            {
                return false;
            }

            if (!handles.TryCreateVoxel())
            {
                handles.RollbackEcsOnVoxelFailure();
                return false;
            }

            return true;
        }
    }
}
