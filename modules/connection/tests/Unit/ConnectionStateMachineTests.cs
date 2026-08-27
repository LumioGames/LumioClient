using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Unit;

public sealed class ConnectionStateMachineTests
{
    [Fact]
    public void GenerationIsImmutableAfterCreate()
    {
        var factory = new ClientConnectionFactory();
        var request = new ClientConnectionCreateRequest(7, 8);
        factory.Create(in request, out var connection);
        Assert.Equal(new ConnectionGeneration(7), connection.Generation);
        connection.Start();
        connection.RequestClose(ConnectionCloseReason.OwnerRequest);
        Assert.Equal(new ConnectionGeneration(7), connection.Generation);
        Assert.Equal(new ConnectionGeneration(7), connection.GetSnapshot().Generation);
    }
}
