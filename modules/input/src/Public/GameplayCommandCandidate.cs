using System;

namespace Lumio.Client.Input
{
    public readonly struct GameplayCommandCandidate
    {
        public GameplayCommandCandidate(InputSampleSeq sampleSeq, ReadOnlyMemory<byte> payload)
        {
            SampleSeq = sampleSeq;
            Payload = payload;
            ClientCommandSeq = null;
        }

        public InputSampleSeq SampleSeq { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public ulong? ClientCommandSeq { get; }
    }
}
