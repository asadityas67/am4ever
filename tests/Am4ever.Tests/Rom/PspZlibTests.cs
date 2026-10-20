// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;

namespace Am4ever.Tests.Rom;

public class PspZlibTests
{
    [Fact] public void RoundTrip_PreservesPlainBytes()
    {
        var header = new byte[0x100];
        var plain = Enumerable.Range(0, 5000).Select(i => (byte)(i * 7)).ToArray();
        var blob = PspZlib.Compress(header, plain);
        var (_, back) = PspZlib.Decompress(blob);
        Assert.Equal(plain, back);
    }

    [Fact] public void Compress_WritesCompressedLengthIntoHeader()
    {
        var header = new byte[0x100];
        var blob = PspZlib.Compress(header, new byte[1000]);
        Assert.Equal((uint)(blob.Length - 0x100), Bin.U32(blob, 0x14));
    }

    [RomFact] public void DecompressesAsrockBootVolume()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var blob = rom.Data.AsSpan(0xE02000, 0xD57C6).ToArray();
        var (_, pei) = PspZlib.Decompress(blob);
        // The PEI firmware volume sits 0x20 bytes into the decompressed blob; check its FV signature.
        Assert.Equal(0x4856465Fu, Bin.U32(pei, 0x20 + 40));
        Assert.Equal(0x400020, pei.Length);
    }
}
