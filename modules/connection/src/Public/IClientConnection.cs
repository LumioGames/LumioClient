using System;

namespace Lumio.Client.Connection
{
    public interface IClientConnection
    {
        ConnectionGeneration Generation { get; }

        ConnectionCommandResult Start();

        ConnectionSendResult TrySend(in EncodedFrame frame);

        int DrainEvents(Span<ConnectionEvent> destination);

        ConnectionCommandResult RequestClose(ConnectionCloseReason reason);

        ClientConnectionSnapshot GetSnapshot();
    }
}
