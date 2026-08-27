using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Fault;

public sealed class TransportFaultDecoratorTests
{
    [Fact]
    public void DropDuplicateDelayDisconnect_AreDeterministic()
    {
        var a = new FaultDecoratingTransport(new SeededFaultPolicy());
        var b = new FaultDecoratingTransport(new SeededFaultPolicy());
        var first = new[] { a.Next(3), a.Next(3), a.Next(3), a.Next(3) };
        var second = new[] { b.Next(3), b.Next(3), b.Next(3), b.Next(3) };
        Assert.Equal(first, second);
    }
}
