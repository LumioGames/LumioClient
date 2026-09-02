using Lumio.Client.Bot;

namespace Lumio.Client.Bot.Tests.Support;

/// <summary>
/// Test stand-in for NativeCore C-4 tickFrame ABI (advance window (committed, toTick],
/// repeating due += interval). Production ClientTimerManager only calls this surface.
/// </summary>
internal sealed class C4TickFrameAbi : INativeTimerAbi
{
    private const int Success = 0;
    private const int InvalidArgument = 1;
    private const int BufferTooSmall = 5;
    private int _next = 1;
    private readonly Dictionary<IntPtr, Manager> _managers = new();

    public int CreateManager(uint mode, out IntPtr manager)
    {
        manager = IntPtr.Zero;
        if (mode != ClientTimerManager.TickFrameMode)
        {
            return InvalidArgument;
        }

        manager = NextHandle();
        _managers[manager] = new Manager();
        return Success;
    }

    public int DestroyManager(IntPtr manager)
    {
        if (!_managers.TryGetValue(manager, out Manager? state) || !state.Running)
        {
            return state is null ? InvalidArgument : 17;
        }

        state.Running = false;
        return Success;
    }

    public int RegisterDispatch(IntPtr manager, uint dispatchId)
    {
        if (!TryRunning(manager, out Manager state) || dispatchId == 0 || state.Dispatch == dispatchId)
        {
            return InvalidArgument;
        }

        state.Dispatch = dispatchId;
        return Success;
    }

    public int RegisterScope(IntPtr manager, ulong scopeId, uint scopeKind, out uint generation)
    {
        generation = 0;
        if (!TryRunning(manager, out Manager state) || scopeKind != ClientTimerManager.AdapterScopeKind)
        {
            return InvalidArgument;
        }

        state.ScopeId = scopeId;
        state.ScopeGeneration = 1;
        generation = 1;
        return Success;
    }

    public int CreateSlot(IntPtr manager, out IntPtr slot)
    {
        slot = IntPtr.Zero;
        if (!TryRunning(manager, out Manager state))
        {
            return InvalidArgument;
        }

        slot = NextHandle();
        state.Slot = slot;
        return Success;
    }

    public int BindSlot(IntPtr manager, IntPtr slot, uint dispatchId)
    {
        if (!TryRunning(manager, out Manager state) || state.Slot != slot || state.Dispatch != dispatchId)
        {
            return InvalidArgument;
        }

        state.SlotBound = true;
        return Success;
    }

    public int ScheduleRepeating(
        IntPtr manager,
        ulong scopeId,
        uint scopeKind,
        uint scopeGeneration,
        ulong firstDue,
        ulong interval,
        IntPtr slot,
        out NativeTimerHandle handle)
    {
        handle = default;
        if (!TryRunning(manager, out Manager state)
            || !state.SlotBound
            || state.Slot != slot
            || state.ScopeId != scopeId
            || state.ScopeGeneration != scopeGeneration
            || interval == 0
            || firstDue <= state.Committed)
        {
            return InvalidArgument;
        }

        state.Due = firstDue;
        state.Interval = interval;
        state.HasRepeating = true;
        handle = new NativeTimerHandle(1, 1, 1);
        return Success;
    }

    public int Advance(IntPtr manager, ulong toTick)
    {
        if (!TryRunning(manager, out Manager state))
        {
            return InvalidArgument;
        }

        if (toTick < state.Committed)
        {
            return 9;
        }

        state.Pending.Clear();
        if (state.HasRepeating)
        {
            while (state.Due <= toTick)
            {
                if (state.Due > state.Committed)
                {
                    state.Pending.Add(new NativeTimerDrainRecord(state.Due, (ulong)state.Pending.Count + 1, state.Dispatch));
                }

                state.Due += state.Interval;
            }
        }

        state.Committed = toTick;
        return Success;
    }

    public int Drain(IntPtr manager, Span<NativeTimerDrainRecord> records, out int count)
    {
        count = 0;
        if (!TryRunning(manager, out Manager state))
        {
            return InvalidArgument;
        }

        count = state.Pending.Count;
        if (count > records.Length)
        {
            return BufferTooSmall;
        }

        for (int i = 0; i < count; i++)
        {
            records[i] = state.Pending[i];
        }

        state.Pending.Clear();
        return Success;
    }

    private bool TryRunning(IntPtr manager, out Manager state)
    {
        if (_managers.TryGetValue(manager, out Manager? found) && found.Running)
        {
            state = found;
            return true;
        }

        state = found!;
        return false;
    }

    private IntPtr NextHandle()
    {
        return new IntPtr(_next++);
    }

    private sealed class Manager
    {
        public bool Running = true;
        public uint Dispatch;
        public ulong ScopeId;
        public uint ScopeGeneration;
        public IntPtr Slot;
        public bool SlotBound;
        public bool HasRepeating;
        public ulong Due;
        public ulong Interval;
        public ulong Committed;
        public List<NativeTimerDrainRecord> Pending = new();
    }
}
