using System.Collections.Generic;

namespace Lumio.Client.Replica
{
    internal sealed class ReplicaStageLedger
    {
        private readonly Dictionary<ulong, ReplicaStageRequest> _open = new Dictionary<ulong, ReplicaStageRequest>();
        private ulong _nextToken = 1;

        public int OpenCount
        {
            get { return _open.Count; }
        }

        public ReplicaStageHandle Add(in ReplicaStageRequest request)
        {
            ulong token = _nextToken++;
            _open[token] = Copy(in request);
            return new ReplicaStageHandle(token, request.Generation);
        }

        public bool TryGet(ReplicaStageHandle handle, out ReplicaStageRequest request)
        {
            if (handle.IsEmpty || !_open.TryGetValue(handle.Token, out request))
            {
                request = default(ReplicaStageRequest);
                return false;
            }

            if (request.Generation != handle.Generation)
            {
                request = default(ReplicaStageRequest);
                return false;
            }

            return true;
        }

        public bool TryRemove(ReplicaStageHandle handle, out ReplicaStageRequest request)
        {
            if (!TryGet(handle, out request))
            {
                return false;
            }

            _open.Remove(handle.Token);
            return true;
        }

        public void Clear()
        {
            _open.Clear();
        }

        private static ReplicaStageRequest Copy(in ReplicaStageRequest request)
        {
            return new ReplicaStageRequest(
                request.Generation,
                request.Kind,
                request.Baseline,
                request.FromRevision,
                request.ToRevision,
                request.Sequence,
                request.Update.ToArray(),
                request.TombstoneEntityIds.ToArray(),
                request.TouchedEntityIds.ToArray());
        }
    }
}
