// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System.Buffers.Binary;

namespace SharpEmu.Libs.SystemGesture;

/// <summary>Hosted implementation of the documented libSceSystemGesture no-input ABI.</summary>
public static class SystemGestureExports
{
    private const int TouchPadInput = 0;
    private const int Handle = 1;
    private const int ErrorInvalidArgument = unchecked((int)0x80890001);
    private const int ErrorEventDataNotFound = unchecked((int)0x80890005);
    private const int ErrorInvalidHandle = unchecked((int)0x80890006);
    private const int PrimitiveTouchEventSize = 80;
    private const int TouchRecognizerSize = sizeof(ulong) * 361;
    private const int TouchRecognizerInformationSize = 296;
    private const int TouchEventSize = 168;

    [SysAbiExport(Nid = "qpo-mEOwje0", ExportName = "sceSystemGestureOpen", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureOpen(CpuContext ctx) => ctx.SetReturn(
        unchecked((int)ctx[CpuRegister.Rdi]) == TouchPadInput ? Handle : ErrorInvalidArgument);

    [SysAbiExport(Nid = "j4yXIA2jJ68", ExportName = "sceSystemGestureClose", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureClose(CpuContext ctx) => ReturnForHandle(ctx);

    [SysAbiExport(Nid = "3pcAvmwKCvM", ExportName = "sceSystemGestureInitializePrimitiveTouchRecognizer", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureInitializePrimitiveTouchRecognizer(CpuContext ctx) => ctx.SetReturn(0);

    [SysAbiExport(Nid = "3QYCmMlOlCY", ExportName = "sceSystemGestureFinalizePrimitiveTouchRecognizer", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureFinalizePrimitiveTouchRecognizer(CpuContext ctx) => ctx.SetReturn(0);

    [SysAbiExport(Nid = "o11J529VaAE", ExportName = "sceSystemGestureResetPrimitiveTouchRecognizer", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureResetPrimitiveTouchRecognizer(CpuContext ctx) => ReturnForHandle(ctx);

    [SysAbiExport(Nid = "GgFMb22sbbI", ExportName = "sceSystemGestureUpdatePrimitiveTouchRecognizer", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureUpdatePrimitiveTouchRecognizer(CpuContext ctx) => ReturnForHandle(ctx);

    [SysAbiExport(Nid = "L8YmemOeSNY", ExportName = "sceSystemGestureGetPrimitiveTouchEvents", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetPrimitiveTouchEvents(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        var countAddress = ctx[CpuRegister.Rcx];
        if (countAddress == 0) return ctx.SetReturn(ErrorInvalidArgument);
        if (!WriteInt32(ctx, countAddress, 0)) return MemoryFault(ctx);
        return ClearWhenProvided(ctx, ctx[CpuRegister.Rsi], ctx[CpuRegister.Rdx], PrimitiveTouchEventSize);
    }

    [SysAbiExport(Nid = "JhwByySf9FY", ExportName = "sceSystemGestureGetPrimitiveTouchEventsCount", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetPrimitiveTouchEventsCount(CpuContext ctx) => ReturnForHandle(ctx);

    [SysAbiExport(Nid = "KAeP0+cQPVU", ExportName = "sceSystemGestureGetPrimitiveTouchEventByIndex", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetPrimitiveTouchEventByIndex(CpuContext ctx) => GetMissingPrimitiveEvent(ctx, ctx[CpuRegister.Rdx]);

    [SysAbiExport(Nid = "yBaQ0h9m1NM", ExportName = "sceSystemGestureGetPrimitiveTouchEventByPrimitiveID", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetPrimitiveTouchEventByPrimitiveId(CpuContext ctx) => GetMissingPrimitiveEvent(ctx, ctx[CpuRegister.Rdx]);

    [SysAbiExport(Nid = "FWF8zkhr854", ExportName = "sceSystemGestureCreateTouchRecognizer", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureCreateTouchRecognizer(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        var recognizerAddress = ctx[CpuRegister.Rsi];
        return recognizerAddress == 0 ? ctx.SetReturn(ErrorInvalidArgument) : Clear(ctx, recognizerAddress, TouchRecognizerSize);
    }

    [SysAbiExport(Nid = "1MMK0W-kMgA", ExportName = "sceSystemGestureAppendTouchRecognizer", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureAppendTouchRecognizer(CpuContext ctx) => ReturnForRecognizer(ctx);

    [SysAbiExport(Nid = "ELvBVG-LKT0", ExportName = "sceSystemGestureRemoveTouchRecognizer", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureRemoveTouchRecognizer(CpuContext ctx) => ReturnForRecognizer(ctx);

    [SysAbiExport(Nid = "oBuH3zFWYIg", ExportName = "sceSystemGestureResetTouchRecognizer", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureResetTouchRecognizer(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        var recognizerAddress = ctx[CpuRegister.Rsi];
        return recognizerAddress == 0 ? ctx.SetReturn(ErrorInvalidArgument) : Clear(ctx, recognizerAddress, TouchRecognizerSize);
    }

    [SysAbiExport(Nid = "0KrW5eMnrwY", ExportName = "sceSystemGestureGetTouchRecognizerInformation", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetTouchRecognizerInformation(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        return ctx[CpuRegister.Rsi] == 0 || ctx[CpuRegister.Rdx] == 0
            ? ctx.SetReturn(ErrorInvalidArgument)
            : Clear(ctx, ctx[CpuRegister.Rdx], TouchRecognizerInformationSize);
    }

    [SysAbiExport(Nid = "j4h82CQWENo", ExportName = "sceSystemGestureUpdateTouchRecognizer", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureUpdateTouchRecognizer(CpuContext ctx) => ReturnForRecognizer(ctx);

    [SysAbiExport(Nid = "wPJGwI2RM2I", ExportName = "sceSystemGestureUpdateAllTouchRecognizer", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureUpdateAllTouchRecognizer(CpuContext ctx) => ReturnForHandle(ctx);

    [SysAbiExport(Nid = "4WOA1eTx3V8", ExportName = "sceSystemGestureUpdateTouchRecognizerRectangle", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureUpdateTouchRecognizerRectangle(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        return ctx[CpuRegister.Rsi] == 0 || ctx[CpuRegister.Rdx] == 0 ? ctx.SetReturn(ErrorInvalidArgument) : ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "fLTseA7XiWY", ExportName = "sceSystemGestureGetTouchEvents", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetTouchEvents(CpuContext ctx)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        if (ctx[CpuRegister.Rsi] == 0 || ctx[CpuRegister.R8] == 0) return ctx.SetReturn(ErrorInvalidArgument);
        if (!WriteInt32(ctx, ctx[CpuRegister.R8], 0)) return MemoryFault(ctx);
        return ClearWhenProvided(ctx, ctx[CpuRegister.Rdx], ctx[CpuRegister.Rcx], TouchEventSize);
    }

    [SysAbiExport(Nid = "h8uongcBNVs", ExportName = "sceSystemGestureGetTouchEventsCount", Target = Generation.Gen4 | Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetTouchEventsCount(CpuContext ctx) => ReturnForRecognizer(ctx);

    [SysAbiExport(Nid = "TSKvgSz5ChU", ExportName = "sceSystemGestureGetTouchEventByIndex", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetTouchEventByIndex(CpuContext ctx) => GetMissingTouchEvent(ctx, ctx[CpuRegister.Rcx]);

    [SysAbiExport(Nid = "lpsXm7tzeoc", ExportName = "sceSystemGestureGetTouchEventByEventID", Target = Generation.Gen5, LibraryName = "libSceSystemGesture")]
    public static int SystemGestureGetTouchEventByEventId(CpuContext ctx) => GetMissingTouchEvent(ctx, ctx[CpuRegister.Rcx]);

    private static bool IsValidHandle(CpuContext ctx) => unchecked((int)ctx[CpuRegister.Rdi]) == Handle;
    private static int ReturnForHandle(CpuContext ctx) => ctx.SetReturn(IsValidHandle(ctx) ? 0 : ErrorInvalidHandle);
    private static int ReturnForRecognizer(CpuContext ctx) => !IsValidHandle(ctx) ? ctx.SetReturn(ErrorInvalidHandle) : ctx.SetReturn(ctx[CpuRegister.Rsi] != 0 ? 0 : ErrorInvalidArgument);

    private static int GetMissingPrimitiveEvent(CpuContext ctx, ulong eventAddress)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        if (eventAddress != 0 && Clear(ctx, eventAddress, PrimitiveTouchEventSize) != 0) return MemoryFault(ctx);
        return ctx.SetReturn(ErrorEventDataNotFound);
    }

    private static int GetMissingTouchEvent(CpuContext ctx, ulong eventAddress)
    {
        if (!IsValidHandle(ctx)) return ctx.SetReturn(ErrorInvalidHandle);
        if (ctx[CpuRegister.Rsi] == 0) return ctx.SetReturn(ErrorInvalidArgument);
        if (eventAddress != 0 && Clear(ctx, eventAddress, TouchEventSize) != 0) return MemoryFault(ctx);
        return ctx.SetReturn(ErrorEventDataNotFound);
    }

    private static int ClearWhenProvided(CpuContext ctx, ulong address, ulong capacity, int size) =>
        address == 0 || capacity == 0 ? ctx.SetReturn(0) : Clear(ctx, address, size);

    private static int Clear(CpuContext ctx, ulong address, int size) =>
        ctx.Memory.TryWrite(address, new byte[size]) ? ctx.SetReturn(0) : MemoryFault(ctx);

    private static bool WriteInt32(CpuContext ctx, ulong address, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        return ctx.Memory.TryWrite(address, bytes);
    }

    private static int MemoryFault(CpuContext ctx) => ctx.SetReturn((int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);
}
