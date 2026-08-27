namespace Lumio.Client.Observability
{
    public readonly struct ClientEventPipelineCreateResult
    {
        public ClientEventPipelineCreateResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }
}
