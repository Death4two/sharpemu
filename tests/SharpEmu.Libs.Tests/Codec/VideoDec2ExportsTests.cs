// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Codec;
using Xunit;

namespace SharpEmu.Libs.Tests.Codec;

public sealed class VideoDec2ExportsTests
{
    private const ulong Base = 0x6_0000_0000;

    [Fact]
    public void ComputeMemoryInfo_UsesKytySizeAndMinimum()
    {
        var memory = new FakeCpuMemory(Base, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        Write64(memory, Base, 24);
        context[CpuRegister.Rdi] = Base;

        Assert.Equal(0, VideoDec2Exports.QueryComputeMemoryInfo(context));
        Assert.Equal(16UL * 1024 * 1024, Read64(memory, Base + 8));
        Assert.Equal(0UL, Read64(memory, Base + 16));
    }

    [Fact]
    public void DecoderLifecycle_ProducesKytyNoPictureOutput()
    {
        var memory = new FakeCpuMemory(Base, 0x2000);
        var context = new CpuContext(memory, Generation.Gen5);
        var config = Base; var memoryInfo = Base + 0x100; var handleOut = Base + 0x200;
        var input = Base + 0x300; var frame = Base + 0x400; var output = Base + 0x500;
        Write64(memory, config, 72); Write32(memory, config + 8, 1); Write32(memory, config + 24, 1920); Write32(memory, config + 28, 1080); Write32(memory, config + 32, 1); Write32(memory, config + 36, 1); Write64(memory, config + 40, 0x4000);
        Write64(memory, memoryInfo, 72);
        foreach (var offset in new[] { 8, 24, 40, 56 }) Write64(memory, memoryInfo + (ulong)offset, 16UL * 1024 * 1024);
        Write64(memory, memoryInfo + 16, 0x1000); Write64(memory, memoryInfo + 32, 0x2000); Write64(memory, memoryInfo + 48, 0x3000);
        context[CpuRegister.Rdi] = config; context[CpuRegister.Rsi] = memoryInfo; context[CpuRegister.Rdx] = handleOut;
        Assert.Equal(0, VideoDec2Exports.CreateDecoder(context));
        var decoder = Read64(memory, handleOut);

        Write64(memory, input, 48); Write64(memory, frame, 32); Write64(memory, frame + 8, 0x5000); Write64(memory, frame + 16, 0x1000); Write64(memory, output, 56);
        context[CpuRegister.Rdi] = decoder; context[CpuRegister.Rsi] = input; context[CpuRegister.Rdx] = frame; context[CpuRegister.Rcx] = output;
        Assert.Equal(0, VideoDec2Exports.Decode(context));
        Assert.Equal(0, ReadByte(memory, frame + 24));
        Assert.Equal(0, ReadByte(memory, output + 8));
        Assert.Equal(0x5000UL, Read64(memory, output + 32));
        Assert.Equal(0x1000UL, Read64(memory, output + 40));
    }

    private static void Write32(FakeCpuMemory memory, ulong address, uint value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, value); Assert.True(memory.TryWrite(address, b)); }
    private static void Write64(FakeCpuMemory memory, ulong address, ulong value) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(b, value); Assert.True(memory.TryWrite(address, b)); }
    private static ulong Read64(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[8]; Assert.True(memory.TryRead(address, b)); return BinaryPrimitives.ReadUInt64LittleEndian(b); }
    private static byte ReadByte(FakeCpuMemory memory, ulong address) { Span<byte> b = stackalloc byte[1]; Assert.True(memory.TryRead(address, b)); return b[0]; }
}
