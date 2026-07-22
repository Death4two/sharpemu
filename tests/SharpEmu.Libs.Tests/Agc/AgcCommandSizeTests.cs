// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class AgcCommandSizeTests
{
    [Theory]
    [InlineData(0u, 0u)]
    [InlineData(1u, 4u)]
    [InlineData(17u, 68u)]
    public void CbNopGetSize_MatchesKyty(uint dwordCount, uint expectedBytes) =>
        AssertSize(AgcExports.CbNopGetSize, dwordCount, expectedBytes);

    [Theory]
    [InlineData(1u, 12u)]
    [InlineData(17u, 76u)]
    public void CbSetShRegisterRangeDirectGetSize_MatchesKyty(uint valueCount, uint expectedBytes) =>
        AssertSize(AgcExports.CbSetShRegisterRangeDirectGetSize, valueCount, expectedBytes);

    [Theory]
    [InlineData(0u, 56u)]
    [InlineData(1u, 64u)]
    [InlineData(2u, 0u)]
    public void DcbWaitOnAddressGetSize_MatchesKyty(uint size, uint expectedBytes) =>
        AssertSize(AgcExports.DcbWaitOnAddressGetSize, size, expectedBytes);

    [Fact]
    public void FixedCommandSizes_MatchKyty()
    {
        AssertSize(AgcExports.CbDispatchGetSize, 0, 20);
        AssertSize(AgcExports.CbQueueEndOfPipeActionGetSize, 0, 32);
        AssertSize(AgcExports.DcbSetCxRegisterDirectGetSize, 0, 12);
        AssertSize(AgcExports.DcbDrawIndexAutoGetSize, 0, 12);
        AssertSize(AgcExports.DcbDrawIndexOffsetGetSize, 0, 20);
        AssertSize(AgcExports.DcbDrawIndirectGetSize, 0, 20);
        AssertSize(AgcExports.DcbAcquireMemGetSize, 0, 32);
        AssertSize(AgcExports.AcbAcquireMemGetSize, 0, 32);
        AssertSize(AgcExports.DcbCondExecGetSize, 0, 20);
        AssertSize(AgcExports.AcbCondExecGetSize, 0, 20);
        AssertSize(AgcExports.DcbJumpGetSize, 0, 16);
        AssertSize(AgcExports.AcbJumpGetSize, 0, 16);
        AssertSize(AgcExports.DcbRewindGetSize, 0, 8);
    }

    [Theory]
    [InlineData(0u, 16u)]
    [InlineData(1u, 20u)]
    [InlineData(29u, 132u)]
    public void DcbWriteDataGetSize_MatchesKyty(uint dwordCount, uint expectedBytes) =>
        AssertSize(AgcExports.DcbWriteDataGetSize, dwordCount, expectedBytes);

    private static void AssertSize(Func<CpuContext, int> query, uint input, uint expectedBytes)
    {
        var context = new CpuContext(new FakeCpuMemory(0x1_0000_0000, 0x1000), Generation.Gen5);
        context[CpuRegister.Rdi] = input;

        Assert.Equal(unchecked((int)expectedBytes), query(context));
        Assert.Equal(expectedBytes, context[CpuRegister.Rax]);
    }
}
