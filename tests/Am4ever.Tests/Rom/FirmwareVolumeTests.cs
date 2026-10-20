// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Rom;

public class FirmwareVolumeTests
{
    [Fact] public void Files_FindsTheSingleFile()
    {
        var buf = SyntheticFv.WithOneFile(out var g);
        var fv = new FirmwareVolume(buf, 0);
        var files = fv.Files().ToList();
        Assert.Single(files);
        Assert.Equal(g, files[0].Guid);
    }

    [Fact] public void Files_StopsOnZeroSizeFile()
    {
        var buf = SyntheticFv.WithOneFile(out _);
        SyntheticFv.CorruptFirstFileSizeToZero(buf);
        var fv = new FirmwareVolume(buf, 0);
        Assert.Throws<RomFormatException>(() => fv.Files().ToList());
    }

    [Fact] public void FixChecksums_MakesHeaderChecksumValid()
    {
        var buf = SyntheticFv.WithOneFile(out _);
        var fv = new FirmwareVolume(buf, 0);
        var file = fv.Files().First();
        buf[file.DataOffset] ^= 0xFF;          // disturb the body
        buf[file.Offset + 16] ^= 0xFF;         // and the header checksum
        file.FixChecksums(buf);
        Assert.True(SyntheticFv.HeaderChecksumValid(buf, file));
    }

    [RomFact] public void FindsOemModulesInHpPeiVolume()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!));
        var fv = new FirmwareVolume(rom.Data, 0xC00000);
        var names = fv.Files().Select(f => f.Name()).Where(n => n.StartsWith("AmdCpmOem")).ToList();
        Assert.Contains("AmdCpmOemInitPeim", names);
        Assert.Contains("AmdCpmOemRvInitPeim", names);
    }

    [RomFact] public void HpPeiVolumeHasExpectedFileCount()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!));
        Assert.Equal(123, new FirmwareVolume(rom.Data, 0xC00000).Files().Count());
    }
}
