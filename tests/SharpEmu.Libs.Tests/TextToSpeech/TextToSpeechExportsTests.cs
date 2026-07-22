// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.TextToSpeech;
using Xunit;

namespace SharpEmu.Libs.Tests.TextToSpeech;

public sealed class TextToSpeechExportsTests
{
    [Fact]
    public void InitializeSucceedsWithoutClaimingSpeechSynthesis()
    {
        var context = new CpuContext(new FakeCpuMemory(0x1_0000_0000, 1), Generation.Gen5);
        Assert.Equal(0, TextToSpeechExports.Initialize(context));
        Assert.Equal(0UL, context[CpuRegister.Rax]);
    }
}
