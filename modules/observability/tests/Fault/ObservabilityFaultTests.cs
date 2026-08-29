using System.Diagnostics.CodeAnalysis;
using Lumio.Client.Observability;

namespace Lumio.Client.Observability.Tests.Fault;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Foundation named tests.")]
public sealed class ObservabilityFaultTests
{
    [Fact]
    public static async Task SinkThrows_BatchRetainedAndExceptionContained()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var sink = new ThrowingSink();
        var factory = new DefaultClientEventPipelineFactory();
        var options = new ClientEventPipelineOptions(4, 1, TimeSpan.FromMilliseconds(200));
        var created = factory.Create(in options, sink, out var writer);
        Assert.True(created.Succeeded);
        var bounded = Assert.IsType<BoundedEventWriter>(writer);
        var record = new ClientEventRecord(EventSchemaClass.Critical, new byte[] { 9 });

        try
        {
            Exception? thrown = null;
            try
            {
                var accepted = writer.TryWrite(record);
                Assert.True(accepted.Succeeded);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.Null(thrown);
            await WaitForAsync(() => writer.GetSnapshot().SinkFaulted, cancellationToken);

            var snapshot = writer.GetSnapshot();
            Assert.True(snapshot.SinkFaulted);
            Assert.True(snapshot.QueueDepth >= 1);
            Assert.False(bounded.LastFailureEvidence.IsEmpty);

            var second = writer.TryWrite(record);
            Assert.True(
                second.Outcome == ClientEventWriteOutcome.Accepted
                || second.Outcome == ClientEventWriteOutcome.QueueFull);
            Assert.True(writer.GetSnapshot().SinkFaulted);
        }
        finally
        {
            bounded.Close();
        }

        Assert.True(writer.GetSnapshot().SinkFaulted);
        Assert.True(writer.GetSnapshot().QueueDepth >= 1);
    }

    [Fact]
    public static async Task CloseWriteRace_NoSilentLoss()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var sink = new InMemoryClientEventSink(512);
            var factory = new DefaultClientEventPipelineFactory();
            var options = new ClientEventPipelineOptions(256, 8, TimeSpan.FromSeconds(2));
            var created = factory.Create(in options, sink, out var writer);
            Assert.True(created.Succeeded);
            var bounded = Assert.IsType<BoundedEventWriter>(writer);
            const int writerCount = 8;
            const int writesPer = 32;
            var accepted = new int[writerCount];
            var barrier = new Barrier(writerCount + 1);
            var tasks = new Task[writerCount];

            for (var w = 0; w < writerCount; w++)
            {
                var id = w;
                tasks[w] = Task.Run(() =>
                {
                    barrier.SignalAndWait(cancellationToken);
                    var count = 0;
                    for (var i = 0; i < writesPer; i++)
                    {
                        var record = new ClientEventRecord(EventSchemaClass.Critical, new byte[] { (byte)i });
                        var result = writer.TryWrite(record);
                        if (result.Succeeded)
                        {
                            count++;
                            continue;
                        }

                        Assert.True(
                            result.Outcome == ClientEventWriteOutcome.Rejected
                            || result.Outcome == ClientEventWriteOutcome.QueueFull,
                            result.Outcome.ToString());
                    }

                    accepted[id] = count;
                }, cancellationToken);
            }

            var closer = Task.Run(() =>
            {
                barrier.SignalAndWait(cancellationToken);
                Thread.SpinWait(64);
                bounded.Close();
                bounded.Close();
            }, cancellationToken);

            await Task.WhenAll(tasks);
            await closer;
            bounded.Close();

            var totalAccepted = 0;
            for (var i = 0; i < accepted.Length; i++)
            {
                totalAccepted += accepted[i];
            }

            var pipeline = writer.GetSnapshot();
            Assert.True(pipeline.Closed);
            var memory = sink.Capture();
            Assert.Equal(totalAccepted, memory.Records.Length + pipeline.QueueDepth);
            Assert.Equal((ulong)totalAccepted, pipeline.LastProducerSequence);

            ulong? previous = null;
            for (var i = 0; i < memory.Records.Length; i++)
            {
                Assert.True(memory.Records.Span[i].ProducerSequence.HasValue);
                var sequence = memory.Records.Span[i].ProducerSequence!.Value;
                if (previous.HasValue)
                {
                    Assert.True(sequence > previous.Value);
                }

                previous = sequence;
            }
        }
    }

    // Waits for the observed state, never for elapsed time: a fault that never arrives must surface as a
    // hung test cancelled by the runner, not as a pass/fail decided by how fast the host schedules.
    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            // Pacing only — the loop is bounded by cancellation, not by a deadline.
            await Task.Delay(1, cancellationToken);
        }
    }

    private sealed class ThrowingSink : IClientEventSink
    {
        public ValueTask<ClientEventSinkResult> WriteBatchAsync(
            ReadOnlyMemory<ClientEventRecord> records,
            CancellationToken cancellationToken)
        {
            _ = records;
            _ = cancellationToken;
            throw new InvalidOperationException("sink exploded");
        }
    }
}
