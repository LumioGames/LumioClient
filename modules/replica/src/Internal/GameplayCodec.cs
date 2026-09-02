using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Lumio.Client.Replica
{
    internal static class GameplayReject
    {
        public const string BadEnvelope = "bad_envelope";
        public const string UnknownCommandType = "unknown_command_type";
        public const string BadPayloadHash = "bad_payload_hash";
        public const string UndecodablePayload = "undecodable_payload";
        public const string BlockOrderViolation = "block_order_violation";
        public const string StateBlockKindMismatch = "state_block_kind_mismatch";
        public const string ChatTextTooLong = "chat_text_too_long";
    }

    internal static class GameplayMappings
    {
        public const string ChatInput = "chat.input";
        public const string ChatEvent = "chat.event";
        public const string ChatComponent = "chat.component";
        public const string EntityIdentity = "entity.identity";
        public const int ChatTextMaxUtf8Bytes = 512;
        public const int MaxBlocksPerEnvelope = 4096;
        public const int MaxFrameBytes = 65536;

        public static bool TryKind(string mappingId, out string kind)
        {
            kind = string.Empty;
            if (mappingId == ChatInput)
            {
                kind = "command";
                return true;
            }

            if (mappingId == ChatEvent)
            {
                kind = "event";
                return true;
            }

            if (mappingId == ChatComponent)
            {
                kind = "componentState";
                return true;
            }

            if (mappingId == EntityIdentity)
            {
                kind = "state";
                return true;
            }

            return false;
        }
    }

    internal readonly struct DecodedChatEvent
    {
        public DecodedChatEvent(ulong messageId, ulong roomSequence, string senderNetEntityId, string text, ulong appliedTick)
        {
            MessageId = messageId;
            RoomSequence = roomSequence;
            SenderNetEntityId = senderNetEntityId;
            Text = text;
            AppliedTick = appliedTick;
        }

        public ulong MessageId { get; }

        public ulong RoomSequence { get; }

        public string SenderNetEntityId { get; }

        public string Text { get; }

        public ulong AppliedTick { get; }
    }

    internal readonly struct DecodedIdentityRecord
    {
        public DecodedIdentityRecord(ulong netEntityId, string entityType, string unmappedMark)
        {
            NetEntityId = netEntityId;
            EntityType = entityType;
            UnmappedMark = unmappedMark;
        }

        public ulong NetEntityId { get; }

        public string EntityType { get; }

        public string UnmappedMark { get; }
    }

    internal readonly struct DecodedGameplayBlock
    {
        public DecodedGameplayBlock(string mappingId, byte[] payload, DecodedChatEvent chatEvent, bool hasChatEvent)
            : this(mappingId, payload, chatEvent, hasChatEvent, Array.Empty<DecodedIdentityRecord>(), false)
        {
        }

        public DecodedGameplayBlock(
            string mappingId,
            byte[] payload,
            DecodedChatEvent chatEvent,
            bool hasChatEvent,
            DecodedIdentityRecord[] identityRecords,
            bool hasIdentity)
        {
            MappingId = mappingId;
            Payload = payload;
            ChatEvent = chatEvent;
            HasChatEvent = hasChatEvent;
            IdentityRecords = identityRecords ?? Array.Empty<DecodedIdentityRecord>();
            HasIdentity = hasIdentity;
        }

        public string MappingId { get; }

        public byte[] Payload { get; }

        public DecodedChatEvent ChatEvent { get; }

        public bool HasChatEvent { get; }

        public DecodedIdentityRecord[] IdentityRecords { get; }

        public bool HasIdentity { get; }
    }

    internal readonly struct DecodedGameplayMessage
    {
        public DecodedGameplayMessage(string messageType, ulong tickId, ulong revision, DecodedGameplayBlock[] blocks)
        {
            MessageType = messageType;
            TickId = tickId;
            Revision = revision;
            Blocks = blocks;
        }

        public string MessageType { get; }

        public ulong TickId { get; }

        public ulong Revision { get; }

        public DecodedGameplayBlock[] Blocks { get; }
    }

    internal static class GameplayCodec
    {
        private static readonly Regex LowerSha256 = new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

        public static bool TryDecodeAuthority(
            ReplicaUpdateKind kind,
            ReadOnlyMemory<byte> utf8,
            out DecodedGameplayMessage message,
            out string rejectCode)
        {
            message = default(DecodedGameplayMessage);
            rejectCode = GameplayReject.BadEnvelope;
            if (utf8.Length > GameplayMappings.MaxFrameBytes)
            {
                return false;
            }

            if (!LiteJsonParser.TryParse(utf8.Span, out LiteNode root) || root.Kind != LiteKind.Object)
            {
                return false;
            }

            if (!root.TryGetString("messageType", out string messageType))
            {
                return false;
            }

            string expected = kind == ReplicaUpdateKind.FullSnapshot ? "FullSnapshot" : "Delta";
            if (!string.Equals(messageType, expected, StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetUInt64("tickId", out ulong tickId) || !root.TryGetUInt64("revision", out ulong revision))
            {
                return false;
            }

            string arrayName = kind == ReplicaUpdateKind.FullSnapshot ? "stateBlocks" : "changedBlocks";
            if (!root.TryGetArray(arrayName, out var blocks))
            {
                return false;
            }

            if (blocks.Count > GameplayMappings.MaxBlocksPerEnvelope)
            {
                return false;
            }

            var decoded = new DecodedGameplayBlock[blocks.Count];
            string previousMapping = string.Empty;
            for (int i = 0; i < blocks.Count; i++)
            {
                LiteNode block = blocks[i];
                if (block.Kind != LiteKind.Object)
                {
                    return false;
                }

                if (!block.TryGetString("mappingId", out string mappingId)
                    || !block.TryGetString("payload", out string payloadHex)
                    || !block.TryGetString("payloadSha256", out string payloadSha))
                {
                    return false;
                }

                if (i > 0)
                {
                    int order = string.CompareOrdinal(previousMapping, mappingId);
                    if (order >= 0)
                    {
                        rejectCode = GameplayReject.BlockOrderViolation;
                        return false;
                    }
                }

                previousMapping = mappingId;
                if (!TryDecodeBlock(kind, mappingId, payloadHex, payloadSha, out decoded[i], out rejectCode))
                {
                    return false;
                }
            }

            message = new DecodedGameplayMessage(messageType, tickId, revision, decoded);
            rejectCode = string.Empty;
            return true;
        }

        public static bool TryDecodeConnectionSuperseded(
            ReadOnlyMemory<byte> utf8,
            out ReplicaConnectionSuperseded notice,
            out string rejectCode)
        {
            notice = default(ReplicaConnectionSuperseded);
            rejectCode = GameplayReject.BadEnvelope;
            if (utf8.Length > GameplayMappings.MaxFrameBytes)
            {
                return false;
            }

            if (!LiteJsonParser.TryParse(utf8.Span, out LiteNode root) || root.Kind != LiteKind.Object)
            {
                return false;
            }

            if (!root.TryGetString("messageType", out string messageType)
                || !string.Equals(messageType, "ConnectionSuperseded", StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetString("reasonCode", out string reasonCode)
                || !string.Equals(reasonCode, "connection_superseded", StringComparison.Ordinal))
            {
                return false;
            }

            if (!root.TryGetUInt64("netEntityId", out ulong netEntityId)
                || !root.TryGetUInt64("newConnectionGeneration", out ulong newConnectionGeneration)
                || newConnectionGeneration < 1UL)
            {
                return false;
            }

            notice = new ReplicaConnectionSuperseded(
                true,
                reasonCode,
                netEntityId.ToString(CultureInfo.InvariantCulture),
                newConnectionGeneration);
            rejectCode = string.Empty;
            return true;
        }

        private static bool TryDecodeBlock(
            ReplicaUpdateKind updateKind,
            string mappingId,
            string payloadHex,
            string payloadSha,
            out DecodedGameplayBlock block,
            out string rejectCode)
        {
            block = default(DecodedGameplayBlock);
            if (!GameplayMappings.TryKind(mappingId, out string mappingKind))
            {
                rejectCode = updateKind == ReplicaUpdateKind.Delta || updateKind == ReplicaUpdateKind.FullSnapshot
                    ? GameplayReject.StateBlockKindMismatch
                    : GameplayReject.UnknownCommandType;
                return false;
            }

            if (updateKind == ReplicaUpdateKind.FullSnapshot && mappingKind != "state")
            {
                rejectCode = GameplayReject.StateBlockKindMismatch;
                return false;
            }

            if (updateKind == ReplicaUpdateKind.Delta && mappingKind != "event" && mappingKind != "state")
            {
                rejectCode = GameplayReject.StateBlockKindMismatch;
                return false;
            }

            if (!TryDecodeHex(payloadHex, out byte[] payload))
            {
                rejectCode = GameplayReject.UndecodablePayload;
                return false;
            }

            if (payloadSha == null || !LowerSha256.IsMatch(payloadSha) || !Sha256Lower(payload).Equals(payloadSha, StringComparison.Ordinal))
            {
                rejectCode = GameplayReject.BadPayloadHash;
                return false;
            }

            if (mappingId == GameplayMappings.ChatEvent)
            {
                if (!TryDecodeChatEvent(payload, out DecodedChatEvent chatEvent, out rejectCode))
                {
                    return false;
                }

                block = new DecodedGameplayBlock(mappingId, payload, chatEvent, true);
                rejectCode = string.Empty;
                return true;
            }

            if (mappingId == GameplayMappings.EntityIdentity)
            {
                if (!TryDecodeIdentity(payload, out DecodedIdentityRecord[] records, out rejectCode))
                {
                    return false;
                }

                block = new DecodedGameplayBlock(
                    mappingId,
                    payload,
                    default(DecodedChatEvent),
                    false,
                    records,
                    true);
                rejectCode = string.Empty;
                return true;
            }

            block = new DecodedGameplayBlock(mappingId, payload, default(DecodedChatEvent), false);
            rejectCode = string.Empty;
            return true;
        }

        private static bool TryDecodeIdentity(
            byte[] payload,
            out DecodedIdentityRecord[] records,
            out string rejectCode)
        {
            records = Array.Empty<DecodedIdentityRecord>();
            int offset = 0;
            if (!TryReadUInt32(payload, ref offset, out uint count))
            {
                rejectCode = GameplayReject.UndecodablePayload;
                return false;
            }

            if (count == 0U)
            {
                rejectCode = GameplayReject.UndecodablePayload;
                return false;
            }

            records = new DecodedIdentityRecord[count];
            ulong previous = 0UL;
            for (uint i = 0; i < count; i++)
            {
                if (!TryReadUInt64(payload, ref offset, out ulong netEntityId)
                    || !TryReadString(payload, ref offset, out string entityType)
                    || !TryReadString(payload, ref offset, out string unmappedMark))
                {
                    rejectCode = GameplayReject.UndecodablePayload;
                    return false;
                }

                if (!string.Equals(entityType, "player", StringComparison.Ordinal)
                    && !string.Equals(entityType, "bot", StringComparison.Ordinal))
                {
                    rejectCode = GameplayReject.UndecodablePayload;
                    return false;
                }

                if (i > 0U && netEntityId <= previous)
                {
                    rejectCode = GameplayReject.BlockOrderViolation;
                    return false;
                }

                previous = netEntityId;
                records[i] = new DecodedIdentityRecord(netEntityId, entityType, unmappedMark ?? string.Empty);
            }

            if (offset != payload.Length)
            {
                rejectCode = GameplayReject.UndecodablePayload;
                return false;
            }

            rejectCode = string.Empty;
            return true;
        }

        private static bool TryDecodeChatEvent(byte[] payload, out DecodedChatEvent chatEvent, out string rejectCode)
        {
            chatEvent = default(DecodedChatEvent);
            int offset = 0;
            if (!TryReadUInt64(payload, ref offset, out ulong messageId)
                || !TryReadUInt64(payload, ref offset, out ulong roomSequence)
                || !TryReadUInt64(payload, ref offset, out ulong sender)
                || !TryReadString(payload, ref offset, out string text)
                || !TryReadUInt64(payload, ref offset, out ulong appliedTick)
                || offset != payload.Length)
            {
                rejectCode = GameplayReject.UndecodablePayload;
                return false;
            }

            int textBytes = Encoding.UTF8.GetByteCount(text);
            if (textBytes > GameplayMappings.ChatTextMaxUtf8Bytes)
            {
                rejectCode = GameplayReject.ChatTextTooLong;
                return false;
            }

            chatEvent = new DecodedChatEvent(
                messageId,
                roomSequence,
                sender.ToString(CultureInfo.InvariantCulture),
                text,
                appliedTick);
            rejectCode = string.Empty;
            return true;
        }

        private static bool TryReadUInt32(byte[] data, ref int offset, out uint value)
        {
            value = 0U;
            if (offset + 4 > data.Length)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;
            return true;
        }

        private static bool TryReadUInt64(byte[] data, ref int offset, out ulong value)
        {
            value = 0UL;
            if (offset + 8 > data.Length)
            {
                return false;
            }

            value = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8));
            offset += 8;
            return true;
        }

        private static bool TryReadString(byte[] data, ref int offset, out string value)
        {
            value = string.Empty;
            if (offset + 4 > data.Length)
            {
                return false;
            }

            uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
            offset += 4;
            if (length > (uint)(data.Length - offset))
            {
                return false;
            }

            value = Encoding.UTF8.GetString(data, offset, (int)length);
            offset += (int)length;
            return true;
        }

        private static bool TryDecodeHex(string hex, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (hex == null || (hex.Length & 1) != 0)
            {
                return false;
            }

            bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = Nibble(hex[i * 2]);
                int lo = Nibble(hex[(i * 2) + 1]);
                if (hi < 0 || lo < 0)
                {
                    return false;
                }

                bytes[i] = (byte)((hi << 4) | lo);
            }

            return true;
        }

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }

            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }

            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }

            return -1;
        }

        private static string Sha256Lower(byte[] payload)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(payload);
                var chars = new char[digest.Length * 2];
                for (int i = 0; i < digest.Length; i++)
                {
                    int hi = digest[i] >> 4;
                    int lo = digest[i] & 0xF;
                    chars[i * 2] = (char)(hi < 10 ? '0' + hi : 'a' + (hi - 10));
                    chars[(i * 2) + 1] = (char)(lo < 10 ? '0' + lo : 'a' + (lo - 10));
                }

                return new string(chars);
            }
        }
    }
}
