using Lumio.Client.Connection;
using Lumio.Client.Handshake;
using Lumio.Client.Input;
using Lumio.Client.Observability;
using Lumio.Client.Persistence;
using Lumio.Client.Session;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionPublicApiTests
{
    [Fact]
    public void RuntimeHandleLedger_AndFirstConnectHappyPath()
    {
        var session = CreateSession(committed: true);
        Assert.True(session.RequestConnect(new SessionConnectRequest(1), CancellationToken.None).Succeeded);
        SessionTickResult tick = session.Tick(new ClientOwnerTick(1));
        Assert.Equal(ClientSessionState.Active, tick.State);
        ClientSessionSnapshot snapshot = session.GetSnapshot();
        Assert.True(snapshot.RuntimeCommitted);
        Assert.True(snapshot.LedgerCount > 0);
        session.RequestClose(new SessionCloseRequest(false));
        Assert.Equal(ClientSessionState.Closed, session.GetSnapshot().State);
        Assert.Equal(0, session.GetSnapshot().LedgerCount);
    }

    [Fact]
    public void AuthorityTransactionFault_DoesNotCommit()
    {
        var session = CreateSession(committed: false);
        session.RequestConnect(new SessionConnectRequest(2), CancellationToken.None);
        session.Tick(new ClientOwnerTick(1));
        Assert.Equal(ClientSessionState.Faulted, session.GetSnapshot().State);
        Assert.False(session.GetSnapshot().RuntimeCommitted);
    }

    private static IClientSession CreateSession(bool committed)
    {
        var deps = new ClientSessionDependencies(
            new ClientConnectionFactory(),
            new ClientHandshakeFactory(),
            new OkCapability(),
            new InputSampleIngress(8),
            new MemoryPersistence().Source,
            new ClientEventPipelineFactory().Writer(),
            new StubRuntime(committed));
        new ClientSessionFactory().Create(in deps, out var session);
        return session;
    }

    private sealed class OkCapability : IPlatformCapabilityProvider
    {
        public ValueTask<PlatformCapabilityResult> QueryAsync(in PlatformCapabilityQuery query, CancellationToken cancellationToken)
        {
            return new ValueTask<PlatformCapabilityResult>(new PlatformCapabilityResult(query.Attempt, query.Generation, true));
        }
    }

    private sealed class StubRuntime : IClientRuntimePort
    {
        private readonly bool _committed;

        public StubRuntime(bool committed)
        {
            _committed = committed;
        }

        public ValueTask<RuntimeTransactionOutcome> ApplyAuthoritativeTransaction(
            in RuntimeTransactionRequest request,
            CancellationToken cancellationToken)
        {
            return new ValueTask<RuntimeTransactionOutcome>(new RuntimeTransactionOutcome(_committed));
        }
    }

    private sealed class MemoryPersistence
    {
        public IVerifiedSessionArtifactSource Source { get; } = IClientPersistenceFactory.CreateMemory().CreateVerifiedSessionArtifactSource();
    }
}

internal static class WriterExtensions
{
    public static IClientEventWriter Writer(this ClientEventPipelineFactory factory)
    {
        var options = new ClientEventPipelineOptions(8, 4, TimeSpan.FromSeconds(1));
        factory.Create(in options, new InMemoryClientEventSink(8), out var writer);
        return writer;
    }
}
