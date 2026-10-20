// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Build;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Reference;

/// <summary>
/// The acceptance test: applying the HP 285 G3 profile to the user's ASRock P10.43 + HP F.65 ROMs
/// reproduces the decompressed boot/PEI volume and the SMBIOS static table of the T5 reference image.
/// Compressed bytes may differ (managed zlib/LZMA ≠ the Python/EDK encoders); the decompressed
/// contents must be identical. The EC-fan driver and the emulator run are milestone 4.
/// </summary>
public class Hp285G3ReferenceTests
{
    const int BootBlob = 0xE02000;
    const int DxeFv = 0x94F000;
    static readonly Guid DxeFileGuid = Guid.Parse("9e21fd93-9c72-4c15-8c4b-e77f1db2d792");

    [T5Fact] public void ReproducesT5BootVolumeAndSmbios()
    {
        var oem = File.ReadAllBytes(TestRoms.HpF65!);
        var donor = File.ReadAllBytes(TestRoms.AsrockP1043!);
        var t5 = File.ReadAllBytes(T5Reference.Path!);

        var result = BuildPipeline.Build(donor, oem,
            BundledData.LoadProfile("hp-285-g3-8434"),
            BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"), dmiOverride: null);

        Assert.Equal(DecompressPei(t5), DecompressPei(result.Image));     // boot/PEI volume identical
        Assert.Equal(SmbiosBytes(t5), SmbiosBytes(result.Image));         // SMBIOS static table identical
        Assert.All(AmdDirectory.FindAll(RomImage.Load(result.Image)), d => Assert.True(d.ChecksumValid));
    }

    static byte[] DecompressPei(byte[] image)
    {
        uint compressedLength = Bin.U32(image, BootBlob + 0x14);
        var (_, pei) = PspZlib.Decompress(image.AsSpan(BootBlob, 0x100 + (int)compressedLength));
        return pei;
    }

    static byte[] SmbiosBytes(byte[] image)
    {
        var fv = new FirmwareVolume(image, DxeFv);
        var file = fv.FindByGuid(DxeFileGuid);
        var lzma = file.Sections().First(s => s.Type == SectionType.GuidDefined);
        var dec = GuidedSection.DecompressLzmaGuided(image, lzma);
        var fvImage = SectionWalker.Walk(dec, 0, dec.Length).First(s => s.Type == SectionType.FirmwareVolumeImage);
        var inner = new FirmwareVolume(dec, fvImage.DataOffset);
        var (start, end) = SmbiosTable.LocateStaticData(inner);
        return dec[start..end];
    }
}
