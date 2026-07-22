// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;

namespace SharpEmu.Libs.Network;

/// <summary>Kyty-compatible initialization state for the optional RUDP library.</summary>
public static class RudpExports
{
    private static readonly object StateGate = new();
    private static ulong _eventHandler;
    private static ulong _eventArgument;

    [SysAbiExport(Nid = "amuBfI-AQc4", ExportName = "sceRudpInit", Target = Generation.Gen5, LibraryName = "libSceRudp")]
    public static int RudpInit(CpuContext ctx)
    {
        // Kyty accepts the caller-managed pool without imposing an invented
        // minimum size; no network work is scheduled until a real transport is present.
        return ctx.SetReturn(0);
    }

    [SysAbiExport(Nid = "6PBNpsgyaxw", ExportName = "sceRudpEnableInternalIOThread", Target = Generation.Gen5, LibraryName = "libSceRudp")]
    public static int RudpEnableInternalIoThread(CpuContext ctx) => ctx.SetReturn(0);

    [SysAbiExport(Nid = "SUEVes8gvmw", ExportName = "sceRudpSetEventHandler", Target = Generation.Gen5, LibraryName = "libSceRudp")]
    public static int RudpSetEventHandler(CpuContext ctx)
    {
        lock (StateGate)
        {
            _eventHandler = ctx[CpuRegister.Rdi];
            _eventArgument = ctx[CpuRegister.Rsi];
        }

        return ctx.SetReturn(0);
    }
}
