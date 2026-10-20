// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Cpm;
using Am4ever.Core.Rom;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Board;

public class CpmImageTests
{
    [Fact] public void FindTable_ByTagAndMask_ReturnsTheOne()
    {
        var img = SyntheticCpm.WithTables(("$A10", 0xFFFF00u, 0x40), ("$A10", 0xFFu, 0x40), ("$A11", 0u, 0x20));
        var t = CpmImage.FindTable(img, "$A10", 0xFFu);
        Assert.Equal(0xFFu, t.PlatformMask);
        Assert.Equal(0x40, t.Size);
    }

    [Fact] public void FindTable_Ambiguous_Throws() =>
        Assert.Throws<RomFormatException>(() =>
            CpmImage.FindTable(SyntheticCpm.WithTables(("$A10", 0u, 0x40), ("$A10", 0u, 0x40)), "$A10"));

    [Fact] public void CpmScan_IgnoresGarbageSizeTag()
    {
        var img = SyntheticCpm.WithTables(("$A10", 0u, 0x40));
        SyntheticCpm.SetSizeField(img, "$A10", 0xFFFF);        // absurd size
        Assert.Empty(CpmImage.Tables(img));
    }

    [RomFact] public void FindsCpmTablesInAsrockOemPeim()
    {
        var img = ReferenceImages.AsrockOemPeim("AmdCpmOemInitPeim", 0);   // Summit/Pinnacle
        Assert.NotNull(CpmImage.FindTable(img, "$A10", 0xFFFF00u));
        Assert.NotNull(CpmImage.FindTable(img, "$A11"));
        Assert.NotNull(CpmImage.FindTable(img, "$A26", 0xBFBF00u));
    }
}
