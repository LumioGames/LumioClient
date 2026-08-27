using System;
using System.Collections.Generic;

namespace Lumio.Client.Prediction
{
    internal readonly struct CandidateStageRecord
    {
        public CandidateStageRecord(ulong id, ulong generation, ulong sampleSeq, ReadOnlyMemory<byte> payload)
        {
            Id = id;
            Generation = generation;
            SampleSeq = sampleSeq;
            Payload = payload;
        }

        public ulong Id { get; }

        public ulong Generation { get; }

        public ulong SampleSeq { get; }

        public ReadOnlyMemory<byte> Payload { get; }
    }

    internal readonly struct AuthorityStageRecord
    {
        public AuthorityStageRecord(
            ulong id,
            ulong generation,
            ulong confirmedThrough,
            PredictionUpdateKind kind,
            ReadOnlyMemory<byte> payload)
        {
            Id = id;
            Generation = generation;
            ConfirmedThrough = confirmedThrough;
            Kind = kind;
            Payload = payload;
        }

        public ulong Id { get; }

        public ulong Generation { get; }

        public ulong ConfirmedThrough { get; }

        public PredictionUpdateKind Kind { get; }

        public ReadOnlyMemory<byte> Payload { get; }
    }

    internal sealed class PredictionStageLedger
    {
        private ulong _nextId = 1;
        private readonly Dictionary<ulong, CandidateStageRecord> _candidates = new Dictionary<ulong, CandidateStageRecord>();
        private readonly Dictionary<ulong, AuthorityStageRecord> _authority = new Dictionary<ulong, AuthorityStageRecord>();

        public int OpenCandidateCount
        {
            get { return _candidates.Count; }
        }

        public int OpenAuthorityCount
        {
            get { return _authority.Count; }
        }

        public PredictionCandidateStage OpenCandidate(ulong generation, ulong sampleSeq, ReadOnlyMemory<byte> payload)
        {
            ulong id = _nextId;
            _nextId++;
            _candidates[id] = new CandidateStageRecord(id, generation, sampleSeq, Copy(payload));
            return new PredictionCandidateStage(id, generation);
        }

        public PredictionAuthorityStage OpenAuthority(
            ulong generation,
            ulong confirmedThrough,
            PredictionUpdateKind kind,
            ReadOnlyMemory<byte> payload)
        {
            ulong id = _nextId;
            _nextId++;
            _authority[id] = new AuthorityStageRecord(id, generation, confirmedThrough, kind, Copy(payload));
            return new PredictionAuthorityStage(id, generation);
        }

        public bool TryTakeCandidate(in PredictionCandidateStage stage, out CandidateStageRecord record)
        {
            if (_candidates.TryGetValue(stage.Id, out record) && record.Generation == stage.Generation)
            {
                _candidates.Remove(stage.Id);
                return true;
            }

            record = default;
            return false;
        }

        public bool TryDiscardCandidate(in PredictionCandidateStage stage)
        {
            return TryTakeCandidate(in stage, out _);
        }

        public bool TryTakeAuthority(in PredictionAuthorityStage stage, out AuthorityStageRecord record)
        {
            if (_authority.TryGetValue(stage.Id, out record) && record.Generation == stage.Generation)
            {
                _authority.Remove(stage.Id);
                return true;
            }

            record = default;
            return false;
        }

        public bool TryDiscardAuthority(in PredictionAuthorityStage stage)
        {
            return TryTakeAuthority(in stage, out _);
        }

        public void Reset()
        {
            _candidates.Clear();
            _authority.Clear();
            _nextId = 1;
        }

        private static ReadOnlyMemory<byte> Copy(ReadOnlyMemory<byte> payload)
        {
            if (payload.IsEmpty)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            byte[] copy = new byte[payload.Length];
            payload.Span.CopyTo(copy);
            return copy;
        }
    }
}
