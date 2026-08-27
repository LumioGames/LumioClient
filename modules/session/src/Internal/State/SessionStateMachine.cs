namespace Lumio.Client.Session
{
    internal sealed class SessionStateMachine
    {
        public ClientSessionState State { get; private set; }

        public ulong Generation { get; private set; }

        public bool IsTerminal
        {
            get { return State == ClientSessionState.Closed || State == ClientSessionState.Faulted; }
        }

        public bool TryEnter(ClientSessionState next)
        {
            if (IsTerminal && next != State)
            {
                return false;
            }

            State = next;
            return true;
        }

        public void SetGeneration(ulong generation)
        {
            Generation = generation;
        }
    }
}
