using System;

namespace Lumio.Client.Prediction
{
    public readonly struct PredictionCandidate
    {
        public PredictionCandidate(ulong sampleSeq, ReadOnlyMemory<byte> payload)
        {
            SampleSeq = sampleSeq;
            Payload = payload;
        }

        public ulong SampleSeq { get; }

        public ReadOnlyMemory<byte> Payload { get; }
    }

    public readonly struct PredictionCandidateContext
    {
        public PredictionCandidateContext(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct AcceptedPredictionCommand
    {
        public AcceptedPredictionCommand(ClientCommandSeq commandSeq, PredictionKey key, ReadOnlyMemory<byte> payload)
        {
            CommandSeq = commandSeq;
            Key = key;
            Payload = payload;
        }

        public ClientCommandSeq CommandSeq { get; }

        public PredictionKey Key { get; }

        public ReadOnlyMemory<byte> Payload { get; }
    }

    public readonly struct AuthorityPredictionUpdate
    {
        public AuthorityPredictionUpdate(ReadOnlyMemory<byte> payload, ulong confirmedThroughSeq)
        {
            Payload = payload;
            ConfirmedThroughSeq = confirmedThroughSeq;
        }

        public ReadOnlyMemory<byte> Payload { get; }

        public ulong ConfirmedThroughSeq { get; }
    }

    public readonly struct PredictionAuthorityContext
    {
        public PredictionAuthorityContext(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; }
    }

    public readonly struct PredictionResetRequest
    {
        public PredictionResetRequest(ulong generation, int windowCapacity)
        {
            Generation = generation;
            WindowCapacity = windowCapacity;
        }

        public ulong Generation { get; }

        public int WindowCapacity { get; }
    }
}
