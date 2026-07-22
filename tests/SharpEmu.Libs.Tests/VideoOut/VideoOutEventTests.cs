// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.VideoOut;
using Xunit;

namespace SharpEmu.Libs.Tests.VideoOut;

public sealed class VideoOutEventTests
{
    private const ulong MemoryBase = 0x1_0000_0000;
    private const string OpenNid = "Up36PTk687E";
    private const string CloseNid = "uquVH4-Du78";
    private const string AddOutputModeEventNid = "kmSe30JTs+E";
    private const string DeleteFlipEventNid = "-Ozn0F1AFRg";

    [Fact]
    public void GetEventCount_DecodesKytyCoalescedEventCount()
    {
        var memory = new FakeCpuMemory(MemoryBase, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var eventAddress = MemoryBase + 0x100;
        Assert.True(context.TryWriteUInt16(eventAddress + 0x08, unchecked((ushort)-13)));
        Assert.True(context.TryWriteUInt64(eventAddress + 0x10, 7UL << 12));
        context[CpuRegister.Rdi] = eventAddress;

        Assert.Equal(7, VideoOutExports.VideoOutGetEventCount(context));
    }

    [Fact]
    public void GetEventCount_RejectsNonVideoOutFilter()
    {
        var memory = new FakeCpuMemory(MemoryBase, 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        var eventAddress = MemoryBase + 0x100;
        Assert.True(context.TryWriteUInt16(eventAddress + 0x08, 0));
        Assert.True(context.TryWriteUInt64(eventAddress + 0x10, 1UL << 12));
        context[CpuRegister.Rdi] = eventAddress;

        Assert.Equal(unchecked((int)0x8029000D), VideoOutExports.VideoOutGetEventCount(context));
    }

    [Fact]
    public void KytyEventExports_AreRegisteredAndValidateEqueue()
    {
        var manager = new ModuleManager();
        manager.RegisterExports(SharpEmu.Generated.SysAbiExportRegistry.CreateExports(Generation.Gen5));
        var context = new CpuContext(new FakeCpuMemory(MemoryBase, 0x1000), Generation.Gen5);
        context[CpuRegister.Rdi] = 0;
        context[CpuRegister.Rsi] = 0;
        context[CpuRegister.Rdx] = 0;
        Assert.True(manager.TryDispatch(OpenNid, context, out _));
        var handle = context[CpuRegister.Rax];

        try
        {
            context[CpuRegister.Rdi] = 0;
            context[CpuRegister.Rsi] = handle;
            context[CpuRegister.Rdx] = 0;
            Assert.True(manager.TryDispatch(AddOutputModeEventNid, context, out _));
            Assert.Equal(unchecked((ulong)(int)0x8029000C), context[CpuRegister.Rax]);

            Assert.True(manager.TryDispatch(DeleteFlipEventNid, context, out _));
            Assert.Equal(unchecked((ulong)(int)0x8029000C), context[CpuRegister.Rax]);
        }
        finally
        {
            context[CpuRegister.Rdi] = handle;
            _ = manager.TryDispatch(CloseNid, context, out _);
        }
    }
}
