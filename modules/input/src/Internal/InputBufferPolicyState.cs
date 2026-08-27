namespace Lumio.Client.Input
{
    internal sealed class InputBufferPolicyState
    {
        private InputBufferPolicy _policy = new InputBufferPolicy(InputBufferPolicyKind.Hold, 0);

        public InputBufferPolicy Current
        {
            get { return _policy; }
        }

        public void Set(in InputBufferPolicy policy)
        {
            _policy = policy;
        }

        public bool AppliesTo(ulong generation)
        {
            return _policy.Generation == generation;
        }
    }
}
