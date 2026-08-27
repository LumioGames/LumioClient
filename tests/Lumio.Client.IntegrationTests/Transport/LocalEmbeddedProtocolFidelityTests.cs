using Lumio.Client.Connection;

namespace Lumio.Client.IntegrationTests.Transport;

public sealed class LocalEmbeddedProtocolFidelityTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public void EveryFrameRunsProductionEncodeAndDecode()
    {
        var factory = new ClientConnectionFactory();
        ClientConnectionCreateResult created = factory.Create(new ClientConnectionCreateRequest(1, 8), out IClientConnection connection);
        var trace = new ProtocolTraceRecorder(created.Loopback);
        connection.Start();

        byte[] outbound = { 7, 8, 9 };
        byte[] inbound = { 0x10, 0x32, 0x54 };
        int encodeBeforeSend = trace.EncodeCalls;
        int decodeBeforeSend = trace.DecodeCalls;
        Assert.True(connection.TrySend(new EncodedFrame(outbound)).Accepted);
        Assert.True(trace.EncodeCalls > encodeBeforeSend);
        Assert.True(created.Loopback.TryReceiveFromClient(out EncodedFrame sent));
        Assert.True(trace.DecodeCalls > decodeBeforeSend);
        Assert.True(sent.Bytes.Span.SequenceEqual(outbound));

        int encodeBeforeInject = trace.EncodeCalls;
        int decodeBeforeDrain = trace.DecodeCalls;
        Assert.True(created.Loopback.TryDeliverToClient(new EncodedFrame(inbound)));
        Assert.True(trace.EncodeCalls > encodeBeforeInject);
        var buffer = new ConnectionEvent[8];
        int n = connection.DrainEvents(buffer);
        ConnectionEvent? received = null;
        for (int i = 0; i < n; i++)
        {
            if (buffer[i].Kind == ConnectionEventKind.FrameReceived)
            {
                received = buffer[i];
                break;
            }
        }

        Assert.True(received.HasValue);
        Assert.True(received.Value.Frame.Bytes.Span.SequenceEqual(inbound));
        Assert.True(trace.DecodeCalls > decodeBeforeDrain);
        Assert.True(trace.EncodeCalls >= 2);
        Assert.True(trace.DecodeCalls >= 2);
        foreach (var type in typeof(IClientConnection).Assembly.GetExportedTypes())
        {
            Assert.NotEqual("Envelope", type.Name);
        }
    }
}

public sealed class LocalEmbeddedIsolationTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public void ClientAndServerDoNotShareWorldOrMutableBuffer()
    {
        var factory = new ClientConnectionFactory();
        ClientConnectionCreateResult created = factory.Create(new ClientConnectionCreateRequest(1, 8), out IClientConnection connection);
        connection.Start();
        byte[] payload = { 1, 2, 3 };
        Assert.True(connection.TrySend(new EncodedFrame(payload)).Accepted);
        payload[0] = 9;
        Assert.True(created.Loopback.TryReceiveFromClient(out EncodedFrame sent));
        Assert.Equal(1, sent.Bytes.Span[0]);

        byte[] inbound = { 4, 5, 6 };
        Assert.True(created.Loopback.TryDeliverToClient(new EncodedFrame(inbound)));
        inbound[0] = 8;
        var buffer = new ConnectionEvent[8];
        connection.DrainEvents(buffer);
        ConnectionEvent? received = null;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].Kind == ConnectionEventKind.FrameReceived)
            {
                received = buffer[i];
                break;
            }
        }

        Assert.True(received.HasValue);
        Assert.Equal(4, received.Value.Frame.Bytes.Span[0]);
    }

    [Fact]
    [Trait("Category", "Foundation")]
    public void SendNeverSynchronouslyReentersReceiver()
    {
        var factory = new ClientConnectionFactory();
        ClientConnectionCreateResult created = factory.Create(new ClientConnectionCreateRequest(1, 8), out IClientConnection connection);
        connection.Start();
        Assert.True(created.Loopback.TryDeliverToClient(new EncodedFrame(new byte[] { 9 })));
        Assert.True(connection.TrySend(new EncodedFrame(new byte[] { 3 })).Accepted);
        var buffer = new ConnectionEvent[8];
        int before = connection.GetSnapshot().EventCount;
        _ = before;
        int n = connection.DrainEvents(buffer);
        int frames = 0;
        for (int i = 0; i < n; i++)
        {
            if (buffer[i].Kind == ConnectionEventKind.FrameReceived)
            {
                frames++;
            }
        }

        Assert.Equal(1, frames);
    }
}
