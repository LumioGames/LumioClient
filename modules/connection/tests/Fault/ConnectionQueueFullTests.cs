using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Fault;

public sealed class ConnectionQueueFullTests
{
    [Fact]
    public void IngressFull_NeverOverwritesValidatedFrame()
    {
        var queue = new ConnectionEventQueue(1);
        var first = new ConnectionEvent(ConnectionEventKind.Started, new ConnectionGeneration(1), false);
        var second = new ConnectionEvent(ConnectionEventKind.FrameReceived, new ConnectionGeneration(1), false);
        Assert.True(queue.TryEnqueue(in first));
        Assert.False(queue.TryEnqueue(in second));
        var buffer = new ConnectionEvent[2];
        int n = queue.Drain(buffer);
        Assert.Equal(1, n);
        Assert.Equal(ConnectionEventKind.Started, buffer[0].Kind);
    }

    [Fact]
    public void EgressFull_ReturnsBeforeBlocking()
    {
        var queue = new ConnectionSendQueue(1);
        Assert.True(queue.TryEnqueue(new EncodedFrame(new byte[] { 1 })));
        Assert.False(queue.TryEnqueue(new EncodedFrame(new byte[] { 2 })));
    }

    [Fact]
    public void FactoryPath_EgressFull_DoesNotOverwriteFirstFrame()
    {
        var factory = new ClientConnectionFactory();
        ClientConnectionCreateResult created = factory.Create(new ClientConnectionCreateRequest(1, 1), out IClientConnection connection);
        connection.Start();
        Assert.True(connection.TrySend(new EncodedFrame(new byte[] { 1 })).Accepted);
        connection.TrySend(new EncodedFrame(new byte[] { 2 }));
        connection.TrySend(new EncodedFrame(new byte[] { 3 }));
        Assert.True(created.Loopback.TryReceiveFromClient(out EncodedFrame first));
        Assert.Equal(1, first.Bytes.Span[0]);
    }
}
