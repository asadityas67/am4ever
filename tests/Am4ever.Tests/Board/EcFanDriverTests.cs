// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Ec;
using Am4ever.Core.Rom.Fv;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Board;

public class EcFanDriverTests
{
    [Fact] public void Build_EqualsGoldenFfs()
    {
        var golden = Convert.FromHexString(EcFanGolden.Ffs);   // 1088-byte reference FFS
        Assert.Equal(golden, EcFanDriver.Build());
    }

    [Fact] public void Build_HasDriverNameAndGuid()
    {
        var buf = SyntheticFv.WrapFile(EcFanDriver.Build(), out _);
        var fv = new FirmwareVolume(buf, 0);
        var file = fv.FindByGuid(EcFanDriver.FileGuid);
        Assert.Equal("HpEcFanInit", file.Name());
    }
}
