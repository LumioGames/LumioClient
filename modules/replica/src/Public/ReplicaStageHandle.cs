namespace Lumio.Client.Replica
{
    public readonly struct ReplicaStageHandle
    {
        public ReplicaStageHandle(ulong token, ulong generation)
        {
            Token = token;
            Generation = generation;
        }

        public ulong Token { get; }

        public ulong Generation { get; }

        public bool IsEmpty
        {
            get { return Token == 0; }
        }
    }
}
