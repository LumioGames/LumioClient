namespace Lumio.Client.Observability
{
    public enum ClientEventWriteOutcome
    {
        Accepted = 0,
        Rejected = 1,
        QueueFull = 2,
        Dropped = 3
    }

    public readonly struct ClientEventWriteResult
    {
        public ClientEventWriteResult(ClientEventWriteOutcome outcome)
        {
            Outcome = outcome;
        }

        public ClientEventWriteOutcome Outcome { get; }

        public bool Succeeded
        {
            get { return Outcome == ClientEventWriteOutcome.Accepted; }
        }
    }
}
