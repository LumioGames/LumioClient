using Lumio.Client.Handshake;

namespace Lumio.Client.Session
{
    internal sealed class FirstConnectOrchestrator
    {
        private readonly bool _owned = true;

        public bool TryEnterSynchronizing(
            HandshakeOutcome accepted,
            ClientConfigStagingArea config,
            ScopeAndRuntimeActivationOrchestrator activation,
            IClientGameplayScopeActivator scope,
            GameplayScopeActivationGate gate,
            RuntimeHandleLedger handles,
            ulong generation)
        {
            if (!_owned || !accepted.Accepted)
            {
                return false;
            }

            config.Stage();
            if (!activation.Activate(scope, gate, handles, generation))
            {
                return false;
            }

            config.ActivateBarrier();
            return true;
        }
    }
}
