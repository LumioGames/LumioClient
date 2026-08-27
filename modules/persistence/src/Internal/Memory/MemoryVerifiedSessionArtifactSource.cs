using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lumio.Client.Persistence
{
    internal sealed class MemoryVerifiedSessionArtifactSource : IVerifiedSessionArtifactSource
    {
        private readonly object _gate = new object();
        private readonly Dictionary<ArtifactKey, MemoryArtifact> _artifacts = new Dictionary<ArtifactKey, MemoryArtifact>();

        public void SeedUnverified(in VerifiedArtifactReadRequest request, ReadOnlyMemory<byte> payload)
        {
            Seed(in request, payload, verified: false);
        }

        public void SeedVerified(in VerifiedArtifactReadRequest request, ReadOnlyMemory<byte> payload)
        {
            Seed(in request, payload, verified: true);
        }

        public ValueTask<VerifiedArtifactReadResult> ReadAsync(
            in VerifiedArtifactReadRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MemoryArtifact artifact;
            lock (_gate)
            {
                if (!_artifacts.TryGetValue(new ArtifactKey(request.ArtifactId, request.ReleaseId), out artifact))
                {
                    return new ValueTask<VerifiedArtifactReadResult>(VerifiedArtifactReadResult.NotVerified(request.Generation));
                }
            }

            if (!artifact.Verified || !artifact.ContentHash.Span.SequenceEqual(request.ContentHash.Span))
            {
                return new ValueTask<VerifiedArtifactReadResult>(VerifiedArtifactReadResult.NotVerified(request.Generation));
            }

            return new ValueTask<VerifiedArtifactReadResult>(
                VerifiedArtifactReadResult.Success(Copy(artifact.Payload), request.Generation));
        }

        private void Seed(in VerifiedArtifactReadRequest request, ReadOnlyMemory<byte> payload, bool verified)
        {
            var stored = new MemoryArtifact(Copy(request.ContentHash), Copy(payload), verified);
            lock (_gate)
            {
                _artifacts[new ArtifactKey(request.ArtifactId, request.ReleaseId)] = stored;
            }
        }

        private static ReadOnlyMemory<byte> Copy(ReadOnlyMemory<byte> source)
        {
            if (source.IsEmpty)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            return source.ToArray();
        }

        private readonly struct ArtifactKey : IEquatable<ArtifactKey>
        {
            public ArtifactKey(string artifactId, string releaseId)
            {
                ArtifactId = artifactId ?? string.Empty;
                ReleaseId = releaseId ?? string.Empty;
            }

            public string ArtifactId { get; }

            public string ReleaseId { get; }

            public bool Equals(ArtifactKey other)
            {
                return string.Equals(ArtifactId, other.ArtifactId, StringComparison.Ordinal)
                    && string.Equals(ReleaseId, other.ReleaseId, StringComparison.Ordinal);
            }

            public override bool Equals(object? obj)
            {
                return obj is ArtifactKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ArtifactId.GetHashCode() * 397) ^ ReleaseId.GetHashCode();
                }
            }
        }

        private readonly struct MemoryArtifact
        {
            public MemoryArtifact(ReadOnlyMemory<byte> contentHash, ReadOnlyMemory<byte> payload, bool verified)
            {
                ContentHash = contentHash;
                Payload = payload;
                Verified = verified;
            }

            public ReadOnlyMemory<byte> ContentHash { get; }

            public ReadOnlyMemory<byte> Payload { get; }

            public bool Verified { get; }
        }
    }
}
