using System;

namespace Lumio.Client.Input
{
    public interface IInputCommandSource
    {
        int DrainCandidates(Span<GameplayCommandCandidate> destination, in InputDrainContext context);

        void SetBufferPolicy(in InputBufferPolicy policy);

        InputBufferPolicy GetSnapshotPolicy();
    }
}
