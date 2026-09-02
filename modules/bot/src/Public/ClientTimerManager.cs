using System;
using System.Collections.Generic;

namespace Lumio.Client.Bot
{
    public sealed class BotCadenceTrace
    {
        private readonly List<ulong> _utteranceTicks = new List<ulong>();

        public IReadOnlyList<ulong> UtteranceTicks
        {
            get { return _utteranceTicks.ToArray(); }
        }

        internal void Record(ulong dueTick)
        {
            _utteranceTicks.Add(dueTick);
        }
    }

    public sealed class ClientTimerManager : IDisposable
    {
        public const uint TickFrameMode = 1;
        public const uint AdapterScopeKind = 2;
        public const uint BotChatCadenceDispatch = 100;
        public const ulong BotChatCadenceTicks = 5;

        private const int Success = 0;
        private const int BufferTooSmall = 5;

        private readonly INativeTimerAbi _abi;
        private readonly BotCadenceTrace _trace = new BotCadenceTrace();
        private IntPtr _manager;
        private IntPtr _slot;
        private ulong _scopeId = 1;
        private uint _scopeGeneration;
        private bool _scheduled;

        public ClientTimerManager(INativeTimerAbi abi)
        {
            _abi = abi ?? throw new ArgumentNullException(nameof(abi));
        }

        public BotCadenceTrace Trace
        {
            get { return _trace; }
        }

        public bool ScheduleBotChatCadence()
        {
            if (_scheduled)
            {
                return true;
            }

            if (_abi.CreateManager(TickFrameMode, out _manager) != Success)
            {
                return false;
            }

            if (_abi.RegisterDispatch(_manager, BotChatCadenceDispatch) != Success)
            {
                return false;
            }

            if (_abi.RegisterScope(_manager, _scopeId, AdapterScopeKind, out _scopeGeneration) != Success)
            {
                return false;
            }

            if (_abi.CreateSlot(_manager, out _slot) != Success)
            {
                return false;
            }

            if (_abi.BindSlot(_manager, _slot, BotChatCadenceDispatch) != Success)
            {
                return false;
            }

            NativeTimerHandle handle;
            if (_abi.ScheduleRepeating(
                _manager,
                _scopeId,
                AdapterScopeKind,
                _scopeGeneration,
                BotChatCadenceTicks,
                BotChatCadenceTicks,
                _slot,
                out handle) != Success)
            {
                return false;
            }

            _ = handle;
            _scheduled = true;
            return true;
        }

        public IReadOnlyList<ulong> Advance(ulong toTick)
        {
            if (!_scheduled && !ScheduleBotChatCadence())
            {
                return Array.Empty<ulong>();
            }

            if (_abi.Advance(_manager, toTick) != Success)
            {
                return Array.Empty<ulong>();
            }

            var buffer = new NativeTimerDrainRecord[8];
            int count;
            int status = _abi.Drain(_manager, buffer, out count);
            if (status == BufferTooSmall)
            {
                buffer = new NativeTimerDrainRecord[Math.Max(count, 8)];
                status = _abi.Drain(_manager, buffer, out count);
            }

            if (status != Success || count <= 0)
            {
                return Array.Empty<ulong>();
            }

            var dues = new ulong[count];
            for (int i = 0; i < count; i++)
            {
                dues[i] = buffer[i].Due;
                _trace.Record(buffer[i].Due);
            }

            return dues;
        }

        public void Dispose()
        {
            if (_manager != IntPtr.Zero)
            {
                _abi.DestroyManager(_manager);
                _manager = IntPtr.Zero;
            }
        }
    }
}
