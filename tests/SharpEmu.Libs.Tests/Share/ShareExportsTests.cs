using SharpEmu.HLE;
using SharpEmu.Libs.Share;
using System.Buffers.Binary;
using Xunit;

namespace SharpEmu.Libs.Tests.Share;

public sealed class ShareExportsTests
{
    [Fact]
    public void CaptureWritesInvalidRequestIdAndReportsUnsupported()
    {
        const ulong address = 0x1_0000_0000;
        var memory = new FakeCpuMemory(address, 4);
        var ctx = new CpuContext(memory, Generation.Gen5);
        ctx[CpuRegister.Rsi] = address;
        Assert.Equal(unchecked((int)0x81960007), ShareExports.Screenshot(ctx));
        Span<byte> value = stackalloc byte[4];
        Assert.True(memory.TryRead(address, value));
        Assert.Equal(-1, BinaryPrimitives.ReadInt32LittleEndian(value));
    }

    [Fact]
    public void StatusRequiresFeatureFlagAndClearsOutput()
    {
        const ulong address = 0x1_0000_0000;
        var memory = new FakeCpuMemory(address, 16);
        var ctx = new CpuContext(memory, Generation.Gen5);
        ctx[CpuRegister.Rsi] = address;
        Assert.Equal(unchecked((int)0x81960002), ShareExports.Status(ctx));
        ctx[CpuRegister.Rdi] = 1;
        Assert.Equal(0, ShareExports.Status(ctx));
    }
}
