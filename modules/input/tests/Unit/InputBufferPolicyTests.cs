using Lumio.Client.Input;

namespace Lumio.Client.Input.Tests.Unit;

public sealed class InputBufferPolicyTests
{
    [Fact]
    public void ResyncPolicy_IsGenerationScoped()
    {
        var ingress = new InputSampleIngress(4);
        var source = new InputCommandSource(ingress, new IdentityMapper());
        source.SetBufferPolicy(new InputBufferPolicy(InputBufferPolicyKind.Drop, generation: 2));
        ingress.TryEnqueue(new RawInputSample(1, 0, 0));
        var buffer = new GameplayCommandCandidate[4];
        int dropped = source.DrainCandidates(buffer, new InputDrainContext(2, 4));
        Assert.Equal(0, dropped);
        ingress.TryEnqueue(new RawInputSample(2, 0, 0));
        int kept = source.DrainCandidates(buffer, new InputDrainContext(3, 4));
        Assert.Equal(1, kept);
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
