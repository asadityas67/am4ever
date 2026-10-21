// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Emu;

namespace Am4ever.Tests.Emu;

public class HostLocatorTests
{
    [Fact] public void HostPath_Resolve_DoesNotThrow()
    {
        _ = EcEmulator.HostPath;   // resolving the host must never throw, present or not
    }

    [EmuFact] public void Run_HostFailure_ThrowsClean() =>
        Assert.Throws<EmulatorException>(() => EcEmulator.RunEcFan(new byte[] { 1, 2, 3 }));   // not a valid PE
}
