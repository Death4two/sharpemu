// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Codec;
using Xunit;

namespace SharpEmu.Libs.Tests.Codec;

public sealed class PngDecExportsTests
{
    private const ulong BaseAddress = 0x6_0000_0000;

    [Fact]
    public void QueryCreateParseAndDelete_MatchKytyAbi()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var create = BaseAddress;
        var work = BaseAddress + 0x100;
        var handleOut = BaseAddress + 0x110;
        var png = BaseAddress + 0x200;
        var parse = BaseAddress + 0x300;
        var info = BaseAddress + 0x320;
        WriteUInt32(memory, create + 4, 0);
        WriteUInt32(memory, create + 8, 1920);
        context[CpuRegister.Rdi] = create;
        Assert.Equal(8, PngDecExports.QueryMemorySize(context));

        context[CpuRegister.Rdi] = create;
        context[CpuRegister.Rsi] = work;
        context[CpuRegister.Rdx] = 8;
        context[CpuRegister.Rcx] = handleOut;
        Assert.Equal(0, PngDecExports.Create(context));
        Assert.Equal(work, ReadUInt64(memory, handleOut));

        var header = new byte[33];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(header);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8), 13);
        "IHDR"u8.CopyTo(header.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), 2);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(20), 3);
        header[24] = 8;
        header[25] = 6;
        Assert.True(memory.TryWrite(png, header));
        WriteUInt64(memory, parse, png);
        WriteUInt32(memory, parse + 8, (uint)header.Length);
        WriteUInt32(memory, parse + 12, 0);
        context[CpuRegister.Rdi] = parse;
        context[CpuRegister.Rsi] = info;
        Assert.Equal(0, PngDecExports.ParseHeader(context));
        Assert.Equal(2u, ReadUInt32(memory, info));
        Assert.Equal(3u, ReadUInt32(memory, info + 4));
        Assert.Equal((ushort)19, ReadUInt16(memory, info + 8));
        Assert.Equal((ushort)8, ReadUInt16(memory, info + 10));

        context[CpuRegister.Rdi] = work;
        Assert.Equal(0, PngDecExports.Delete(context));
    }

    private static void WriteUInt32(FakeCpuMemory memory, ulong address, uint value)
    { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, value); Assert.True(memory.TryWrite(address, bytes)); }
    private static void WriteUInt64(FakeCpuMemory memory, ulong address, ulong value)
    { Span<byte> bytes = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(bytes, value); Assert.True(memory.TryWrite(address, bytes)); }
    private static uint ReadUInt32(FakeCpuMemory memory, ulong address)
    { Span<byte> bytes = stackalloc byte[4]; Assert.True(memory.TryRead(address, bytes)); return BinaryPrimitives.ReadUInt32LittleEndian(bytes); }
    private static ushort ReadUInt16(FakeCpuMemory memory, ulong address)
    { Span<byte> bytes = stackalloc byte[2]; Assert.True(memory.TryRead(address, bytes)); return BinaryPrimitives.ReadUInt16LittleEndian(bytes); }
    private static ulong ReadUInt64(FakeCpuMemory memory, ulong address)
    { Span<byte> bytes = stackalloc byte[8]; Assert.True(memory.TryRead(address, bytes)); return BinaryPrimitives.ReadUInt64LittleEndian(bytes); }
}
