// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class AgcDeferredGraphicsWaitTests
{
    private const ulong BaseAddress = 0x2_0000_0000;
    private const ulong WaitDcbAddress = BaseAddress + 0x100;
    private const ulong ProducerDcbAddress = BaseAddress + 0x200;
    private const ulong WaitSubmitAddress = BaseAddress + 0x300;
    private const ulong ProducerSubmitAddress = BaseAddress + 0x320;
    private const ulong LabelAddress = BaseAddress + 0x400;

    [Fact]
    public void LaterGraphicsSubmission_ProducesLabelForSuspendedGraphicsWait()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x2000);
        var context = new CpuContext(memory, Generation.Gen5);

        // NOP/R_WAIT_MEM32: wait until label == 1.
        WriteUInt32(memory, WaitDcbAddress, Pm4(6, 0x10, 0x0A));
        WriteUInt64(memory, WaitDcbAddress + 4, LabelAddress);
        WriteUInt32(memory, WaitDcbAddress + 12, uint.MaxValue);
        WriteUInt32(memory, WaitDcbAddress + 16, 3); // compare equal
        WriteUInt32(memory, WaitDcbAddress + 20, 1);
        WriteSubmitPacket(memory, WaitSubmitAddress, WaitDcbAddress, 6);

        context[CpuRegister.Rdi] = WaitSubmitAddress;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DriverSubmitDcb(context));
        Assert.Equal(0u, ReadUInt32(memory, LabelAddress));

        // NOP/R_DMA_DATA, compact layout: immediately fill four bytes of the
        // label with the 32-bit immediate value 1. Kyty yields the blocked
        // batch so this later graphics producer can execute.
        WriteUInt32(memory, ProducerDcbAddress, Pm4(7, 0x10, 0x19));
        WriteUInt64(memory, ProducerDcbAddress + 4, LabelAddress);
        WriteUInt64(memory, ProducerDcbAddress + 12, 1);
        WriteUInt32(memory, ProducerDcbAddress + 20, 4);
        WriteUInt32(memory, ProducerDcbAddress + 24, 2); // src=immediate, dst=memory
        WriteSubmitPacket(memory, ProducerSubmitAddress, ProducerDcbAddress, 7);

        context[CpuRegister.Rdi] = ProducerSubmitAddress;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DriverSubmitDcb(context));
        Assert.Equal(1u, ReadUInt32(memory, LabelAddress));
    }

    [Fact]
    public void CbBranch_ParsesSelectedKytyIndirectBuffer()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x2000);
        var context = new CpuContext(memory, Generation.Gen5);
        var root = BaseAddress + 0x600;
        var thenDcb = BaseAddress + 0x700;
        var submit = BaseAddress + 0x800;
        var compare = BaseAddress + 0x900;
        var result = BaseAddress + 0xA00;
        WriteUInt64(memory, compare, 1);

        // CbBranch: mode 2 (then/else), compare equal, then -> DMA immediate.
        WriteUInt32(memory, root, Pm4(14, 0x3F, 0));
        WriteUInt32(memory, root + 4, 0x302);
        WriteUInt64(memory, root + 8, compare);
        WriteUInt64(memory, root + 16, ulong.MaxValue);
        WriteUInt64(memory, root + 24, 1);
        WriteUInt64(memory, root + 32, thenDcb);
        WriteUInt32(memory, root + 40, 7);
        WriteUInt64(memory, root + 44, 0);
        WriteUInt32(memory, root + 52, 0);

        WriteUInt32(memory, thenDcb, Pm4(7, 0x10, 0x19));
        WriteUInt64(memory, thenDcb + 4, result);
        WriteUInt64(memory, thenDcb + 12, 0xC0DE_CAFE);
        WriteUInt32(memory, thenDcb + 20, 4);
        WriteUInt32(memory, thenDcb + 24, 2);
        WriteSubmitPacket(memory, submit, root, 14);

        context[CpuRegister.Rdi] = submit;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DriverSubmitDcb(context));
        Assert.Equal(0xC0DE_CAFEu, ReadUInt32(memory, result));
    }

    [Fact]
    public void CbBranchWait_ResumesParentStreamAfterSelectedChild()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x3000);
        var context = new CpuContext(memory, Generation.Gen5);
        var root = BaseAddress + 0x600;
        var child = BaseAddress + 0x700;
        var producer = BaseAddress + 0x800;
        var rootSubmit = BaseAddress + 0x900;
        var producerSubmit = BaseAddress + 0x920;
        var compare = BaseAddress + 0xA00;
        var label = BaseAddress + 0xA08;
        var result = BaseAddress + 0xA10;
        WriteUInt64(memory, compare, 1);

        // Root: branch to child, then write result after the child completes.
        WriteUInt32(memory, root, Pm4(14, 0x3F, 0));
        WriteUInt32(memory, root + 4, 0x302);
        WriteUInt64(memory, root + 8, compare);
        WriteUInt64(memory, root + 16, ulong.MaxValue);
        WriteUInt64(memory, root + 24, 1);
        WriteUInt64(memory, root + 32, child);
        WriteUInt32(memory, root + 40, 6);
        WriteUInt64(memory, root + 44, 0);
        WriteUInt32(memory, root + 52, 0);
        WriteUInt32(memory, root + 56, Pm4(7, 0x10, 0x19));
        WriteUInt64(memory, root + 60, result);
        WriteUInt64(memory, root + 68, 0x1234_5678);
        WriteUInt32(memory, root + 76, 4);
        WriteUInt32(memory, root + 80, 2);

        // Child: wait until the later graphics submission produces label == 1.
        WriteUInt32(memory, child, Pm4(6, 0x10, 0x0A));
        WriteUInt64(memory, child + 4, label);
        WriteUInt32(memory, child + 12, uint.MaxValue);
        WriteUInt32(memory, child + 16, 3);
        WriteUInt32(memory, child + 20, 1);
        WriteSubmitPacket(memory, rootSubmit, root, 21);

        context[CpuRegister.Rdi] = rootSubmit;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DriverSubmitDcb(context));
        Assert.Equal(0u, ReadUInt32(memory, result));

        WriteUInt32(memory, producer, Pm4(7, 0x10, 0x19));
        WriteUInt64(memory, producer + 4, label);
        WriteUInt64(memory, producer + 12, 1);
        WriteUInt32(memory, producer + 20, 4);
        WriteUInt32(memory, producer + 24, 2);
        WriteSubmitPacket(memory, producerSubmit, producer, 7);
        context[CpuRegister.Rdi] = producerSubmit;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DriverSubmitDcb(context));
        Assert.Equal(0x1234_5678u, ReadUInt32(memory, result));
    }

    private static uint Pm4(uint lengthDwords, uint op, uint register) =>
        0xC000_0000u | ((lengthDwords - 2) << 16) | ((op & 0xFFu) << 8) | ((register & 0x3Fu) << 2);

    private static void WriteSubmitPacket(
        FakeCpuMemory memory,
        ulong packetAddress,
        ulong commandAddress,
        uint dwordCount)
    {
        WriteUInt64(memory, packetAddress, commandAddress);
        WriteUInt32(memory, packetAddress + 8, dwordCount);
    }

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
}
