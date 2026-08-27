using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Fault;

public sealed class ConnectionCloseRaceTests
{
    [Fact]
    public void CloseDisconnectSuccess_EmitsOneTerminal()
    {
        var factory = new ClientConnectionFactory();
        factory.Create(new ClientConnectionCreateRequest(1, 16), out var connection);
        connection.Start();
        Assert.True(connection.RequestClose(ConnectionCloseReason.OwnerRequest).Succeeded);
        Assert.False(connection.RequestClose(ConnectionCloseReason.Disconnect).Succeeded);
        var events = new ConnectionEvent[16];
        int n = connection.DrainEvents(events);
        int terminals = 0;
        for (int i = 0; i < n; i++)
        {
            if (events[i].Terminal)
            {
                terminals++;
            }
        }

        Assert.Equal(1, terminals);
        Assert.True(connection.GetSnapshot().Terminal);
    }
}

public sealed class LateGenerationTests
{
    [Fact]
    public void G1Callback_CannotReachG2()
    {
        var factory = new ClientConnectionFactory();
        factory.Create(new ClientConnectionCreateRequest(1, 8), out var g1);
        factory.Create(new ClientConnectionCreateRequest(2, 8), out var g2);
        g1.Start();
        g2.Start();
        var owner = Assert.IsType<OwnerConnection>(g2);
        Assert.False(owner.DeliverCallback(new ConnectionGeneration(1)));
        Assert.True(owner.DeliverCallback(new ConnectionGeneration(2)));
        g2.RequestClose(ConnectionCloseReason.OwnerRequest);
        Assert.False(owner.DeliverCallback(new ConnectionGeneration(2)));
    }
}
