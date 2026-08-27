namespace Lumio.Client.Observability
{
    public readonly struct ClientEventSinkResult
    {
        public ClientEventSinkResult(bool succeeded, int writtenCount, bool retryable)
        {
            Succeeded = succeeded;
            WrittenCount = writtenCount;
            Retryable = retryable;
        }

        public bool Succeeded { get; }

        public int WrittenCount { get; }

        public bool Retryable { get; }
    }
}
