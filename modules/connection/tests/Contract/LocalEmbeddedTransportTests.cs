using System.Reflection;
using Lumio.Client.Connection;

namespace Lumio.Client.Connection.Tests.Contract;

public sealed class LocalEmbeddedTransportTests
{
    [Fact]
    public void TypedShortcut_IsImpossibleAndCodecRuns()
    {
        var transport = new LocalEmbeddedTransport(8);
        byte[] payload = { 1, 2, 3 };
        Assert.True(transport.TrySendClient(new EncodedFrame(payload)));
        Assert.True(transport.TryReceiveServer(out var frame));
        Assert.True(frame.Bytes.Span.SequenceEqual(payload));
        foreach (var method in typeof(IClientConnection).GetMethods())
        {
            foreach (var parameter in method.GetParameters())
            {
                Assert.False(string.Equals(parameter.ParameterType.Name, "Envelope", StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Send_DoesNotSynchronouslyReenterReceiver()
    {
        var transport = new LocalEmbeddedTransport(8);
        int receives = 0;
        Assert.True(transport.TrySendClient(new EncodedFrame(new byte[] { 9 })));
        Assert.Equal(0, receives);
        Assert.True(transport.TryReceiveServer(out _));
    }
}

public sealed class GeneratedEnvelopeCodecAdapterFixtureTests
{
    [Fact]
    public void ValidInvalidVectors()
    {
        var codec = new GeneratedEnvelopeCodecAdapter();
        Assert.False(codec.TryEncode(default, out _));
        Assert.True(codec.TryEncode(new EncodedFrame(new byte[] { 4 }), out var bytes));
        Assert.True(codec.TryDecode(bytes, out var frame));
        Assert.Equal(4, frame.Bytes.Span[0]);
        Assert.False(codec.TryDecode(ReadOnlyMemory<byte>.Empty, out _));
    }
}

public sealed class ConnectionReplayWindowTests
{
    [Fact]
    public void DuplicateAndOutOfOrder_FollowContract()
    {
        var window = new ReplayWindow();
        Assert.True(window.Accept(1));
        Assert.False(window.Accept(1));
        Assert.True(window.Accept(3));
        Assert.True(window.Accept(2));
    }
}
