using Lumio.Client.Connection;

namespace Lumio.Client.IntegrationTests.Transport;

public sealed class ProtocolTraceRecorder
{
    private readonly LocalEmbeddedLoopback _loopback;

    public ProtocolTraceRecorder(LocalEmbeddedLoopback loopback)
    {
        _loopback = loopback ?? throw new ArgumentNullException(nameof(loopback));
    }

    public int EncodeCalls
    {
        get { return _loopback.EncodeCalls; }
    }

    public int DecodeCalls
    {
        get { return _loopback.DecodeCalls; }
    }
}
