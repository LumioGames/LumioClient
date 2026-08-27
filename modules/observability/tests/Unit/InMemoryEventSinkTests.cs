using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Lumio.Client.Observability;

namespace Lumio.Client.Observability.Tests.Unit;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Foundation named tests.")]
public sealed class InMemoryEventSinkTests
{
    [Fact]
    public static async Task BatchOrder_IsStable()
    {
        var sink = new InMemoryClientEventSink(16);
        var first = new[]
        {
            Record(EventSchemaClass.Critical, 1),
            Record(EventSchemaClass.Durable, 2),
            Record(EventSchemaClass.Droppable, 3)
        };
        var second = new[]
        {
            Record(EventSchemaClass.Critical, 4)
        };

        var firstResult = await sink.WriteBatchAsync(first, CancellationToken.None);
        var secondResult = await sink.WriteBatchAsync(second, CancellationToken.None);

        Assert.True(firstResult.Succeeded);
        Assert.Equal(3, firstResult.WrittenCount);
        Assert.True(secondResult.Succeeded);
        Assert.Equal(1, secondResult.WrittenCount);

        var snapshot = sink.Capture();
        Assert.Equal(4, snapshot.Records.Length);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Payloads(snapshot));
    }

    [Fact]
    public static async Task SnapshotIsImmutableAndBounded()
    {
        var sink = new InMemoryClientEventSink(2);
        var overflow = new[]
        {
            Record(EventSchemaClass.Droppable, 10),
            Record(EventSchemaClass.Droppable, 11),
            Record(EventSchemaClass.Droppable, 12)
        };

        var result = await sink.WriteBatchAsync(overflow, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(2, result.WrittenCount);

        var snapshot = sink.Capture();
        Assert.Equal(2, snapshot.Capacity);
        Assert.Equal(2, snapshot.Records.Length);
        Assert.Equal(new byte[] { 10, 11 }, Payloads(snapshot));
        Assert.True(snapshot.DroppedCount >= 1);

        Assert.True(MemoryMarshal.TryGetArray(snapshot.Records, out ArraySegment<ClientEventRecord> segment));
        Assert.NotNull(segment.Array);
        segment.Array[segment.Offset] = Record(EventSchemaClass.Invalid, 255);
        if (MemoryMarshal.TryGetArray(snapshot.Records.Span[1].Payload, out ArraySegment<byte> payloadSegment)
            && payloadSegment.Array is not null
            && payloadSegment.Count > 0)
        {
            payloadSegment.Array[payloadSegment.Offset] = 99;
        }

        var afterMutation = sink.Capture();
        Assert.Equal(2, afterMutation.Records.Length);
        Assert.Equal(new byte[] { 10, 11 }, Payloads(afterMutation));
        Assert.Equal(snapshot.DroppedCount, afterMutation.DroppedCount);
        Assert.Equal(snapshot.Capacity, afterMutation.Capacity);
    }

    [Fact]
    public static async Task CloseIsIdempotent()
    {
        var sink = new InMemoryClientEventSink(4);
        var written = await sink.WriteBatchAsync(
            new[] { Record(EventSchemaClass.Durable, 7) },
            CancellationToken.None);
        Assert.True(written.Succeeded);

        sink.Close();
        sink.Close();

        var closed = sink.Capture();
        Assert.True(closed.Closed);
        Assert.Equal(new byte[] { 7 }, Payloads(closed));

        var afterClose = await sink.WriteBatchAsync(
            new[] { Record(EventSchemaClass.Critical, 8) },
            CancellationToken.None);
        Assert.False(afterClose.Succeeded);
        Assert.Equal(0, afterClose.WrittenCount);

        sink.Close();
        var stillClosed = sink.Capture();
        Assert.True(stillClosed.Closed);
        Assert.Equal(new byte[] { 7 }, Payloads(stillClosed));
    }

    private static ClientEventRecord Record(EventSchemaClass schemaClass, byte payload)
    {
        return new ClientEventRecord(schemaClass, new byte[] { payload });
    }

    private static byte[] Payloads(ClientEventMemorySnapshot snapshot)
    {
        var values = new byte[snapshot.Records.Length];
        for (var i = 0; i < snapshot.Records.Length; i++)
        {
            var payload = snapshot.Records.Span[i].Payload;
            Assert.False(payload.IsEmpty);
            values[i] = payload.Span[0];
        }

        return values;
    }
}
