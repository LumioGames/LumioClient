using System;

namespace Lumio.Client.Replica
{
    public readonly struct ReplicaRuntimeOutcome
    {
        public ReplicaRuntimeOutcome(bool committed, bool indeterminate, ReadOnlyMemory<byte> evidence)
        {
            Committed = committed;
            Indeterminate = indeterminate;
            Evidence = evidence;
        }

        public bool Committed { get; }

        public bool Indeterminate { get; }

        public ReadOnlyMemory<byte> Evidence { get; }

        public static ReplicaRuntimeOutcome CommittedOutcome()
        {
            return new ReplicaRuntimeOutcome(true, false, ReadOnlyMemory<byte>.Empty);
        }

        public static ReplicaRuntimeOutcome AbortedOutcome()
        {
            return new ReplicaRuntimeOutcome(false, false, ReadOnlyMemory<byte>.Empty);
        }

        public static ReplicaRuntimeOutcome IndeterminateOutcome(ReadOnlyMemory<byte> evidence)
        {
            return new ReplicaRuntimeOutcome(false, true, evidence);
        }
    }
}
