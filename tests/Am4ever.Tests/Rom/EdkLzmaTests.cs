// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Rom;

public class EdkLzmaTests
{
    [Fact] public void RoundTrip_PreservesPlainBytes()
    {
        var plain = Enumerable.Range(0, 20000).Select(i => (byte)(i ^ (i >> 3))).ToArray();
        Assert.Equal(plain, EdkLzma.Decompress(EdkLzma.Compress(plain)));
    }

    [Fact] public void Compress_WritesTrueSizeNotMinusOne()
    {
        var plain = new byte[1234];
        var lzma = EdkLzma.Compress(plain);
        Assert.Equal((ulong)plain.Length, Bin.U64(lzma, 5));
    }

    [Fact] public void ReplaceSection_TooBig_LeavesImageUntouched()
    {
        var buf = SyntheticLzma.ContainerWithPlain(new byte[64], slot: 0x200, out var file, out var section);
        var before = (byte[])buf.Clone();
        var huge = new byte[0x4000];
        new Random(1).NextBytes(huge);          // incompressible, so it cannot fit the 0x200 slot
        Assert.Throws<RomEditException>(() => GuidedSection.ReplaceLzmaGuided(buf, file, section, huge));
        Assert.Equal(before, buf);
    }

    [Fact] public void ReplaceSection_Fits_RoundTripsThroughDecompress()
    {
        var buf = SyntheticLzma.ContainerWithPlain(new byte[64], slot: 0x800, out var file, out var section);
        var newPlain = Enumerable.Range(0, 200).Select(i => (byte)(i * 3)).ToArray();
        GuidedSection.ReplaceLzmaGuided(buf, file, section, newPlain);
        var reFile = new FirmwareVolume(buf, 0).Files().First();
        Assert.Equal(newPlain, GuidedSection.DecompressLzmaGuided(buf, reFile.Sections()[0]));
    }

    [RomFact] public void DecompressesAsrockDxeVolume()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var fv = new FirmwareVolume(rom.Data, 0x94F000);
        var file = fv.FindByGuid(Guid.Parse("9e21fd93-9c72-4c15-8c4b-e77f1db2d792"));
        var plain = GuidedSection.DecompressLzmaGuided(rom.Data, file.Sections()[0]);
        Assert.True(plain.Length > 0x900000, $"DXE volume should be ~9 MiB, got {plain.Length:#0x}");
    }
}
