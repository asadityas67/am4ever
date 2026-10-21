// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Emu;
using Am4ever.Tests.Board;

namespace Am4ever.Tests.Emu;

public class PeiEmulatorTests
{
    const uint Pinnacle = 0x00800F82;   // a family the Summit/Pinnacle module serves
    const uint Matisse = 0x00870F10;    // a family it does not

    static byte[] ZpModule() => ReferenceImages.AsrockOemPeim("AmdCpmOemInitPeim", 0);

    [EmuRomFact] public void Pei_ServedFamily_PublishesTables()
    {
        var r = PeiEmulator.Run(ZpModule(), Pinnacle);
        Assert.Equal(0u, r.Entry);
        Assert.Equal(0, r.WritesOutside);
        Assert.Contains("$CPM", r.Tables);
        Assert.Contains(r.Tables, t => t.StartsWith("$A10"));
        Assert.Contains(r.Tables, t => t.StartsWith("$A26"));
    }

    [EmuRomFact] public void Pei_UnsupportedFamily_NotAFailure()
    {
        var r = PeiEmulator.Run(ZpModule(), Matisse);   // EFI_UNSUPPORTED, no tables, no throw/hang
        Assert.NotEqual(0u, r.Entry);
        Assert.Empty(r.Tables);
    }

    [EmuRomFact] public void Pei_OverrideCheck_DetectsLiveRoutine()
    {
        var r = PeiEmulator.Run(ZpModule(), Pinnacle, overrideOffset: 0x2D6);   // donor: un-neutralised
        Assert.False(r.OverrideOk);
    }
}
