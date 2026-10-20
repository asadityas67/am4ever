// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Rom;

/// <summary>Builds minimal valid FFSv2 volumes in memory for parser/editor tests.</summary>
public static class SyntheticFv
{
    const int FvHeaderLength = 0x48;
    // EFI_FIRMWARE_FILE_SYSTEM2_GUID
    static readonly Guid Ffs2 = Guid.Parse("8c8ce578-8a3d-4f1c-9935-896185c32dd3");

    /// <summary>A volume of <paramref name="total"/> bytes with one raw FFS file holding <paramref name="body"/>.</summary>
    public static byte[] Build(int total, Guid fileGuid, byte[] body)
    {
        var buf = new byte[total];
        // FV header
        fileGuidToFsGuid(buf);
        Bin.W64(buf, 32, (ulong)total);
        Bin.W32(buf, 40, 0x4856465F);           // "_FVH"
        Bin.W16(buf, 48, FvHeaderLength);
        Bin.W16(buf, 52, 0);                     // no extended header
        buf[55] = 2;                             // revision

        // one FFS file at the aligned first-file offset
        int o = (FvHeaderLength + 7) & ~7;
        fileGuid.ToByteArray().CopyTo(buf, o);
        buf[o + 18] = SectionType.Raw;           // file type: raw
        buf[o + 19] = 0;                         // attributes
        int fileSize = 24 + body.Length;
        Bin.W24(buf, o + 20, (uint)fileSize);
        buf[o + 23] = 0xF8;                       // state: data valid
        body.CopyTo(buf, o + 24);
        // header checksum (8-bit, state treated as zero)
        buf[o + 16] = 0; buf[o + 17] = 0;
        byte state = buf[o + 23]; buf[o + 23] = 0;
        buf[o + 16] = (byte)((0x100 - Sum(buf, o, 24)) & 0xFF);
        buf[o + 17] = 0xAA;
        buf[o + 23] = state;

        // fill the rest with the erase value
        for (int i = o + ((fileSize + 7) & ~7); i < total; i++) buf[i] = 0xFF;
        return buf;
    }

    public static byte[] WithOneFile(out Guid fileGuid)
    {
        fileGuid = Guid.NewGuid();
        return Build(0x10000, fileGuid, new byte[] { 10, 20, 30, 40 });
    }

    public static byte[] WithFreeSpace(int freeBytes, out Guid fileGuid)
    {
        fileGuid = Guid.NewGuid();
        return Build(0x8000 + freeBytes, fileGuid, new byte[] { 1, 2, 3, 4 });
    }

    public static void CorruptFirstFileSizeToZero(byte[] buf)
    {
        int o = (FvHeaderLength + 7) & ~7;
        Bin.W24(buf, o + 20, 0);
    }

    /// <summary>True if the FFS file's 8-bit header checksum is self-consistent.</summary>
    public static bool HeaderChecksumValid(byte[] buf, FfsFile file)
    {
        int o = file.Offset;
        int sum = 0;
        for (int i = 0; i < file.HeaderLength; i++)
        {
            byte b = i is 16 or 17 or 23 ? (byte)0 : buf[o + i];
            sum += b;
        }
        return ((sum + buf[o + 16]) & 0xFF) == 0;
    }

    static int Sum(byte[] b, int start, int len)
    {
        int s = 0;
        for (int i = 0; i < len; i++) s += b[start + i];
        return s;
    }

    static void fileGuidToFsGuid(byte[] buf) => Ffs2.ToByteArray().CopyTo(buf, 16);
}
