// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;

namespace SharpEmu.Libs.Pad;

/// <summary>Kyty-compatible libSceKeyboard exports for the hosted no-input path.</summary>
public static class KeyboardExports
{
    private const int ErrorInvalidArgument = unchecked((int)0x80DA0001);
    private const int ErrorInvalidHandle = unchecked((int)0x80DA0003);
    private const int KeyboardDataSize = 96;
    private const int KeyboardCharDataSize = 20;
    private const int MaxDataCount = 16;

    [SysAbiExport(Nid = "wadT3QBCGY0", ExportName = "sceKeyboardInit", Target = Generation.Gen5, LibraryName = "libSceKeyboard")]
    public static int KeyboardInit(CpuContext ctx) => ctx.SetReturn(0);

    [SysAbiExport(Nid = "HJ+KnEHcaxI", ExportName = "sceKeyboardOpen", Target = Generation.Gen5, LibraryName = "libSceKeyboard")]
    public static int KeyboardOpen(CpuContext ctx)
    {
        var type = unchecked((int)ctx[CpuRegister.Rsi]);
        var index = unchecked((int)ctx[CpuRegister.Rdx]);
        return ctx.SetReturn(type == 0 && index is >= 0 and < 2 ? 1 : ErrorInvalidArgument);
    }

    [SysAbiExport(Nid = "0LWei+c7RNc", ExportName = "sceKeyboardClose", Target = Generation.Gen5, LibraryName = "libSceKeyboard")]
    public static int KeyboardClose(CpuContext ctx) => ctx.SetReturn(IsValidHandle(ctx) ? 0 : ErrorInvalidHandle);

    [SysAbiExport(Nid = "6HpE68bzX6M", ExportName = "sceKeyboardReadState", Target = Generation.Gen5, LibraryName = "libSceKeyboard")]
    public static int KeyboardReadState(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        var dataAddress = ctx[CpuRegister.Rsi];
        return dataAddress == 0 ? ctx.SetReturn(ErrorInvalidArgument) : Clear(ctx, dataAddress, KeyboardDataSize);
    }

    [SysAbiExport(Nid = "xybbGMCr738", ExportName = "sceKeyboardRead", Target = Generation.Gen5, LibraryName = "libSceKeyboard")]
    public static int KeyboardRead(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        var dataAddress = ctx[CpuRegister.Rsi];
        var count = unchecked((int)ctx[CpuRegister.Rdx]);
        if (dataAddress == 0 || count is <= 0 or > MaxDataCount) return ctx.SetReturn(ErrorInvalidArgument);
        return Clear(ctx, dataAddress, checked(KeyboardDataSize * count));
    }

    [SysAbiExport(Nid = "yO9JwdRhtSA", ExportName = "sceKeyboardGetKey2Char", Target = Generation.Gen5, LibraryName = "libSceKeyboard")]
    public static int KeyboardGetKey2Char(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        var mapping = unchecked((int)ctx[CpuRegister.Rsi]);
        var charDataAddress = ctx[CpuRegister.R9];
        if (charDataAddress == 0 || mapping is not (0 or 1)) return ctx.SetReturn(ErrorInvalidArgument);
        return Clear(ctx, charDataAddress, KeyboardCharDataSize);
    }

    private static bool IsValidHandle(CpuContext ctx) => unchecked((int)ctx[CpuRegister.Rdi]) > 0;

    private static int Clear(CpuContext ctx, ulong address, int size) =>
        ctx.Memory.TryWrite(address, new byte[size])
            ? ctx.SetReturn(0)
            : ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
}
