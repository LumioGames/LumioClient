namespace Lumio.Client.Observability
{
    public interface IClientEventMemorySnapshotSource
    {
        ClientEventMemorySnapshot Capture();
    }
}
