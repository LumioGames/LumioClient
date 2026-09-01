using System.Collections.Generic;

namespace Lumio.Client.Replica
{
    public interface IReplicaWorld
    {
        ReplicaAdmissionResult InstallAdmission(in ReplicaAdmission admission);

        ReplicaBindingLookup SelfLookup();

        ReplicaEntityResolve Resolve(string roomId, string netEntityId, ulong connectionGeneration, bool hasConnectionGeneration);

        ReplicaAttributeQueryResult QueryAttribute(in ReplicaAttributeQuery query);

        IReadOnlyList<ReplicaChatLine> CopyChatWindow();

        int VisibleEntityCount { get; }

        string LastRejectCode { get; }
    }
}
