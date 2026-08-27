using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;

namespace Lumio.Client.Session
{
    public readonly struct ClientSessionDependencies
    {
        public ClientSessionDependencies(
            IClientConnectionFactory connections,
            IClientHandshakeFactory handshakes,
            IPlatformCapabilityProvider capabilities,
            IInputSampleIngress input,
            IVerifiedSessionArtifactSource artifacts,
            IClientEventWriter events,
            IClientRuntimePort runtime)
        {
            Connections = connections;
            Handshakes = handshakes;
            Capabilities = capabilities;
            Input = input;
            Artifacts = artifacts;
            Events = events;
            Runtime = runtime;
        }

        public IClientConnectionFactory Connections { get; }

        public IClientHandshakeFactory Handshakes { get; }

        public IPlatformCapabilityProvider Capabilities { get; }

        public IInputSampleIngress Input { get; }

        public IVerifiedSessionArtifactSource Artifacts { get; }

        public IClientEventWriter Events { get; }

        public IClientRuntimePort Runtime { get; }
    }
}
