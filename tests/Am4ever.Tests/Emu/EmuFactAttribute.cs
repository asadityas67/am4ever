// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Emu;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Emu;

/// <summary>A Fact that runs only when the native uc_host emulator binary has been built.</summary>
public sealed class EmuFactAttribute : FactAttribute
{
    public EmuFactAttribute() =>
        Skip = EcEmulator.HostPath is null
            ? "build the native emulator host (native/build) to run emulator tests"
            : null;
}

/// <summary>A Fact that needs both the native host AND the developer's reference ROMs.</summary>
public sealed class EmuRomFactAttribute : FactAttribute
{
    public EmuRomFactAttribute() =>
        Skip = UcHost.HostPath is null ? "build the native emulator host (native/build)" : TestRoms.SkipReason;
}
