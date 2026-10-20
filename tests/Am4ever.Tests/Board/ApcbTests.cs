// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board;
using Am4ever.Core.Rom;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Board;

public class ApcbTests
{
    [RomFact] public void FindsApcbInBothRoms()
    {
        var asrock = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var hp = RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!));
        Assert.NotEmpty(Apcb.FindAll(asrock));
        Assert.NotEmpty(Apcb.FindAll(hp));
    }

    [RomFact] public void HpAndAsrockSpdMapsAreCompatible()
    {
        var asrock = File.ReadAllBytes(TestRoms.AsrockP1043!);
        var hp = File.ReadAllBytes(TestRoms.HpF65!);
        var a = Apcb.FindAll(RomImage.Load(asrock))[0];
        var h = Apcb.FindAll(RomImage.Load(hp))[0];
        Assert.True(Apcb.SpdMapsCompatible(hp, h, asrock, a, out var report));
        Assert.Contains("SPD", report);
    }
}
