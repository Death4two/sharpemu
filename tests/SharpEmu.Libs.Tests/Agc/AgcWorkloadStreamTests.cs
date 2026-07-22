// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class AgcWorkloadStreamTests
{
    private const ulong BaseAddress = 0x1_0000_0000;
    private const ulong CommandBufferAddress = BaseAddress + 0x100;
    private const ulong CommandAddress = BaseAddress + 0x1000;
    private const ulong StreamAddress = BaseAddress + 0x300;
    private const ulong WorkloadIdsAddress = BaseAddress + 0x400;

    [Fact]
    public void WorkloadPackets_MatchKytyPacketLayout()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x4000);
        var context = new CpuContext(memory, Generation.Gen5);
        InitializeCommandBuffer(memory);

        // Stream 31 is valid and unused by the test suite. Kyty keeps stream
        // registration process-global, just like the native driver.
        WriteBytes(memory, StreamAddress, new byte[32]);
        context[CpuRegister.Rdi] = 31;
        context[CpuRegister.Rsi] = StreamAddress;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DriverRegisterWorkloadStream(context));

        WriteUInt32(memory, WorkloadIdsAddress, 1);
        WriteUInt32(memory, WorkloadIdsAddress + 4, 63);
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 31;
        context[CpuRegister.Rdx] = WorkloadIdsAddress;
        context[CpuRegister.Rcx] = 2;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetWorkloadsActive(context));
        Assert.Equal(CommandAddress, context[CpuRegister.Rax]);

        Assert.Equal(Pm4(18, 0x10, 0), ReadUInt32(memory, CommandAddress));
        Assert.Equal(31u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(2u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x8000_0000u, ReadUInt32(memory, CommandAddress + 12));
        for (var offset = 16; offset < 18 * 4; offset += 4)
        {
            Assert.Equal(0u, ReadUInt32(memory, CommandAddress + (ulong)offset));
        }

        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 31;
        context[CpuRegister.Rdx] = 63;
        var completeAddress = CommandAddress + (18 * 4UL);
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetWorkloadComplete(context));
        Assert.Equal(completeAddress, context[CpuRegister.Rax]);

        Assert.Equal(Pm4(12, 0x10, 0), ReadUInt32(memory, completeAddress));
        Assert.Equal(31u, ReadUInt32(memory, completeAddress + 4));
        Assert.Equal(63u, ReadUInt32(memory, completeAddress + 8));
        Assert.Equal(0xFFFF_FFFFu, ReadUInt32(memory, completeAddress + 12));
        Assert.Equal(0x7FFF_FFFFu, ReadUInt32(memory, completeAddress + 16));
        for (var offset = 20; offset < 12 * 4; offset += 4)
        {
            Assert.Equal(0u, ReadUInt32(memory, completeAddress + (ulong)offset));
        }
    }

    private static void InitializeCommandBuffer(FakeCpuMemory memory)
    {
        WriteUInt64(memory, CommandBufferAddress + 0x10, CommandAddress);
        WriteUInt64(memory, CommandBufferAddress + 0x18, CommandAddress + 0x400);
    }

    private static uint Pm4(uint lengthDwords, uint op, uint register) =>
        0xC000_0000u | ((lengthDwords - 2) << 16) | ((op & 0xFFu) << 8) | ((register & 0x3Fu) << 2);

    private static uint ReadUInt32(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[4];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
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

    private static void WriteBytes(FakeCpuMemory memory, ulong address, ReadOnlySpan<byte> value) =>
        Assert.True(memory.TryWrite(address, value));
}
