// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.UserService;
using System.Buffers.Binary;
using Xunit;

namespace SharpEmu.Libs.Tests.UserService;

public sealed class UserServiceExportsTests
{
    private const ulong MemoryBase = 0x1_0000_0000;
    private const int PrimaryUserId = 0x10000000;

    [Fact]
    public void Initialize2Succeeds()
    {
        var context = new CpuContext(new FakeCpuMemory(MemoryBase, 1), Generation.Gen5);
        Assert.Equal(0, UserServiceExports.UserServiceInitialize2(context));
        Assert.Equal(0UL, context[CpuRegister.Rax]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UserNumberAliasesWriteHostedUserNumber(bool alternate)
    {
        var memory = new FakeCpuMemory(MemoryBase, sizeof(int));
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rdi] = PrimaryUserId;
        context[CpuRegister.Rsi] = MemoryBase;
        var result = alternate
            ? UserServiceExports.UserServiceGetUserNumberAlt(context)
            : UserServiceExports.UserServiceGetUserNumber(context);

        Assert.Equal(0, result);
        Span<byte> value = stackalloc byte[sizeof(int)];
        Assert.True(memory.TryRead(MemoryBase, value));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(value));
    }

    [Fact]
    public void UserNumberValidatesOutputBeforeUser()
    {
        var context = new CpuContext(new FakeCpuMemory(MemoryBase, 1), Generation.Gen5);
        context[CpuRegister.Rdi] = PrimaryUserId;
        Assert.Equal(unchecked((int)0x80960005), UserServiceExports.UserServiceGetUserNumber(context));
    }
}
