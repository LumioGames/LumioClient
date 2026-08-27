using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    public readonly struct ClientSessionDependencies
    {
        public ClientSessionDependencies(
            IClientConnectionFactory connections,
            IClientHandshakeFactory handshakes,
            IPlatformCapabilityProvider capabilities,
            IHandshakeFrameClassifier handshakeFrames,
            IInputSampleIngress input,
            IInputCommandSource commands,
            IVerifiedSessionArtifactSource artifacts,
            IClientEventWriter events,
            IClientRuntimePort runtime,
            IClientReplicaFactory replicas,
            IClientPredictionFactory predictions,
            IClientGameplayScopeActivator scope,
            IClientPresentationSink presentation,
            ISessionMessageKindMap messages)
        {
            Connections = connections;
            Handshakes = handshakes;
            Capabilities = capabilities;
            HandshakeFrames = handshakeFrames;
            Input = input;
            Commands = commands;
            Artifacts = artifacts;
            Events = events;
            Runtime = runtime;
            Replicas = replicas;
            Predictions = predictions;
            Scope = scope;
            Presentation = presentation;
            Messages = messages;
        }

        public IClientConnectionFactory Connections { get; }

        public IClientHandshakeFactory Handshakes { get; }

        public IPlatformCapabilityProvider Capabilities { get; }

        public IHandshakeFrameClassifier HandshakeFrames { get; }

        public IInputSampleIngress Input { get; }

        public IInputCommandSource Commands { get; }

        public IVerifiedSessionArtifactSource Artifacts { get; }

        public IClientEventWriter Events { get; }

        public IClientRuntimePort Runtime { get; }

        public IClientReplicaFactory Replicas { get; }

        public IClientPredictionFactory Predictions { get; }

        public IClientGameplayScopeActivator Scope { get; }

        public IClientPresentationSink Presentation { get; }

        public ISessionMessageKindMap Messages { get; }
    }
}
