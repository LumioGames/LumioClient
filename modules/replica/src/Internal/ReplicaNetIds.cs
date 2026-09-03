using System.Globalization;
using Lumio.GameRuntime.Ecs;

namespace Lumio.Client.Replica
{
    internal static class ReplicaNetIds
    {
        public static bool TryParse(string text, ulong instanceId, out NetEntityId id)
        {
            if (NetEntityId.TryParse(text, out id))
            {
                return true;
            }

            if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong counter))
            {
                id = new NetEntityId(instanceId, counter);
                return true;
            }

            id = default(NetEntityId);
            return false;
        }

        public static string Format(NetEntityId id)
        {
            if (id.InstanceId == 0UL)
            {
                return id.Counter.ToString(CultureInfo.InvariantCulture);
            }

            return id.ToHex();
        }
    }
}
