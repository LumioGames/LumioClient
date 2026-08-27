namespace Lumio.Client.Observability
{
    public interface IClientEventWriter
    {
        ClientEventWriteResult TryWrite(in ClientEventRecord record);

        ClientEventPipelineSnapshot GetSnapshot();
    }
}
