namespace Lumio.Client.Prediction
{
    public enum PredictionCandidateStatus
    {
        Staged = 0,
        Rejected = 1,
        WindowBusy = 2,
        Frozen = 3,
        StaleGeneration = 4
    }

    public enum PredictionLocalOutcomeStatus
    {
        None = 0,
        Assigned = 1,
        Discarded = 2,
        StaleStage = 3,
        Frozen = 4,
        Indeterminate = 5
    }

    public enum PredictionAuthorityStatus
    {
        Staged = 0,
        Rejected = 1,
        RequiresResync = 2,
        Frozen = 3,
        StaleGeneration = 4
    }

    public enum PredictionAuthorityOutcomeStatus
    {
        None = 0,
        Applied = 1,
        Discarded = 2,
        StaleStage = 3,
        Frozen = 4,
        Indeterminate = 5
    }

    public enum PredictionStageDiscardReason
    {
        None = 0,
        Aborted = 1,
        Cancelled = 2,
        Stale = 3,
        SessionReset = 4
    }

    public readonly struct PredictionCandidateResult
    {
        public PredictionCandidateResult(PredictionCandidateStatus status)
        {
            Status = status;
        }

        public PredictionCandidateStatus Status { get; }

        public bool Succeeded
        {
            get { return Status == PredictionCandidateStatus.Staged; }
        }
    }

    public readonly struct PredictionLocalOutcomeResult
    {
        public PredictionLocalOutcomeResult(PredictionLocalOutcomeStatus status)
        {
            Status = status;
        }

        public PredictionLocalOutcomeStatus Status { get; }
    }

    public readonly struct PredictionAuthorityResult
    {
        public PredictionAuthorityResult(PredictionAuthorityStatus status)
        {
            Status = status;
        }

        public PredictionAuthorityStatus Status { get; }

        public bool Succeeded
        {
            get { return Status == PredictionAuthorityStatus.Staged; }
        }
    }

    public readonly struct PredictionAuthorityOutcomeResult
    {
        public PredictionAuthorityOutcomeResult(PredictionAuthorityOutcomeStatus status)
        {
            Status = status;
        }

        public PredictionAuthorityOutcomeStatus Status { get; }
    }

    public readonly struct PredictionResetResult
    {
        public PredictionResetResult(bool succeeded)
        {
            Succeeded = succeeded;
        }

        public bool Succeeded { get; }
    }
}
