using Lumio.Client.Input;

namespace Lumio.Client.Input.Tests.Contract;

public sealed class InputMappingFixtureTests
{
    [Fact]
    public void GeneratedVectors_AreDeterministic()
    {
        var mapper = new IdentityMapper();
        var sample = new SequencedInputSample(new InputSampleSeq(4), new RawInputSample(9, 1, -1));
        var context = new InputDrainContext(1, 1);
        Assert.True(mapper.TryMap(in sample, in context, out var first));
        Assert.True(mapper.TryMap(in sample, in context, out var second));
        Assert.Equal(first.SampleSeq, second.SampleSeq);
        Assert.False(first.ClientCommandSeq.HasValue);
    }

    private sealed class IdentityMapper : IGameInputMapper
    {
        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            candidate = new GameplayCommandCandidate(sample.Sequence, ReadOnlyMemory<byte>.Empty);
            return true;
        }
    }
}
