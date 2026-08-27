using System;

namespace Lumio.Client.Replica
{
    public interface IClientReplicaFactory
    {
        IClientReplica Create();

        IClientReplica Create(IReplicaMapper mapper);
    }

    public sealed class ClientReplicaFactory : IClientReplicaFactory
    {
        public IClientReplica Create()
        {
            return Create(new RuntimeReplicaPlanAdapter());
        }

        public IClientReplica Create(IReplicaMapper mapper)
        {
            if (mapper is null)
            {
                throw new ArgumentNullException(nameof(mapper));
            }

            return new ClientReplica(mapper);
        }
    }
}
