using Lumio.Client.Bot;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Prediction;
using Lumio.Client.Replica;
using Lumio.Client.Session;

namespace Lumio.Client.Bot.Host;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        _ = args;
        var ingress = new InputSampleIngress(16);
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var deps = new ClientSessionDependencies(
            new ClientConnectionFactory(),
            new ClientHandshakeFactory(),
            new HostCapability(),
            new UnpublishedHandshakeFrameClassifier(),
            ingress,
            new InputCommandSource(ingress, new HostInputMapper()),
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            new HostRuntime(),
            new ClientReplicaFactory(),
            new ClientPredictionFactory(),
            new ImmediateGameplayScopeActivator(),
            new NullPresentationSink(),
            new UnpublishedSessionMessageKindMap());
        new ClientSessionFactory().Create(in deps, out IClientSession session);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress);
        return await host.RunAsync(new BotRunRequest(2, 0), CancellationToken.None);
    }

    private sealed class HostCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class HostInputMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            _ = context;
            candidate = new GameplayCommandCandidate(sample.Sequence, new byte[] { 0x42 });
            return true;
        }
    }

    private sealed class HostRuntime : IClientRuntimePort
    {
        public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }

        public ValueTask<RuntimeTransactionOutcome> ApplyLocalPrediction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            _ = request;
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }
    }
}
