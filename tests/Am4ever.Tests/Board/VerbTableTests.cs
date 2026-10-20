// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board;
using Am4ever.Core.Rom;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Board;

public class VerbTableTests
{
    [Fact] public void Replace_InPlace_PadsRemainderWithFf()
    {
        var img = SyntheticVerbs.List((0x10EC0899, new uint[] { 1, 2, 3 }), (0x10EC0898, new uint[] { 4, 5 }));
        VerbTable.Replace(img, new uint[] { 0x10EC0899, 0x10EC0898 }, 0x10EC0671, new uint[] { 9, 9 });
        Assert.Equal(0x10EC0671u, Bin.U32(img, SyntheticVerbs.ListStart));
        Assert.Equal(0xFFu, img[^1]);                         // tail padded
    }

    [Fact] public void ReplaceVerbs_TooBig_LeavesImageUntouched()
    {
        var img = SyntheticVerbs.List((0x10EC0899, new uint[] { 1 }), (0x10EC0898, new uint[] { 2 }));
        var before = (byte[])img.Clone();
        var huge = Enumerable.Range(0, 500).Select(i => (uint)i).ToArray();
        Assert.Throws<RomEditException>(() =>
            VerbTable.Replace(img, new uint[] { 0x10EC0899, 0x10EC0898 }, 0x10EC0671, huge));
        Assert.Equal(before, img);
    }

    [RomFact] public void ExtractsHpAlc671VerbTable()
    {
        var hp = File.ReadAllBytes(TestRoms.HpF65!);
        var verbs = VerbTable.ExtractHp(hp, 0xC00000, 0x10EC0671, 0xFF000000);
        Assert.NotEmpty(verbs);                               // validated subsystem ID inside ExtractHp
    }
}
