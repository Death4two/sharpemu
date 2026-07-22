// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System.Buffers.Binary;

namespace SharpEmu.Libs.Np;

public static class NpUniversalDataSystemExports
{
    private const int NpUniversalDataSystemErrorInvalidArgument = unchecked((int)0x80553102);
    private static readonly object _eventGate = new();
    private static readonly HashSet<int> _createdEvents = [];
    private static readonly Dictionary<ulong, IGuestMemoryAllocator> _eventPropertyObjects = [];
    private static int _nextHandle = 1;
    private static int _nextEvent = 1;

    [SysAbiExport(
        Nid = "sjaobBgqeB4",
        ExportName = "sceNpUniversalDataSystemInitialize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemInitialize(CpuContext ctx)
    {
        var parameterAddress = ctx[CpuRegister.Rdi];
        if (parameterAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        Span<byte> parameters = stackalloc byte[16];
        return ctx.Memory.TryRead(parameterAddress, parameters)
            ? ctx.SetReturn(0, typeof(long))
            : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
    }

    [SysAbiExport(
        Nid = "5zBnau1uIEo",
        ExportName = "sceNpUniversalDataSystemCreateContext",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemCreateContext(CpuContext ctx)
    {
        var contextAddress = ctx[CpuRegister.Rdi];
        if (contextAddress == 0)
        {
            return ctx.SetReturn(0, typeof(long));
        }

        Span<byte> context = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(context, 1);
        return ctx.Memory.TryWrite(contextAddress, context)
            ? ctx.SetReturn(0, typeof(long))
            : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
    }

    [SysAbiExport(
        Nid = "hT0IAEvN+M0",
        ExportName = "sceNpUniversalDataSystemCreateHandle",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemCreateHandle(CpuContext ctx)
    {
        var handle = Interlocked.Increment(ref _nextHandle);
        if (ctx.TryWriteInt32(ctx[CpuRegister.Rdi], handle, checkNil: true) ||
            ctx.TryWriteInt32(ctx[CpuRegister.Rsi], handle, checkNil: true))
        {
            return ctx.SetReturn(0, typeof(long));
        }

        return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
    }

    [SysAbiExport(
        Nid = "p+GcLqwpL9M",
        ExportName = "sceNpUniversalDataSystemCreateEvent",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemCreateEvent(CpuContext ctx)
    {
        var parameterAddress = ctx[CpuRegister.Rdi];
        if (parameterAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        var eventId = Interlocked.Increment(ref _nextEvent);
        lock (_eventGate)
        {
            _createdEvents.Add(eventId);
        }

        if (ctx.TryWriteInt32(ctx[CpuRegister.Rdx], eventId, checkNil: true) ||
            ctx.TryWriteInt32(ctx[CpuRegister.Rcx], eventId, checkNil: true))
        {
            return ctx.SetReturn(0, typeof(long));
        }

        lock (_eventGate)
        {
            _createdEvents.Remove(eventId);
        }

        return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
    }

    [SysAbiExport(
        Nid = "wG+84pnNIuo",
        ExportName = "sceNpUniversalDataSystemDestroyEvent",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemDestroyEvent(CpuContext ctx)
    {
        var eventId = unchecked((int)ctx[CpuRegister.Rdi]);
        lock (_eventGate)
        {
            _createdEvents.Remove(eventId);
        }

        return ctx.SetReturn(0, typeof(long));
    }

    // Kyty implements these as opaque host objects.  The guest ABI exposes a
    // pointer, so allocate a small zeroed guest object instead and retain its
    // allocator for lifetime validation/freeing.  The object contents are
    // deliberately private to the HLE; property setters consume it only as an
    // opaque identity.
    [SysAbiExport(
        Nid = "s6W4Zl4Slgk",
        ExportName = "sceNpUniversalDataSystemCreateEventPropertyObject",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemCreateEventPropertyObject(CpuContext ctx)
    {
        var outputAddress = ctx[CpuRegister.Rdi];
        if (outputAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        if (ctx.Memory is not IGuestMemoryAllocator allocator ||
            !allocator.TryAllocateGuestMemory(0x10, 0x10, out var objectAddress))
        {
            return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
        }

        Span<byte> emptyObject = stackalloc byte[0x10];
        emptyObject.Clear();
        if (!ctx.Memory.TryWrite(objectAddress, emptyObject) ||
            !ctx.TryWriteUInt64(outputAddress, objectAddress))
        {
            _ = allocator.TryFreeGuestMemory(objectAddress);
            return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
        }

        lock (_eventGate)
        {
            _eventPropertyObjects[objectAddress] = allocator;
        }

        return ctx.SetReturn(0, typeof(long));
    }

    [SysAbiExport(
        Nid = "kKUH0Viib3c",
        ExportName = "sceNpUniversalDataSystemDestroyEventPropertyObject",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemDestroyEventPropertyObject(CpuContext ctx)
    {
        var objectAddress = ctx[CpuRegister.Rdi];
        if (objectAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        IGuestMemoryAllocator? allocator;
        lock (_eventGate)
        {
            if (!_eventPropertyObjects.Remove(objectAddress, out allocator))
            {
                return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
            }
        }

        _ = allocator.TryFreeGuestMemory(objectAddress);
        return ctx.SetReturn(0, typeof(long));
    }

    [SysAbiExport(
        Nid = "MfDb+4Nln64",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetString",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetString(CpuContext ctx)
    {
        var propertyObjectAddress = ctx[CpuRegister.Rdi];
        var keyAddress = ctx[CpuRegister.Rsi];
        var valueAddress = ctx[CpuRegister.Rdx];
        if (!IsEventPropertyObject(propertyObjectAddress) || keyAddress == 0 || valueAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        Span<byte> probe = stackalloc byte[1];
        return ctx.Memory.TryRead(keyAddress, probe) &&
               ctx.Memory.TryRead(valueAddress, probe)
            ? ctx.SetReturn(0, typeof(long))
            : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
    }

    // The typed setters share the (object, UTF-8 key, scalar value) ABI. The
    // scalar lives entirely in registers, so only the opaque object identity
    // and key pointer need guest-memory validation.
    [SysAbiExport(
        Nid = "YE4dbtbz6OE",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetInt32",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetInt32(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "AzD4irAcKE4",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetUInt32",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetUInt32(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "56QLTqx911s",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetInt64",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetInt64(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "xvsP5Yz6FmY",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetUInt64",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetUInt64(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "lbPlT4+QVcE",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetFloat32",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetFloat32(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "4Fu8tHW+u-k",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetFloat64",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetFloat64(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "Fidd8vWgyVE",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetBool",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetBool(CpuContext ctx) =>
        SetEventPropertyObjectKeyValue(ctx);

    [SysAbiExport(
        Nid = "wAcxBDLHj1M",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetBinary",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetBinary(CpuContext ctx)
    {
        var propertyObjectAddress = ctx[CpuRegister.Rdi];
        var keyAddress = ctx[CpuRegister.Rsi];
        var valueAddress = ctx[CpuRegister.Rdx];
        var valueSize = ctx[CpuRegister.Rcx];
        if (!IsEventPropertyObject(propertyObjectAddress) || keyAddress == 0 ||
            (valueAddress == 0 && valueSize != 0))
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        if (valueSize != 0 && valueAddress > ulong.MaxValue - (valueSize - 1))
        {
            return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
        }

        Span<byte> probe = stackalloc byte[1];
        if (!ctx.Memory.TryRead(keyAddress, probe) ||
            (valueSize != 0 &&
             (!ctx.Memory.TryRead(valueAddress, probe) ||
              !ctx.Memory.TryRead(valueAddress + valueSize - 1, probe))))
        {
            return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
        }

        return ctx.SetReturn(0, typeof(long));
    }

    [SysAbiExport(
        Nid = "Wxbg5x3pTXA",
        ExportName = "sceNpUniversalDataSystemEventPropertyObjectSetArray",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyObjectSetArray(CpuContext ctx)
    {
        var propertyObjectAddress = ctx[CpuRegister.Rdi];
        var keyAddress = ctx[CpuRegister.Rsi];
        var valueAddress = ctx[CpuRegister.Rdx];
        if (!IsEventPropertyObject(propertyObjectAddress) || keyAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        Span<byte> probe = stackalloc byte[1];
        if (!ctx.Memory.TryRead(keyAddress, probe))
        {
            return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
        }

        if (valueAddress != 0 && !ctx.Memory.TryRead(valueAddress, probe))
        {
            return ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
        }

        return ctx.SetReturn(0, typeof(long));
    }

    [SysAbiExport(
        Nid = "CzkKf7ahIyU",
        ExportName = "sceNpUniversalDataSystemPostEvent",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemPostEvent(CpuContext ctx)
    {
        return ctx.SetReturn(0, typeof(long));
    }

    [SysAbiExport(
        Nid = "tpFJ8LIKvPw",
        ExportName = "sceNpUniversalDataSystemRegisterContext",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemRegisterContext(CpuContext ctx)
    {
        return ctx.SetReturn(0, typeof(long));
    }

    [SysAbiExport(
        Nid = "AUIHb7jUX3I",
        ExportName = "sceNpUniversalDataSystemDestroyHandle",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemDestroyHandle(CpuContext ctx)
    {
        return ctx.SetReturn(0, typeof(long));
    }

    // Telemetry property setter (event property array, string value). We do not
    // upload analytics, so accept and drop it — matching the other Set* stubs.
    [SysAbiExport(
        Nid = "4llLk7YJRTE",
        ExportName = "sceNpUniversalDataSystemEventPropertyArraySetString",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceNpUniversalDataSystem")]
    public static int NpUniversalDataSystemEventPropertyArraySetString(CpuContext ctx)
    {
        return ctx.SetReturn(0, typeof(long));
    }

    private static bool IsEventPropertyObject(ulong address)
    {
        lock (_eventGate)
        {
            return _eventPropertyObjects.ContainsKey(address);
        }
    }

    private static int SetEventPropertyObjectKeyValue(CpuContext ctx)
    {
        var propertyObjectAddress = ctx[CpuRegister.Rdi];
        var keyAddress = ctx[CpuRegister.Rsi];
        if (!IsEventPropertyObject(propertyObjectAddress) || keyAddress == 0)
        {
            return ctx.SetReturn(NpUniversalDataSystemErrorInvalidArgument, typeof(long));
        }

        Span<byte> probe = stackalloc byte[1];
        return ctx.Memory.TryRead(keyAddress, probe)
            ? ctx.SetReturn(0, typeof(long))
            : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT, typeof(long));
    }
}
