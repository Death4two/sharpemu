// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System.Collections.Concurrent;
using System.Threading;

namespace SharpEmu.Libs.Ult;

/// <summary>ULT resource pool, queue and mutex services.</summary>
public static class UltExports
{
#pragma warning disable SHEM004
    private const int ErrorNull = unchecked((int)0x80810001);
    private const int ErrorInvalid = unchecked((int)0x80810004);
    private const int ErrorState = unchecked((int)0x80810006);
    private const int ErrorAgain = unchecked((int)0x80810008);
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, ResourcePool> WaitingPools = new();
    private static readonly Dictionary<ulong, DataPool> DataPools = new();
    private static readonly Dictionary<ulong, QueueState> Queues = new();
    private static readonly Dictionary<ulong, object> Mutexes = new();

    private sealed record ResourcePool(uint Threads, uint SyncObjects, ulong WorkArea);
    private sealed record DataPool(uint Count, ulong DataSize, uint QueueCount, ulong WaitingPool, ulong WorkArea);
    private sealed class QueueState { public required ulong DataSize; public required uint Capacity; public readonly Queue<byte[]> Items = new(); public readonly object Gate = new(); }

    [SysAbiExport(Nid = "hZIg1EWGsHM", ExportName = "sceUltInitialize", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceUlt")]
    public static int Initialize(CpuContext ctx) => ctx.SetReturn(0, typeof(long));

    [SysAbiExport(Nid = "d-kSG2fLrvI", ExportName = "sceUltFinalize", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int Finalize(CpuContext ctx)
    {
        lock (Gate) { WaitingPools.Clear(); DataPools.Clear(); Queues.Clear(); Mutexes.Clear(); }
        return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "V2u3WLrwh64", ExportName = "sceUltUlthreadRuntimeOptParamInitialize", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int RuntimeOptInitialize(CpuContext ctx) => Zero(ctx, ctx[CpuRegister.Rdi], 128);

    [SysAbiExport(Nid = "grs2pbc2awM", ExportName = "sceUltUlthreadRuntimeGetWorkAreaSize", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int RuntimeWorkAreaSize(CpuContext ctx)
    {
        var result = Align((ulong)(uint)ctx[CpuRegister.Rdi] * 256 + (ulong)(uint)ctx[CpuRegister.Rsi] * 16 * 1024, 8);
        ctx[CpuRegister.Rax] = result; return unchecked((int)result);
    }

    [SysAbiExport(Nid = "WIWV1Qd7PFU", ExportName = "sceUltWaitingQueueResourcePoolGetWorkAreaSize", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int WaitingPoolWorkAreaSize(CpuContext ctx)
    {
        var result = Align(((ulong)(uint)ctx[CpuRegister.Rdi] + (uint)ctx[CpuRegister.Rsi]) * 256, 8);
        ctx[CpuRegister.Rax] = result; return unchecked((int)result);
    }

    [SysAbiExport(Nid = "YiHujOG9vXY", ExportName = "sceUltWaitingQueueResourcePoolCreate", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int WaitingPoolCreate(CpuContext ctx)
    {
        var pool = ctx[CpuRegister.Rdi];
        if (pool == 0) return ctx.SetReturn(ErrorNull);
        if (Zero(ctx, pool, 256) != 0) return unchecked((int)ctx[CpuRegister.Rax]);
        lock (Gate) WaitingPools[pool] = new((uint)ctx[CpuRegister.Rdx], (uint)ctx[CpuRegister.Rcx], ctx[CpuRegister.R8]);
        return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "evj9YPkS8s4", ExportName = "sceUltQueueDataResourcePoolGetWorkAreaSize", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int QueueDataPoolWorkAreaSize(CpuContext ctx)
    {
        var data = (ulong)(uint)ctx[CpuRegister.Rdi] * Align(ctx[CpuRegister.Rsi], 8);
        var result = Align(data + (ulong)(uint)ctx[CpuRegister.Rdx] * 512, 8);
        ctx[CpuRegister.Rax] = result; return unchecked((int)result);
    }

    [SysAbiExport(Nid = "TFHm6-N6vks", ExportName = "sceUltQueueDataResourcePoolCreate", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int QueueDataPoolCreate(CpuContext ctx)
    {
        var pool = ctx[CpuRegister.Rdi]; var waiting = ctx[CpuRegister.R9];
        if (pool == 0) return ctx.SetReturn(ErrorNull);
        lock (Gate) if (waiting != 0 && !WaitingPools.ContainsKey(waiting)) return ctx.SetReturn(ErrorInvalid);
        if (Zero(ctx, pool, 512) != 0) return unchecked((int)ctx[CpuRegister.Rax]);
        _ = ctx.TryReadUInt64(ctx[CpuRegister.Rsp] + 8, out var workArea);
        lock (Gate) DataPools[pool] = new((uint)ctx[CpuRegister.Rdx], ctx[CpuRegister.Rcx], (uint)ctx[CpuRegister.R8], waiting, workArea);
        return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "9Y5keOvb6ok", ExportName = "sceUltQueueCreate", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int QueueCreate(CpuContext ctx)
    {
        var queue = ctx[CpuRegister.Rdi]; var waiting = ctx[CpuRegister.Rcx]; var dataPool = ctx[CpuRegister.R8];
        if (queue == 0) return ctx.SetReturn(ErrorNull);
        DataPool? pool;
        lock (Gate) { if (!DataPools.TryGetValue(dataPool, out pool) || (waiting != 0 && !WaitingPools.ContainsKey(waiting))) return ctx.SetReturn(ErrorInvalid); }
        if (Zero(ctx, queue, 512) != 0) return unchecked((int)ctx[CpuRegister.Rax]);
        lock (Gate) Queues[queue] = new QueueState { DataSize = ctx[CpuRegister.Rsi], Capacity = pool!.Count };
        return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "dUwpX3e5NDE", ExportName = "sceUltQueuePush", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int QueuePush(CpuContext ctx)
    {
        if (!GetQueue(ctx, ctx[CpuRegister.Rdi], out var state)) return unchecked((int)ctx[CpuRegister.Rax]);
        var source = ctx[CpuRegister.Rsi]; if (source == 0 && state.DataSize != 0) return ctx.SetReturn(ErrorNull);
        if (state.DataSize > int.MaxValue) return ctx.SetReturn(ErrorInvalid);
        var item = new byte[(int)state.DataSize]; if (item.Length != 0 && !ctx.Memory.TryRead(source, item)) return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        lock (state.Gate) { if (state.Capacity == 0 || state.Items.Count < state.Capacity) state.Items.Enqueue(item); }
        return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "uZz3ci7XYqc", ExportName = "sceUltQueueTryPop", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int QueueTryPop(CpuContext ctx)
    {
        if (!GetQueue(ctx, ctx[CpuRegister.Rdi], out var state)) return unchecked((int)ctx[CpuRegister.Rax]);
        var destination = ctx[CpuRegister.Rsi]; if (destination == 0 && state.DataSize != 0) return ctx.SetReturn(ErrorNull);
        byte[]? item; lock (state.Gate) { item = state.Items.Count == 0 ? null : state.Items.Dequeue(); }
        return item is null ? ctx.SetReturn(ErrorAgain) : (item.Length == 0 || ctx.Memory.TryWrite(destination, item) ? ctx.SetReturn(0) : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT));
    }

    [SysAbiExport(Nid = "1+8t9aHLiz8", ExportName = "sceUltMutexOptParamInitialize", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int MutexOptInitialize(CpuContext ctx) => Zero(ctx, ctx[CpuRegister.Rdi], 16);

    [SysAbiExport(Nid = "mmt8Sa6tL6c", ExportName = "sceUltMutexCreate", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int MutexCreate(CpuContext ctx)
    {
        var mutex = ctx[CpuRegister.Rdi]; var waiting = ctx[CpuRegister.Rdx]; if (mutex == 0) return ctx.SetReturn(ErrorNull);
        lock (Gate) if (waiting != 0 && !WaitingPools.ContainsKey(waiting)) return ctx.SetReturn(ErrorInvalid);
        if (Zero(ctx, mutex, 256) != 0) return unchecked((int)ctx[CpuRegister.Rax]); lock (Gate) Mutexes[mutex] = new object(); return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "8hEGkR1pfr8", ExportName = "sceUltMutexLock", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int MutexLock(CpuContext ctx) { if (!GetMutex(ctx, ctx[CpuRegister.Rdi], out var mutex)) return unchecked((int)ctx[CpuRegister.Rax]); Monitor.Enter(mutex); return ctx.SetReturn(0); }
    [SysAbiExport(Nid = "h0XebKiMBtk", ExportName = "sceUltMutexUnlock", Target = Generation.Gen5, LibraryName = "libSceUlt")]
    public static int MutexUnlock(CpuContext ctx) { if (!GetMutex(ctx, ctx[CpuRegister.Rdi], out var mutex)) return unchecked((int)ctx[CpuRegister.Rax]); try { Monitor.Exit(mutex); return ctx.SetReturn(0); } catch (SynchronizationLockException) { return ctx.SetReturn(ErrorState); } }

    private static bool GetQueue(CpuContext ctx, ulong queue, out QueueState state) { lock (Gate) if (Queues.TryGetValue(queue, out state!)) return true; state = null!; ctx.SetReturn(queue == 0 ? ErrorNull : ErrorState); return false; }
    private static bool GetMutex(CpuContext ctx, ulong address, out object mutex) { lock (Gate) if (Mutexes.TryGetValue(address, out mutex!)) return true; mutex = null!; ctx.SetReturn(address == 0 ? ErrorNull : ErrorState); return false; }
    private static int Zero(CpuContext ctx, ulong address, int size) => address == 0 ? ctx.SetReturn(ErrorNull) : ctx.Memory.TryWrite(address, new byte[size]) ? ctx.SetReturn(0) : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    private static ulong Align(ulong value, ulong alignment) => (value + alignment - 1) & ~(alignment - 1);
#pragma warning restore SHEM004
}
