namespace Lumio.Client.IntegrationTests.Fakes;

public sealed class FakeRuntimePortTests
{
    [Fact]
    public void RecordsSingleTransactionCalls()
    {
        var port = new FakeClientRuntimePort();
        var request = new FakeRuntimeTransactionRequest(FakeRuntimeTransactionKind.Authoritative, 1, ReadOnlyMemory<byte>.Empty);
        var first = port.ApplyAuthoritativeTransaction(request);
        Assert.True(first.Committed);
        Assert.Equal(1, port.ApplyAuthoritativeTransactionCalls);
        port.InjectOutcome(new FakeRuntimeTransactionOutcome(false, "aborted"));
        var second = port.ApplyAuthoritativeTransaction(request);
        Assert.False(second.Committed);
        Assert.Equal(2, port.ApplyAuthoritativeTransactionCalls);
        Assert.DoesNotContain("Envelope", typeof(FakeClientRuntimePort).Assembly.GetExportedTypes().Select(t => t.Name));
    }
}
