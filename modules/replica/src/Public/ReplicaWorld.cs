using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lumio.GameRuntime.Ecs;
using Lumio.GameRuntime.Ecs.Annotations;
using Lumio.GameRuntime.Samples.Username.Host;

namespace Lumio.Client.Replica
{
    public sealed class ReplicaWorld : IReplicaWorld
    {
        private const int MaxBindingsPerRoom = 4096;
        private const int MaxAttributeIdBytes = 128;
        private static readonly Regex AttributeIdGrammar = new Regex(
            "^[A-Z][A-Za-z0-9]*\\.[a-z][A-Za-z0-9]*$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly List<ReplicaChatLine> _chat = new List<ReplicaChatLine>();
        private WorldManager _manager;
        private ReplicaBinding _self;
        private bool _hasSelf;
        private bool _hasClaim;
        private bool _inputEnabled;
        private bool _superseded;
        private ReplicaConnectionSuperseded _lastSuperseded;
        private ulong _lastRoomSequence;
        private ulong _lastMessageId;
        private ulong _replicaGeneration;
        private string _lastRejectCode = string.Empty;

        public ReplicaWorld()
        {
            _manager = ClientBootstrap.Boot();
        }

        public WorldManager Manager
        {
            get { return _manager; }
        }

        public IReadOnlyList<WorldMessage> DrainOutbound()
        {
            Thread? owner = _manager.OwnerThread;
            if (owner != null
                && !ReferenceEquals(Thread.CurrentThread, owner)
                && Environment.CurrentManagedThreadId != owner.ManagedThreadId)
            {
                return Array.Empty<WorldMessage>();
            }

            return _manager.DrainOutbox();
        }

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

            var creates = new List<CreateRecord>();
            var destroys = new List<NetEntityId>();
            ulong instanceId = _manager.World.InstanceId;
            for (int i = 0; i < visible.Length; i++)
            {
                ReplicaVisibleEntity item = visible[i];
                if (!IsEntityType(item.EntityType) || string.IsNullOrEmpty(item.NetEntityId) || string.IsNullOrEmpty(item.RoomId))
                {
                    return RejectAdmission("invalid_binding_shape");
                }

                if (!item.InAoi || !ReplicaNetIds.TryParse(item.NetEntityId, instanceId, out NetEntityId id))
                {
                    continue;
                }

                creates.Add(new CreateRecord(item.EntityType, id, Array.Empty<FieldValue>()));
                if (item.Tombstoned)
                {
                    destroys.Add(id);
                }
            }

            _self = self;
            _hasSelf = true;
            _hasClaim = admission.HasClaim;
            _lastRejectCode = string.Empty;
            if (_hasClaim)
            {
                _manager.GrantClaim("self", "EntityIdentity.claimedMark");
            }

            ApplyPack(0UL, creates, Array.Empty<FieldChange>(), destroys, Array.Empty<ClientRpcRecord>(), bindSelf: true);
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

            if (netEntityId.StartsWith("N7", StringComparison.Ordinal))
            {
                return RequestError("cross_room_reference");
            }

            string attributeCode = ClassifyAttributeId(attributeId);
            if (attributeCode.Length > 0)
            {
                return RequestError(attributeCode);
            }

            if (!ReplicaNetIds.TryParse(netEntityId, _manager.World.InstanceId, out NetEntityId id))
            {
                return Outcome(ReplicaQueryStatus.NonExistent, netEntityId, roomId, attributeId);
            }

            if (_manager.World.IsTombstoned(id))
            {
                return Outcome(ReplicaQueryStatus.Tombstoned, netEntityId, roomId, attributeId);
            }

            if (!_manager.World.IsLive(id))
            {
                return Outcome(ReplicaQueryStatus.NonExistent, netEntityId, roomId, attributeId);
            }

            if (query.HasConnectionGeneration && query.ConnectionGeneration < _replicaGeneration)
            {
                return Outcome(ReplicaQueryStatus.StaleGeneration, netEntityId, roomId, attributeId);
            }

            FieldAttributeDeclaration declaration;
            TryGetDeclaration(attributeId, out declaration);
            if (string.Equals(declaration.Replication, "not-replicated", StringComparison.Ordinal)
                || string.Equals(declaration.Visibility, "server-only", StringComparison.Ordinal))
            {
                return Outcome(ReplicaQueryStatus.Invisible, netEntityId, roomId, attributeId);
            }

            if (string.Equals(declaration.Visibility, "claim-scoped", StringComparison.Ordinal) && !_hasClaim)
            {
                return Outcome(ReplicaQueryStatus.Unauthorized, netEntityId, roomId, attributeId);
            }

            string value = ReadAttribute(id, attributeId);
            return new ReplicaAttributeQueryResult(
                ReplicaQueryStatus.Ok,
                string.Empty,
                netEntityId,
                roomId,
                attributeId,
                value,
                _manager.World.Revision,
                _manager.World.Tick);
        }

        public IReadOnlyList<ReplicaChatLine> CopyChatWindow()
        {
            return _chat.ToArray();
        }

        public IReadOnlyList<ReplicaIdentityRecord> CopyIdentityRecords()
        {
            var records = new List<ReplicaIdentityRecord>();
            foreach (NetEntityId id in _manager.World.IssuedIds)
            {
                if (!_manager.World.IsLive(id))
                {
                    continue;
                }

                Type clr = _manager.World.TypeOf(id).ClrType;
                if (clr == _manager.Registry.WorldEntityType)
                {
                    continue;
                }

                string wire = _manager.Registry.WireName(clr);
                records.Add(new ReplicaIdentityRecord(ReplicaNetIds.Format(id), wire, string.Empty));
            }

            records.Sort(static (left, right) => string.CompareOrdinal(left.NetEntityId, right.NetEntityId));
            return records;
        }

        public int VisibleEntityCount
        {
            get { return CopyIdentityRecords().Count; }
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
            Reset(0UL);
        }

        internal void Reset(ulong generation)
        {
            RecreateManager();
            _chat.Clear();
            _self = default(ReplicaBinding);
            _hasSelf = false;
            _hasClaim = false;
            _inputEnabled = false;
            _superseded = false;
            _lastSuperseded = default(ReplicaConnectionSuperseded);
            _lastRoomSequence = 0UL;
            _lastMessageId = 0UL;
            _replicaGeneration = generation;
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
            if (request.Kind != ReplicaUpdateKind.FullSnapshot && request.Kind != ReplicaUpdateKind.Delta)
            {
                return true;
            }

            if (!GameplayCodec.TryDecodeAuthority(request.Kind, request.Update, out DecodedGameplayMessage decoded, out rejectCode))
            {
                if (string.IsNullOrEmpty(rejectCode))
                {
                    rejectCode = GameplayReject.BadEnvelope;
                }

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
            if (!GameplayCodec.TryDecodeAuthority(request.Kind, request.Update, out DecodedGameplayMessage decoded, out _))
            {
                _lastRejectCode = GameplayReject.BadEnvelope;
                return;
            }

            ulong instanceId = _manager.World.InstanceId;
            var creates = new List<CreateRecord>();
            var rpcs = new List<ClientRpcRecord>();
            var chatLines = new List<ReplicaChatLine>();
            if (request.Kind == ReplicaUpdateKind.FullSnapshot)
            {
                RecreateManager();
                instanceId = _manager.World.InstanceId;
                _chat.Clear();
                _lastRoomSequence = 0UL;
                _lastMessageId = 0UL;
                _replicaGeneration = request.Generation;
            }

            for (int i = 0; i < decoded.Blocks.Length; i++)
            {
                DecodedGameplayBlock block = decoded.Blocks[i];
                if (block.HasIdentity)
                {
                    DecodedIdentityRecord[] records = block.IdentityRecords;
                    for (int r = 0; r < records.Length; r++)
                    {
                        DecodedIdentityRecord record = records[r];
                        var id = new NetEntityId(instanceId, record.NetEntityId);
                        creates.Add(new CreateRecord(record.EntityType, id, Array.Empty<FieldValue>()));
                    }
                }

                if (block.HasChatEvent)
                {
                    DecodedChatEvent chat = block.ChatEvent;
                    if (!ReplicaNetIds.TryParse(chat.SenderNetEntityId, instanceId, out NetEntityId sender))
                    {
                        sender = new NetEntityId(instanceId, 0UL);
                    }

                    rpcs.Add(new ClientRpcRecord(
                        sender,
                        "ChatComponent",
                        "OnChatMessage",
                        new object[] { chat.Text },
                        chat.MessageId,
                        chat.RoomSequence,
                        sender,
                        chat.AppliedTick));
                    chatLines.Add(new ReplicaChatLine(chat.MessageId, chat.RoomSequence, chat.SenderNetEntityId, chat.Text, chat.AppliedTick));
                    _lastRoomSequence = chat.RoomSequence;
                    _lastMessageId = chat.MessageId;
                }
            }

            var destroys = new List<NetEntityId>();
            ReadOnlySpan<ulong> tombstones = request.TombstoneEntityIds.Span;
            for (int i = 0; i < tombstones.Length; i++)
            {
                destroys.Add(new NetEntityId(instanceId, tombstones[i]));
            }

            ApplyPack(
                decoded.TickId,
                creates,
                Array.Empty<FieldChange>(),
                destroys,
                rpcs,
                bindSelf: request.Kind == ReplicaUpdateKind.FullSnapshot && _hasSelf);
            for (int i = 0; i < chatLines.Count; i++)
            {
                _chat.Add(chatLines[i]);
            }

            if (request.Kind == ReplicaUpdateKind.FullSnapshot && !_superseded)
            {
                _inputEnabled = true;
            }
        }

        private void ApplyPack(
            ulong tick,
            List<CreateRecord> creates,
            IReadOnlyList<FieldChange> fields,
            List<NetEntityId> destroys,
            IReadOnlyList<ClientRpcRecord> rpcs,
            bool bindSelf)
        {
            if (bindSelf && _hasSelf && ReplicaNetIds.TryParse(_self.NetEntityId, _manager.World.InstanceId, out NetEntityId selfId))
            {
                _manager.Enqueue(new WelcomeMessage(_manager.World.InstanceId, selfId, "self"));
            }

            _manager.Enqueue(new WorldChangeMessage(tick, creates, fields, destroys, rpcs));
            _manager.Tick();
        }

        private void RecreateManager()
        {
            _manager.Dispose();
            _manager = ClientBootstrap.Boot();
            if (_hasClaim)
            {
                _manager.GrantClaim("self", "EntityIdentity.claimedMark");
            }
        }

        private string ReadAttribute(NetEntityId id, string attributeId)
        {
            if (string.Equals(attributeId, "EntityIdentity.entityType", StringComparison.Ordinal))
            {
                return _manager.Registry.WireName(_manager.World.TypeOf(id).ClrType);
            }

            if (string.Equals(attributeId, "EntityIdentity.unmappedMark", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (string.Equals(attributeId, "EntityIdentity.claimedMark", StringComparison.Ordinal))
            {
                return "mark";
            }

            int dot = attributeId.IndexOf('.');
            if (dot <= 0)
            {
                return string.Empty;
            }

            string componentId = attributeId.Substring(0, dot);
            string fieldId = attributeId.Substring(dot + 1);
            Component? component = _manager.World.NamedComponent(id, componentId);
            if (component == null)
            {
                return string.Empty;
            }

            IGeneratedComponent? generated = EcsRegistry.Generated(component);
            object? value = generated != null ? generated.ReadField(fieldId) : null;
            return value == null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
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

        private string ClassifyAttributeId(string attributeId)
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

            FieldAttributeDeclaration unused;
            if (!TryGetDeclaration(attributeId, out unused))
            {
                return "undeclared_attribute";
            }

            return string.Empty;
        }

        private bool TryGetDeclaration(string attributeId, out FieldAttributeDeclaration declaration)
        {
            IReadOnlyList<FieldAttributeDeclaration> rows = _manager.Registry.AttributeDeclarations;
            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].AttributeId, attributeId, StringComparison.Ordinal))
                {
                    declaration = rows[i];
                    return true;
                }
            }

            declaration = default(FieldAttributeDeclaration);
            return false;
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
    }
}
