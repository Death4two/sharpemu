// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Np;
using Xunit;

namespace SharpEmu.Libs.Tests.Np;

public sealed class NpUniversalDataSystemExportsTests
{
    private const ulong Base = 0x1_0000_0000;
    private readonly FakeCpuMemory _memory = new(Base, 0x4000);
    private readonly CpuContext _ctx;

    public NpUniversalDataSystemExportsTests() => _ctx = new CpuContext(_memory, Generation.Gen5);

    [Fact]
    public void EventPropertyObject_CreateUseDestroy_UsesGuestOpaquePointer()
    {
        var output = Base + 0x100;
        _ctx[CpuRegister.Rdi] = output;
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemCreateEventPropertyObject(_ctx));

        Span<byte> pointerBytes = stackalloc byte[sizeof(ulong)];
        Assert.True(_memory.TryRead(output, pointerBytes));
        var propertyObject = BinaryPrimitives.ReadUInt64LittleEndian(pointerBytes);
        Assert.NotEqual(0UL, propertyObject);

        _ctx[CpuRegister.Rdi] = propertyObject;
        _ctx[CpuRegister.Rsi] = _memory.WriteCString(Base + 0x200, "key");
        _ctx[CpuRegister.Rdx] = _memory.WriteCString(Base + 0x300, "value");
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetString(_ctx));

        _ctx[CpuRegister.Rdx] = 42;
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetInt32(_ctx));
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetUInt32(_ctx));
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetInt64(_ctx));
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetUInt64(_ctx));
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetFloat32(_ctx));
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetFloat64(_ctx));
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetBool(_ctx));

        Assert.True(_memory.TryWrite(Base + 0x400, [1, 2, 3]));
        _ctx[CpuRegister.Rdx] = Base + 0x400;
        _ctx[CpuRegister.Rcx] = 3;
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetBinary(_ctx));

        _ctx[CpuRegister.Rdx] = 0;
        _ctx[CpuRegister.Rcx] = 1;
        Assert.NotEqual(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetBinary(_ctx));

        _ctx[CpuRegister.Rdi] = propertyObject;
        Assert.Equal(0, NpUniversalDataSystemExports.NpUniversalDataSystemDestroyEventPropertyObject(_ctx));

        _ctx[CpuRegister.Rdi] = propertyObject;
        Assert.NotEqual(0, NpUniversalDataSystemExports.NpUniversalDataSystemEventPropertyObjectSetString(_ctx));
    }
}
