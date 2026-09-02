using System;

namespace Lumio.Client.Bot
{
    public readonly struct NativeTimerHandle
    {
        public NativeTimerHandle(uint index, uint generation, ulong context)
        {
            Index = index;
            Generation = generation;
            Context = context;
        }

        public uint Index { get; }

        public uint Generation { get; }

        public ulong Context { get; }
    }

    public readonly struct NativeTimerDrainRecord
    {
        public NativeTimerDrainRecord(ulong due, ulong scheduleSequence, uint slotDispatchId)
        {
            Due = due;
            ScheduleSequence = scheduleSequence;
            SlotDispatchId = slotDispatchId;
        }

        public ulong Due { get; }

        public ulong ScheduleSequence { get; }

        public uint SlotDispatchId { get; }
    }

    public interface INativeTimerAbi
    {
        int CreateManager(uint mode, out IntPtr manager);

        int DestroyManager(IntPtr manager);

        int RegisterDispatch(IntPtr manager, uint dispatchId);

        int RegisterScope(IntPtr manager, ulong scopeId, uint scopeKind, out uint generation);

        int CreateSlot(IntPtr manager, out IntPtr slot);

        int BindSlot(IntPtr manager, IntPtr slot, uint dispatchId);

        int ScheduleRepeating(
            IntPtr manager,
            ulong scopeId,
            uint scopeKind,
            uint scopeGeneration,
            ulong firstDue,
            ulong interval,
            IntPtr slot,
            out NativeTimerHandle handle);

        int Advance(IntPtr manager, ulong toTick);

        int Drain(IntPtr manager, Span<NativeTimerDrainRecord> records, out int count);
    }
}
