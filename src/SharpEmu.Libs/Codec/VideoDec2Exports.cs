// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Threading;

namespace SharpEmu.Libs.Codec;

/// <summary>
/// Source-compatible libSceVideodec2 control-plane implementation. It
/// exposes decoder allocation and validation but produces no
/// decoded pictures; retain that contract until a real video decoder exists.
/// </summary>
public static class VideoDec2Exports
{
    private const int ErrorStructSize = unchecked((int)0x811D0101);
    private const int ErrorArgumentPointer = unchecked((int)0x811D0102);
    private const int ErrorDecoderInstance = unchecked((int)0x811D0103);
    private const int ErrorMemorySize = unchecked((int)0x811D0104);
    private const int ErrorMemoryPointer = unchecked((int)0x811D0105);
    private const int ErrorFrameBufferSize = unchecked((int)0x811D0106);
    private const int ErrorFrameBufferPointer = unchecked((int)0x811D0107);
    private const int ErrorConfigInfo = unchecked((int)0x811D0200);
    private const int ErrorComputePipeId = unchecked((int)0x811D0201);
    private const int ErrorComputeQueueId = unchecked((int)0x811D0202);
    private const int ErrorResourceType = unchecked((int)0x811D0203);
    private const int ErrorInputQueueDepth = unchecked((int)0x811D0206);
    private const int ErrorDpbFrameCount = unchecked((int)0x811D0209);
    private const int ErrorFrameWidthHeight = unchecked((int)0x811D020A);
    private const ulong MinimumMemorySize = 16UL * 1024 * 1024;

    private static long _nextDecoder;
    private static readonly ConcurrentDictionary<ulong, uint> Decoders = new();

    [SysAbiExport(Nid = "RnDibcGCPKw", ExportName = "sceVideodec2QueryComputeMemoryInfo", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int QueryComputeMemoryInfo(CpuContext ctx)
    {
        var address = ctx[CpuRegister.Rdi];
        if (address == 0) return SetReturn(ctx, ErrorArgumentPointer);
        if (!TryRead(ctx, address, 24, out var info)) return SetReturn(ctx, ErrorArgumentPointer);
        if (U64(info, 0) != 24) return SetReturn(ctx, ErrorStructSize);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(8), MinimumMemorySize);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(16), 0);
        return Write(ctx, address, info);
    }

    [SysAbiExport(Nid = "eD+X2SmxUt4", ExportName = "sceVideodec2AllocateComputeQueue", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int AllocateComputeQueue(CpuContext ctx)
    {
        var configAddress = ctx[CpuRegister.Rdi];
        var memoryAddress = ctx[CpuRegister.Rsi];
        var outputAddress = ctx[CpuRegister.Rdx];
        if (configAddress == 0 || memoryAddress == 0 || outputAddress == 0) return SetReturn(ctx, ErrorArgumentPointer);
        if (!TryRead(ctx, configAddress, 16, out var config) || !TryRead(ctx, memoryAddress, 24, out var memory)) return SetReturn(ctx, ErrorArgumentPointer);
        if (U64(config, 0) != 16 || U64(memory, 0) != 24) return SetReturn(ctx, ErrorStructSize);
        if (config[13] != 0 || U16(config, 14) != 0) return SetReturn(ctx, ErrorConfigInfo);
        if (U16(config, 8) > 4) return SetReturn(ctx, ErrorComputePipeId);
        if (U16(config, 10) > 7) return SetReturn(ctx, ErrorComputeQueueId);
        if (U64(memory, 8) < MinimumMemorySize) return SetReturn(ctx, ErrorMemorySize);
        var queue = U64(memory, 16);
        if (queue == 0) return SetReturn(ctx, ErrorMemoryPointer);
        return ctx.TryWriteUInt64(outputAddress, queue) ? SetReturn(ctx, 0) : SetReturn(ctx, ErrorArgumentPointer);
    }

    [SysAbiExport(Nid = "UvtA3FAiF4Y", ExportName = "sceVideodec2ReleaseComputeQueue", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int ReleaseComputeQueue(CpuContext ctx) => SetReturn(ctx, ctx[CpuRegister.Rdi] == 0 ? ErrorComputeQueueId : 0);

    [SysAbiExport(Nid = "qqMCwlULR+E", ExportName = "sceVideodec2QueryDecoderMemoryInfo", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int QueryDecoderMemoryInfo(CpuContext ctx)
    {
        var configAddress = ctx[CpuRegister.Rdi];
        var infoAddress = ctx[CpuRegister.Rsi];
        if (configAddress == 0 || infoAddress == 0) return SetReturn(ctx, ErrorArgumentPointer);
        if (!TryRead(ctx, configAddress, 72, out var config) || !TryRead(ctx, infoAddress, 72, out var info)) return SetReturn(ctx, ErrorArgumentPointer);
        if (U64(config, 0) != 72 || U64(info, 0) != 72) return SetReturn(ctx, ErrorStructSize);
        var result = ValidateConfig(config);
        if (result != 0) return SetReturn(ctx, result);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(8), MinimumMemorySize);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(16), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(24), MinimumMemorySize);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(32), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(40), MinimumMemorySize);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(48), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(info.AsSpan(56), MinimumMemorySize);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(64), 0x100);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(68), 0);
        return Write(ctx, infoAddress, info);
    }

    [SysAbiExport(Nid = "CNNRoRYd8XI", ExportName = "sceVideodec2CreateDecoder", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int CreateDecoder(CpuContext ctx)
    {
        var configAddress = ctx[CpuRegister.Rdi];
        var infoAddress = ctx[CpuRegister.Rsi];
        var outputAddress = ctx[CpuRegister.Rdx];
        if (configAddress == 0 || infoAddress == 0 || outputAddress == 0) return SetReturn(ctx, ErrorArgumentPointer);
        if (!TryRead(ctx, configAddress, 72, out var config) || !TryRead(ctx, infoAddress, 72, out var info)) return SetReturn(ctx, ErrorArgumentPointer);
        if (U64(config, 0) != 72 || U64(info, 0) != 72) return SetReturn(ctx, ErrorStructSize);
        var result = ValidateConfig(config);
        if (result != 0) return SetReturn(ctx, result);
        if (U64(info, 8) < MinimumMemorySize || U64(info, 24) < MinimumMemorySize || U64(info, 40) < MinimumMemorySize || U64(info, 56) < MinimumMemorySize) return SetReturn(ctx, ErrorMemorySize);
        if (U64(info, 16) == 0 || U64(info, 32) == 0 || U64(info, 48) == 0) return SetReturn(ctx, ErrorMemoryPointer);
        var handle = unchecked((ulong)Interlocked.Increment(ref _nextDecoder));
        Decoders[handle] = U32(config, 12);
        return ctx.TryWriteUInt64(outputAddress, handle) ? SetReturn(ctx, 0) : SetReturn(ctx, ErrorArgumentPointer);
    }

    [SysAbiExport(Nid = "jwImxXRGSKA", ExportName = "sceVideodec2DeleteDecoder", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int DeleteDecoder(CpuContext ctx) => SetReturn(ctx, Decoders.TryRemove(ctx[CpuRegister.Rdi], out _) ? 0 : ErrorDecoderInstance);

    [SysAbiExport(Nid = "852F5+q6+iM", ExportName = "sceVideodec2Decode", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int Decode(CpuContext ctx) => DecodeOrFlush(ctx, true);

    [SysAbiExport(Nid = "l1hXwscLuCY", ExportName = "sceVideodec2Flush", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int Flush(CpuContext ctx) => DecodeOrFlush(ctx, false);

    [SysAbiExport(Nid = "wJXikG6QFN8", ExportName = "sceVideodec2Reset", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int Reset(CpuContext ctx) => SetReturn(ctx, Decoders.ContainsKey(ctx[CpuRegister.Rdi]) ? 0 : ErrorDecoderInstance);

    [SysAbiExport(Nid = "NtXRa3dRzU0", ExportName = "sceVideodec2GetPictureInfo", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int GetPictureInfo(CpuContext ctx)
    {
        var address = ctx[CpuRegister.Rdi];
        if (address == 0) return SetReturn(ctx, ErrorArgumentPointer);
        if (!TryRead(ctx, address, 8, out var header)) return SetReturn(ctx, ErrorArgumentPointer);
        return SetReturn(ctx, IsOutputSize(U64(header, 0)) ? 0 : ErrorStructSize);
    }

    [SysAbiExport(Nid = "kjrLbcyhEiw", ExportName = "sceVideodec2GetPictureInfo2", Target = Generation.Gen5, LibraryName = "libSceVideodec2")]
    public static int GetPictureInfo2(CpuContext ctx) => GetPictureInfo(ctx);

    private static int DecodeOrFlush(CpuContext ctx, bool decode)
    {
        if (!Decoders.TryGetValue(ctx[CpuRegister.Rdi], out var codec)) return SetReturn(ctx, ErrorDecoderInstance);
        var frameAddress = ctx[CpuRegister.Rdx];
        var outputAddress = ctx[CpuRegister.Rcx];
        if (decode)
        {
            var inputAddress = ctx[CpuRegister.Rsi];
            if (inputAddress == 0 || frameAddress == 0 || outputAddress == 0) return SetReturn(ctx, ErrorArgumentPointer);
            if (!TryRead(ctx, inputAddress, 48, out var input) || !TryRead(ctx, frameAddress, 32, out var frame) || !TryRead(ctx, outputAddress, 56, out var output)) return SetReturn(ctx, ErrorArgumentPointer);
            if (U64(input, 0) != 48 || U64(frame, 0) != 32 || !IsOutputSize(U64(output, 0))) return SetReturn(ctx, ErrorStructSize);
            if (U64(input, 16) != 0 && U64(input, 8) == 0) return SetReturn(ctx, ErrorArgumentPointer);
            return FillNoPicture(ctx, frameAddress, frame, outputAddress, output, codec);
        }
        if (frameAddress == 0 || outputAddress == 0) return SetReturn(ctx, ErrorArgumentPointer);
        if (!TryRead(ctx, frameAddress, 32, out var flushFrame) || !TryRead(ctx, outputAddress, 56, out var flushOutput)) return SetReturn(ctx, ErrorArgumentPointer);
        if (U64(flushFrame, 0) != 32 || !IsOutputSize(U64(flushOutput, 0))) return SetReturn(ctx, ErrorStructSize);
        return FillNoPicture(ctx, frameAddress, flushFrame, outputAddress, flushOutput, codec);
    }

    private static int FillNoPicture(CpuContext ctx, ulong frameAddress, Span<byte> frame, ulong outputAddress, Span<byte> output, uint codec)
    {
        if (U64(frame, 16) == 0) return SetReturn(ctx, ErrorFrameBufferSize);
        if (U64(frame, 8) == 0) return SetReturn(ctx, ErrorFrameBufferPointer);
        frame[24] = 0;
        output.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(output, 56);
        BinaryPrimitives.WriteUInt32LittleEndian(output[12..], codec);
        BinaryPrimitives.WriteUInt64LittleEndian(output[32..], U64(frame, 8));
        BinaryPrimitives.WriteUInt64LittleEndian(output[40..], U64(frame, 16));
        return ctx.Memory.TryWrite(frameAddress, frame) && ctx.Memory.TryWrite(outputAddress, output) ? SetReturn(ctx, 0) : SetReturn(ctx, ErrorArgumentPointer);
    }

    private static int ValidateConfig(ReadOnlySpan<byte> config)
    {
        if (U32(config, 8) != 1) return ErrorResourceType;
        if (config[61] != 0 || config[62] != 0) return ErrorConfigInfo;
        if (U32(config, 36) == 0) return ErrorInputQueueDepth;
        var dpb = I32(config, 32);
        if (dpb < -1 || dpb == 0) return ErrorDpbFrameCount;
        var width = I32(config, 24); var height = I32(config, 28);
        if (width < -1 || height < -1 || width == 0 || height == 0) return ErrorFrameWidthHeight;
        return U64(config, 40) == 0 ? ErrorConfigInfo : 0;
    }

    private static bool IsOutputSize(ulong size) => size == 56 || (size | 8UL) == 56;
    private static bool TryRead(CpuContext ctx, ulong address, int size, out byte[] buffer) { buffer = new byte[size]; return ctx.Memory.TryRead(address, buffer); }
    private static int Write(CpuContext ctx, ulong address, ReadOnlySpan<byte> buffer) => ctx.Memory.TryWrite(address, buffer) ? SetReturn(ctx, 0) : SetReturn(ctx, ErrorArgumentPointer);
    private static ushort U16(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b[o..]);
    private static uint U32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
    private static int I32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b[o..]);
    private static ulong U64(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt64LittleEndian(b[o..]);
    private static int SetReturn(CpuContext ctx, int result) { ctx[CpuRegister.Rax] = unchecked((ulong)result); return result; }
}
