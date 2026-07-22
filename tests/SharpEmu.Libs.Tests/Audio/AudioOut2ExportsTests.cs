// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Audio;
using Xunit;

namespace SharpEmu.Libs.Tests.Audio;

public sealed class AudioOut2ExportsTests
{
    private const ulong Base = 0x6_0000_0000;

    [Fact]
    public void ContextParameterAndMemoryQuery_MatchKyty()
    {
        var memory = new FakeCpuMemory(Base, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var parameter = Base;
        var sizeOut = Base + 0x100;
        context[CpuRegister.Rdi] = parameter;
        Assert.Equal(0, AudioOut2Exports.AudioOut2ContextResetParam(context));

        Assert.Equal(256U, Read32(memory, parameter));
        Assert.Equal(256U, Read32(memory, parameter + 4));
        Assert.Equal(4U, Read32(memory, parameter + 12));
        Assert.Equal(512U, Read32(memory, parameter + 16));
        Assert.Equal(1U, Read32(memory, parameter + 20));

        context[CpuRegister.Rdi] = parameter;
        context[CpuRegister.Rsi] = sizeOut;
        Assert.Equal(0, AudioOut2Exports.AudioOut2ContextQueryMemory(context));
        Assert.Equal(0x10000UL + 4UL * 0x590UL, Read64(memory, sizeOut));
    }

    [Fact]
    public void ContextCreate_AcceptsKytyOptionalWorkBuffer()
    {
        var memory = new FakeCpuMemory(Base, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var parameter = Base;
        var contextOut = Base + 0x100;
        context[CpuRegister.Rdi] = parameter;
        Assert.Equal(0, AudioOut2Exports.AudioOut2ContextResetParam(context));

        context[CpuRegister.Rdi] = parameter;
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.Rdx] = 0;
        context[CpuRegister.Rcx] = contextOut;
        Assert.Equal(0, AudioOut2Exports.AudioOut2ContextCreate(context));
        Assert.NotEqual(0UL, Read64(memory, contextOut));
    }

    [Fact]
    public void PortCreateAndState_UseKytyAbi()
    {
        var memory = new FakeCpuMemory(Base, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var parameter = Base;
        var portOut = Base + 0x100;
        var state = Base + 0x200;
        // port_type=main, data format advertises six channels at bits 8..15.
        Write32(memory, parameter + 4, 6U << 8);
        Write32(memory, parameter + 8, 48_000);
        context[CpuRegister.Rdi] = 1;
        context[CpuRegister.Rsi] = parameter;
        context[CpuRegister.Rdx] = portOut;
        Assert.Equal(0, AudioOut2Exports.AudioOut2PortCreate(context));
        var port = Read64(memory, portOut);

        context[CpuRegister.Rdi] = port;
        context[CpuRegister.Rsi] = state;
        Assert.Equal(0, AudioOut2Exports.AudioOut2PortGetState(context));
        Assert.Equal((ushort)1, Read16(memory, state));
        Assert.Equal((byte)6, ReadByte(memory, state + 2));
        Assert.Equal((short)127, ReadInt16(memory, state + 4));
    }

    private static uint Read32(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[4]; Assert.True(memory.TryRead(address, b)); return BinaryPrimitives.ReadUInt32LittleEndian(b); }
    private static void Write32(FakeCpuMemory memory, ulong address, uint value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); Assert.True(memory.TryWrite(address, b)); }
    private static ushort Read16(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[2]; Assert.True(memory.TryRead(address, b)); return BinaryPrimitives.ReadUInt16LittleEndian(b); }
    private static short ReadInt16(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[2]; Assert.True(memory.TryRead(address, b)); return BinaryPrimitives.ReadInt16LittleEndian(b); }
    private static byte ReadByte(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[1]; Assert.True(memory.TryRead(address, b)); return b[0]; }
    private static ulong Read64(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[8]; Assert.True(memory.TryRead(address, b)); return BinaryPrimitives.ReadUInt64LittleEndian(b); }
}
