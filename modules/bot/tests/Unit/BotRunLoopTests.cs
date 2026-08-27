using Lumio.Client.Bot;
using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Session;

namespace Lumio.Client.Bot.Tests.Unit;

public sealed class BotRunLoopTests
{
    [Fact]
    public async Task BotRunLoop_CompletesWithExitCode()
    {
        var ingress = new InputSampleIngress(8);
        var session = CreateSession(ingress);
        var host = new HeadlessBotHost(session, new DeterministicBotDriver(), ingress);
        int code = await host.RunAsync(new BotRunRequest(3, 0), CancellationToken.None);
        Assert.Equal(0, code);
        Assert.Equal(ClientSessionState.Closed, session.GetSnapshot().State);
    }

    [Fact]
    public async Task BotCancellation_Stops()
    {
        var ingress = new InputSampleIngress(8);
        var host = new HeadlessBotHost(CreateSession(ingress), new DeterministicBotDriver(), ingress);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => host.RunAsync(new BotRunRequest(10, 0), cts.Token));
    }

    private static IClientSession CreateSession(IInputSampleIngress ingress)
    {
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
        return session;
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
