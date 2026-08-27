using Lumio.Client.Connection;

namespace Lumio.Client.Session
{
    internal sealed class SessionEventArbiter
    {
        private readonly bool _owned = true;

        public SessionEventPriority MapConnection(ConnectionEventKind kind)
        {
            if (!_owned)
            {
                return SessionEventPriority.Normal;
            }

            switch (kind)
            {
                case ConnectionEventKind.Faulted:
                    return SessionEventPriority.Fault;
                case ConnectionEventKind.Closed:
                    return SessionEventPriority.ForcedClose;
                case ConnectionEventKind.Disconnected:
                    return SessionEventPriority.Disconnect;
                case ConnectionEventKind.Started:
                    return SessionEventPriority.Success;
                default:
                    return SessionEventPriority.Normal;
            }
        }

        public SessionEventPriority MapMessage(SessionMessageKind kind)
        {
            if (!_owned)
            {
                return SessionEventPriority.Normal;
            }

            if (kind == SessionMessageKind.Gap)
            {
                return SessionEventPriority.Gap;
            }

            return SessionEventPriority.Normal;
        }
    }
}
