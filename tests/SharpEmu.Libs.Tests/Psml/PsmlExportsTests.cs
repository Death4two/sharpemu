// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Psml;
using Xunit;

namespace SharpEmu.Libs.Tests.Psml;

public sealed class PsmlExportsTests
{
    private const ulong BaseAddress = 0x5_0000_0000;

    [Fact]
    public void MainMemoryRequirements_MatchKytyTable()
    {
        var (memory, context) = NewContext();
        Assert.Equal(0, PsmlExports.Initialize(context));
        var parameters = BaseAddress + 0x100;
        var output = BaseAddress + 0x200;
        context[CpuRegister.Rdi] = output;
        context[CpuRegister.Rsi] = parameters;

        WriteUInt32(memory, parameters, 0);
        Assert.Equal(0, PsmlExports.GetMainMemoryRequirements(context));
        Assert.Equal(0x20_0000UL, ReadUInt64(memory, output));
        Assert.Equal(0x20_0000UL, ReadUInt64(memory, output + 8));
        Assert.Equal(52UL, ReadUInt64(memory, output + 16));

        WriteUInt32(memory, parameters, 1);
        Assert.Equal(0, PsmlExports.GetMainMemoryRequirements(context));
        Assert.Equal(196UL, ReadUInt64(memory, output + 16));

        WriteUInt32(memory, parameters, 2);
        Assert.Equal(0, PsmlExports.GetMainMemoryRequirements(context));
        Assert.Equal(148UL, ReadUInt64(memory, output + 16));
    }

    [Fact]
    public void SharedResourcesAndContextLayouts_MatchKyty()
    {
        var (memory, context) = NewContext();
        Assert.Equal(0, PsmlExports.Initialize(context));
        var shared = BaseAddress + 0x1000;
        var sharedParameters = BaseAddress + 0x1100;
        var blocks = BaseAddress + 0x1200;
        WriteUInt32(memory, sharedParameters, 0);
        WriteUInt32(memory, sharedParameters + 4, 0);
        WriteUInt64(memory, sharedParameters + 8, blocks);
        WriteUInt64(memory, sharedParameters + 16, 52);
        WriteUInt64(memory, sharedParameters + 24, 0x6_0000_0000);
        context[CpuRegister.Rdi] = shared;
        context[CpuRegister.Rsi] = sharedParameters;
        Assert.Equal(0, PsmlExports.SharedResourcesInitialize(context));
        Assert.Equal(0xA9C4u, ReadUInt32(memory, shared));
        Assert.Equal(blocks, ReadUInt64(memory, shared + 8));
        Assert.Equal(52UL, ReadUInt64(memory, shared + 24));
        Assert.Equal(0u, ReadUInt32(memory, shared + 32));
        Assert.Equal(0x6_0000_0000UL, ReadUInt64(memory, shared + 40));

        var contextAddress = BaseAddress + 0x2000;
        var contextParameters = BaseAddress + 0x2400;
        WriteUInt64(memory, contextParameters + 8, shared);
        context[CpuRegister.Rdi] = contextAddress;
        context[CpuRegister.Rsi] = contextParameters;
        Assert.Equal(0, PsmlExports.ContextInitialize(context));
        Assert.Equal(0x9231u, ReadUInt32(memory, contextAddress));
        Assert.Equal(shared, ReadUInt64(memory, contextAddress + 0x360));

        var progress = BaseAddress + 0x2800;
        context[CpuRegister.Rdi] = contextAddress;
        context[CpuRegister.Rsi] = progress;
        Assert.Equal(0, PsmlExports.GetProgress(context));
        Assert.Equal(0u, ReadUInt32(memory, progress));
        context[CpuRegister.Rdi] = contextAddress;
        Assert.Equal(0, PsmlExports.ContextFinalize(context));
        Assert.Equal(0u, ReadUInt32(memory, contextAddress));
    }

    private static (FakeCpuMemory Memory, CpuContext Context) NewContext()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x8000);
        return (memory, new CpuContext(memory, Generation.Gen5));
    }

    private static uint ReadUInt32(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[4];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static ulong ReadUInt64(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[8];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static void WriteUInt32(FakeCpuMemory memory, ulong address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Assert.True(memory.TryWrite(address, bytes));
    }

    private static void WriteUInt64(FakeCpuMemory memory, ulong address, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        Assert.True(memory.TryWrite(address, bytes));
    }
}
