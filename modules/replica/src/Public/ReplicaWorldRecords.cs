using System;

namespace Lumio.Client.Replica
{
    public readonly struct ReplicaBinding
    {
        public ReplicaBinding(string accountId, string roomId, string netEntityId, string entityType, ulong connectionGeneration)
        {
            AccountId = accountId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            NetEntityId = netEntityId ?? string.Empty;
            EntityType = entityType ?? string.Empty;
            ConnectionGeneration = connectionGeneration;
        }

        public string AccountId { get; }

        public string RoomId { get; }

        public string NetEntityId { get; }

        public string EntityType { get; }

        public ulong ConnectionGeneration { get; }
    }

    public readonly struct ReplicaAttributeValue
    {
        public ReplicaAttributeValue(string attributeId, string value)
        {
            AttributeId = attributeId ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public string AttributeId { get; }

        public string Value { get; }
    }

    public readonly struct ReplicaVisibleEntity
    {
        public ReplicaVisibleEntity(
            string netEntityId,
            string entityType,
            string roomId,
            ulong connectionGeneration,
            ulong revision,
            ulong tick,
            ReplicaAttributeValue[] attributes,
            bool inAoi,
            bool tombstoned)
        {
            NetEntityId = netEntityId ?? string.Empty;
            EntityType = entityType ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ConnectionGeneration = connectionGeneration;
            Revision = revision;
            Tick = tick;
            Attributes = attributes ?? Array.Empty<ReplicaAttributeValue>();
            InAoi = inAoi;
            Tombstoned = tombstoned;
        }

        public string NetEntityId { get; }

        public string EntityType { get; }

        public string RoomId { get; }

        public ulong ConnectionGeneration { get; }

        public ulong Revision { get; }

        public ulong Tick { get; }

        public ReplicaAttributeValue[] Attributes { get; }

        public bool InAoi { get; }

        public bool Tombstoned { get; }
    }

    public readonly struct ReplicaAdmission
    {
        public ReplicaAdmission(in ReplicaBinding self, ReplicaVisibleEntity[] visibleEntities)
            : this(in self, visibleEntities, false, false)
        {
        }

        public ReplicaAdmission(
            in ReplicaBinding self,
            ReplicaVisibleEntity[] visibleEntities,
            bool hasClaim,
            bool hasForbiddenAccountEntityRef)
        {
            Self = self;
            VisibleEntities = visibleEntities ?? Array.Empty<ReplicaVisibleEntity>();
            HasClaim = hasClaim;
            HasForbiddenAccountEntityRef = hasForbiddenAccountEntityRef;
        }

        public ReplicaBinding Self { get; }

        public ReplicaVisibleEntity[] VisibleEntities { get; }

        public bool HasClaim { get; }

        public bool HasForbiddenAccountEntityRef { get; }
    }

    public readonly struct ReplicaAdmissionResult
    {
        public ReplicaAdmissionResult(bool accepted, string rejectCode)
        {
            Accepted = accepted;
            RejectCode = rejectCode ?? string.Empty;
        }

        public bool Accepted { get; }

        public string RejectCode { get; }
    }
}
