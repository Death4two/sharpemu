// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace SharpEmu.Libs.AppContent;

public static class AppContentExports
{
    private const string Temp0MountPoint = "/temp0";
    private const uint AppParamSkuFlag = 0;
    private const int AppParamSkuFlagFull = 3;
    private const int AppContentErrorParameter = unchecked((int)0x80D90002);
    private const int AppContentErrorNotMounted = unchecked((int)0x80D90004);
    private const int AppContentErrorDrmNoEntitlement = unchecked((int)0x80D90007);
    private const int MountPointSize = 16;

    [SysAbiExport(
        Nid = "R9lA82OraNs",
        ExportName = "sceAppContentInitialize",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentInitialize(CpuContext ctx)
    {
        var bootParamAddress = ctx[CpuRegister.Rsi];
        // Accept a null init parameter and an optional boot parameter.
        if (bootParamAddress != 0)
        {
            Span<byte> bootParameter = stackalloc byte[40];
            bootParameter.Clear();
            if (!ctx.Memory.TryWrite(bootParamAddress, bootParameter))
            {
                return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
            }
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "xnd8BJzAxmk",
        ExportName = "sceAppContentGetAddcontInfoList",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentGetAddcontInfoList(CpuContext ctx)
    {
        var hitCountAddress = ctx[CpuRegister.Rcx];
        if (hitCountAddress != 0)
        {
            Span<byte> hitCountBytes = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(hitCountBytes, 0);
            if (!ctx.Memory.TryWrite(hitCountAddress, hitCountBytes))
            {
                return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
            }
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "99b82IKXpH4",
        ExportName = "sceAppContentAppParamGetInt",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentAppParamGetInt(CpuContext ctx)
    {
        var paramId = (uint)ctx[CpuRegister.Rdi];
        var valueAddress = ctx[CpuRegister.Rsi];
        if (valueAddress == 0)
        {
            return ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        int value;
        if (paramId == AppParamSkuFlag)
        {
            value = AppParamSkuFlagFull;
        }
        else if (!TryReadUserDefinedParam(paramId, out value))
        {
            return ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT);
        }

        Span<byte> valueBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(valueBytes, value);
        if (!ctx.Memory.TryWrite(valueAddress, valueBytes))
        {
            return ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        TraceAppContent($"app_param_get_int id={paramId} value={value}");
        return ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_OK);
    }

    [SysAbiExport(
        Nid = "buYbeLOGWmA",
        ExportName = "sceAppContentTemporaryDataMount2",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentTemporaryDataMount2(CpuContext ctx)
    {
        var mountPointAddress = ctx[CpuRegister.Rsi];
        if (mountPointAddress == 0)
        {
            return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_INVALID_ARGUMENT;
        }

        Span<byte> mountPointBytes = stackalloc byte[MountPointSize];
        mountPointBytes.Clear();
        Encoding.ASCII.GetBytes(Temp0MountPoint, mountPointBytes);
        if (!ctx.Memory.TryWrite(mountPointAddress, mountPointBytes))
        {
            return (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
        }

        ctx[CpuRegister.Rax] = 0;
        return (int)OrbisGen2Result.ORBIS_GEN2_OK;
    }

    [SysAbiExport(
        Nid = "a5N7lAG0y2Q",
        ExportName = "sceAppContentTemporaryDataFormat",
        Target = Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentTemporaryDataFormat(CpuContext ctx) =>
        ctx[CpuRegister.Rdi] == 0 ? ctx.SetReturn(AppContentErrorParameter) : ctx.SetReturn(0);

    [SysAbiExport(
        Nid = "VANhIWcqYak",
        ExportName = "sceAppContentAddcontMount",
        Target = Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentAddcontMount(CpuContext ctx)
    {
        var entitlementLabelAddress = ctx[CpuRegister.Rsi];
        var mountPointAddress = ctx[CpuRegister.Rdx];
        if (entitlementLabelAddress == 0 || mountPointAddress == 0)
        {
            return ctx.SetReturn(AppContentErrorParameter);
        }

        return ctx.Memory.TryWrite(mountPointAddress, new byte[MountPointSize])
            ? ctx.SetReturn(AppContentErrorDrmNoEntitlement)
            : ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

    [SysAbiExport(
        Nid = "3rHWaV-1KC4",
        ExportName = "sceAppContentAddcontUnmount",
        Target = Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentAddcontUnmount(CpuContext ctx)
    {
        var mountPointAddress = ctx[CpuRegister.Rdi];
        if (mountPointAddress == 0)
        {
            return ctx.SetReturn(AppContentErrorParameter);
        }

        Span<byte> firstByte = stackalloc byte[1];
        if (!ctx.Memory.TryRead(mountPointAddress, firstByte))
        {
            return ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }

        return ctx.SetReturn(firstByte[0] == 0 ? AppContentErrorNotMounted : 0);
    }

    [SysAbiExport(
        Nid = "SaKib2Ug0yI",
        ExportName = "sceAppContentTemporaryDataGetAvailableSpaceKb",
        Target = Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentTemporaryDataGetAvailableSpaceKb(CpuContext ctx)
    {
        var mountPointAddress = ctx[CpuRegister.Rdi];
        var availableSpaceAddress = ctx[CpuRegister.Rsi];
        if (mountPointAddress == 0 || availableSpaceAddress == 0 ||
            !TryReadMountPoint(ctx, mountPointAddress, out var mountPoint) ||
            !string.Equals(mountPoint, Temp0MountPoint, StringComparison.Ordinal))
        {
            return ctx.SetReturn(AppContentErrorParameter);
        }

        try
        {
            var root = Path.GetPathRoot(Environment.CurrentDirectory);
            var availableKb = (ulong)new DriveInfo(root!).AvailableFreeSpace / 1024UL;
            Span<byte> spaceBytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(spaceBytes, availableKb);
            return ctx.Memory.TryWrite(availableSpaceAddress, spaceBytes)
                ? ctx.SetReturn(0)
                : ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }
        catch (IOException) { return ctx.SetReturn(AppContentErrorParameter); }
        catch (UnauthorizedAccessException) { return ctx.SetReturn(AppContentErrorParameter); }
    }

    [SysAbiExport(
        Nid = "Gl6w5i0JokY",
        ExportName = "sceAppContentDownloadDataGetAvailableSpaceKb",
        Target = Generation.Gen4 | Generation.Gen5,
        LibraryName = "libSceAppContent")]
    public static int AppContentDownloadDataGetAvailableSpaceKb(CpuContext ctx)
    {
        var availableSpaceAddress = ctx[CpuRegister.Rsi];
        if (availableSpaceAddress == 0)
        {
            return ctx.SetReturn(AppContentErrorParameter);
        }

        try
        {
            var root = Path.GetPathRoot(Environment.CurrentDirectory);
            var availableSpaceKb = (ulong)new DriveInfo(root!).AvailableFreeSpace / 1024UL;
            Span<byte> spaceBytes = stackalloc byte[sizeof(ulong)];
            BinaryPrimitives.WriteUInt64LittleEndian(spaceBytes, availableSpaceKb);
            return ctx.Memory.TryWrite(availableSpaceAddress, spaceBytes)
                ? ctx.SetReturn(0)
                : ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
        }
        catch (IOException)
        {
            return WriteFallbackDownloadSpace(ctx, availableSpaceAddress);
        }
        catch (UnauthorizedAccessException)
        {
            return WriteFallbackDownloadSpace(ctx, availableSpaceAddress);
        }
    }

    private static bool TryReadUserDefinedParam(uint paramId, out int value)
    {
        value = 0;
        if (paramId is < 1 or > 4)
        {
            return false;
        }

        var app0Root = Environment.GetEnvironmentVariable("SHARPEMU_APP0_DIR");
        if (string.IsNullOrWhiteSpace(app0Root))
        {
            return true;
        }

        var paramJsonPath = Path.Combine(app0Root, "sce_sys", "param.json");
        if (!File.Exists(paramJsonPath))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(paramJsonPath);
            using var document = JsonDocument.Parse(stream);
            var propertyName = $"userDefinedParam{paramId}";
            if (document.RootElement.TryGetProperty(propertyName, out var element) &&
                element.TryGetInt32(out var parsedValue))
            {
                value = parsedValue;
            }

            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static bool TryReadMountPoint(CpuContext ctx, ulong address, out string mountPoint)
    {
        Span<byte> bytes = stackalloc byte[MountPointSize];
        if (!ctx.Memory.TryRead(address, bytes))
        {
            mountPoint = string.Empty;
            return false;
        }

        var terminator = bytes.IndexOf((byte)0);
        mountPoint = Encoding.ASCII.GetString(bytes[..(terminator < 0 ? bytes.Length : terminator)]);
        return true;
    }

    private static void TraceAppContent(string message)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SHARPEMU_LOG_APP_CONTENT"), "1", StringComparison.Ordinal))
        {
            return;
        }

        Console.Error.WriteLine($"[LOADER][TRACE] app_content.{message}");
    }

    private static int WriteFallbackDownloadSpace(CpuContext ctx, ulong outputAddress)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, 1024UL * 1024UL);
        return ctx.Memory.TryWrite(outputAddress, bytes)
            ? ctx.SetReturn(0)
            : ctx.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
    }

}
