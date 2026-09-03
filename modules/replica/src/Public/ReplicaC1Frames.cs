using System.Text;

namespace Lumio.Client.Replica
{
    public static class ReplicaC1Frames
    {
        public const string EmptyFullSnapshotJson =
            "{\"messageType\":\"FullSnapshot\",\"tickId\":0,\"revision\":0,\"stateBlocks\":[]}";

        public static readonly byte[] EmptyFullSnapshot = Encoding.UTF8.GetBytes(EmptyFullSnapshotJson);
    }
}
