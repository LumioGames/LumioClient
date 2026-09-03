using System;

namespace Lumio.Client.Session
{
    public enum SessionMessageKind
    {
        Unknown = 0,
        FullSnapshot = 1,
        Delta = 2,
        Gap = 3,
        AuthorityUpdate = 4,
        ConnectionSuperseded = 5
    }

    public interface ISessionMessageKindMap
    {
        SessionMessageKind Map(ReadOnlyMemory<byte> frame);
    }

    public sealed class UnpublishedSessionMessageKindMap : ISessionMessageKindMap
    {
        public SessionMessageKind Map(ReadOnlyMemory<byte> frame)
        {
            _ = frame;
            return SessionMessageKind.Unknown;
        }
    }
}
