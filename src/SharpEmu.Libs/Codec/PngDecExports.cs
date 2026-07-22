// SPDX-License-Identifier: GPL-2.0-or-later

using System.Buffers.Binary;
using SharpEmu.HLE;
using SharpEmu.Libs.VideoOut;

namespace SharpEmu.Libs.Codec;

/// <summary>libScePngDec front end over SharpEmu's PNG scanline decoder.</summary>
public static class PngDecExports
{
    private const int InvalidAddress = unchecked((int)0x80690001);
    private const int InvalidSize = unchecked((int)0x80690002);
    private const int InvalidParameter = unchecked((int)0x80690003);
    private const int InvalidHandle = unchecked((int)0x80690004);
    private const int InvalidWorkMemory = unchecked((int)0x80690005);
    private const int InvalidData = unchecked((int)0x80690010);
    private const int DecodeError = unchecked((int)0x80690012);
    private const ulong ContextMagic = 0x4B59_5459_504E_4744;

    [SysAbiExport(Nid = "-6srIGbLTIU", ExportName = "scePngDecQueryMemorySize", Target = Generation.Gen5, LibraryName = "libScePngDec")]
    public static int QueryMemorySize(CpuContext ctx)
    {
        if (!TryReadCreateParameter(ctx, ctx[CpuRegister.Rdi], out var attribute, out var maxWidth))
            return Return(ctx, InvalidParameter);
        if (attribute > 1) return Return(ctx, InvalidParameter);
        return Return(ctx, maxWidth == 0 ? InvalidSize : sizeof(ulong));
    }

    [SysAbiExport(Nid = "m0uW+8pFyaw", ExportName = "scePngDecCreate", Target = Generation.Gen5, LibraryName = "libScePngDec")]
    public static int Create(CpuContext ctx)
    {
        var parameter = ctx[CpuRegister.Rdi];
        var workMemory = ctx[CpuRegister.Rsi];
        var workMemorySize = (uint)ctx[CpuRegister.Rdx];
        var handleOut = ctx[CpuRegister.Rcx];
        if (parameter == 0 || handleOut == 0 || !TryReadCreateParameter(ctx, parameter, out var attribute, out var maxWidth))
            return Return(ctx, InvalidParameter);
        if (attribute > 1) return Return(ctx, InvalidParameter);
        if (maxWidth == 0) return Return(ctx, InvalidSize);
        if (workMemory == 0) return Return(ctx, InvalidAddress);
        if (workMemorySize < sizeof(ulong)) return Return(ctx, InvalidWorkMemory);
        return ctx.TryWriteUInt64(workMemory, ContextMagic) && ctx.TryWriteUInt64(handleOut, workMemory)
            ? Return(ctx, 0) : Return(ctx, MemoryFault);
    }

    [SysAbiExport(Nid = "U6h4e5JRPaQ", ExportName = "scePngDecParseHeader", Target = Generation.Gen5, LibraryName = "libScePngDec")]
    public static int ParseHeader(CpuContext ctx)
    {
        var parameter = ctx[CpuRegister.Rdi];
        var infoOut = ctx[CpuRegister.Rsi];
        if (parameter == 0 || infoOut == 0 || !ctx.TryReadUInt64(parameter, out var dataAddress) ||
            !ctx.TryReadUInt32(parameter + 8, out var dataSize) || !ctx.TryReadUInt32(parameter + 12, out var reserved))
            return Return(ctx, InvalidParameter);
        if (dataAddress == 0) return Return(ctx, InvalidAddress);
        if (reserved != 0) return Return(ctx, InvalidParameter);
        return TryParseHeader(ctx, dataAddress, dataSize, out var header)
            ? WriteImageInfo(ctx, infoOut, header) ? Return(ctx, 0) : Return(ctx, MemoryFault)
            : Return(ctx, InvalidData);
    }

    [SysAbiExport(Nid = "WC216DD3El4", ExportName = "scePngDecDecode", Target = Generation.Gen5, LibraryName = "libScePngDec")]
    public static int Decode(CpuContext ctx)
    {
        var handle = ctx[CpuRegister.Rdi];
        var parameter = ctx[CpuRegister.Rsi];
        var infoOut = ctx[CpuRegister.Rdx];
        if (handle == 0 || !ctx.TryReadUInt64(handle, out var magic) || magic != ContextMagic) return Return(ctx, InvalidHandle);
        if (parameter == 0) return Return(ctx, InvalidParameter);
        if (!ctx.TryReadUInt64(parameter, out var dataAddress) || !ctx.TryReadUInt64(parameter + 8, out var imageAddress) ||
            !ctx.TryReadUInt32(parameter + 16, out var dataSize) || !ctx.TryReadUInt32(parameter + 20, out var imageSize) ||
            !TryReadUInt16(ctx, parameter + 24, out var pixelFormat) || !TryReadUInt16(ctx, parameter + 26, out var alpha) ||
            !ctx.TryReadUInt32(parameter + 28, out var pitch)) return Return(ctx, MemoryFault);
        if (dataAddress == 0 || imageAddress == 0) return Return(ctx, InvalidAddress);
        if (pixelFormat is not (0 or 1)) return Return(ctx, InvalidParameter);
        if (!TryParseHeader(ctx, dataAddress, dataSize, out var header)) return Return(ctx, InvalidData);
        if (infoOut != 0 && !WriteImageInfo(ctx, infoOut, header)) return Return(ctx, MemoryFault);
        var minPitch = checked(header.Width * 4u);
        var effectivePitch = pitch == 0 ? minPitch : pitch;
        var minimumSize = (ulong)effectivePitch * (header.Height - 1) + minPitch;
        if (effectivePitch < minPitch || minimumSize > imageSize) return Return(ctx, InvalidSize);
        var png = new byte[dataSize];
        if (!ctx.Memory.TryRead(dataAddress, png)) return Return(ctx, MemoryFault);
        if (!PngSplashLoader.TryDecode(png, out var pixels, out var width, out var height, requestRgba: pixelFormat == 0) ||
            width != header.Width || height != header.Height) return Return(ctx, DecodeError);
        var sourceHasAlpha = header.ColorSpace is 18 or 19 || (header.Flags & 2) != 0;
        for (uint row = 0; row < height; row++)
        {
            var line = pixels.AsSpan(checked((int)(row * minPitch)), checked((int)minPitch));
            if (!sourceHasAlpha)
                for (var index = 3; index < line.Length; index += 4) line[index] = (byte)Math.Min(alpha, (ushort)255);
            if (!ctx.Memory.TryWrite(imageAddress + (ulong)row * effectivePitch, line)) return Return(ctx, MemoryFault);
        }
        return Return(ctx, width > 32767 || height > 32767 ? 0 : unchecked((int)((width << 16) | height)));
    }

    [SysAbiExport(Nid = "QbD+eENEwo8", ExportName = "scePngDecDelete", Target = Generation.Gen5, LibraryName = "libScePngDec")]
    public static int Delete(CpuContext ctx)
    {
        var handle = ctx[CpuRegister.Rdi];
        if (handle == 0 || !ctx.TryReadUInt64(handle, out var magic) || magic != ContextMagic) return Return(ctx, InvalidHandle);
        return ctx.TryWriteUInt64(handle, 0) ? Return(ctx, 0) : Return(ctx, MemoryFault);
    }

    private readonly record struct Header(uint Width, uint Height, ushort ColorSpace, ushort BitDepth, uint Flags);
    private static bool TryReadCreateParameter(CpuContext ctx, ulong address, out uint attribute, out uint maxWidth)
    {
        attribute = 0;
        maxWidth = 0;
        return address != 0 && ctx.TryReadUInt32(address + 4, out attribute) &&
               ctx.TryReadUInt32(address + 8, out maxWidth);
    }
    private static bool TryParseHeader(CpuContext ctx, ulong address, uint size, out Header header)
    {
        header = default;
        if (size < 33) return false;
        var bytes = new byte[size];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (!ctx.Memory.TryRead(address, bytes) || !bytes.AsSpan(0, 8).SequenceEqual(signature) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)) != 13 || !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return false;
        var color = bytes[25] switch { 0 => (ushort)2, 2 => (ushort)3, 3 => (ushort)4, 4 => (ushort)18, 6 => (ushort)19, _ => (ushort)0 };
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)); var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));
        if (color == 0 || width == 0 || height == 0) return false;
        var flags = bytes[28] == 1 ? 1u : 0u;
        for (var offset = 33; offset <= bytes.Length - 12; )
        { var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)); if (length > int.MaxValue || offset > bytes.Length - 12 - (int)length) return false; var type = bytes.AsSpan(offset + 4, 4); if (type.SequenceEqual("tRNS"u8)) flags |= 2; if (type.SequenceEqual("IDAT"u8) || type.SequenceEqual("IEND"u8)) break; offset += (int)length + 12; }
        header = new(width, height, color, bytes[24], flags); return true;
    }
    private static bool WriteImageInfo(CpuContext ctx, ulong address, Header value) =>
        ctx.TryWriteUInt32(address, value.Width) && ctx.TryWriteUInt32(address + 4, value.Height) && TryWriteUInt16(ctx, address + 8, value.ColorSpace) && TryWriteUInt16(ctx, address + 10, value.BitDepth) && ctx.TryWriteUInt32(address + 12, value.Flags);
    private static bool TryReadUInt16(CpuContext ctx, ulong address, out ushort value) { Span<byte> bytes = stackalloc byte[2]; if (!ctx.Memory.TryRead(address, bytes)) { value = 0; return false; } value = BinaryPrimitives.ReadUInt16LittleEndian(bytes); return true; }
    private static bool TryWriteUInt16(CpuContext ctx, ulong address, ushort value) { Span<byte> bytes = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, value); return ctx.Memory.TryWrite(address, bytes); }
    private static int Return(CpuContext ctx, int result) => ctx.SetReturn(result);
    private const int MemoryFault = (int)OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT;
}
