// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.AppContent;
using Xunit;

namespace SharpEmu.Libs.Tests.AppContent;

public sealed class AppContentExportsTests
{
    private const ulong MemoryBase = 0x1_0000_0000;

    [Fact]
    public void AddcontMountClearsMountPointAndReportsNoEntitlement()
    {
        var memory = new FakeCpuMemory(MemoryBase, 36);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rsi] = MemoryBase;
        context[CpuRegister.Rdx] = MemoryBase + 20;
        Assert.True(memory.TryWrite(MemoryBase + 20, Enumerable.Repeat((byte)0xA5, 16).ToArray()));

        Assert.Equal(unchecked((int)0x80D90007), AppContentExports.AppContentAddcontMount(context));
        Span<byte> mount = stackalloc byte[16];
        Assert.True(memory.TryRead(MemoryBase + 20, mount));
        Assert.All(mount.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void AddcontUnmountRejectsEmptyMountPoint()
    {
        var memory = new FakeCpuMemory(MemoryBase, 16);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = MemoryBase;
        Assert.Equal(unchecked((int)0x80D90004), AppContentExports.AppContentAddcontUnmount(context));
    }

    [Fact]
    public void InitializeAcceptsNullParametersAndClearsFullKytyBootParam()
    {
        var memory = new FakeCpuMemory(MemoryBase, 64);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = 0;
        context[CpuRegister.Rsi] = MemoryBase;
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, 40).ToArray()));

        Assert.Equal(0, AppContentExports.AppContentInitialize(context));
        Span<byte> bootParam = stackalloc byte[40];
        Assert.True(memory.TryRead(MemoryBase, bootParam));
        Assert.All(bootParam.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void TemporaryDataMount2_WritesZeroPaddedKytyMountPoint()
    {
        var memory = new FakeCpuMemory(MemoryBase, 16);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rsi] = MemoryBase;
        Assert.True(memory.TryWrite(MemoryBase, Enumerable.Repeat((byte)0xA5, 16).ToArray()));

        Assert.Equal(0, AppContentExports.AppContentTemporaryDataMount2(context));
        Span<byte> mount = stackalloc byte[16];
        Assert.True(memory.TryRead(MemoryBase, mount));
        Assert.Equal("/temp0\0\0\0\0\0\0\0\0\0\0", System.Text.Encoding.ASCII.GetString(mount));
    }
}
