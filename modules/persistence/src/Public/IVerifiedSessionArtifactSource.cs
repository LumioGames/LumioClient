using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Persistence
{
    public interface IVerifiedSessionArtifactSource
    {
        ValueTask<VerifiedArtifactReadResult> ReadAsync(
            in VerifiedArtifactReadRequest request,
            CancellationToken cancellationToken);
    }
}
