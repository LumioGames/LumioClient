namespace Lumio.Client.Session
{
    internal sealed class TerminalSessionState
    {
        public bool Frozen { get; private set; }

        public void Freeze()
        {
            Frozen = true;
        }

        public void Unfreeze()
        {
            Frozen = false;
        }
    }
}
