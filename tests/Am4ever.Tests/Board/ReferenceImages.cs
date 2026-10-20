// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Board;

/// <summary>
/// Pulls real OEM module bytes out of the developer's reference ROMs for [RomFact] tests:
/// decompresses the ASRock PEI boot volume and returns the image section of a named CPM OEM PEIM.
/// </summary>
public static class ReferenceImages
{
    const int AsrockBootBlob = 0xE02000;
    const int AsrockBootBlobLength = 0xD57C6;
    const int BootFvOffset = 0x20;           // the FV sits 0x20 bytes into the decompressed blob
    const int HpPeiFv = 0xC00000;

    /// <summary>The image-section bytes of the <paramref name="index"/>-th ASRock PEIM named <paramref name="name"/>.</summary>
    public static byte[] AsrockOemPeim(string name, int index) =>
        OemPeim(File.ReadAllBytes(TestRoms.AsrockP1043!), AsrockBootBlob, AsrockBootBlobLength, name, index);

    static byte[] OemPeim(byte[] rom, int blobOffset, int blobLength, string name, int index)
    {
        var image = RomImage.Load(rom);
        var (_, plain) = PspZlib.Decompress(image.Data.AsSpan(blobOffset, blobLength));
        var fv = new FirmwareVolume(plain, BootFvOffset);
        var match = fv.Files().Where(f => f.Name() == name).ToList()[index];
        var (start, end) = match.ImageSection();
        return plain[start..end];
    }
}
