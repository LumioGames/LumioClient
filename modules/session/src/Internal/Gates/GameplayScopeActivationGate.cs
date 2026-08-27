namespace Lumio.Client.Session
{
    internal sealed class GameplayScopeActivationGate
    {
        public bool Prepared { get; private set; }

        public bool Activated { get; private set; }

        public bool TryPrepare()
        {
            Prepared = true;
            return true;
        }

        public bool TryActivate()
        {
            if (!Prepared)
            {
                return false;
            }

            Activated = true;
            return true;
        }

        public bool CanCreateWorldHandles()
        {
            return Activated;
        }

        public void Reset()
        {
            Prepared = false;
            Activated = false;
        }
    }
}
