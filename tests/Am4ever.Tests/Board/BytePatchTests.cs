// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board;
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Board;

public class BytePatchTests
{
    [Fact] public void Neutralise_WritesXorRet()
    {
        var img = new byte[] { 0x81, 0xEC, 0xC4, 0x00, 0x00, 0x00, 0x90 };
        BytePatch.NeutraliseOverride(img, 0, new byte[] { 0x81, 0xEC, 0xC4, 0x00, 0x00, 0x00 });
        Assert.Equal(new byte[] { 0x31, 0xC0, 0xC3 }, img[..3]);
    }

    [Fact] public void Neutralise_WrongExpectedBytes_Throws()
    {
        var img = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        var before = (byte[])img.Clone();
        Assert.Throws<RomEditException>(() =>
            BytePatch.NeutraliseOverride(img, 0, new byte[] { 0x81, 0xEC, 0xC4, 0x00, 0x00, 0x00 }));
        Assert.Equal(before, img);
    }

    [Fact] public void Apply_ReplacementLongerThanExpected_Throws() =>
        Assert.Throws<RomEditException>(() =>
            BytePatch.Apply(new byte[4], 0, new byte[] { 0, 0 }, new byte[] { 1, 2, 3 }));
}
