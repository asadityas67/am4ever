// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Rom;

/// <summary>Builds an FFS file containing one GUID-defined LZMA section, for guided-section tests.</summary>
public static class SyntheticLzma
{
    const int FvHeaderLength = 0x48;
    static readonly Guid Ffs2 = Guid.Parse("8c8ce578-8a3d-4f1c-9935-896185c32dd3");
    // EFI LZMA-compressed GUID (EDK2 LzmaCustomDecompressGuid)
    static readonly Guid LzmaGuid = Guid.Parse("ee4e5898-3914-4259-9d6e-dc7bd79403cf");

    /// <summary>
    /// A volume holding one FFS file whose single section is a GUID-defined LZMA section wrapping
    /// <paramref name="plain"/>. The section occupies a <paramref name="slot"/>-byte region.
    /// </summary>
    public static byte[] ContainerWithPlain(byte[] plain, int slot, out FfsFile file, out FvSection section)
    {
        var lzma = EdkLzma.Compress(plain);
        // GUID-defined section: size(3)+type(1)+guid(16)+dataOffset(u16)+attributes(u16) = 24-byte header.
        int sectionHeader = 24;
        int sectionSize = sectionHeader + lzma.Length;
        if (sectionSize > slot)
            throw new ArgumentException("plain too big for slot");

        int fvHeader = (FvHeaderLength + 7) & ~7;
        int fileHeader = 24;
        int fileSize = fileHeader + slot;
        int total = ((fvHeader + fileHeader + slot + 7) & ~7) + 0x100;

        var buf = new byte[total];
        Ffs2.ToByteArray().CopyTo(buf, 16);
        Bin.W64(buf, 32, (ulong)total);
        Bin.W32(buf, 40, 0x4856465F);
        Bin.W16(buf, 48, FvHeaderLength);
        buf[55] = 2;

        int fo = fvHeader;
        Guid.NewGuid().ToByteArray().CopyTo(buf, fo);
        buf[fo + 18] = SectionType.FirmwareVolumeImage;  // container file type (arbitrary here)
        buf[fo + 19] = 0;
        Bin.W24(buf, fo + 20, (uint)fileSize);
        buf[fo + 23] = 0xF8;

        int so = fo + fileHeader;
        Bin.W24(buf, so, (uint)sectionSize);
        buf[so + 3] = SectionType.GuidDefined;
        LzmaGuid.ToByteArray().CopyTo(buf, so + 4);
        Bin.W16(buf, so + 20, (ushort)sectionHeader);    // data offset: payload starts after the 24-byte header
        Bin.W16(buf, so + 22, 0x0001);                   // processing-required attribute
        lzma.CopyTo(buf, so + sectionHeader);
        for (int i = so + sectionSize; i < fo + fileSize; i++) buf[i] = 0xFF;
        for (int i = fo + fileSize; i < total; i++) buf[i] = 0xFF;

        new FfsFile(buf, fo).FixChecksums(buf);
        var fv = new FirmwareVolume(buf, 0);
        file = fv.Files().First();
        section = file.Sections()[0];
        return buf;
    }
}
