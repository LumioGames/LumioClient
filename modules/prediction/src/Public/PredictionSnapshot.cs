namespace Lumio.Client.Prediction
{
    public readonly struct PredictionSnapshot
    {
        public PredictionSnapshot(
            ulong generation,
            ulong lastAssignedSeq,
            ulong confirmedSeq,
            int historyCount,
            int windowCapacity,
            int highWatermark,
            int openCandidateStages,
            int openAuthorityStages,
            bool frozen)
        {
            Generation = generation;
            LastAssignedSeq = lastAssignedSeq;
            ConfirmedSeq = confirmedSeq;
            HistoryCount = historyCount;
            WindowCapacity = windowCapacity;
            HighWatermark = highWatermark;
            OpenCandidateStages = openCandidateStages;
            OpenAuthorityStages = openAuthorityStages;
            Frozen = frozen;
        }

        public ulong Generation { get; }

        public ulong LastAssignedSeq { get; }

        public ulong ConfirmedSeq { get; }

        public int HistoryCount { get; }

        public int WindowCapacity { get; }

        public int HighWatermark { get; }

        public int OpenCandidateStages { get; }

        public int OpenAuthorityStages { get; }

        public bool Frozen { get; }
    }
}
