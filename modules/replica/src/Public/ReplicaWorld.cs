using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Lumio.Client.Replica
{
    public sealed class ReplicaWorld : IReplicaWorld
    {
        private const int MaxBindingsPerRoom = 4096;
        private const int MaxAttributeIdBytes = 128;
        private static readonly Regex AttributeIdGrammar = new Regex(
            "^[A-Z][A-Za-z0-9]*\\.[a-z][A-Za-z0-9]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly Dictionary<string, EntityRecord> _entities = new Dictionary<string, EntityRecord>(StringComparer.Ordinal);
        private readonly List<ReplicaChatLine> _chat = new List<ReplicaChatLine>();
        private ReplicaBinding _self;
        private bool _hasSelf;
        private bool _hasClaim;
        private bool _inputEnabled;
        private bool _superseded;
        private ReplicaConnectionSuperseded _lastSuperseded;
        private ulong _lastRoomSequence;
        private ulong _lastMessageId;
        private string _lastRejectCode = string.Empty;

        public ReplicaAdmissionResult InstallAdmission(in ReplicaAdmission admission)
        {
            if (admission.HasForbiddenAccountEntityRef)
            {
                return RejectAdmission("invalid_binding_shape");
            }

            ReplicaBinding self = admission.Self;
            if (!IsEntityType(self.EntityType)
                || self.ConnectionGeneration < 1UL
                || string.IsNullOrEmpty(self.AccountId)
                || string.IsNullOrEmpty(self.RoomId)
                || string.IsNullOrEmpty(self.NetEntityId))
            {
                return RejectAdmission("invalid_binding_shape");
            }

            ReplicaVisibleEntity[] visible = admission.VisibleEntities ?? Array.Empty<ReplicaVisibleEntity>();
            if (visible.Length > MaxBindingsPerRoom)
            {
                return RejectAdmission("invalid_binding_shape");
            }

            var next = new Dictionary<string, EntityRecord>(StringComparer.Ordinal);
            for (int i = 0; i < visible.Length; i++)
            {
                ReplicaVisibleEntity item = visible[i];
                if (!IsEntityType(item.EntityType) || string.IsNullOrEmpty(item.NetEntityId) || string.IsNullOrEmpty(item.RoomId))
                {
                    return RejectAdmission("invalid_binding_shape");
                }

                next[item.NetEntityId] = EntityRecord.FromVisible(item);
            }

            _entities.Clear();
            foreach (KeyValuePair<string, EntityRecord> pair in next)
            {
                _entities[pair.Key] = pair.Value;
            }

            _self = self;
            _hasSelf = true;
            _hasClaim = admission.HasClaim;
            _lastRejectCode = string.Empty;
            return new ReplicaAdmissionResult(true, string.Empty);
        }

        public ReplicaBindingLookup SelfLookup()
        {
            if (!_hasSelf)
            {
                return new ReplicaBindingLookup(false, "binding_not_found", default(ReplicaBinding));
            }

            return new ReplicaBindingLookup(true, string.Empty, in _self);
        }

        public ReplicaEntityResolve Resolve(string roomId, string netEntityId, ulong connectionGeneration, bool hasConnectionGeneration)
        {
            ReplicaAttributeQueryResult query = QueryAttribute(new ReplicaAttributeQuery(
                "client-replica",
                roomId ?? string.Empty,
                netEntityId ?? string.Empty,
                "EntityIdentity.entityType",
                connectionGeneration,
                hasConnectionGeneration,
                string.Empty,
                false));
            if (query.Status == ReplicaQueryStatus.Ok)
            {
                return new ReplicaEntityResolve(
                    ReplicaQueryStatus.Ok,
                    string.Empty,
                    query.NetEntityId,
                    query.RoomId,
                    query.Value,
                    query.ObservedRevision);
            }

            return new ReplicaEntityResolve(query.Status, query.Code, string.Empty, string.Empty, string.Empty, 0UL);
        }

        public ReplicaAttributeQueryResult QueryAttribute(in ReplicaAttributeQuery query)
        {
            string callerScope = query.CallerScope ?? string.Empty;
            string roomId = query.RoomId ?? string.Empty;
            string netEntityId = query.NetEntityId ?? string.Empty;
            string attributeId = query.AttributeId ?? string.Empty;

            if (query.HasAccountEntityRef)
            {
                return RequestError("invalid_binding_shape");
            }

            if (!string.Equals(callerScope, "client-replica", StringComparison.Ordinal))
            {
                return RequestError("scope_violation");
            }

            if (_entities.TryGetValue(netEntityId, out EntityRecord known) && !string.Equals(known.RoomId, roomId, StringComparison.Ordinal))
            {
                return RequestError("cross_room_reference");
            }

            string attributeCode = ClassifyAttributeId(attributeId);
            if (attributeCode.Length > 0)
            {
                return RequestError(attributeCode);
            }

            if (!_entities.TryGetValue(netEntityId, out EntityRecord entity))
            {
                return Outcome(ReplicaQueryStatus.NonExistent, netEntityId, roomId, attributeId);
            }

            if (entity.Tombstoned)
            {
                return Outcome(ReplicaQueryStatus.Tombstoned, netEntityId, roomId, attributeId);
            }

            if (query.HasConnectionGeneration && query.ConnectionGeneration < entity.ConnectionGeneration)
            {
                return Outcome(ReplicaQueryStatus.StaleGeneration, netEntityId, roomId, attributeId);
            }

            AttributeDeclarationTable.TryGet(attributeId, out AttributeDeclaration declaration);
            if (!entity.InAoi
                || string.Equals(declaration.Replication, "not-replicated", StringComparison.Ordinal)
                || string.Equals(declaration.Visibility, "server-only", StringComparison.Ordinal))
            {
                return Outcome(ReplicaQueryStatus.Invisible, netEntityId, roomId, attributeId);
            }

            if (string.Equals(declaration.Visibility, "claim-scoped", StringComparison.Ordinal) && !_hasClaim)
            {
                return Outcome(ReplicaQueryStatus.Unauthorized, netEntityId, roomId, attributeId);
            }

            if (!entity.Attributes.TryGetValue(attributeId, out string value))
            {
                return Outcome(ReplicaQueryStatus.Invisible, netEntityId, roomId, attributeId);
            }

            return new ReplicaAttributeQueryResult(
                ReplicaQueryStatus.Ok,
                string.Empty,
                netEntityId,
                roomId,
                attributeId,
                value,
                entity.Revision,
                entity.Tick);
        }

        public IReadOnlyList<ReplicaChatLine> CopyChatWindow()
        {
            return _chat.ToArray();
        }

        public IReadOnlyList<ReplicaIdentityRecord> CopyIdentityRecords()
        {
            var records = new ReplicaIdentityRecord[_entities.Count];
            int index = 0;
            foreach (KeyValuePair<string, EntityRecord> pair in _entities)
            {
                string mark = string.Empty;
                if (pair.Value.Attributes.TryGetValue("EntityIdentity.unmappedMark", out string value))
                {
                    mark = value;
                }

                records[index] = new ReplicaIdentityRecord(pair.Value.NetEntityId, pair.Value.EntityType, mark);
                index++;
            }

            return records;
        }

        public int VisibleEntityCount
        {
            get { return _entities.Count; }
        }

        public bool InputEnabled
        {
            get { return _inputEnabled; }
        }

        public ReplicaConnectionSuperseded LastConnectionSuperseded
        {
            get { return _lastSuperseded; }
        }

        public string LastRejectCode
        {
            get { return _lastRejectCode; }
        }

        internal void Reset()
        {
            _entities.Clear();
            _chat.Clear();
            _self = default(ReplicaBinding);
            _hasSelf = false;
            _hasClaim = false;
            _inputEnabled = false;
            _superseded = false;
            _lastSuperseded = default(ReplicaConnectionSuperseded);
            _lastRoomSequence = 0UL;
            _lastMessageId = 0UL;
            _lastRejectCode = string.Empty;
        }

        internal void ObserveSuperseded(in ReplicaConnectionSuperseded notice)
        {
            _superseded = true;
            _inputEnabled = false;
            _lastSuperseded = notice;
        }

        internal bool TryValidateAuthority(in ReplicaStageRequest request, out string rejectCode)
        {
            rejectCode = string.Empty;
            if (!LiteJsonParser.LooksLikeObject(request.Update.Span))
            {
                return true;
            }

            if (!GameplayCodec.TryDecodeAuthority(request.Kind, request.Update, out DecodedGameplayMessage decoded, out rejectCode))
            {
                _lastRejectCode = rejectCode;
                return false;
            }

            for (int i = 0; i < decoded.Blocks.Length; i++)
            {
                DecodedGameplayBlock block = decoded.Blocks[i];
                if (!block.HasChatEvent)
                {
                    continue;
                }

                DecodedChatEvent chat = block.ChatEvent;
                // Room-scoped chat.event: C-1 envelope/sequence only. Receiver AOI/admission does not gate delivery.
                bool sequenceOk = _lastRoomSequence == 0UL
                    ? chat.RoomSequence > 0UL
                    : chat.RoomSequence == _lastRoomSequence + 1UL;
                if (!sequenceOk || chat.MessageId <= _lastMessageId)
                {
                    rejectCode = GameplayReject.BadEnvelope;
                    _lastRejectCode = rejectCode;
                    return false;
                }
            }

            _lastRejectCode = string.Empty;
            return true;
        }

        internal void ApplyCommitted(in ReplicaStageRequest request)
        {
            if (!LiteJsonParser.LooksLikeObject(request.Update.Span))
            {
                ApplyTombstones(request.TombstoneEntityIds);
                return;
            }

            if (!GameplayCodec.TryDecodeAuthority(request.Kind, request.Update, out DecodedGameplayMessage decoded, out _))
            {
                return;
            }

            if (request.Kind == ReplicaUpdateKind.FullSnapshot)
            {
                RebuildFromIdentity(in decoded, request.Generation);
                _chat.Clear();
                _lastRoomSequence = 0UL;
                _lastMessageId = 0UL;
                if (!_superseded)
                {
                    _inputEnabled = true;
                }

                ApplyTombstones(request.TombstoneEntityIds);
                return;
            }

            ApplyTombstones(request.TombstoneEntityIds);
            for (int i = 0; i < decoded.Blocks.Length; i++)
            {
                DecodedGameplayBlock block = decoded.Blocks[i];
                if (!block.HasChatEvent)
                {
                    continue;
                }

                DecodedChatEvent chat = block.ChatEvent;
                _chat.Add(new ReplicaChatLine(chat.MessageId, chat.RoomSequence, chat.SenderNetEntityId, chat.Text, chat.AppliedTick));
                _lastRoomSequence = chat.RoomSequence;
                _lastMessageId = chat.MessageId;
            }
        }

        private void RebuildFromIdentity(in DecodedGameplayMessage decoded, ulong generation)
        {
            _entities.Clear();
            string roomId = _hasSelf ? _self.RoomId : string.Empty;
            for (int i = 0; i < decoded.Blocks.Length; i++)
            {
                DecodedGameplayBlock block = decoded.Blocks[i];
                if (!block.HasIdentity)
                {
                    continue;
                }

                DecodedIdentityRecord[] records = block.IdentityRecords;
                for (int r = 0; r < records.Length; r++)
                {
                    DecodedIdentityRecord record = records[r];
                    string id = record.NetEntityId.ToString(CultureInfo.InvariantCulture);
                    var attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["EntityIdentity.entityType"] = record.EntityType,
                        ["EntityIdentity.unmappedMark"] = record.UnmappedMark ?? string.Empty
                    };
                    _entities[id] = new EntityRecord(
                        id,
                        record.EntityType,
                        roomId,
                        generation,
                        decoded.Revision,
                        decoded.TickId,
                        true,
                        false,
                        attributes);
                }
            }
        }

        private void ApplyTombstones(ReadOnlyMemory<ulong> tombstoneEntityIds)
        {
            ReadOnlySpan<ulong> span = tombstoneEntityIds.Span;
            for (int i = 0; i < span.Length; i++)
            {
                string id = span[i].ToString(CultureInfo.InvariantCulture);
                if (_entities.TryGetValue(id, out EntityRecord entity))
                {
                    entity.Tombstoned = true;
                    _entities[id] = entity;
                }
            }
        }

        private ReplicaAdmissionResult RejectAdmission(string code)
        {
            _lastRejectCode = code;
            return new ReplicaAdmissionResult(false, code);
        }

        private static bool IsEntityType(string entityType)
        {
            return string.Equals(entityType, "player", StringComparison.Ordinal)
                || string.Equals(entityType, "bot", StringComparison.Ordinal);
        }

        private static string ClassifyAttributeId(string attributeId)
        {
            if (string.IsNullOrEmpty(attributeId) || Encoding.UTF8.GetByteCount(attributeId) > MaxAttributeIdBytes)
            {
                return "invalid_attribute_id";
            }

            if (attributeId.Contains('(')
                || attributeId.StartsWith("Storage.", StringComparison.Ordinal)
                || attributeId.Contains('/')
                || attributeId.Contains('\\'))
            {
                return "storage_access_forbidden";
            }

            if (!AttributeIdGrammar.IsMatch(attributeId))
            {
                return "invalid_attribute_id";
            }

            if (!AttributeDeclarationTable.TryGet(attributeId, out _))
            {
                return "undeclared_attribute";
            }

            return string.Empty;
        }

        private static ReplicaAttributeQueryResult RequestError(string code)
        {
            return new ReplicaAttributeQueryResult(
                ReplicaQueryStatus.RequestError,
                code,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0UL,
                0UL);
        }

        private static ReplicaAttributeQueryResult Outcome(ReplicaQueryStatus status, string netEntityId, string roomId, string attributeId)
        {
            return new ReplicaAttributeQueryResult(
                status,
                string.Empty,
                netEntityId,
                roomId,
                attributeId,
                string.Empty,
                0UL,
                0UL);
        }

        private sealed class EntityRecord
        {
            public EntityRecord(
                string netEntityId,
                string entityType,
                string roomId,
                ulong connectionGeneration,
                ulong revision,
                ulong tick,
                bool inAoi,
                bool tombstoned,
                Dictionary<string, string> attributes)
            {
                NetEntityId = netEntityId;
                EntityType = entityType;
                RoomId = roomId;
                ConnectionGeneration = connectionGeneration;
                Revision = revision;
                Tick = tick;
                InAoi = inAoi;
                Tombstoned = tombstoned;
                Attributes = attributes;
            }

            public string NetEntityId { get; }

            public string EntityType { get; }

            public string RoomId { get; }

            public ulong ConnectionGeneration { get; }

            public ulong Revision { get; }

            public ulong Tick { get; }

            public bool InAoi { get; }

            public bool Tombstoned { get; set; }

            public Dictionary<string, string> Attributes { get; }

            public static EntityRecord FromVisible(in ReplicaVisibleEntity visible)
            {
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                ReplicaAttributeValue[] values = visible.Attributes ?? Array.Empty<ReplicaAttributeValue>();
                for (int i = 0; i < values.Length; i++)
                {
                    attributes[values[i].AttributeId] = values[i].Value ?? string.Empty;
                }

                return new EntityRecord(
                    visible.NetEntityId,
                    visible.EntityType,
                    visible.RoomId,
                    visible.ConnectionGeneration,
                    visible.Revision,
                    visible.Tick,
                    visible.InAoi,
                    visible.Tombstoned,
                    attributes);
            }
        }
    }
}
