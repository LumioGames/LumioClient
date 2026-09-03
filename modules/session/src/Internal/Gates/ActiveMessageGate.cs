using System;

namespace Lumio.Client.Session
{
    internal sealed class ActiveMessageGate
    {
        public int RejectedCalls { get; private set; }

        public bool Allow(ClientSessionState state, ulong eventGeneration, ulong sessionGeneration, SessionMessageKind kind)
        {
            if (eventGeneration != sessionGeneration)
            {
                RejectedCalls++;
                return false;
            }

            if (kind == SessionMessageKind.Unknown)
            {
                RejectedCalls++;
                return false;
            }

            if (state == ClientSessionState.Synchronizing || state == ClientSessionState.Resyncing)
            {
                return kind == SessionMessageKind.FullSnapshot
                    || kind == SessionMessageKind.ConnectionSuperseded;
            }

            if (state == ClientSessionState.Active)
            {
                return kind == SessionMessageKind.Delta
                    || kind == SessionMessageKind.Gap
                    || kind == SessionMessageKind.AuthorityUpdate
                    || kind == SessionMessageKind.FullSnapshot
                    || kind == SessionMessageKind.ConnectionSuperseded;
            }

            RejectedCalls++;
            return false;
        }
    }
}
