using Lumio.Client.Connection;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class LocalEmbeddedProtocolFidelityTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public void PublicSendAndLoopback_UseOpaqueBytesNotEnvelope()
    {
        var factory = new ClientConnectionFactory();
        ClientConnectionCreateResult created = factory.Create(new ClientConnectionCreateRequest(1, 8), out IClientConnection connection);
        Assert.True(created.Succeeded);
        connection.Start();
        byte[] payload = { 7, 8, 9 };
        Assert.True(connection.TrySend(new EncodedFrame(payload)).Accepted);
        Assert.True(created.Loopback.TryDeliverToClient(new EncodedFrame(payload)));
        var buffer = new ConnectionEvent[8];
        int n = connection.DrainEvents(buffer);
        Assert.True(n >= 2);
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
        Assert.True(received.Value.Frame.Bytes.Span.SequenceEqual(payload));
        foreach (var type in typeof(IClientConnection).Assembly.GetExportedTypes())
        {
            Assert.NotEqual("Envelope", type.Name);
        }
    }
}
