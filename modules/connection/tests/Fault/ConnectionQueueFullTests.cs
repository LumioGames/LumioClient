using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Fault;

public sealed class ConnectionQueueFullTests
{
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
