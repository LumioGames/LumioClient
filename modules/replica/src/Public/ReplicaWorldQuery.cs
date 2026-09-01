namespace Lumio.Client.Replica
{
    public enum ReplicaQueryStatus
    {
        Ok = 0,
        NonExistent = 1,
        StaleGeneration = 2,
        Invisible = 3,
        Unauthorized = 4,
        Tombstoned = 5,
        RequestError = 6
    }

    public readonly struct ReplicaAttributeQuery
    {
        public ReplicaAttributeQuery(string callerScope, string roomId, string netEntityId, string attributeId)
            : this(callerScope, roomId, netEntityId, attributeId, 0UL, false, string.Empty, false)
        {
        }

        public ReplicaAttributeQuery(
            string callerScope,
            string roomId,
            string netEntityId,
            string attributeId,
            ulong connectionGeneration,
            bool hasConnectionGeneration,
            string origin,
            bool hasAccountEntityRef)
        {
            CallerScope = callerScope ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            NetEntityId = netEntityId ?? string.Empty;
            AttributeId = attributeId ?? string.Empty;
            ConnectionGeneration = connectionGeneration;
            HasConnectionGeneration = hasConnectionGeneration;
            Origin = origin ?? string.Empty;
            HasAccountEntityRef = hasAccountEntityRef;
        }

        public string CallerScope { get; }

        public string RoomId { get; }

        public string NetEntityId { get; }

        public string AttributeId { get; }

        public ulong ConnectionGeneration { get; }

        public bool HasConnectionGeneration { get; }

        public string Origin { get; }

        public bool HasAccountEntityRef { get; }
    }

    public readonly struct ReplicaAttributeQueryResult
    {
        public ReplicaAttributeQueryResult(
            ReplicaQueryStatus status,
            string code,
            string netEntityId,
            string roomId,
            string attributeId,
            string value,
            ulong observedRevision,
            ulong observedTick)
        {
            Status = status;
            Code = code ?? string.Empty;
            NetEntityId = netEntityId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            AttributeId = attributeId ?? string.Empty;
            Value = value ?? string.Empty;
            ObservedRevision = observedRevision;
            ObservedTick = observedTick;
        }

        public ReplicaQueryStatus Status { get; }

        public string Code { get; }

        public string NetEntityId { get; }

        public string RoomId { get; }

        public string AttributeId { get; }

        public string Value { get; }

        public ulong ObservedRevision { get; }

        public ulong ObservedTick { get; }
    }

    public readonly struct ReplicaBindingLookup
    {
        public ReplicaBindingLookup(bool found, string rejectCode, in ReplicaBinding binding)
        {
            Found = found;
            RejectCode = rejectCode ?? string.Empty;
            Binding = binding;
        }

        public bool Found { get; }

        public string RejectCode { get; }

        public ReplicaBinding Binding { get; }
    }

    public readonly struct ReplicaEntityResolve
    {
        public ReplicaEntityResolve(
            ReplicaQueryStatus status,
            string code,
            string netEntityId,
            string roomId,
            string entityType,
            ulong revision)
        {
            Status = status;
            Code = code ?? string.Empty;
            NetEntityId = netEntityId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            EntityType = entityType ?? string.Empty;
            Revision = revision;
        }

        public ReplicaQueryStatus Status { get; }

        public string Code { get; }

        public string NetEntityId { get; }

        public string RoomId { get; }

        public string EntityType { get; }

        public ulong Revision { get; }
    }
}
