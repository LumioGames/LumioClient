using Lumio.Client.Input;

namespace Lumio.Client.Input.Tests.Unit;

public sealed class InputSampleIngressTests
{
    [Fact]
    public void AcceptedSamples_GetStrictlyIncreasingSeq()
    {
        var ingress = new InputSampleIngress(capacity: 8);
        var first = ingress.TryEnqueue(new RawInputSample(1, 0, 0));
        var second = ingress.TryEnqueue(new RawInputSample(2, 0, 0));
        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(second.Sequence.Value > first.Sequence.Value);
        Assert.Equal(1UL, second.Sequence.Value - first.Sequence.Value);
    }

    [Fact]
    public void QueueFull_DoesNotAdvanceSequence()
    {
        var ingress = new InputSampleIngress(capacity: 1);
        var first = ingress.TryEnqueue(new RawInputSample(1, 0, 0));
        var full = ingress.TryEnqueue(new RawInputSample(2, 0, 0));
        Assert.True(first.Accepted);
        Assert.False(full.Accepted);
        Assert.Equal(first.Sequence, full.LastAcceptedSequence);
        var snapshot = ingress.DrainAccepted();
        var only = Assert.Single(snapshot);
        Assert.Equal(first.Sequence, only.Sequence);
    }
}
