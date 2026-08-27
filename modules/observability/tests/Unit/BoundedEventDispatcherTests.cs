using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Lumio.Client.Observability;

namespace Lumio.Client.Observability.Tests.Unit;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Foundation named tests.")]
public sealed class BoundedEventDispatcherTests
{
    [Fact]
    public static async Task CriticalQueueFull_ReturnsWithoutBlocking()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var sink = new BlockingSink();
        var factory = new DefaultClientEventPipelineFactory();
        var options = new ClientEventPipelineOptions(1, 1, TimeSpan.FromSeconds(2));
        var created = factory.Create(in options, sink, out var writer);
        Assert.True(created.Succeeded);
        var bounded = Assert.IsType<BoundedEventWriter>(writer);
        var record = new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 1 });

        try
        {
            var accepted = 0;
            var last = new ClientEventWriteResult(ClientEventWriteOutcome.Rejected);
            var clock = Stopwatch.StartNew();
            for (var i = 0; i < 8; i++)
            {
                last = await TryWriteWithoutBlocking(writer, record, cancellationToken);
                if (last.Succeeded)
                {
                    accepted++;
                    continue;
                }

                break;
            }

            clock.Stop();
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), clock.Elapsed.ToString());
            Assert.Equal(ClientEventWriteOutcome.QueueFull, last.Outcome);
            Assert.InRange(accepted, 1, 3);
            var snapshot = writer.GetSnapshot();
            Assert.Equal((ulong)accepted, snapshot.LastProducerSequence);
            Assert.False(snapshot.Closed);
        }
        finally
        {
            sink.Release.Set();
            bounded.Close();
        }
    }

    [Fact]
    public static async Task DroppableQueueFull_DropsOnlySchemaAllowedClass()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var sink = new BlockingSink();
        var factory = new DefaultClientEventPipelineFactory();
        var options = new ClientEventPipelineOptions(1, 1, TimeSpan.FromSeconds(2));
        var created = factory.Create(in options, sink, out var writer);
        Assert.True(created.Succeeded);
        var bounded = Assert.IsType<BoundedEventWriter>(writer);
        var critical = new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 2 });

        try
        {
            await FillUntilQueueFull(writer, critical, cancellationToken);

            var before = writer.GetSnapshot();
            var droppable = new ClientEventRecord(EventSchemaClass.Droppable, new byte[] { 3 });
            var durable = new ClientEventRecord(EventSchemaClass.Durable, new byte[] { 4 });
            var anotherCritical = new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 5 });
            var invalid = new ClientEventRecord(EventSchemaClass.Invalid, ReadOnlyMemory<byte>.Empty);

            var dropped = writer.TryWrite(droppable);
            var durableFull = writer.TryWrite(durable);
            var criticalFull = writer.TryWrite(anotherCritical);
            var rejected = writer.TryWrite(invalid);
            var after = writer.GetSnapshot();

            Assert.Equal(ClientEventWriteOutcome.Dropped, dropped.Outcome);
            Assert.Equal(ClientEventWriteOutcome.QueueFull, durableFull.Outcome);
            Assert.Equal(ClientEventWriteOutcome.QueueFull, criticalFull.Outcome);
            Assert.Equal(ClientEventWriteOutcome.Rejected, rejected.Outcome);
            Assert.Equal(before.LastProducerSequence, after.LastProducerSequence);
            Assert.Equal(before.DropCount + 1, after.DropCount);
        }
        finally
        {
            sink.Release.Set();
            bounded.Close();
        }
    }

    private static async Task FillUntilQueueFull(
        IClientEventWriter writer,
        ClientEventRecord record,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 8; i++)
        {
            var result = await TryWriteWithoutBlocking(writer, record, cancellationToken);
            if (result.Outcome == ClientEventWriteOutcome.QueueFull)
            {
                return;
            }

            Assert.True(result.Succeeded);
        }

        Assert.Fail("Capacity-1 pipeline never returned QueueFull.");
    }

    private static async Task<ClientEventWriteResult> TryWriteWithoutBlocking(
        IClientEventWriter writer,
        ClientEventRecord record,
        CancellationToken cancellationToken)
    {
        var write = Task.Run(() => writer.TryWrite(record), cancellationToken);
        var timeout = Task.Delay(250, cancellationToken);
        var completed = await Task.WhenAny(write, timeout);
        Assert.Same(write, completed);
        return await write;
    }

    private sealed class BlockingSink : IClientEventSink
    {
        public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);

        public ValueTask<ClientEventSinkResult> WriteBatchAsync(
            ReadOnlyMemory<ClientEventRecord> records,
            CancellationToken cancellationToken)
        {
            Release.Wait(cancellationToken);
            return new ValueTask<ClientEventSinkResult>(new ClientEventSinkResult(true, records.Length, false));
        }
    }
}
