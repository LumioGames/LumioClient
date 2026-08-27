using Lumio.Client.Prediction;
using Lumio.Client.Replica;

namespace Lumio.Client.Session
{
    internal sealed class AuthorityStageBundle
    {
        public ReplicaStageHandle Replica { get; set; }

        public PredictionAuthorityStage Prediction { get; set; }

        public bool ReplicaStaged { get; set; }

        public bool PredictionStaged { get; set; }

        public void Clear()
        {
            ReplicaStaged = false;
            PredictionStaged = false;
        }
    }
}
