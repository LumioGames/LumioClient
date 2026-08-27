using Lumio.Client.Input;

namespace Lumio.Client.Input.Tests.Unit;

public sealed class InputCommandSourceTests
{
    [Fact]
    public void MapperInvokedInSampleOrder()
    {
        var ingress = new InputSampleIngress(8);
        var mapper = new RecordingMapper();
        var source = new InputCommandSource(ingress, mapper);
        ingress.TryEnqueue(new RawInputSample(1, 0, 0));
        ingress.TryEnqueue(new RawInputSample(2, 0, 0));
        var buffer = new GameplayCommandCandidate[8];
        int count = source.DrainCandidates(buffer, new InputDrainContext(1, 8));
        Assert.Equal(2, count);
        Assert.Equal(new uint[] { 1, 2 }, mapper.Buttons);
        Assert.True(buffer[1].SampleSeq.Value > buffer[0].SampleSeq.Value);
    }

    [Fact]
    public void Candidate_DoesNotAllocateClientCommandSeq()
    {
        var ingress = new InputSampleIngress(4);
        var source = new InputCommandSource(ingress, new RecordingMapper());
        ingress.TryEnqueue(new RawInputSample(3, 0, 0));
        var buffer = new GameplayCommandCandidate[1];
        int count = source.DrainCandidates(buffer, new InputDrainContext(1, 1));
        Assert.Equal(1, count);
        Assert.False(buffer[0].ClientCommandSeq.HasValue);
    }

    private sealed class RecordingMapper : IGameInputMapper
    {
        public List<uint> Buttons { get; } = new();

        public bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate)
        {
            Buttons.Add(sample.Sample.Buttons);
            candidate = new GameplayCommandCandidate(sample.Sequence, ReadOnlyMemory<byte>.Empty);
            return true;
        }
    }
}
