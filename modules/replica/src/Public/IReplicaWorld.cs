using System.Collections.Generic;
using Lumio.GameRuntime.Ecs;

namespace Lumio.Client.Replica
{
    public interface IReplicaWorld
    {
        WorldManager Manager { get; }

        IReadOnlyList<WorldMessage> DrainOutbound();

        ReplicaAdmissionResult InstallAdmission(in ReplicaAdmission admission);

        ReplicaBindingLookup SelfLookup();

        ReplicaEntityResolve Resolve(string roomId, string netEntityId, ulong connectionGeneration, bool hasConnectionGeneration);

        ReplicaAttributeQueryResult QueryAttribute(in ReplicaAttributeQuery query);

        IReadOnlyList<ReplicaChatLine> CopyChatWindow();

        IReadOnlyList<ReplicaIdentityRecord> CopyIdentityRecords();

        int VisibleEntityCount { get; }

        bool InputEnabled { get; }

        ReplicaConnectionSuperseded LastConnectionSuperseded { get; }

        string LastRejectCode { get; }
    }
}
