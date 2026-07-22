// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.SystemGesture;
using Xunit;

namespace SharpEmu.Libs.Tests.SystemGesture;

public sealed class SystemGestureExportsTests
{
    private const ulong MemoryBase = 0x1_0000_0000;
    private const int ErrorInvalidArgument = unchecked((int)0x80890001);
    private const int ErrorEventDataNotFound = unchecked((int)0x80890005);
    private const int ErrorInvalidHandle = unchecked((int)0x80890006);

    [Fact]
    public void OpenAcceptsOnlyTouchPadAndReturnsStableHandle()
    {
        var context = CreateContext(1);
        context[CpuRegister.Rdi] = 0;
        Assert.Equal(1, SystemGestureExports.SystemGestureOpen(context));
        Assert.Equal(1UL, context[CpuRegister.Rax]);

        context[CpuRegister.Rdi] = 1;
        Assert.Equal(ErrorInvalidArgument, SystemGestureExports.SystemGestureOpen(context));
    }

    [Fact]
    public void CreateRecognizerValidatesHandleAndClearsFullAbiObject()
    {
        const int recognizerSize = sizeof(ulong) * 361;
        var memory = new FakeCpuMemory(MemoryBase, recognizerSize);
        var context = new CpuContext(memory, Generation.Gen5);
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, recognizerSize).ToArray()));
        context[CpuRegister.Rdi] = 2;
        context[CpuRegister.Rsi] = MemoryBase;
        Assert.Equal(ErrorInvalidHandle, SystemGestureExports.SystemGestureCreateTouchRecognizer(context));

        context[CpuRegister.Rdi] = 1;
        Assert.Equal(0, SystemGestureExports.SystemGestureCreateTouchRecognizer(context));
        Span<byte> recognizer = stackalloc byte[recognizerSize];
        Assert.True(memory.TryRead(MemoryBase, recognizer));
        Assert.All(recognizer.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void PrimitiveEventQueryClearsOutputsWhenNoEventsExist()
    {
        const int primitiveEventSize = 80;
        var memory = new FakeCpuMemory(MemoryBase, primitiveEventSize + sizeof(int));
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = MemoryBase;
        context[CpuRegister.Rdx] = 1;
        context[CpuRegister.Rcx] = MemoryBase + primitiveEventSize;
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, primitiveEventSize + sizeof(int)).ToArray()));

        Assert.Equal(0, SystemGestureExports.SystemGestureGetPrimitiveTouchEvents(context));
        Span<byte> output = stackalloc byte[primitiveEventSize + sizeof(int)];
        Assert.True(memory.TryRead(MemoryBase, output));
        Assert.All(output.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void MissingTouchEventClearsProvidedObjectAndReportsNoData()
    {
        const int touchEventSize = 168;
        var memory = new FakeCpuMemory(MemoryBase, touchEventSize);
        var context = new CpuContext(memory, Generation.Gen5);
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, touchEventSize).ToArray()));
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = MemoryBase;
        context[CpuRegister.Rcx] = MemoryBase;

        Assert.Equal(ErrorEventDataNotFound, SystemGestureExports.SystemGestureGetTouchEventByIndex(context));
        Span<byte> output = stackalloc byte[touchEventSize];
        Assert.True(memory.TryRead(MemoryBase, output));
        Assert.All(output.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void TouchEventQueryRequiresRecognizerAndCountPointer()
    {
        var context = CreateContext(1);
        context[CpuRegister.Rdi] = 1;
        Assert.Equal(ErrorInvalidArgument, SystemGestureExports.SystemGestureGetTouchEvents(context));

        context[CpuRegister.Rsi] = MemoryBase;
        Assert.Equal(ErrorInvalidArgument, SystemGestureExports.SystemGestureGetTouchEvents(context));
    }

    private static CpuContext CreateContext(int size) => new(new FakeCpuMemory(MemoryBase, size), Generation.Gen5);
}
