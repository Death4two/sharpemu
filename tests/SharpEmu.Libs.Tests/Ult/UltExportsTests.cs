// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Ult;
using Xunit;

namespace SharpEmu.Libs.Tests.Ult;

public sealed class UltExportsTests
{
    private const ulong Base = 0x6_0000_0000;

    [Fact]
    public void Queue_RoundTripsPayloadAndReportsEmptyAsAgain()
    {
        var memory = new FakeCpuMemory(Base, 0x4000);
        var ctx = new CpuContext(memory, Generation.Gen5);
        var waitingPool = Base; var dataPool = Base + 0x400; var queue = Base + 0x800;
        var stack = Base + 0x1000; var source = Base + 0x1200; var destination = Base + 0x1300;

        ctx[CpuRegister.Rdi] = waitingPool;
        ctx[CpuRegister.Rdx] = 1;
        ctx[CpuRegister.Rcx] = 1;
        ctx[CpuRegister.R8] = Base + 0x1800;
        Assert.Equal(0, UltExports.WaitingPoolCreate(ctx));

        ctx[CpuRegister.Rsp] = stack;
        Assert.True(ctx.TryWriteUInt64(stack + 8, Base + 0x1A00));
        ctx[CpuRegister.Rdi] = dataPool;
        ctx[CpuRegister.Rdx] = 1;
        ctx[CpuRegister.Rcx] = 4;
        ctx[CpuRegister.R8] = 1;
        ctx[CpuRegister.R9] = waitingPool;
        Assert.Equal(0, UltExports.QueueDataPoolCreate(ctx));

        ctx[CpuRegister.Rdi] = queue;
        ctx[CpuRegister.Rsi] = 4;
        ctx[CpuRegister.Rcx] = waitingPool;
        ctx[CpuRegister.R8] = dataPool;
        Assert.Equal(0, UltExports.QueueCreate(ctx));

        Assert.True(memory.TryWrite(source, [1, 2, 3, 4]));
        ctx[CpuRegister.Rdi] = queue;
        ctx[CpuRegister.Rsi] = source;
        Assert.Equal(0, UltExports.QueuePush(ctx));

        ctx[CpuRegister.Rsi] = destination;
        Assert.Equal(0, UltExports.QueueTryPop(ctx));
        var actual = new byte[4];
        Assert.True(memory.TryRead(destination, actual));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, actual);
        Assert.Equal(unchecked((int)0x80810008), UltExports.QueueTryPop(ctx));
    }

    [Fact]
    public void WorkAreaSizes_MatchKytyAlignment()
    {
        var ctx = new CpuContext(new FakeCpuMemory(Base, 0x1000), Generation.Gen5);
        ctx[CpuRegister.Rdi] = 3;
        ctx[CpuRegister.Rsi] = 2;
        Assert.Equal(33_536, UltExports.RuntimeWorkAreaSize(ctx));
        Assert.Equal(33_536UL, ctx[CpuRegister.Rax]);

        ctx[CpuRegister.Rdi] = 3;
        ctx[CpuRegister.Rsi] = 4;
        Assert.Equal(1_792, UltExports.WaitingPoolWorkAreaSize(ctx));
    }
}
