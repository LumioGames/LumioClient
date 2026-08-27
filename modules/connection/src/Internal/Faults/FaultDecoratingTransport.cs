namespace Lumio.Client.Connection
{
    internal sealed class FaultDecoratingTransport
    {
        private readonly ITransportFaultPolicy _policy;
        private int _sequence;

        public FaultDecoratingTransport(ITransportFaultPolicy policy)
        {
            _policy = policy;
        }

        public TransportFaultAction Next(int seed)
        {
            _sequence++;
            return _policy.Decide(new TransportFaultContext(seed, _sequence));
        }
    }

    internal sealed class SeededFaultPolicy : ITransportFaultPolicy
    {
        public TransportFaultAction Decide(in TransportFaultContext context)
        {
            int mixed = (context.Seed * 397) ^ context.Sequence;
            switch (mixed % 4)
            {
                case 0:
                    return TransportFaultAction.Pass;
                case 1:
                    return TransportFaultAction.Drop;
                case 2:
                    return TransportFaultAction.Duplicate;
                default:
                    return TransportFaultAction.Delay;
            }
        }
    }
}
