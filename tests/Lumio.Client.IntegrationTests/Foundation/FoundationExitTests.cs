using Lumio.Client.Bot;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Session;

namespace Lumio.Client.IntegrationTests.Foundation;

public sealed class FoundationExitTests
{
    [Fact]
    [Trait("Category", "Foundation")]
    public async Task HeadlessBot_ConnectHandshakeTickClose()
    {
        var ingress = new InputSampleIngress(8);
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        new ClientEventPipelineFactory().Create(in options, new InMemoryClientEventSink(8), out var writer);
        var deps = new ClientSessionDependencies(
            new ClientConnectionFactory(),
            new ClientHandshakeFactory(),
            new Cap(),
            ingress,
            IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource(),
            writer,
            new Runtime());
        new ClientSessionFactory().Create(in deps, out var session);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress);
        int code = await host.RunAsync(new BotRunRequest(2, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(ClientSessionState.Closed, session.GetSnapshot().State);
    }

    [Fact]
    [Trait("Category", "Foundation")]
    public void LocalEmbeddedProtocolFidelity_PublicSendUsesBytesNotEnvelope()
    {
        var factory = new ClientConnectionFactory();
        factory.Create(new ClientConnectionCreateRequest(1, 8), out var connection);
        connection.Start();
        Assert.True(connection.TrySend(new EncodedFrame(new byte[] { 7, 8, 9 })).Accepted);
        foreach (var type in typeof(IClientConnection).Assembly.GetExportedTypes())
        {
            Assert.NotEqual("Envelope", type.Name);
        }
    }

    private sealed class Cap : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class Runtime : IClientRuntimePort
    {
        public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(in RuntimeTransactionRequest request, CancellationToken cancellationToken)
        {
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(true));
        }
    }
}
