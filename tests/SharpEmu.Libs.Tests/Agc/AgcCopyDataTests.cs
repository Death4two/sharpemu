// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class AgcCopyDataTests
{
    [Theory]
    [InlineData(0UL, 10UL, 0UL)]
    [InlineData(10UL, 10UL, 100_000_000UL)]
    [InlineData(15UL, 10UL, 150_000_000UL)]
    [InlineData(1_234_567UL, 1_000_000UL, 123_456_700UL)]
    public void GpuReferenceClockScalesHostTicksTo100MHz(
        ulong ticks,
        ulong frequency,
        ulong expected)
    {
        Assert.Equal(expected, AgcExports.ScaleGpuReferenceClock(ticks, frequency));
    }

    [Fact]
    public void GpuReferenceClockRejectsZeroFrequency()
    {
        Assert.Equal(0UL, AgcExports.ScaleGpuReferenceClock(123, 0));
    }

    [Theory]
    [InlineData(0x1000_0000u, 0x191u)]
    [InlineData(0x1000_001Fu, 0x1B0u)]
    [InlineData(0u, 0u)]
    [InlineData(0x190u, 0x190u)]
    public void IndirectCxInterpolantSlotsNormalizeToHardwareRegisters(
        uint encoded,
        uint expected)
    {
        Assert.Equal(expected, AgcExports.NormalizeIndirectCxRegisterOffset(encoded));
    }
}
