namespace Lumio.Client.Handshake
{
    public interface IClientHandshakeFactory
    {
        IClientHandshake Create(IPlatformCapabilityProvider capabilities);
    }

    public sealed class ClientHandshakeFactory : IClientHandshakeFactory
    {
        public IClientHandshake Create(IPlatformCapabilityProvider capabilities)
        {
            return new HandshakeSession(capabilities);
        }
    }
}
