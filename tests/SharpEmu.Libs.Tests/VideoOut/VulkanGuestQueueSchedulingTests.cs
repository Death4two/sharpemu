// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.VideoOut;
using Xunit;

namespace SharpEmu.Libs.Tests.VideoOut;

public sealed class VulkanGuestQueueSchedulingTests
{
    [Fact]
    public void DeferredWaitYieldsToAlreadyQueuedProducer()
    {
        var queue = new LinkedList<string>(["producer"]);

        VulkanGuestQueueScheduling.RequeueDeferredWork(queue, "wait");

        Assert.Equal(["producer", "wait"], queue);
    }
}
