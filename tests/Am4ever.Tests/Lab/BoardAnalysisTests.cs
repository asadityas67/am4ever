// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Lab;
using Am4ever.Core.Rom;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Lab;

public class BoardAnalysisTests
{
    const int HpPeiFv = 0xC00000, HpBootBlob = 0xE02000;   // HP is uncompressed PEI at 0xC00000

    static BoardAnalysis Hp() => BoardAnalysis.Load(File.ReadAllBytes(TestRoms.HpF65!), HpPeiFv, HpBootBlob);

    [Fact] public void Load_NonAm4_Throws() =>
        Assert.Throws<RomFormatException>(() => BoardAnalysis.Load(new byte[4 * 1024 * 1024], HpPeiFv, HpBootBlob));

    [RomFact] public void Tables_UnknownModule_Reported() => Assert.Empty(Hp().CpmTables("NoSuchPeim", 0));

    [RomFact] public void Oem_ListsCpmPeims()
    {
        var a = Hp();
        Assert.Contains(a.Modules(), m => m.Name == "AmdCpmOemInitPeim");
        var tags = a.CpmTables("AmdCpmOemInitPeim", 0);
        Assert.Contains(tags, t => t.StartsWith("$A10"));
        Assert.Contains(tags, t => t.StartsWith("$A26"));
    }
}
