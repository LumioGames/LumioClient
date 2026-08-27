namespace Lumio.Client.Handshake
{
    public enum HandshakePhase
    {
        Idle,
        AwaitingHello,
        AwaitingCapability,
        Accepted,
        Rejected,
        Cancelled
    }

    public enum HandshakeRejectReason
    {
        None,
        InvalidHello,
        CapabilityMismatch,
        Cancelled,
        Disconnect
    }

    public readonly struct HandshakeOutcome
    {
        public HandshakeOutcome(HandshakePhase phase, HandshakeRejectReason reject, bool accepted)
        {
            Phase = phase;
            Reject = reject;
            Accepted = accepted;
        }

        public HandshakePhase Phase { get; }

        public HandshakeRejectReason Reject { get; }

        public bool Accepted { get; }
    }
}
