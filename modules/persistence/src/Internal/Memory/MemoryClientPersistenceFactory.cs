namespace Lumio.Client.Persistence
{
    internal sealed class MemoryClientPersistenceFactory : IClientPersistenceFactory
    {
        private readonly MemoryVerifiedSessionArtifactSource _artifacts = new MemoryVerifiedSessionArtifactSource();
        private readonly MemoryClientCheckpointStore _checkpoints = new MemoryClientCheckpointStore();

        public IVerifiedSessionArtifactSource CreateVerifiedSessionArtifactSource()
        {
            return _artifacts;
        }

        public IClientCheckpointStore CreateCheckpointStore()
        {
            return _checkpoints;
        }
    }
}
