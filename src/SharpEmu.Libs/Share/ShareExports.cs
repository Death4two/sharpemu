// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later
using SharpEmu.HLE;
using System.Buffers.Binary;
namespace SharpEmu.Libs.Share;
public static class ShareExports
{
    const int InvalidParam = unchecked((int)0x81960002), NotSupported = unchecked((int)0x81960007);
    static bool Flags(CpuContext c) => (uint)c[CpuRegister.Rdi] != 0;
    static int Capture(CpuContext c) { var p=c[CpuRegister.Rsi]; if(p!=0){Span<byte>b=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(b,-1);if(!c.Memory.TryWrite(p,b))return c.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);}return c.SetReturn(NotSupported); }
    [SysAbiExport(Nid="4jt8pMDudgk",ExportName="sceShareCaptureVideoClip",Target=Generation.Gen5,LibraryName="libSceShare")] public static int CaptureVideo(CpuContext c)=>Capture(c);
    [SysAbiExport(Nid="AcDNpEpoT9U",ExportName="sceShareCaptureVideoClipExtended",Target=Generation.Gen5,LibraryName="libSceShare")] public static int CaptureVideoEx(CpuContext c)=>Capture(c);
    [SysAbiExport(Nid="ErH6tKS7fzE",ExportName="sceShareCaptureScreenshot",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Screenshot(CpuContext c)=>Capture(c);
    [SysAbiExport(Nid="GQTObcITIXI",ExportName="sceShareCaptureScreenshotExtended",Target=Generation.Gen5,LibraryName="libSceShare")] public static int ScreenshotEx(CpuContext c)=>Capture(c);
    [SysAbiExport(Nid="8qAJ0Jd58-Q",ExportName="sceShareOpenMenuForContent",Target=Generation.Gen5,LibraryName="libSceShare")] public static int OpenMenu(CpuContext c)=>c.SetReturn(NotSupported);
    [SysAbiExport(Nid="KnsfHKmZqFA",ExportName="sceShareUnregisterContentEventCallback",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Unregister(CpuContext c)=>c.SetReturn(0);
    [SysAbiExport(Nid="T64o-315wbg",ExportName="sceShareSetScreenshotOverlayImage",Target=Generation.Gen5,LibraryName="libSceShare")] public static int SetOverlay(CpuContext c)=>c.SetReturn(0);
    [SysAbiExport(Nid="Sygnk9dr5WQ",ExportName="sceShareRegisterContentEventCallback",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Register(CpuContext c)=>c.SetReturn(0);
    [SysAbiExport(Nid="7QZtURYnXG4",ExportName="sceShareSetContentParam",Target=Generation.Gen5,LibraryName="libSceShare")] public static int SetContent(CpuContext c)=>c.SetReturn(c[CpuRegister.Rdi]==0?InvalidParam:0);
    [SysAbiExport(Nid="ORspsWDXPps",ExportName="sceShareSetContentParamForApplicationTitle",Target=Generation.Gen5,LibraryName="libSceShare")] public static int SetTitle(CpuContext c)=>c.SetReturn(c[CpuRegister.Rdi]==0?InvalidParam:0);
    [SysAbiExport(Nid="5wjxESwX68I",ExportName="sceShareFeatureProhibit",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Prohibit(CpuContext c)=>c.SetReturn(Flags(c)?0:InvalidParam);
    [SysAbiExport(Nid="YBiIdcDPrxs",ExportName="sceShareFeaturePermit",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Permit(CpuContext c)=>c.SetReturn(Flags(c)?0:InvalidParam);
    [SysAbiExport(Nid="kCurUZVFqcI",ExportName="sceShareSetCaptureSource",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Source(CpuContext c)=>c.SetReturn(Flags(c)?0:InvalidParam);
    [SysAbiExport(Nid="QNop2YAtIDE",ExportName="sceShareGetCurrentStatus",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Status(CpuContext c){var p=c[CpuRegister.Rsi];if(!Flags(c)||p==0)return c.SetReturn(InvalidParam);return c.Memory.TryWrite(p,new byte[16])?c.SetReturn(0):c.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);}
    [SysAbiExport(Nid="crFxyW3HdK0",ExportName="sceShareGetRunningStatus",Target=Generation.Gen5,LibraryName="libSceShare")] public static int Running(CpuContext c){var p=c[CpuRegister.Rdi];if(p==0)return c.SetReturn(InvalidParam);Span<byte>b=stackalloc byte[4];b.Clear();return c.Memory.TryWrite(p,b)?c.SetReturn(0):c.SetReturn(OrbisGen2Result.ORBIS_GEN2_ERROR_MEMORY_FAULT);}
}
