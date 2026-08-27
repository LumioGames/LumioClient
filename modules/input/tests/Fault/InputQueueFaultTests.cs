using Lumio.Client.Input;

namespace Lumio.Client.Input.Tests.Fault;

public sealed class InputQueueFaultTests
{
    [Fact]
    public void MapperThrows_CurrentSampleRejectedByPolicy()
    {
        var ingress = new InputSampleIngress(4);
        var source = new InputCommandSource(ingress, new ThrowingMapper());
        ingress.TryEnqueue(new RawInputSample(1, 0, 0));
        ingress.TryEnqueue(new RawInputSample(2, 0, 0));
        var buffer = new GameplayCommandCandidate[4];
        int count = source.DrainCandidates(buffer, new InputDrainContext(1, 4));
        Assert.Equal(0, count);
    }

    private sealed class ThrowingMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            candidate = default;
            throw new InvalidOperationException("mapper");
        }
    }
}
