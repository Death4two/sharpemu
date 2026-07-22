// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Pad;
using Xunit;

namespace SharpEmu.Libs.Tests.Pad;

public sealed class KeyboardExportsTests
{
    private const ulong MemoryBase = 0x1_0000_0000;
    private const int ErrorInvalidArgument = unchecked((int)0x80DA0001);
    private const int ErrorInvalidHandle = unchecked((int)0x80DA0003);

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, ErrorInvalidArgument)]
    [InlineData(0, 2, ErrorInvalidArgument)]
    public void OpenValidatesKytyKeyboardTypeAndIndex(int type, int index, int expected)
    {
        var context = Context(1);
        context[CpuRegister.Rsi] = unchecked((uint)type);
        context[CpuRegister.Rdx] = unchecked((uint)index);
        Assert.Equal(expected, KeyboardExports.KeyboardOpen(context));
    }

    [Fact]
    public void ReadStateRequiresHandleAndClearsAbiObject()
    {
        const int dataSize = 96;
        var memory = new FakeCpuMemory(MemoryBase, dataSize);
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, dataSize).ToArray()));
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rsi] = MemoryBase;
        Assert.Equal(ErrorInvalidHandle, KeyboardExports.KeyboardReadState(context));

        context[CpuRegister.Rdi] = 1;
        Assert.Equal(0, KeyboardExports.KeyboardReadState(context));
        Span<byte> data = stackalloc byte[dataSize];
        Assert.True(memory.TryRead(MemoryBase, data));
        Assert.All(data.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void ReadChecksCountBeforeGuestWrite()
    {
        var context = Context(1);
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = MemoryBase;
        context[CpuRegister.Rdx] = 17;
        Assert.Equal(ErrorInvalidArgument, KeyboardExports.KeyboardRead(context));
    }

    [Fact]
    public void GetKey2CharClearsAbiOutput()
    {
        const int charDataSize = 20;
        var memory = new FakeCpuMemory(MemoryBase, charDataSize);
        var context = new CpuContext(memory, Generation.Gen5);
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, charDataSize).ToArray()));
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.R9] = MemoryBase;
        Assert.Equal(0, KeyboardExports.KeyboardGetKey2Char(context));
        Span<byte> data = stackalloc byte[charDataSize];
        Assert.True(memory.TryRead(MemoryBase, data));
        Assert.All(data.ToArray(), value => Assert.Equal(0, value));
    }

    private static CpuContext Context(int size) => new(new FakeCpuMemory(MemoryBase, size), Generation.Gen5);
}
