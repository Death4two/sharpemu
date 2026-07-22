// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.Libs.Tests.Agc;

public sealed class AgcPacketBuilderParityTests
{
    private const ulong BaseAddress = 0x3_0000_0000;
    private const ulong CommandBufferAddress = BaseAddress + 0x100;
    private const ulong CommandAddress = BaseAddress + 0x1000;
    private const ulong StackAddress = BaseAddress + 0x3000;

    [Fact]
    public void DcbRegisterDirect_UsesKytyShaderRegisterSysVLayout()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x89AB_CDEF_0000_1234;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetShRegisterDirect(context));
        Assert.Equal(CommandAddress, context[CpuRegister.Rax]);
        Assert.Equal(Pm4(3, 0x76), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x1234u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x89AB_CDEFu, ReadUInt32(memory, CommandAddress + 8));
    }

    [Fact]
    public void DcbDrawIndexIndirect_DecodesKytyModifierPatchOffsets()
    {
        var (memory, context) = NewContext();
        // Stage 3, patch base vertex/start instance/base instance at SGPRs
        // 0x10f/0x111/0x110 and request the draw-index initiator bit.
        const ulong modifier = 0x6029_0707;
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x800;
        context[CpuRegister.Rdx] = modifier;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbDrawIndexIndirect(context));
        Assert.Equal(Pm4(5, 0x25), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x800u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x0110_010Fu, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x0800_0111u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(0x22u, ReadUInt32(memory, CommandAddress + 16));
    }

    [Fact]
    public void DcbCondExecAndRewind_MatchKytyPackets()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = BaseAddress + 0x204;
        context[CpuRegister.Rdx] = 0x200;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbCondExec(context));
        Assert.Equal(Pm4(5, 0x22), ReadUInt32(memory, CommandAddress));
        Assert.Equal(unchecked((uint)(BaseAddress + 0x204)), ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal((uint)((BaseAddress + 0x204) >> 32), ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(0x200u, ReadUInt32(memory, CommandAddress + 16));

        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 1;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbRewind(context));
        Assert.Equal(Pm4(2, 0x59), ReadUInt32(memory, CommandAddress + 20));
        Assert.Equal(0x8000_0000u, ReadUInt32(memory, CommandAddress + 24));
    }

    [Fact]
    public void CopyData_MatchesKytyDcbAndAcbSourceEncoding()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 3;
        context[CpuRegister.Rdx] = 2;
        context[CpuRegister.Rcx] = 0x1122_3344_5566_7788;
        context[CpuRegister.R8] = 5;
        context[CpuRegister.R9] = 1;
        WriteUInt64(memory, StackAddress + 8, 0x8877_6655_4433_2211);
        WriteUInt64(memory, StackAddress + 16, 1);
        WriteUInt64(memory, StackAddress + 24, 1);

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbCopyData(context));
        Assert.Equal(Pm4(6, 0x40), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x4411_2102u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x4433_2211u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x8877_6655u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 16));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 20));

        context[CpuRegister.Rdi] = CommandBufferAddress;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.AcbCopyData(context));
        // Kyty converts ACB source selector 5 to DCB selector 10.
        Assert.Equal(Pm4(6, 0x40), ReadUInt32(memory, CommandAddress + 24));
        Assert.Equal(0x0411_2105u, ReadUInt32(memory, CommandAddress + 28));
    }

    [Fact]
    public void DcbSetPredication_UsesKytySetPredicationPacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 1;
        context[CpuRegister.Rdx] = 6;
        context[CpuRegister.Rcx] = 1;
        context[CpuRegister.R8] = 0x1122_3344_5566_778F;
        context[CpuRegister.R9] = 0xDEAD; // Not encoded by the native packet.

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetPredication(context));
        Assert.Equal(Pm4(4, 0x20), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x0006_1100u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x5566_7780u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 12));
    }

    [Fact]
    public void RewindPatchSetRewindState_PreservesOtherControlBits()
    {
        var (memory, context) = NewContext();
        WriteUInt32(memory, CommandAddress + 4, 0x0123_4567);
        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = 1;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.RewindPatchSetRewindState(context));
        Assert.Equal(0x8123_4567u, ReadUInt32(memory, CommandAddress + 4));

        context[CpuRegister.Rsi] = 0;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.RewindPatchSetRewindState(context));
        Assert.Equal(0x0123_4567u, ReadUInt32(memory, CommandAddress + 4));
    }

    [Fact]
    public void JumpPatchSetTarget_PreservesIndirectBufferControlFields()
    {
        var (memory, context) = NewContext();
        WriteUInt32(memory, CommandAddress, Pm4(4, 0x3F));
        WriteUInt32(memory, CommandAddress + 8, 0xABCD_0000);
        WriteUInt32(memory, CommandAddress + 12, 0xF120_0000);
        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = 0x1122_3344_5566_7788;
        context[CpuRegister.Rdx] = 0x123456;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.JumpPatchSetTarget(context));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0xABCD_3344u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0xF122_3456u, ReadUInt32(memory, CommandAddress + 12));
    }

    [Fact]
    public void DcbSetShRegistersIndirect_UsesKytyNativePacketAndPatchFields()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = BaseAddress + 0x204;
        context[CpuRegister.Rdx] = 3;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetShRegistersIndirect(context));
        Assert.Equal(Pm4(5, 0x63), ReadUInt32(memory, CommandAddress));
        Assert.Equal(unchecked((uint)(BaseAddress + 0x204)), ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal((uint)((BaseAddress + 0x204) >> 32), ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x8000_0000u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(3u, ReadUInt32(memory, CommandAddress + 16));

        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = 0x12_345;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.SetShRegIndirectPatchSetNumRegisters(context));
        Assert.Equal(0x2345u, ReadUInt32(memory, CommandAddress + 16));

        context[CpuRegister.Rsi] = 2;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.SetShRegIndirectPatchAddRegisters(context));
        Assert.Equal(0x2347u, ReadUInt32(memory, CommandAddress + 16));
    }

    [Fact]
    public void DcbSetIndexCount_UsesKytyIndexBufferSizePacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x1337;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetIndexCount(context));
        Assert.Equal(Pm4(2, 0x13), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x1337u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(8, AgcExports.DcbSetIndexCountGetSize(context));
        Assert.Equal(8u, context[CpuRegister.Rax]);
    }

    [Fact]
    public void DcbSetIndexSize_UsesKytyUconfigIndexPacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 2;
        context[CpuRegister.Rdx] = 3;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetIndexSize(context));
        Assert.Equal(Pm4(3, 0x7A), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x2000_0243u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x4C2u, ReadUInt32(memory, CommandAddress + 8));
    }

    [Fact]
    public void DcbSetIndexBuffer_UsesKytyThreeDwordIndexBasePacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x1122_3344_5566_7788;
        context[CpuRegister.Rdx] = 99; // Not part of the native API ABI.

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbSetIndexBuffer(context));
        Assert.Equal(Pm4(3, 0x26), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 8));
    }

    [Fact]
    public void DcbDrawIndex_UsesKytySixDwordDrawIndex2Packet()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 12;
        context[CpuRegister.Rdx] = 0x1122_3344_5566_7788;
        context[CpuRegister.Rcx] = 0x100; // selects initiator bit 5

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbDrawIndex(context));
        Assert.Equal(Pm4(6, 0x27), ReadUInt32(memory, CommandAddress));
        Assert.Equal(12u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(12u, ReadUInt32(memory, CommandAddress + 16));
        Assert.Equal(0x20u, ReadUInt32(memory, CommandAddress + 20));
    }

    [Fact]
    public void DcbDrawIndexAuto_UsesKytyThreeDwordPacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 12;
        context[CpuRegister.Rdx] = 0x100;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbDrawIndexAuto(context));
        Assert.Equal(Pm4(3, 0x2D), ReadUInt32(memory, CommandAddress));
        Assert.Equal(12u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x22u, ReadUInt32(memory, CommandAddress + 8));
    }

    [Fact]
    public void DcbDrawIndexOffset_UsesKytyCountAndInitiatorEncoding()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 4;
        context[CpuRegister.Rdx] = 0;
        context[CpuRegister.Rcx] = 0x100;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbDrawIndexOffset(context));
        Assert.Equal(Pm4(5, 0x35), ReadUInt32(memory, CommandAddress));
        Assert.Equal(1u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(4u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(0x20u, ReadUInt32(memory, CommandAddress + 16));
    }

    [Fact]
    public void CbSetShRegisterRangeDirect_UsesSingleKytyRegisterPacket()
    {
        var (memory, context) = NewContext();
        var valuesAddress = BaseAddress + 0x600;
        WriteUInt32(memory, valuesAddress, 0x1111_2222);
        WriteUInt32(memory, valuesAddress + 4, 0x3333_4444);
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x1234;
        context[CpuRegister.Rdx] = valuesAddress;
        context[CpuRegister.Rcx] = 2;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.CbSetShRegisterRangeDirect(context));
        Assert.Equal(CommandAddress, context[CpuRegister.Rax]);
        Assert.Equal(Pm4(4, 0x76), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x1234u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x1111_2222u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x3333_4444u, ReadUInt32(memory, CommandAddress + 12));
    }

    [Fact]
    public void DcbDrawIndexMultiInstanced_UsesKytyNineDwordPreamble()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 12;
        context[CpuRegister.Rdx] = 0x1122_3344_5566_7788;
        context[CpuRegister.Rcx] = 0x8877_6655_4433_2211;
        context[CpuRegister.R8] = 2;
        context[CpuRegister.R9] = 0x100;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbDrawIndexMultiInstanced(context));
        Assert.Equal(Pm4(9, 0x3A), ReadUInt32(memory, CommandAddress));
        Assert.Equal(12u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(2u, ReadUInt32(memory, CommandAddress + 16));
        Assert.Equal(0x4433_2211u, ReadUInt32(memory, CommandAddress + 20));
        Assert.Equal(0x8877_6655u, ReadUInt32(memory, CommandAddress + 24));
        Assert.Equal(2u, ReadUInt32(memory, CommandAddress + 28));
        Assert.Equal(0xA0u, ReadUInt32(memory, CommandAddress + 32));
    }

    [Fact]
    public void UnknownIkfdtRIqCE_MatchesKytyIndirectBufferPatch()
    {
        var (memory, context) = NewContext();
        WriteUInt32(memory, CommandAddress, Pm4(4, 0x3F));
        WriteUInt32(memory, CommandAddress + 4, 3);
        WriteUInt32(memory, CommandAddress + 12, 0xC0F0_0000);
        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = 2;
        context[CpuRegister.Rdx] = 0x1122_3344_5566_7788;
        context[CpuRegister.Rcx] = 0x123456;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.UnknownIkfdtRIqCE(context));
        Assert.Equal(0x5566_778Bu, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0xE0F2_3456u, ReadUInt32(memory, CommandAddress + 12));
    }

    [Fact]
    public void DcbEventWrite_UsesKytyAddressedAndSpecialEncodings()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x38;
        context[CpuRegister.Rdx] = 0x1122_3344_5566_778F;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbEventWrite(context));
        Assert.Equal(Pm4(4, 0x46), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x138u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 12));

        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 7;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbEventWrite(context));
        Assert.Equal(Pm4(2, 0x46), ReadUInt32(memory, CommandAddress + 16));
        Assert.Equal(0x407u, ReadUInt32(memory, CommandAddress + 20));

        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 15;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.AcbEventWrite(context));
        Assert.Equal(Pm4(2, 0x46), ReadUInt32(memory, CommandAddress + 24));
        Assert.Equal(0x40Fu, ReadUInt32(memory, CommandAddress + 28));
    }

    [Fact]
    public void DcbResetQueue_UsesKytyClearStatePacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x3FF;
        context[CpuRegister.Rdx] = 0x1B;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbResetQueue(context));
        Assert.Equal(Pm4(2, 0x12), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0xBu, ReadUInt32(memory, CommandAddress + 4));
    }

    [Fact]
    public void DcbDrawIndexIndirectMulti_UsesKytyTenDwordPacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 0x40;
        context[CpuRegister.Rdx] = 1;
        context[CpuRegister.Rcx] = 20;
        context[CpuRegister.R8] = BaseAddress + 0x2A4;
        context[CpuRegister.R9] = 24;
        WriteUInt64(memory, StackAddress + 8, 0x6029_0707);

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.DcbDrawIndexIndirectMulti(context));
        Assert.Equal(Pm4(10, 0x38), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x40u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x0110_010Fu, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x0800_0111u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(0x4000_0000u, ReadUInt32(memory, CommandAddress + 16));
        Assert.Equal(20u, ReadUInt32(memory, CommandAddress + 20));
        Assert.Equal(unchecked((uint)(BaseAddress + 0x2A4)), ReadUInt32(memory, CommandAddress + 24));
        Assert.Equal((uint)((BaseAddress + 0x2A4) >> 32), ReadUInt32(memory, CommandAddress + 28));
        Assert.Equal(24u, ReadUInt32(memory, CommandAddress + 32));
        Assert.Equal(0x22u, ReadUInt32(memory, CommandAddress + 36));
    }

    [Fact]
    public void GetDataPacketPayloadRange_MatchesKytyMemoryRangeLayout()
    {
        var (memory, context) = NewContext();
        var rangeAddress = BaseAddress + 0x500;
        WriteUInt32(memory, CommandAddress, 0xC002_0000);
        context[CpuRegister.Rdi] = rangeAddress;
        context[CpuRegister.Rsi] = CommandAddress;
        context[CpuRegister.Rdx] = 0;

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.GetDataPacketPayloadRange(context));
        Assert.Equal(CommandAddress + 4, ReadUInt64(memory, rangeAddress));
        Assert.Equal(12UL, ReadUInt64(memory, rangeAddress + 8));

        context[CpuRegister.Rdx] = 1;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.GetDataPacketPayloadRange(context));
        Assert.Equal(CommandAddress + 8, ReadUInt64(memory, rangeAddress));
        Assert.Equal(8UL, ReadUInt64(memory, rangeAddress + 8));
    }

    [Fact]
    public void CondExecPatchHelpers_MatchKyty()
    {
        var (memory, context) = NewContext();
        WriteUInt32(memory, CommandAddress, Pm4(5, 0x22));
        WriteUInt32(memory, CommandAddress + 4, 3);
        WriteUInt32(memory, CommandAddress + 16, 0xFFFF_C000);

        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = CommandAddress + 5 * 4 + 0x80;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.CondExecPatchSetEnd(context));
        Assert.Equal(0xFFFF_C020u, ReadUInt32(memory, CommandAddress + 16));

        context[CpuRegister.Rsi] = 0x1122_3344_5566_7788;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.CondExecPatchSetCommandAddress(context));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 8));
    }

    [Fact]
    public void PacketAndRangePredication_MatchKyty()
    {
        var (memory, context) = NewContext();
        WriteUInt32(memory, CommandAddress, Pm4(2, 0x10));
        WriteUInt32(memory, CommandAddress + 8, Pm4(3, 0x15));

        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = 1;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.SetPacketPredication(context));
        Assert.Equal(Pm4(2, 0x10) | 1u, ReadUInt32(memory, CommandAddress));

        context[CpuRegister.Rdi] = CommandAddress;
        context[CpuRegister.Rsi] = CommandAddress + 20;
        context[CpuRegister.Rdx] = 0;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.SetRangePredication(context));
        Assert.Equal(Pm4(2, 0x10), ReadUInt32(memory, CommandAddress));
        Assert.Equal(Pm4(3, 0x15), ReadUInt32(memory, CommandAddress + 8));

        context[CpuRegister.Rdi] = CommandAddress;
        Assert.Equal(2, AgcExports.GetPacketSize(context));
        Assert.Equal(2UL, context[CpuRegister.Rax]);
    }

    [Fact]
    public void UpdatePrimState_MatchesKytyInPlaceRegisterUpdates()
    {
        var (memory, context) = NewContext();
        var cx = BaseAddress + 0x600;
        var uc = BaseAddress + 0x700;
        WriteUInt32(memory, cx + 4, 0); // VGT_SHADER_STAGES_EN value
        WriteUInt32(memory, cx + 12, 0xA8); // VGT_GS_OUT_PRIM_TYPE value
        WriteUInt32(memory, uc + 20, 0xFFFF_FFE0);
        context[CpuRegister.Rdi] = cx;
        context[CpuRegister.Rsi] = uc;
        context[CpuRegister.Rdx] = 3; // LineStrip

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.UpdatePrimState(context));
        Assert.Equal(0xA9u, ReadUInt32(memory, cx + 12));
        Assert.Equal(0xFFFF_FFE3u, ReadUInt32(memory, uc + 20));
    }

    [Fact]
    public void FuseShaderHalves_MatchesKytyGsFusion()
    {
        var (memory, context) = NewContext();
        var front = BaseAddress + 0x800;
        var back = BaseAddress + 0x900;
        var result = BaseAddress + 0xA00;
        var frontRegisters = BaseAddress + 0xB00;
        var backRegisters = BaseAddress + 0xC00;
        var scratch = BaseAddress + 0xD00;
        var frontSpecials = BaseAddress + 0xE00;
        var backSpecials = BaseAddress + 0xF00;
        const ulong frontCode = 0x1234_5678_9ABC_DEF0;

        WriteUInt64(memory, front + 0x10, frontCode);
        WriteUInt64(memory, front + 0x20, frontRegisters);
        WriteUInt64(memory, front + 0x28, frontSpecials);
        WriteByte(memory, front + 0x5A, 4); // GS front
        WriteByte(memory, front + 0x5C, 2);
        WriteUInt64(memory, back + 0x08, 0xDEAD_BEEF);
        WriteUInt64(memory, back + 0x20, backRegisters);
        WriteUInt64(memory, back + 0x28, backSpecials);
        WriteByte(memory, back + 0x5A, 6); // GS back
        WriteByte(memory, back + 0x5C, 4);
        WriteUInt32(memory, frontSpecials + 12, 0x0040_0000);
        WriteUInt32(memory, backSpecials + 12, 0x0040_0000);
        WriteRegister(memory, frontRegisters, 0, 0x80, 0x1111_1111);
        WriteRegister(memory, frontRegisters, 1, 0x80, 0x2222_2222);
        WriteRegister(memory, backRegisters, 0, 0x80, 0);
        WriteRegister(memory, backRegisters, 1, 0x80, 0);
        WriteRegister(memory, backRegisters, 2, 0xC8, 0);
        WriteRegister(memory, backRegisters, 3, 0xC9, 0xAB00_0000);

        context[CpuRegister.Rdi] = result;
        context[CpuRegister.Rsi] = front;
        context[CpuRegister.Rdx] = back;
        context[CpuRegister.Rcx] = scratch;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.UnknownFuseShaderHalves(context));
        Assert.Equal((byte)2, ReadByte(memory, result + 0x5A));
        Assert.Equal(0UL, ReadUInt64(memory, result + 0x08));
        Assert.Equal(scratch, ReadUInt64(memory, result + 0x20));
        Assert.Equal(0x1111_1111u, ReadUInt32(memory, scratch + 4));
        Assert.Equal(0x2222_2222u, ReadUInt32(memory, scratch + 12));
        Assert.Equal((uint)((frontCode >> 8) & uint.MaxValue), ReadUInt32(memory, scratch + 20));
        Assert.Equal(0xAB00_0056u, ReadUInt32(memory, scratch + 28));

        var size = BaseAddress + 0x1000;
        context[CpuRegister.Rdi] = size;
        context[CpuRegister.Rsi] = front;
        context[CpuRegister.Rdx] = back;
        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.UnknownGetFusedShaderSize(context));
        Assert.Equal(32UL, ReadUInt64(memory, size));
        Assert.Equal(4UL, ReadUInt64(memory, size + 8));
    }

    [Fact]
    public void CbBranch_UsesKytyConditionalIndirectBufferPacket()
    {
        var (memory, context) = NewContext();
        context[CpuRegister.Rdi] = CommandBufferAddress;
        context[CpuRegister.Rsi] = 2;
        context[CpuRegister.Rdx] = 3;
        context[CpuRegister.Rcx] = 0x1122_3344_5566_778F;
        context[CpuRegister.R8] = 0xAABB_CCDD_EEFF_0011;
        context[CpuRegister.R9] = 0x8877_6655_4433_2211;
        WriteUInt64(memory, StackAddress + 8, 2);
        WriteUInt64(memory, StackAddress + 16, 0x1234_5678_9ABC_DEF3);
        WriteUInt64(memory, StackAddress + 24, 0x1ABCDE);
        WriteUInt64(memory, StackAddress + 32, 1);
        WriteUInt64(memory, StackAddress + 40, 0x0102_0304_0506_070B);
        WriteUInt64(memory, StackAddress + 48, 0x23456);

        Assert.Equal((int)OrbisGen2Result.ORBIS_GEN2_OK, AgcExports.CbBranch(context));
        Assert.Equal(Pm4(14, 0x3F), ReadUInt32(memory, CommandAddress));
        Assert.Equal(0x0000_0302u, ReadUInt32(memory, CommandAddress + 4));
        Assert.Equal(0x5566_7788u, ReadUInt32(memory, CommandAddress + 8));
        Assert.Equal(0x1122_3344u, ReadUInt32(memory, CommandAddress + 12));
        Assert.Equal(0xEEFF_0011u, ReadUInt32(memory, CommandAddress + 16));
        Assert.Equal(0xAABB_CCDDu, ReadUInt32(memory, CommandAddress + 20));
        Assert.Equal(0x4433_2211u, ReadUInt32(memory, CommandAddress + 24));
        Assert.Equal(0x8877_6655u, ReadUInt32(memory, CommandAddress + 28));
        Assert.Equal(0x9ABC_DEF0u, ReadUInt32(memory, CommandAddress + 32));
        Assert.Equal(0x1234_5678u, ReadUInt32(memory, CommandAddress + 36));
        Assert.Equal(0x200A_BCDEu, ReadUInt32(memory, CommandAddress + 40));
        Assert.Equal(0x0506_0708u, ReadUInt32(memory, CommandAddress + 44));
        Assert.Equal(0x0102_0304u, ReadUInt32(memory, CommandAddress + 48));
        Assert.Equal(0x1002_3456u, ReadUInt32(memory, CommandAddress + 52));
    }

    private static (FakeCpuMemory Memory, CpuContext Context) NewContext()
    {
        var memory = new FakeCpuMemory(BaseAddress, 0x4000);
        WriteUInt64(memory, CommandBufferAddress + 0x10, CommandAddress);
        WriteUInt64(memory, CommandBufferAddress + 0x18, CommandAddress + 0x1000);
        var context = new CpuContext(memory, Generation.Gen5);
        context[CpuRegister.Rsp] = StackAddress;
        return (memory, context);
    }

    private static uint Pm4(uint count, uint op) => 0xC000_0000u | ((count - 2) << 16) | (op << 8);

    private static uint ReadUInt32(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[4];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    }

    private static ulong ReadUInt64(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[8];
        Assert.True(memory.TryRead(address, bytes));
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static void WriteUInt64(FakeCpuMemory memory, ulong address, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        Assert.True(memory.TryWrite(address, bytes));
    }

    private static void WriteUInt32(FakeCpuMemory memory, ulong address, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        Assert.True(memory.TryWrite(address, bytes));
    }

    private static byte ReadByte(FakeCpuMemory memory, ulong address)
    {
        Span<byte> bytes = stackalloc byte[1];
        Assert.True(memory.TryRead(address, bytes));
        return bytes[0];
    }

    private static void WriteByte(FakeCpuMemory memory, ulong address, byte value)
    {
        Span<byte> bytes = stackalloc byte[] { value };
        Assert.True(memory.TryWrite(address, bytes));
    }

    private static void WriteRegister(FakeCpuMemory memory, ulong address, uint index, uint offset, uint value)
    {
        WriteUInt32(memory, address + index * 8, offset);
        WriteUInt32(memory, address + index * 8 + 4, value);
    }
}
