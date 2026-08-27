namespace Lumio.Client.Session
{
    public enum ClientSessionState
    {
        Disconnected,
        Connecting,
        Negotiating,
        Synchronizing,
        Active,
        Resyncing,
        Reconnecting,
        Closed,
        Faulted
    }
}
