// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;

namespace SharpEmu.Libs.Psml;

public static class PsmlExports
{
    private const int ErrorNotInitialized = unchecked((int)0x8A810001u);
    private const int ErrorInvalidObject = unchecked((int)0x8A810005u);
    private const int ErrorInvalidPointer = unchecked((int)0x8A810009u);
    private const int ErrorInvalidValue = unchecked((int)0x8A81000Du);
    private const int ErrorNullObject = unchecked((int)0x8A810014u);
    private const uint SharedResourcesMagic = 0xA9C4;
    private const uint ContextMagic = 0x9231;
    private const ulong MainMemoryBlockSize = 0x20_0000;
    private const ulong MainMemoryAlignment = 0x20_0000;
    private const ulong ExtraVaBytes = 0x60_0000;
    private static bool _initialized;
    private static uint _supportedModes;
    private static int _sharedReferenceCount;

    [SysAbiExport(Nid = "3WVD91e12ZQ", ExportName = "scePsmlInitialize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int Initialize(CpuContext ctx)
    {
        _initialized = true;
        _supportedModes = 3;
        _sharedReferenceCount = 0;
        return Return(ctx, 0);
    }

    [SysAbiExport(Nid = "+2KpvixvL6E", ExportName = "scePsmlGetMainMemoryRequirements", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetMainMemoryRequirements(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        var output = ctx[CpuRegister.Rdi];
        var parameters = ctx[CpuRegister.Rsi];
        if (output == 0 || parameters == 0) return Return(ctx, ErrorInvalidPointer);
        if (!TryReadUInt32(ctx, parameters, out var type)) return Return(ctx, MemoryFault);
        if (!IsSupportedType(type)) return Return(ctx, ErrorInvalidValue);
        return WriteRequirements(ctx, output, RequiredBlockCount(type));
    }

    [SysAbiExport(Nid = "eWoKNeB6V-k", ExportName = "scePsmlSharedResourcesInitialize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int SharedResourcesInitialize(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        var resources = ctx[CpuRegister.Rdi];
        var parameters = ctx[CpuRegister.Rsi];
        if (resources == 0 || parameters == 0 ||
            !TryReadUInt32(ctx, parameters, out var type) ||
            !TryReadUInt32(ctx, parameters + 4, out var reserved) ||
            !TryReadUInt64(ctx, parameters + 8, out var blocks) ||
            !TryReadUInt64(ctx, parameters + 16, out var blockCount) ||
            !TryReadUInt64(ctx, parameters + 24, out var virtualAddressStart))
        {
            return Return(ctx, resources == 0 || parameters == 0 ? ErrorInvalidPointer : MemoryFault);
        }

        if (blocks == 0) return Return(ctx, ErrorInvalidPointer);
        if (reserved != 0 || !IsSupportedType(type) || blockCount < RequiredBlockCount(type))
            return Return(ctx, ErrorInvalidValue);
        if (!TryWriteUInt32(ctx, resources, SharedResourcesMagic) ||
            !ctx.TryWriteUInt64(resources + 8, blocks) ||
            !ctx.TryWriteUInt64(resources + 24, blockCount) ||
            !TryWriteUInt32(ctx, resources + 32, type) ||
            !ctx.TryWriteUInt64(resources + 40, virtualAddressStart))
            return Return(ctx, MemoryFault);

        Interlocked.Increment(ref _sharedReferenceCount);
        return Return(ctx, 0);
    }

    [SysAbiExport(Nid = "jEevBXmagOQ", ExportName = "scePsmlSharedResourcesFinalize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int SharedResourcesFinalize(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        if (ctx[CpuRegister.Rdi] == 0) return Return(ctx, ErrorNullObject);
        Interlocked.Decrement(ref _sharedReferenceCount);
        return Return(ctx, 0);
    }

    [SysAbiExport(Nid = "EO9YQXmJEN8", ExportName = "scePsmlGetContextMemoryRequirements", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetContextMemoryRequirements(CpuContext ctx) => GetContextRequirementsCore(ctx);
    [SysAbiExport(Nid = "IMlj247LdTo", ExportName = "scePsmlGetContextMemoryRequirements", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetContextMemoryRequirements2(CpuContext ctx) => GetContextRequirementsCore(ctx);
    [SysAbiExport(Nid = "sjTyYheKTrU", ExportName = "scePsmlGetContextMemoryRequirements", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetContextMemoryRequirements3(CpuContext ctx) => GetContextRequirementsCore(ctx);
    [SysAbiExport(Nid = "ArakEpzsZo0", ExportName = "scePsmlGetContextMemoryRequirements", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetContextMemoryRequirements4(CpuContext ctx) => GetContextRequirementsCore(ctx);
    [SysAbiExport(Nid = "VGjrQa-WqdU", ExportName = "scePsmlGetContextMemoryRequirements", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetContextMemoryRequirements5(CpuContext ctx) => GetContextRequirementsCore(ctx);

    [SysAbiExport(Nid = "2ecEbQaf9VU", ExportName = "scePsmlContextInitialize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int ContextInitialize(CpuContext ctx) => ContextInitializeCore(ctx);
    [SysAbiExport(Nid = "vUk2pWMx3KQ", ExportName = "scePsmlContextInitialize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int ContextInitialize2(CpuContext ctx) => ContextInitializeCore(ctx);
    [SysAbiExport(Nid = "gxv3i+MTEzU", ExportName = "scePsmlContextInitialize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int ContextInitialize3(CpuContext ctx) => ContextInitializeCore(ctx);

    [SysAbiExport(Nid = "JaLBe0P3jSU", ExportName = "scePsmlContextFinalize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int ContextFinalize(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        var context = ctx[CpuRegister.Rdi];
        if (!HasKnownObjectMagic(ctx, context)) return Return(ctx, ErrorInvalidObject);
        return TryWriteUInt32(ctx, context, 0) ? Return(ctx, 0) : Return(ctx, MemoryFault);
    }

    [SysAbiExport(Nid = "AHalTX9wFZY", ExportName = "scePsmlGetWorkAreaSize", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetWorkAreaSize(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        if (!HasKnownObjectMagic(ctx, ctx[CpuRegister.Rdi])) return Return(ctx, ErrorInvalidObject);
        var output = ctx[CpuRegister.Rsi];
        return output == 0 ? Return(ctx, ErrorInvalidPointer) :
            TryWriteUInt32(ctx, output, 0x600) ? Return(ctx, 0) : Return(ctx, MemoryFault);
    }

    [SysAbiExport(Nid = "RUNLFro+qok", ExportName = "scePsmlDispatch", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int Dispatch(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        if (!HasKnownObjectMagic(ctx, ctx[CpuRegister.Rdi])) return Return(ctx, ErrorInvalidObject);
        return ctx[CpuRegister.Rsi] == 0 || ctx[CpuRegister.Rdx] == 0
            ? Return(ctx, ErrorInvalidPointer) : Return(ctx, 0);
    }

    [SysAbiExport(Nid = "GHna9-DvnUk", ExportName = "scePsmlGetProgress", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int GetProgress(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        if (!HasKnownObjectMagic(ctx, ctx[CpuRegister.Rdi])) return Return(ctx, ErrorInvalidObject);
        var output = ctx[CpuRegister.Rsi];
        return output == 0 ? Return(ctx, ErrorInvalidPointer) :
            TryWriteUInt32(ctx, output, 0) ? Return(ctx, 0) : Return(ctx, MemoryFault);
    }

    [SysAbiExport(Nid = "GJY0MvuTcs8", ExportName = "scePsmlRequestCapture", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int RequestCapture(CpuContext ctx) => ValidateObjectCore(ctx);
    [SysAbiExport(Nid = "LXq+6mIxpCw", ExportName = "scePsmlValidateObject", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int ValidateObject(CpuContext ctx) => ValidateObjectCore(ctx);
    [SysAbiExport(Nid = "FSGaTQze0UY", ExportName = "scePsmlValidateObject", Target = Generation.Gen5, LibraryName = "libScePsml")]
    public static int ValidateObject2(CpuContext ctx) => ValidateObjectCore(ctx);

    private static int GetContextRequirementsCore(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        var output = ctx[CpuRegister.Rdi];
        return output == 0 ? Return(ctx, ErrorInvalidPointer) : WriteRequirements(ctx, output, 1);
    }

    private static int ContextInitializeCore(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        var context = ctx[CpuRegister.Rdi];
        var parameters = ctx[CpuRegister.Rsi];
        if (context == 0 || parameters == 0) return Return(ctx, ErrorInvalidPointer);
        if (!TryReadUInt64(ctx, parameters + 8, out var sharedResources)) return Return(ctx, MemoryFault);
        if (sharedResources == 0) return Return(ctx, ErrorInvalidPointer);
        if (!TryWriteUInt32(ctx, context, ContextMagic) ||
            !ctx.TryWriteUInt64(context + 0x360, sharedResources) ||
            !TryWriteByte(ctx, context + 0x368, 0)) return Return(ctx, MemoryFault);
        return Return(ctx, 0);
    }

    private static int ValidateObjectCore(CpuContext ctx)
    {
        if (!IsInitialized(ctx, out var error)) return error;
        return HasKnownObjectMagic(ctx, ctx[CpuRegister.Rdi]) ? Return(ctx, 0) : Return(ctx, ErrorInvalidObject);
    }

    private static bool IsInitialized(CpuContext ctx, out int error)
    {
        error = _initialized ? 0 : ErrorNotInitialized;
        if (error != 0) ctx.SetReturn(error);
        return error == 0;
    }

    private static bool IsSupportedType(uint type) => type switch
    {
        0 => true,
        1 => (_supportedModes & 1) != 0,
        2 => (_supportedModes & 2) != 0,
        _ => false,
    };

    private static ulong RequiredBlockCount(uint type) => type switch
    {
        0 => (0x0620_0000UL + ExtraVaBytes) / MainMemoryBlockSize,
        1 => (0x1820_0000UL + ExtraVaBytes) / MainMemoryBlockSize,
        2 => (0x1220_0000UL + ExtraVaBytes) / MainMemoryBlockSize,
        _ => 0,
    };

    private static int WriteRequirements(CpuContext ctx, ulong output, ulong count) =>
        ctx.TryWriteUInt64(output, MainMemoryBlockSize) &&
        ctx.TryWriteUInt64(output + 8, MainMemoryAlignment) &&
        ctx.TryWriteUInt64(output + 16, count) ? Return(ctx, 0) : Return(ctx, MemoryFault);

    private static bool HasKnownObjectMagic(CpuContext ctx, ulong address) =>
        address != 0 && TryReadUInt32(ctx, address, out var magic) &&
        (magic == SharedResourcesMagic || magic == ContextMagic);

    private static bool TryReadUInt32(CpuContext ctx, ulong address, out uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        if (!ctx.Memory.TryRead(address, bytes)) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        return true;
    }

    private static bool TryReadUInt64(CpuContext ctx, ulong address, out ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        if (!ctx.Memory.TryRead(address, bytes)) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return true;
    }

    private static bool TryWriteUInt32(CpuContext ctx, ulong address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return ctx.Memory.TryWrite(address, bytes);
    }

    private static bool TryWriteByte(CpuContext ctx, ulong address, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        return ctx.Memory.TryWrite(address, bytes);
    }

    private static int Return(CpuContext ctx, int value) => ctx.SetReturn(value);
    private const int MemoryFault = (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
}
