// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Rom;

/// <summary>Hostile-input hardening (M2 whole-branch review findings).</summary>
public class RomHardeningTests
{
    [Fact] public void Files_OversizedLargeFile_ThrowsFormatNotIndexOutOfRange()
    {
        var buf = SyntheticFv.OversizedLargeFile();
        var fv = new FirmwareVolume(buf, 0);
        Assert.Throws<RomFormatException>(() => fv.Files().ToList());
    }

    [Fact] public void Decompress_AbsurdDeclaredSize_ThrowsFormat()
    {
        // valid 5 props + an 8-byte size of 0x4000_0000_0000 (way past any ROM) + a stub byte
        var blob = new byte[14];
        blob[0] = 0x5D;
        Bin.W64(blob, 5, 0x0000_4000_0000_0000UL);
        Assert.Throws<RomFormatException>(() => EdkLzma.Decompress(blob));
    }

    [Fact] public void Decompress_CorruptStream_ThrowsFormat()
    {
        var blob = new byte[64];
        blob[0] = 0x5D;
        Bin.W64(blob, 5, 100);                 // claims 100 bytes out of random garbage
        new Random(2).NextBytes(blob.AsSpan(13).ToArray());
        for (int i = 13; i < blob.Length; i++) blob[i] = (byte)(i * 37);
        Assert.Throws<RomFormatException>(() => EdkLzma.Decompress(blob));
    }

    [Fact] public void ErrorMessages_FormatOffsetsAsHex()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        var e = Assert.Throws<RomFormatException>(() => rom.ToOffset(0x8000_0000));
        Assert.Contains("0x80000000", e.Message);
    }
}
