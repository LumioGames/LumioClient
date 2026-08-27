namespace Lumio.Client.Persistence
{
    public interface IClientPersistenceFactory
    {
        IVerifiedSessionArtifactSource CreateVerifiedSessionArtifactSource();

        IClientCheckpointStore CreateCheckpointStore();

        static IClientPersistenceFactory CreateMemory()
        {
            return new MemoryClientPersistenceFactory();
        }
    }
}
