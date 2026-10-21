// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Ec;
using Am4ever.Core.Rom.Fv;
using Am4ever.Emu;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Emu;

public class EcFanVerificationTests
{
    static byte[] DriverPe()
    {
        var buf = SyntheticFv.WrapFile(EcFanDriver.Build(), out _);
        var file = new FirmwareVolume(buf, 0).FindByGuid(EcFanDriver.FileGuid);
        var (s, e) = file.ImageSection();          // the PE32 section body
        return buf[s..e];
    }

    [EmuFact] public void HpEcFanInit_PerformsEcHandoff()
    {
        var r = EcEmulator.RunEcFan(DriverPe());
        Assert.Equal(0, r.Status);
        Assert.Equal(0xE7, r.Ec[0x7E]);
        Assert.Equal(0x01, r.Ec[0x80]);            // bit 1 cleared from 0x03
        Assert.True((r.Lpc & 0x40000000) != 0);    // D14F3x44 bit 30 set
    }

    [EmuFact] public void EcAbsent_ReturnsBoundedTimeout()
    {
        var r = EcEmulator.RunEcFan(DriverPe(), ecPresent: false);
        Assert.Equal(0, r.Status);                 // returned, did not hang
        Assert.True(r.Instructions > 0);
    }
}
