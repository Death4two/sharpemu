// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Network;
using Xunit;

namespace SharpEmu.Libs.Tests.Network;

public sealed class RudpExportsTests
{
    [Fact]
    public void CompatibilityInitializationAndCallbackRegistrationSucceed()
    {
        var context = new CpuContext(new FakeCpuMemory(0x1_0000_0000, 1), Generation.Gen5);
        Assert.Equal(0, RudpExports.RudpInit(context));
        Assert.Equal(0, RudpExports.RudpEnableInternalIoThread(context));
        context[CpuRegister.Rdi] = 0x1234;
        context[CpuRegister.Rsi] = 0x5678;
        Assert.Equal(0, RudpExports.RudpSetEventHandler(context));
        Assert.Equal(0UL, context[CpuRegister.Rax]);
    }
}
