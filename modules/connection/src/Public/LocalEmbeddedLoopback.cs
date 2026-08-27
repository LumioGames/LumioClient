namespace Lumio.Client.Connection
{
    public sealed class LocalEmbeddedLoopback
    {
        private readonly OwnerConnection _client;

        internal LocalEmbeddedLoopback(OwnerConnection client)
        {
            _client = client;
        }

        public int EncodeCalls
        {
            get { return _client.Transport.EncodeCalls; }
        }

        public int DecodeCalls
        {
            get { return _client.Transport.DecodeCalls; }
        }

        public bool TryDeliverToClient(in EncodedFrame frame)
        {
            return _client.Transport.TrySendServer(in frame);
        }

        public bool TryReceiveFromClient(out EncodedFrame frame)
        {
            return _client.Transport.TryReceiveServer(out frame);
        }

        public bool TryDisconnectClient()
        {
            return _client.DeliverDisconnect();
        }
    }
}
