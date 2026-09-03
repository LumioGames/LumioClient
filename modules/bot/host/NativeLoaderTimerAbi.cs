#if LUMIO_NATIVE_LOADER
using System.Reflection;
using System.Runtime.InteropServices;
using Lumio.Client.Bot;
using Lumio.Engine.NativeLoader;

namespace Lumio.Client.Bot.Host;

/// <summary>
/// Production <see cref="INativeTimerAbi"/> that reads <c>timer_*</c> from the NativeLoader root table.
/// Does not call LoadLibrary; NativeEngineLoader owns the loaded module.
/// </summary>
internal sealed class NativeLoaderTimerAbi : INativeTimerAbi, IDisposable
{
    private const string EntrySymbol = "lumio_engine_get_api_v1";
    private const uint AbiVersion = 1;

    private readonly NativeEngineLease _lease;
    private readonly CreateManagerDelegate _createManager;
    private readonly DestroyManagerDelegate _destroyManager;
    private readonly RegisterDispatchDelegate _registerDispatch;
    private readonly RegisterScopeDelegate _registerScope;
    private readonly CreateSlotDelegate _createSlot;
    private readonly BindSlotDelegate _bindSlot;
    private readonly ScheduleRepeatingDelegate _scheduleRepeating;
    private readonly AdvanceDelegate _advance;
    private readonly DrainDelegate _drain;
    private bool _disposed;

    private NativeLoaderTimerAbi(
        NativeEngineLease lease,
        CreateManagerDelegate createManager,
        DestroyManagerDelegate destroyManager,
        RegisterDispatchDelegate registerDispatch,
        RegisterScopeDelegate registerScope,
        CreateSlotDelegate createSlot,
        BindSlotDelegate bindSlot,
        ScheduleRepeatingDelegate scheduleRepeating,
        AdvanceDelegate advance,
        DrainDelegate drain)
    {
        _lease = lease;
        _createManager = createManager;
        _destroyManager = destroyManager;
        _registerDispatch = registerDispatch;
        _registerScope = registerScope;
        _createSlot = createSlot;
        _bindSlot = bindSlot;
        _scheduleRepeating = scheduleRepeating;
        _advance = advance;
        _drain = drain;
    }

    public static NativeLoaderTimerAbi Load(string nativePath)
    {
        NativeEngineLease lease = NativeEngineLoader.LoadFromBuildInfo(nativePath);
        FieldInfo? libraryField = typeof(NativeEngineLease).GetField("_library", BindingFlags.NonPublic | BindingFlags.Instance);
        if (libraryField?.GetValue(lease) is not nint library || library == 0)
        {
            lease.Dispose();
            throw new InvalidOperationException("NativeEngineLease did not expose a loaded module handle.");
        }

        nint entry = NativeLibrary.GetExport(library, EntrySymbol);
        GetApiDelegate getApi = Marshal.GetDelegateForFunctionPointer<GetApiDelegate>(entry);
        int status = getApi(AbiVersion, out nint api);
        if (status != 0 || api == 0)
        {
            lease.Dispose();
            throw new InvalidOperationException("Native engine entry rejected ABI version " + AbiVersion + " with status " + status + ".");
        }

        RootApiWithTimers table = Marshal.PtrToStructure<RootApiWithTimers>(api);
        if (table.StructSize < (uint)Marshal.SizeOf<RootApiWithTimers>()
            || table.TimerCreateManager == 0
            || table.TimerDestroyManager == 0
            || table.TimerRegisterDispatch == 0
            || table.TimerRegisterScope == 0
            || table.TimerCreateSlot == 0
            || table.TimerBindSlot == 0
            || table.TimerScheduleRepeating == 0
            || table.TimerAdvance == 0
            || table.TimerDrain == 0)
        {
            lease.Dispose();
            throw new InvalidOperationException("Native root table is missing timer_* slots.");
        }

        return new NativeLoaderTimerAbi(
            lease,
            Marshal.GetDelegateForFunctionPointer<CreateManagerDelegate>(table.TimerCreateManager),
            Marshal.GetDelegateForFunctionPointer<DestroyManagerDelegate>(table.TimerDestroyManager),
            Marshal.GetDelegateForFunctionPointer<RegisterDispatchDelegate>(table.TimerRegisterDispatch),
            Marshal.GetDelegateForFunctionPointer<RegisterScopeDelegate>(table.TimerRegisterScope),
            Marshal.GetDelegateForFunctionPointer<CreateSlotDelegate>(table.TimerCreateSlot),
            Marshal.GetDelegateForFunctionPointer<BindSlotDelegate>(table.TimerBindSlot),
            Marshal.GetDelegateForFunctionPointer<ScheduleRepeatingDelegate>(table.TimerScheduleRepeating),
            Marshal.GetDelegateForFunctionPointer<AdvanceDelegate>(table.TimerAdvance),
            Marshal.GetDelegateForFunctionPointer<DrainDelegate>(table.TimerDrain));
    }

    public int CreateManager(uint mode, out IntPtr manager)
    {
        int status = _createManager(mode, out nint handle);
        manager = handle;
        return status;
    }

    public int DestroyManager(IntPtr manager) => _destroyManager((nint)manager);

    public int RegisterDispatch(IntPtr manager, uint dispatchId) => _registerDispatch((nint)manager, dispatchId);

    public int RegisterScope(IntPtr manager, ulong scopeId, uint scopeKind, out uint generation) =>
        _registerScope((nint)manager, scopeId, scopeKind, out generation);

    public int CreateSlot(IntPtr manager, out IntPtr slot)
    {
        int status = _createSlot((nint)manager, out nint handle);
        slot = handle;
        return status;
    }

    public int BindSlot(IntPtr manager, IntPtr slot, uint dispatchId) =>
        _bindSlot((nint)manager, (nint)slot, dispatchId);

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
        int status = _scheduleRepeating(
            (nint)manager,
            scopeId,
            scopeKind,
            scopeGeneration,
            firstDue,
            interval,
            (nint)slot,
            out NativeTimerAbiHandle native);
        handle = new NativeTimerHandle(native.Index, native.Generation, native.Context);
        return status;
    }

    public int Advance(IntPtr manager, ulong toTick) => _advance((nint)manager, toTick);

    public int Drain(IntPtr manager, Span<NativeTimerDrainRecord> records, out int count)
    {
        count = 0;
        NativeDrainRecord[] buffer = new NativeDrainRecord[Math.Max(records.Length, 1)];
        GCHandle pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            int status = _drain((nint)manager, pin.AddrOfPinnedObject(), (uint)records.Length, out uint nativeCount);
            count = (int)nativeCount;
            if (status != 0)
            {
                return status;
            }

            int copy = Math.Min(count, records.Length);
            for (int i = 0; i < copy; i++)
            {
                records[i] = new NativeTimerDrainRecord(buffer[i].Due, buffer[i].ScheduleSequence, buffer[i].SlotDispatchId);
            }

            return status;
        }
        finally
        {
            pin.Free();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lease.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetApiDelegate(uint requestedVersion, out nint api);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateManagerDelegate(uint mode, out nint manager);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DestroyManagerDelegate(nint manager);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RegisterDispatchDelegate(nint manager, uint dispatchId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RegisterScopeDelegate(nint manager, ulong scopeId, uint scopeKind, out uint generation);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CreateSlotDelegate(nint manager, out nint slot);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BindSlotDelegate(nint manager, nint slot, uint dispatchId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ScheduleRepeatingDelegate(
        nint manager,
        ulong scopeId,
        uint scopeKind,
        uint scopeGeneration,
        ulong firstDue,
        ulong interval,
        nint slot,
        out NativeTimerAbiHandle handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AdvanceDelegate(nint manager, ulong toTick);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DrainDelegate(nint manager, nint records, uint capacity, out uint count);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeTimerAbiHandle
    {
        public uint Index;
        public uint Generation;
        public ulong Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDrainRecord
    {
        public uint HandleIndex;
        public uint HandleGeneration;
        public ulong HandleContext;
        public ulong Due;
        public ulong ScheduleSequence;
        public uint SlotDispatchId;
        public uint Pad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RootApiWithTimers
    {
        public uint AbiVersion;
        public uint StructSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] AbiHash;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] BuildId;
        public nint Ping;
        public nint CreateClrHost;
        public nint ClrHostCall;
        public nint DestroyClrHost;
        public nint TimerCreateManager;
        public nint TimerDestroyManager;
        public nint TimerRegisterDispatch;
        public nint TimerRegisterScope;
        public nint TimerTeardownScope;
        public nint TimerCreateSlot;
        public nint TimerBindSlot;
        public nint TimerCloseSlot;
        public nint TimerScheduleOneShot;
        public nint TimerScheduleRepeating;
        public nint TimerCancel;
        public nint TimerAdvance;
        public nint TimerPump;
        public nint TimerDrain;
    }
}
#endif
