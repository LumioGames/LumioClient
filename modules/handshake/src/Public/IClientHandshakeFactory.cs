namespace Lumio.Client.Handshake
{
    public interface IClientHandshakeFactory
    {
        IClientHandshake Create(IPlatformCapabilityProvider capabilities);

        IClientHandshake Create(IPlatformCapabilityProvider capabilities, IHandshakeFrameClassifier frames);
    }

    public sealed class ClientHandshakeFactory : IClientHandshakeFactory
    {
        public IClientHandshake Create(IPlatformCapabilityProvider capabilities)
        {
            return Create(capabilities, new UnpublishedHandshakeFrameClassifier());
        }

        public IClientHandshake Create(IPlatformCapabilityProvider capabilities, IHandshakeFrameClassifier frames)
        {
            return new HandshakeSession(capabilities, frames);
        }
    }
}
