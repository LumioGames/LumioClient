using System;
using System.Collections.Generic;

namespace Lumio.Client.Replica
{
    public enum ReplicaClientKind
    {
        Browser = 0,
        Bot = 1
    }

    public readonly struct ReplicaChatLine
    {
        public ReplicaChatLine(ulong messageId, ulong roomSequence, string senderNetEntityId, string text, ulong appliedTick)
        {
            MessageId = messageId;
            RoomSequence = roomSequence;
            SenderNetEntityId = senderNetEntityId ?? string.Empty;
            Text = text ?? string.Empty;
            AppliedTick = appliedTick;
        }

        public ulong MessageId { get; }

        public ulong RoomSequence { get; }

        public string SenderNetEntityId { get; }

        public string Text { get; }

        public ulong AppliedTick { get; }
    }

    public sealed class ReplicaChatConsumer
    {
        private readonly IClientReplica _replica;

        public ReplicaChatConsumer(ReplicaClientKind kind, IClientReplica replica)
        {
            if (replica is null)
            {
                throw new ArgumentNullException(nameof(replica));
            }

            Kind = kind;
            _replica = replica;
        }

        public ReplicaClientKind Kind { get; }

        public IClientReplica Replica
        {
            get { return _replica; }
        }

        public IReplicaWorld World
        {
            get { return _replica.World; }
        }

        public IReadOnlyList<ReplicaChatLine> ChatWindow
        {
            get { return _replica.World.CopyChatWindow(); }
        }
    }
}
