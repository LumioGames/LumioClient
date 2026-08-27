namespace Lumio.Client.Connection
{
    public enum TransportFaultAction
    {
        Pass,
        Drop,
        Duplicate,
        Delay,
        Disconnect
    }

    public readonly struct TransportFaultContext
    {
        public TransportFaultContext(int seed, int sequence)
        {
            Seed = seed;
            Sequence = sequence;
        }

        public int Seed { get; }

        public int Sequence { get; }
    }

    public interface ITransportFaultPolicy
    {
        TransportFaultAction Decide(in TransportFaultContext context);
    }
}
