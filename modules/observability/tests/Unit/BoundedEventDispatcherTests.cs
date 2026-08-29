using System.Diagnostics.CodeAnalysis;
using Lumio.Client.Observability;

namespace Lumio.Client.Observability.Tests.Unit;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Foundation named tests.")]
public sealed class BoundedEventDispatcherTests
{
    [Fact]
    public static void CriticalQueueFull_ReturnsWithoutBlocking()
    {
        using var pipeline = ParkedFullPipeline.Create(TestContext.Current.CancellationToken);
        var writer = pipeline.Writer;
        var before = writer.GetSnapshot();

        var result = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 1 }));
        var after = writer.GetSnapshot();

        // The only reader is parked inside the sink and the single slot is occupied, so a writer that
        // waited for capacity could not return at all: reaching this assertion is the non-blocking proof.
        Assert.Equal(ClientEventWriteOutcome.QueueFull, result.Outcome);
        Assert.Equal(before.LastProducerSequence, after.LastProducerSequence);
        Assert.Equal(before.DropCount, after.DropCount);
        Assert.False(after.Closed);
    }

    [Fact]
    public static void DroppableQueueFull_DropsOnlySchemaAllowedClass()
    {
        using var pipeline = ParkedFullPipeline.Create(TestContext.Current.CancellationToken);
        var writer = pipeline.Writer;
        var before = writer.GetSnapshot();

        var dropped = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Droppable, new byte[] { 3 }));
        var durableFull = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Durable, new byte[] { 4 }));
        var criticalFull = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 5 }));
        var rejected = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Invalid, ReadOnlyMemory<byte>.Empty));
        var after = writer.GetSnapshot();

        Assert.Equal(ClientEventWriteOutcome.Dropped, dropped.Outcome);
        Assert.Equal(ClientEventWriteOutcome.QueueFull, durableFull.Outcome);
        Assert.Equal(ClientEventWriteOutcome.QueueFull, criticalFull.Outcome);
        Assert.Equal(ClientEventWriteOutcome.Rejected, rejected.Outcome);
        Assert.Equal(before.LastProducerSequence, after.LastProducerSequence);
        Assert.Equal(before.DropCount + 1, after.DropCount);
    }

    /// <summary>
    /// A capacity-1 pipeline driven into a <em>stable</em> QueueFull state.
    /// </summary>
    /// <remarks>
    /// QueueFull is not observable by simply writing until the outcome appears: the background dispatcher
    /// may dequeue right after that observation and silently free the slot, so the next write is accepted
    /// instead of dropped. Here the dispatcher is first made to take the one record it can take and park
    /// inside the sink; only then is the single slot filled. With the sole reader parked, fullness cannot
    /// change underneath the assertions. Every wait below is a wait on an observed state transition, never
    /// on elapsed time — the tests must fail on QueueFull semantics, not on how fast the host schedules.
    /// </remarks>
    private sealed class ParkedFullPipeline : IDisposable
    {
        private readonly ParkingSink _sink;
        private readonly BoundedEventWriter _writer;

        private ParkedFullPipeline(ParkingSink sink, BoundedEventWriter writer)
        {
            _sink = sink;
            _writer = writer;
        }

        public IClientEventWriter Writer => _writer;

        public static ParkedFullPipeline Create(CancellationToken cancellationToken)
        {
            var sink = new ParkingSink();
            var factory = new DefaultClientEventPipelineFactory();

            // The sink timeout only bounds how long the dispatcher may stay parked; it is never asserted on,
            // and if it did elapse the queue would stay full and every assertion below would still hold.
            var options = new ClientEventPipelineOptions(1, 1, TimeSpan.FromMinutes(10));
            var created = factory.Create(in options, sink, out var writer);
            Assert.True(created.Succeeded);
            var bounded = Assert.IsType<BoundedEventWriter>(writer);
            var pipeline = new ParkedFullPipeline(sink, bounded);

            try
            {
                var first = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 1 }));
                Assert.Equal(ClientEventWriteOutcome.Accepted, first.Outcome);
                sink.WaitUntilParked(cancellationToken);

                var second = writer.TryWrite(new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 2 }));
                Assert.Equal(ClientEventWriteOutcome.Accepted, second.Outcome);

                var snapshot = writer.GetSnapshot();
                Assert.Equal(2UL, snapshot.LastProducerSequence);
                Assert.Equal(2, snapshot.QueueDepth);
                Assert.Equal(0, snapshot.DropCount);
                Assert.False(snapshot.Closed);
                return pipeline;
            }
            catch
            {
                pipeline.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // Release before closing: Close waits for the dispatcher, which is parked inside the sink.
            // The sink outlives Close so the dispatcher's final drain never touches a disposed handle.
            _sink.Release();
            _writer.Close();
            _sink.Dispose();
        }
    }

    private sealed class ParkingSink : IClientEventSink, IDisposable
    {
        private readonly ManualResetEventSlim _parked = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _release = new ManualResetEventSlim(false);

        public void WaitUntilParked(CancellationToken cancellationToken)
        {
            _parked.Wait(cancellationToken);
        }

        public void Release()
        {
            _release.Set();
        }

        public void Dispose()
        {
            _parked.Dispose();
            _release.Dispose();
        }

        public ValueTask<ClientEventSinkResult> WriteBatchAsync(
            ReadOnlyMemory<ClientEventRecord> records,
            CancellationToken cancellationToken)
        {
            _parked.Set();
            _release.Wait(cancellationToken);
            return new ValueTask<ClientEventSinkResult>(new ClientEventSinkResult(true, records.Length, false));
        }
    }
}
