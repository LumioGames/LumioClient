namespace Lumio.Client.Session.Tests.Contract;

public sealed class SessionMessageMatrixFixtureTests
{
    [Fact]
    public void GeneratedValidInvalidRowsMatch()
    {
        var gate = new ActiveMessageGate();
        Assert.True(gate.Allow(ClientSessionState.Synchronizing, 1, 1, SessionMessageKind.FullSnapshot));
        Assert.False(gate.Allow(ClientSessionState.Synchronizing, 1, 1, SessionMessageKind.Delta));
        Assert.True(gate.Allow(ClientSessionState.Active, 1, 1, SessionMessageKind.Gap));
        Assert.False(gate.Allow(ClientSessionState.Disconnected, 1, 1, SessionMessageKind.FullSnapshot));
    }
}
