// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Rom;

public class RomImageTests
{
    [Fact] public void Load_TruncatedImage_ThrowsFormatError() =>
        Assert.Throws<RomFormatException>(() => RomImage.Load(new byte[4 * 1024 * 1024 + 7]));

    [Fact] public void ToOffset_MapsHighAddressesIntoImage()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        Assert.Equal(0xE02000, rom.ToOffset(0xFFE02000));
        Assert.Equal(0xE02000, rom.ToOffset(0xE02000));
        Assert.Throws<RomFormatException>(() => rom.ToOffset(0x8000_0000));
    }

    [Fact] public void Bin_ReadPastEnd_ThrowsFormatError() =>
        Assert.Throws<RomFormatException>(() => Bin.U32(new byte[3], 0));

    [RomFact] public void ReferenceRomsLoad()
    {
        Assert.Equal(16 * 1024 * 1024, RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!)).Size);
        Assert.Equal(16 * 1024 * 1024, RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!)).Size);
    }
}
