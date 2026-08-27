namespace Lumio.Client.Session
{
    internal sealed class ClientConfigStagingArea
    {
        public bool Staged { get; private set; }

        public bool Active { get; private set; }

        public void Stage()
        {
            Staged = true;
            Active = false;
        }

        public void ActivateBarrier()
        {
            if (Staged)
            {
                Active = true;
            }
        }

        public void Clear()
        {
            Staged = false;
            Active = false;
        }
    }
}
