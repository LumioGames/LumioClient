using System;

namespace Lumio.Client.Persistence
{
    public readonly struct VerifiedArtifactReadRequest
    {
        public VerifiedArtifactReadRequest(
            string artifactId,
            string releaseId,
            ReadOnlyMemory<byte> contentHash,
            ulong generation)
        {
            if (artifactId is null)
            {
                throw new ArgumentNullException(nameof(artifactId));
            }

            if (releaseId is null)
            {
                throw new ArgumentNullException(nameof(releaseId));
            }

            ArtifactId = artifactId;
            ReleaseId = releaseId;
            ContentHash = contentHash;
            Generation = generation;
        }

        public string ArtifactId { get; }

        public string ReleaseId { get; }

        public ReadOnlyMemory<byte> ContentHash { get; }

        public ulong Generation { get; }
    }
}
