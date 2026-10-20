// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom.Compression;

namespace Am4ever.Core.Rom.Fv;

/// <summary>Reads and rewrites an LZMA-compressed GUID-defined firmware-volume section.</summary>
public static class GuidedSection
{
    public static byte[] DecompressLzmaGuided(byte[] buffer, FvSection section)
    {
        if (section.Type != SectionType.GuidDefined)
            throw new RomFormatException("section is not GUID-defined");
        return EdkLzma.Decompress(buffer.AsSpan(section.DataOffset, section.End - section.DataOffset));
    }

    /// <summary>
    /// Recompress <paramref name="newPlain"/> and write it back into the section, keeping the
    /// container file's size: the new section plus 0xFF padding must fit the file's existing slot,
    /// else nothing is written.
    /// </summary>
    public static void ReplaceLzmaGuided(byte[] buffer, FfsFile containerFile, FvSection section, ReadOnlySpan<byte> newPlain)
    {
        if (section.Type != SectionType.GuidDefined)
            throw new RomFormatException("section is not GUID-defined");

        var lzma = EdkLzma.Compress(newPlain);
        int sectionHeaderLen = section.DataOffset - section.Offset;
        int newSectionSize = sectionHeaderLen + lzma.Length;
        int slot = containerFile.End - section.Offset;
        if (newSectionSize > slot)
            throw new RomEditException(
                $"recompressed section is {newSectionSize} bytes but only {slot} bytes are available " +
                $"in file {containerFile.Guid}; image left unchanged");

        // Only now, after the fit check, do we mutate the buffer.
        Bin.W24(buffer, section.Offset, (uint)newSectionSize);
        lzma.CopyTo(buffer.AsSpan(section.DataOffset, lzma.Length));
        for (int i = section.DataOffset + lzma.Length; i < containerFile.End; i++)
            buffer[i] = 0xFF;
        new FfsFile(buffer, containerFile.Offset).FixChecksums(buffer);
    }
}
