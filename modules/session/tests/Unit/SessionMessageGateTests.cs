namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionMessageGateTests
{
    [Fact]
    public void InvalidMatrix_HasZeroLeafCalls()
    {
        var gate = new ActiveMessageGate();
        Assert.False(gate.Allow(ClientSessionState.Negotiating, 1, 1, SessionMessageKind.FullSnapshot));
        Assert.False(gate.Allow(ClientSessionState.Active, 2, 1, SessionMessageKind.FullSnapshot));
        Assert.False(gate.Allow(ClientSessionState.Active, 1, 1, SessionMessageKind.Unknown));
        Assert.Equal(3, gate.RejectedCalls);
    }
}

public sealed class GameplayScopeActivationGateTests
{
    [Fact]
    public void ScopeMustActivateBeforeWorldHandles()
    {
        var gate = new GameplayScopeActivationGate();
        Assert.False(gate.CanCreateWorldHandles());
        Assert.True(gate.TryPrepare());
        Assert.True(gate.TryActivate());
        Assert.True(gate.CanCreateWorldHandles());
    }
}
