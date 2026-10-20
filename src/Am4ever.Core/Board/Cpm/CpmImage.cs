// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board.Cpm;

/// <summary>
/// Scans an uncompressed module image for AMD CPM tables and looks them up. A table is a 4-byte tag
/// "$A" followed by two characters in [0-9A-Z]; the scan accepts one only when its size field is
/// sane (12 &lt;= size &lt; 0x4000) and the table stays inside the image. This mirrors the regex scan in
/// the Python reference (<c>build_hp285g3.py</c>) exactly, including finding every occurrence.
/// </summary>
public static class CpmImage
{
    const int MinSize = 12;
    const int MaxSize = 0x4000;

    public static IReadOnlyList<CpmTable> Tables(ReadOnlySpan<byte> img)
    {
        var tables = new List<CpmTable>();
        for (int o = 0; o + CpmTable.HeaderLength <= img.Length; o++)
        {
            if (img[o] != 0x24 || img[o + 1] != 0x41)          // '$', 'A'
                continue;
            if (!IsTagChar(img[o + 2]) || !IsTagChar(img[o + 3]))
                continue;
            int size = BinaryPrimitives.ReadUInt16LittleEndian(img.Slice(o + 4));
            if (size < MinSize || size >= MaxSize || o + size > img.Length)
                continue;
            tables.Add(new CpmTable(
                Offset: o,
                Tag: new string(new[] { (char)img[o], (char)img[o + 1], (char)img[o + 2], (char)img[o + 3] }),
                Size: size,
                FeatureRevision: img[o + 6],
                ContentRevision: img[o + 7],
                PlatformMask: BinaryPrimitives.ReadUInt32LittleEndian(img.Slice(o + 8)),
                Attributes: BinaryPrimitives.ReadUInt32LittleEndian(img.Slice(o + 12))));
        }
        return tables;
    }

    /// <summary>The single table matching the tag and the optional platform mask / size, else <see cref="RomFormatException"/>.</summary>
    public static CpmTable FindTable(ReadOnlySpan<byte> img, string tag, uint? platformMask = null, int? size = null)
    {
        CpmTable? found = null;
        int count = 0;
        foreach (var t in Tables(img))
        {
            if (t.Tag != tag) continue;
            if (platformMask is not null && t.PlatformMask != platformMask) continue;
            if (size is not null && t.Size != size) continue;
            found = t;
            count++;
        }
        if (count != 1)
            throw new RomFormatException(
                $"CPM table {tag} pmask={(platformMask is null ? "any" : $"0x{platformMask:X}")} " +
                $"size={(size is null ? "any" : $"0x{size:X}")}: {count} matches, expected 1");
        return found!;
    }

    static bool IsTagChar(byte b) => (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'Z');
}
