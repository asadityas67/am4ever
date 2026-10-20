// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board.Cpm;

/// <summary>
/// The APU display (DDI) block at the tail of a $A26 table, after the 16 port descriptors. Each
/// connector is 12 bytes starting at block offset 0x0C: a UInt32 flags word (last-entry bit
/// 0x80000000), then connector / AUX / HPD bytes. Ports <c>describe_ddi</c> and <c>port_ddi</c>.
/// </summary>
public static class DdiBlock
{
    static readonly Dictionary<byte, string> Types = new()
    {
        [0] = "DP", [1] = "eDP", [2] = "DVI-SL", [3] = "DVI-DL", [4] = "HDMI", [5] = "DP-VGA",
        [7] = "Nutmeg-VGA", [9] = "CRT", [13] = "auto", [14] = "unused",
    };

    static int BlockStart(CpmTable t, int descriptorSize) => t.Offset + DxioTable.DescriptorBase + 16 * descriptorSize;

    public static string Describe(ReadOnlySpan<byte> block)
    {
        var parts = new List<string>();
        for (int o = 0x0C; o + 12 <= block.Length; o += 12)
        {
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(block.Slice(o));
            byte conn = block[o + 4], aux = block[o + 5], hpd = block[o + 6];
            if (conn != 0 || aux != 0 || hpd != 0)
                parts.Add($"{(Types.TryGetValue(conn, out var n) ? n : $"0x{conn:X}")}/AUX{aux}/HPD{hpd}");
            if ((flags & 0x80000000) != 0) break;
        }
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    /// <summary>Copy the source table's DDI block into the destination table's DDI tail, zero-padding to fit.</summary>
    public static void Copy(byte[] dstImg, CpmTable dst, ReadOnlySpan<byte> srcImg, CpmTable src,
                            int dstDescSize, int srcDescSize)
    {
        int dstStart = BlockStart(dst, dstDescSize);
        int srcStart = BlockStart(src, srcDescSize);
        int srcLen = src.End - srcStart;
        int dstLen = dst.End - dstStart;
        if (srcLen > dstLen)
            throw new RomEditException(
                $"DDI block of {srcLen} bytes does not fit the destination tail of {dstLen} bytes");
        srcImg.Slice(srcStart, srcLen).CopyTo(dstImg.AsSpan(dstStart, srcLen));
        dstImg.AsSpan(dstStart + srcLen, dstLen - srcLen).Clear();
    }
}
