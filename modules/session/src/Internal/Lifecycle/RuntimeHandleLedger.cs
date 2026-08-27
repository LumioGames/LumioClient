using System;
using System.Collections.Generic;

namespace Lumio.Client.Session
{
    internal sealed class RuntimeHandleLedger
    {
        private readonly List<string> _live = new List<string>();
        private readonly List<string> _destroyed = new List<string>();

        public int EcsCount
        {
            get { return CountLive("ecs"); }
        }

        public int VoxelCount
        {
            get { return CountLive("voxel"); }
        }

        public string[] DestroyOrder
        {
            get { return _destroyed.ToArray(); }
        }

        public bool TryCreateEcs()
        {
            _live.Add("ecs");
            return true;
        }

        public bool TryCreateVoxel()
        {
            if (CountLive("ecs") == 0)
            {
                return false;
            }

            _live.Add("voxel");
            return true;
        }

        public void DestroyVoxelThenEcs()
        {
            DestroyKind("voxel");
            DestroyKind("ecs");
        }

        public void RollbackEcsOnVoxelFailure()
        {
            DestroyKind("ecs");
        }

        private int CountLive(string name)
        {
            int n = 0;
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i] == name)
                {
                    n++;
                }
            }

            return n;
        }

        private void DestroyKind(string name)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i] == name)
                {
                    _live.RemoveAt(i);
                    _destroyed.Add(name);
                }
            }
        }
    }
}
