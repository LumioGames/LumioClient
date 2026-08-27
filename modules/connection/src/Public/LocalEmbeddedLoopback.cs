namespace Lumio.Client.Connection
{
    public sealed class LocalEmbeddedLoopback
    {
        private readonly OwnerConnection _client;

        internal LocalEmbeddedLoopback(OwnerConnection client)
        {
            _client = client;
        }

        public bool TryDeliverToClient(in EncodedFrame frame)
        {
            return _client.DeliverInbound(in frame);
        }

        public bool TryDisconnectClient()
        {
            return _client.DeliverDisconnect();
        }
    }
}
