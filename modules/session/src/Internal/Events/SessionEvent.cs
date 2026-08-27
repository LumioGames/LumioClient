using Lumio.Client.Connection;

namespace Lumio.Client.Session
{
    internal enum SessionEventPriority
    {
        Fault = 0,
        ForcedClose = 1,
        Cancel = 2,
        StableReject = 3,
        Disconnect = 4,
        CriticalQueueFull = 5,
        Gap = 6,
        Retryable = 7,
        Success = 8,
        Normal = 9
    }

    internal readonly struct SessionEvent
    {
        public SessionEvent(SessionEventPriority priority, ulong generation, ulong sequence, ConnectionEvent connection)
        {
            Priority = priority;
            Generation = generation;
            Sequence = sequence;
            Connection = connection;
        }

        public SessionEventPriority Priority { get; }

        public ulong Generation { get; }

        public ulong Sequence { get; }

        public ConnectionEvent Connection { get; }
    }
}
