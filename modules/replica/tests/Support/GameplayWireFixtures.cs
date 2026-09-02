using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Lumio.Client.Replica;

namespace Lumio.Client.Replica.Tests.Support;

internal static class GameplayWireFixtures
{
    public const string ChatEventPayload = "0100000000000000010000000000000065000000000000000200000067670700000000000000";
    public const string ChatEventSha256 = "9fafc556e56dc024a90caf7c102dfccfed4189c708e0a51b0139aab28277670c";
    public const string ChatInputPayload = "020000006767";
    public const string ChatInputSha256 = "5dbd584f1718b8bcd0dab4abeea83169f4a990defab81a8316ed845798d92dab";
    public const string ChatComponentPayload = "0200000067670700000000000000";
    public const string ChatComponentSha256 = "ba9d631032a1ecb5c1b4723b9d9603cf29c8db92736620112cac56b0051d5259";
    public const string IdentityTwoLivePayload = "02000000650000000000000006000000706c617965720100000061660000000000000003000000626f740100000062";
    public const string IdentityTwoLiveSha256 = "4ae28198083875a42260bcd2c9493077c1726f351eace497c21c51f136d247b1";

    public static string EmptySnapshot()
    {
        return "{\"messageType\":\"FullSnapshot\",\"tickId\":0,\"revision\":0,\"stateBlocks\":[]}";
    }

    public static string ContractIdentitySnapshot()
    {
        return IdentitySnapshot(IdentityTwoLivePayload, IdentityTwoLiveSha256);
    }

    public static string IdentityCensus(params (ulong NetEntityId, string EntityType, string UnmappedMark)[] records)
    {
        (string payload, string sha) = EncodeIdentity(records);
        return IdentitySnapshot(payload, sha, 0, 0);
    }

    public static bool CommitCensus(IClientReplica replica, params (ulong NetEntityId, string EntityType, string UnmappedMark)[] records)
    {
        return CommitJson(replica, ReplicaUpdateKind.FullSnapshot, IdentityCensus(records), 1, 10, 0, 0);
    }

    public static string IdentitySnapshot(string payload, string sha, ulong tickId = 7, ulong revision = 1)
    {
        return "{\"messageType\":\"FullSnapshot\",\"tickId\":" + tickId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               ",\"revision\":" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               ",\"stateBlocks\":[" + Block("entity.identity", payload, sha) + "]}";
    }

    public static string ConnectionSupersededNotice(ulong netEntityId = 101, ulong newConnectionGeneration = 2)
    {
        return "{\"messageType\":\"ConnectionSuperseded\",\"reasonCode\":\"connection_superseded\",\"netEntityId\":" +
               netEntityId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               ",\"newConnectionGeneration\":" +
               newConnectionGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
    }

    public static (string Payload, string Sha256) EncodeIdentity(params (ulong NetEntityId, string EntityType, string UnmappedMark)[] records)
    {
        int size = 4;
        var utf8 = new List<byte[]>();
        for (int i = 0; i < records.Length; i++)
        {
            byte[] type = Encoding.UTF8.GetBytes(records[i].EntityType);
            byte[] mark = Encoding.UTF8.GetBytes(records[i].UnmappedMark);
            utf8.Add(type);
            utf8.Add(mark);
            size += 8 + 4 + type.Length + 4 + mark.Length;
        }

        byte[] bytes = new byte[size];
        int offset = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), (uint)records.Length);
        offset += 4;
        for (int i = 0; i < records.Length; i++)
        {
            WriteU64(bytes, ref offset, records[i].NetEntityId);
            byte[] type = utf8[i * 2];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), (uint)type.Length);
            offset += 4;
            type.CopyTo(bytes, offset);
            offset += type.Length;
            byte[] mark = utf8[(i * 2) + 1];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), (uint)mark.Length);
            offset += 4;
            mark.CopyTo(bytes, offset);
            offset += mark.Length;
        }

        string payload = Convert.ToHexString(bytes).ToLowerInvariant();
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (payload, sha);
    }

    public static string SnapshotWithChatEvent()
    {
        return "{\"messageType\":\"FullSnapshot\",\"tickId\":7,\"revision\":1,\"stateBlocks\":[" +
               Block("chat.event", ChatEventPayload, ChatEventSha256) + "]}";
    }

    public static string ChatDelta(string payload, string sha, ulong tickId, ulong revision)
    {
        return "{\"messageType\":\"Delta\",\"tickId\":" + tickId.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               ",\"revision\":" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               ",\"changedBlocks\":[" + Block("chat.event", payload, sha) + "]}";
    }

    public static string ContractChatDelta()
    {
        return ChatDelta(ChatEventPayload, ChatEventSha256, 7, 1);
    }

    public static string DeltaWithComponent()
    {
        return "{\"messageType\":\"Delta\",\"tickId\":7,\"revision\":2,\"changedBlocks\":[" +
               Block("chat.component", ChatComponentPayload, ChatComponentSha256) + "]}";
    }

    public static string InputCommand()
    {
        return "{\"messageType\":\"InputCommand\",\"commands\":[" +
               Block("chat.input", ChatInputPayload, ChatInputSha256) + "]}";
    }

    public static string BadHashDelta()
    {
        return ChatDelta(ChatEventPayload, "0000000000000000000000000000000000000000000000000000000000000000", 7, 1);
    }

    public static (string Payload, string Sha256) EncodeChatEvent(
        ulong messageId,
        ulong roomSequence,
        ulong senderNetEntityId,
        string text,
        ulong appliedTick)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(text);
        byte[] bytes = new byte[8 + 8 + 8 + 4 + utf8.Length + 8];
        int offset = 0;
        WriteU64(bytes, ref offset, messageId);
        WriteU64(bytes, ref offset, roomSequence);
        WriteU64(bytes, ref offset, senderNetEntityId);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), (uint)utf8.Length);
        offset += 4;
        utf8.CopyTo(bytes, offset);
        offset += utf8.Length;
        WriteU64(bytes, ref offset, appliedTick);
        string payload = Convert.ToHexString(bytes).ToLowerInvariant();
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (payload, sha);
    }

    public static ReplicaChatConsumer CreateConsumer(ReplicaClientKind kind)
    {
        IClientReplica replica = new ClientReplicaFactory().Create();
        replica.ResetForNewSession(new ReplicaResetRequest(1));
        return new ReplicaChatConsumer(kind, replica);
    }

    public static ReplicaAdmissionResult AdmitRoom(
        IReplicaWorld world,
        string selfId = "1",
        string selfType = "player",
        string roomId = "room-01",
        ReplicaVisibleEntity[]? extras = null)
    {
        var visible = new List<ReplicaVisibleEntity>
        {
            Entity(selfId, selfType, roomId, generation: 1, revision: 1, tick: 0)
        };
        if (extras != null && extras.Length > 0)
        {
            visible.AddRange(extras);
        }
        var admission = new ReplicaAdmission(
            new ReplicaBinding("acct-07", roomId, selfId, selfType, 1),
            visible.ToArray());
        return world.InstallAdmission(in admission);
    }

    public static ReplicaVisibleEntity Entity(
        string netEntityId,
        string entityType,
        string roomId,
        ulong generation,
        ulong revision,
        ulong tick,
        bool inAoi = true,
        bool tombstoned = false)
    {
        return new ReplicaVisibleEntity(
            netEntityId,
            entityType,
            roomId,
            generation,
            revision,
            tick,
            new[] { new ReplicaAttributeValue("EntityIdentity.entityType", entityType) },
            inAoi,
            tombstoned);
    }

    public static ReplicaStageStatus StageJson(
        IClientReplica replica,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision,
        out ReplicaStageHandle handle)
    {
        return StageJson(replica, kind, json, sequence, baseline, fromRevision, toRevision, 1, out handle);
    }

    public static ReplicaStageStatus StageJson(
        IClientReplica replica,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision,
        ulong generation,
        out ReplicaStageHandle handle)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var request = new ReplicaStageRequest(
            generation,
            kind,
            baseline,
            fromRevision,
            toRevision,
            sequence,
            bytes,
            Array.Empty<ulong>(),
            Array.Empty<ulong>());
        return replica.StageAuthority(in request, out handle, out _).Status;
    }

    public static ReplicaStageStatus StageJson(
        IClientReplica replica,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision,
        ulong[] tombstones,
        out ReplicaStageHandle handle)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var request = new ReplicaStageRequest(
            1,
            kind,
            baseline,
            fromRevision,
            toRevision,
            sequence,
            bytes,
            tombstones,
            Array.Empty<ulong>());
        return replica.StageAuthority(in request, out handle, out _).Status;
    }

    public static bool CommitJson(
        IClientReplica replica,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision)
    {
        return CommitJson(replica, kind, json, sequence, baseline, fromRevision, toRevision, 1);
    }

    public static bool CommitJson(
        IClientReplica replica,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision,
        ulong generation)
    {
        ReplicaStageStatus staged = StageJson(
            replica,
            kind,
            json,
            sequence,
            baseline,
            fromRevision,
            toRevision,
            generation,
            out ReplicaStageHandle handle);
        if (staged != ReplicaStageStatus.Staged)
        {
            return false;
        }

        return replica.ObserveRuntimeOutcome(
            handle,
            ReplicaRuntimeOutcome.CommittedOutcome(),
            out _) == ReplicaOutcomeStatus.Observed;
    }

    public static bool CommitEmptySnapshot(IClientReplica replica)
    {
        return CommitJson(replica, ReplicaUpdateKind.FullSnapshot, EmptySnapshot(), 1, 10, 0, 0);
    }

    public static bool CommitJson(
        IClientReplica replica,
        ReplicaUpdateKind kind,
        string json,
        ulong sequence,
        ulong baseline,
        ulong fromRevision,
        ulong toRevision,
        ulong[] tombstones)
    {
        ReplicaStageStatus staged = StageJson(
            replica,
            kind,
            json,
            sequence,
            baseline,
            fromRevision,
            toRevision,
            tombstones,
            out ReplicaStageHandle handle);
        if (staged != ReplicaStageStatus.Staged)
        {
            return false;
        }

        return replica.ObserveRuntimeOutcome(
            handle,
            ReplicaRuntimeOutcome.CommittedOutcome(),
            out _) == ReplicaOutcomeStatus.Observed;
    }

    private static string Block(string mappingId, string payload, string sha)
    {
        return "{\"mappingId\":\"" + mappingId + "\",\"payload\":\"" + payload + "\",\"payloadSha256\":\"" + sha + "\"}";
    }

    private static void WriteU64(byte[] dest, ref int offset, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(dest.AsSpan(offset, 8), value);
        offset += 8;
    }
}
