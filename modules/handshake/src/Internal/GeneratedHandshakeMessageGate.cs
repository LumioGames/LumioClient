namespace Lumio.Client.Handshake
{
    internal sealed class GeneratedHandshakeMessageGate
    {
        private readonly bool _strict = true;

        public bool Allows(HandshakePhase phase, byte messageKind)
        {
            if (!_strict)
            {
                return true;
            }

            if (phase == HandshakePhase.Accepted || phase == HandshakePhase.Rejected || phase == HandshakePhase.Cancelled)
            {
                return false;
            }

            return phase == HandshakePhase.AwaitingHello && messageKind == 1;
        }
    }
}
