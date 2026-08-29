using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Fault;

public sealed class ConnectionQueueFullTests
{
    // 公共面的 IngressFull_NeverOverwritesValidatedFrame 已覆盖同一语义，本用例补的是
    // 队列本体这一层：容量满时 TryEnqueue 必须返回 false，且既有事件的顺序与内容不被动过。
    [Fact]
    public void FullEventQueue_RejectsWithoutSilentlyOverwriting()
    {
        var queue = new ConnectionEventQueue(2);
        var first = new ConnectionEvent(ConnectionEventKind.Started, new ConnectionGeneration(1), false);
        var second = new ConnectionEvent(ConnectionEventKind.FrameReceived, new ConnectionGeneration(1), false);
        var overflow = new ConnectionEvent(ConnectionEventKind.Closed, new ConnectionGeneration(1), true);

        Assert.True(queue.TryEnqueue(in first));
        Assert.True(queue.TryEnqueue(in second));
        Assert.False(queue.TryEnqueue(in overflow));
        Assert.Equal(2, queue.Count);

        var drained = new ConnectionEvent[4];
        Assert.Equal(2, queue.Drain(drained));
        Assert.Equal(ConnectionEventKind.Started, drained[0].Kind);
        Assert.Equal(ConnectionEventKind.FrameReceived, drained[1].Kind);
    }

    [Fact]
    public void IngressFull_NeverOverwritesValidatedFrame()
    {
        var factory = new ClientConnectionFactory();
        ClientConnectionCreateResult created = factory.Create(new ClientConnectionCreateRequest(1, 1), out IClientConnection connection);
        connection.Start();
        var buffer = new ConnectionEvent[8];
        connection.DrainEvents(buffer);
        Assert.True(created.Loopback.TryDeliverToClient(new EncodedFrame(new byte[] { 1, 2, 3 })));
        Assert.True(created.Loopback.TryDeliverToClient(new EncodedFrame(new byte[] { 9, 9, 9 })));
        int n = connection.DrainEvents(buffer);
        int frames = 0;
        byte first = 0;
        for (int i = 0; i < n; i++)
        {
            if (buffer[i].Kind == ConnectionEventKind.FrameReceived)
            {
                frames++;
                if (frames == 1)
                {
                    first = buffer[i].Frame.Bytes.Span[0];
                }
            }
        }

        Assert.Equal(1, frames);
        Assert.Equal(1, first);
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
        bool laterAccepted = true;
        for (byte i = 2; i <= 8; i++)
        {
            if (!connection.TrySend(new EncodedFrame(new byte[] { i })).Accepted)
            {
                laterAccepted = false;
            }
        }

        Assert.False(laterAccepted);
        Assert.True(created.Loopback.TryReceiveFromClient(out EncodedFrame first));
        Assert.Equal(1, first.Bytes.Span[0]);
    }
}
