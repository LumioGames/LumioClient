using Lumio.Client.Connection;

namespace Lumio.Client.Session.Tests.Unit;

public sealed class SessionEventArbiterTests
{
    [Fact]
    public void CancelBeatsRejectDisconnectAndSuccess()
    {
        var inbox = new SessionEventInbox();
        var gen = new ConnectionGeneration(1);
        inbox.Enqueue(SessionEventPriority.Success, 1, new ConnectionEvent(ConnectionEventKind.Started, gen, false));
        inbox.Enqueue(SessionEventPriority.Disconnect, 1, new ConnectionEvent(ConnectionEventKind.Disconnected, gen, true));
        inbox.Enqueue(SessionEventPriority.Cancel, 1, new ConnectionEvent(ConnectionEventKind.Closed, gen, true));
        Assert.True(inbox.TryDequeue(out SessionEvent first));
        Assert.Equal(SessionEventPriority.Cancel, first.Priority);
    }

    [Fact]
    public void FaultBeatsCloseAndCommitted()
    {
        var inbox = new SessionEventInbox();
        var gen = new ConnectionGeneration(1);
        inbox.Enqueue(SessionEventPriority.ForcedClose, 1, new ConnectionEvent(ConnectionEventKind.Closed, gen, true));
        inbox.Enqueue(SessionEventPriority.Fault, 1, new ConnectionEvent(ConnectionEventKind.Faulted, gen, true));
        Assert.True(inbox.TryDequeue(out SessionEvent first));
        Assert.Equal(SessionEventPriority.Fault, first.Priority);
    }
}
