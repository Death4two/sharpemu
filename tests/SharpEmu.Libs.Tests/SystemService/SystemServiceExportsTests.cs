// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.SystemService;
using Xunit;

namespace SharpEmu.Libs.Tests.SystemService;

public sealed class SystemServiceExportsTests
{
    private const ulong MemoryBase = 0x1_0000_0000;

    [Fact]
    public void ReceiveEventClearsWholeEventAndReportsNoEvent()
    {
        const int eventSize = sizeof(int) + 8192;
        var memory = new FakeCpuMemory(MemoryBase, eventSize);
        var context = new CpuContext(memory, Generation.Gen5);
        Assert.True(memory.TryWrite(MemoryBase, new byte[eventSize].Select(_ => (byte)0xA5).ToArray()));
        context[CpuRegister.Rdi] = MemoryBase;

        Assert.Equal(unchecked((int)0x80A10004), SystemServiceExports.SystemServiceReceiveEvent(context));
        Assert.Equal(unchecked((ulong)(long)unchecked((int)0x80A10004)), context[CpuRegister.Rax]);

        Span<byte> systemEvent = stackalloc byte[eventSize];
        Assert.True(memory.TryRead(MemoryBase, systemEvent));
        Assert.Equal(-1, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(systemEvent));
        Assert.All(systemEvent[sizeof(int)..].ToArray(), value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SystemServiceNoOpsSucceed(int export)
    {
        var context = new CpuContext(new FakeCpuMemory(MemoryBase, 1), Generation.Gen5);
        var result = export == 0
            ? SystemServiceExports.SystemServiceDisableNoticeScreenSkipFlagAutoSet(context)
            : SystemServiceExports.SystemServiceSetNoticeScreenSkipFlag(context);

        Assert.Equal(0, result);
        Assert.Equal(0UL, context[CpuRegister.Rax]);
        Assert.Equal(0, SystemServiceExports.SystemServicePowerTick(context));
    }

    [Fact]
    public void GetNoticeScreenSkipFlagWritesOneByteAtMemoryBoundary()
    {
        var memory = new FakeCpuMemory(MemoryBase, 1);
        var context = new CpuContext(memory, Generation.Gen5);
        Assert.True(memory.TryWrite(MemoryBase, new byte[] { 0xA5 }));
        context[CpuRegister.Rdi] = MemoryBase;

        Assert.Equal(0, SystemServiceExports.SystemServiceGetNoticeScreenSkipFlag(context));
        Assert.Equal(0UL, context[CpuRegister.Rax]);

        Span<byte> flag = stackalloc byte[1];
        Assert.True(memory.TryRead(MemoryBase, flag));
        Assert.Equal(0, flag[0]);
    }

    [Fact]
    public void StatusAndHdrToneMapMatchKytyDefaults()
    {
        var memory = new FakeCpuMemory(MemoryBase, 160);
        var context = new CpuContext(memory, Generation.Gen5);
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, 160).ToArray()));
        context[CpuRegister.Rdi] = MemoryBase;
        Assert.Equal(0, SystemServiceExports.SystemServiceGetStatus(context));
        Span<byte> status = stackalloc byte[136];
        Assert.True(memory.TryRead(MemoryBase, status));
        Assert.All(status.ToArray(), value => Assert.Equal(0, value));

        context[CpuRegister.Rdi] = MemoryBase + 140;
        Assert.Equal(0, SystemServiceExports.SystemServiceGetHdrToneMapLuminance(context));
        Span<byte> luminance = stackalloc byte[12];
        Assert.True(memory.TryRead(MemoryBase + 140, luminance));
        Assert.Equal(80.0f, System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(luminance));
        Assert.Equal(1000.0f, System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(luminance[4..]));
        Assert.Equal(0.0f, System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(luminance[8..]));
    }
}
