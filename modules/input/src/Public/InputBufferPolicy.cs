namespace Lumio.Client.Input
{
    public enum InputBufferPolicyKind
    {
        Hold,
        Drop,
        Resync
    }

    public readonly struct InputBufferPolicy
    {
        public InputBufferPolicy(InputBufferPolicyKind kind, ulong generation)
        {
            Kind = kind;
            Generation = generation;
        }

        public InputBufferPolicyKind Kind { get; }

        public ulong Generation { get; }
    }
}
