// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;

namespace SharpEmu.Libs.TextToSpeech;

public static class TextToSpeechExports
{
    // Kyty exposes this optional accessibility-service initializer as a
    // successful compatibility path; speech synthesis itself is not claimed.
    #pragma warning disable SHEM006
    [SysAbiExport(
        Nid = "UOjiprYwVNw",
        ExportName = "sceTextToSpeechInitialize",
        Target = Generation.Gen5,
        LibraryName = "libSceTextToSpeech2")]
    public static int Initialize(CpuContext ctx) => ctx.SetReturn(0);
    #pragma warning restore SHEM006
}
