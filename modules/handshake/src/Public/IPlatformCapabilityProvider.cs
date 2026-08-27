using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Handshake
{
    public readonly struct PlatformCapabilityQuery
    {
        public PlatformCapabilityQuery(HandshakeAttemptId attempt, ulong generation)
        {
            Attempt = attempt;
            Generation = generation;
        }

        public HandshakeAttemptId Attempt { get; }

        public ulong Generation { get; }
    }

    public readonly struct PlatformCapabilityResult
    {
        public PlatformCapabilityResult(HandshakeAttemptId attempt, ulong generation, bool compatible)
        {
            Attempt = attempt;
            Generation = generation;
            Compatible = compatible;
        }

        public HandshakeAttemptId Attempt { get; }

        public ulong Generation { get; }

        public bool Compatible { get; }
    }

    public interface IPlatformCapabilityProvider
    {
        ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken);
    }
}
