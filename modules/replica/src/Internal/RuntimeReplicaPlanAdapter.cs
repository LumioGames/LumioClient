using System;

namespace Lumio.Client.Replica
{
    internal sealed class RuntimeReplicaPlanAdapter : IReplicaMapper
    {
        private readonly GeneratedReplicaAdapter _generated = new GeneratedReplicaAdapter();

        public ReplicaMappingResult Map(
            in ReplicaStageRequest request,
            in ReplicaMappingContext context,
            out ReadOnlyMemory<byte> applyPlan)
        {
            _ = context;
            if (!_generated.TryValidate(request.Kind, request.Update))
            {
                applyPlan = ReadOnlyMemory<byte>.Empty;
                return new ReplicaMappingResult(false);
            }

            applyPlan = request.Update.ToArray();
            return new ReplicaMappingResult(true);
        }
    }
}
