using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Session
{
    public readonly struct GameplayScopePrepareRequest
    {
        public GameplayScopePrepareRequest(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct GameplayScopePrepareResult
    {
        public GameplayScopePrepareResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public readonly struct GameplayScopeActivationRequest
    {
        public GameplayScopeActivationRequest(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct GameplayScopeActivationResult
    {
        public GameplayScopeActivationResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public readonly struct GameplayScopeLease
    {
        public GameplayScopeLease(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct GameplayScopeReleaseResult
    {
        public GameplayScopeReleaseResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }

    public interface IClientGameplayScopeActivator
    {
        ValueTask<GameplayScopePrepareResult> PrepareAsync(in GameplayScopePrepareRequest request, CancellationToken cancellationToken);

        GameplayScopeActivationResult ActivateAtTickBarrier(in GameplayScopeActivationRequest request);

        ValueTask<GameplayScopeReleaseResult> ReleaseAsync(GameplayScopeLease lease, CancellationToken cancellationToken);
    }

    public sealed class ImmediateGameplayScopeActivator : IClientGameplayScopeActivator
    {
        public int PrepareCalls { get; private set; }

        public int ActivateCalls { get; private set; }

        public int ReleaseCalls { get; private set; }

        public ValueTask<GameplayScopePrepareResult> PrepareAsync(in GameplayScopePrepareRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareCalls++;
            return new ValueTask<GameplayScopePrepareResult>(new GameplayScopePrepareResult(true));
        }

        public GameplayScopeActivationResult ActivateAtTickBarrier(in GameplayScopeActivationRequest request)
        {
            _ = request;
            ActivateCalls++;
            return new GameplayScopeActivationResult(true);
        }

        public ValueTask<GameplayScopeReleaseResult> ReleaseAsync(GameplayScopeLease lease, CancellationToken cancellationToken)
        {
            _ = lease;
            cancellationToken.ThrowIfCancellationRequested();
            ReleaseCalls++;
            return new ValueTask<GameplayScopeReleaseResult>(new GameplayScopeReleaseResult(true));
        }
    }
}
