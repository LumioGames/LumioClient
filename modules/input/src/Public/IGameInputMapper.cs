namespace Lumio.Client.Input
{
    public interface IGameInputMapper
    {
        bool TryMap(in SequencedInputSample sample, in InputDrainContext context, out GameplayCommandCandidate candidate);
    }
}
