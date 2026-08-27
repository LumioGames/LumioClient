namespace Lumio.Client.Persistence
{
    public readonly struct CheckpointWriteResult
    {
        public CheckpointWriteResult(bool succeeded, ulong generation)
        {
            Succeeded = succeeded;
            Generation = generation;
        }

        public bool Succeeded { get; }

        public ulong Generation { get; }

        public static CheckpointWriteResult Success(ulong generation)
        {
            return new CheckpointWriteResult(true, generation);
        }

        public static CheckpointWriteResult Failed(ulong generation)
        {
            return new CheckpointWriteResult(false, generation);
        }
    }
}
