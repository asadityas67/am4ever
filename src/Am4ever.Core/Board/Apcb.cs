// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board;

/// <summary>
/// The AMD APCB (AGESA PSP Customization Block), v2/v3. Each block is an "APCB" header followed by
/// groups (MEMG, CCXG, ...) each holding typed entries. The DRAM SPD/I2C address map is type 0x31 in
/// the memory group; <see cref="SpdMapsCompatible"/> checks that the OEM board's map matches the
/// donor's, so memory training behaves the same (a check-only operation; am4ever does not rewrite APCB).
/// </summary>
public static class Apcb
{
    const int MemSpdTypeId = 0x31;
    const int MinApcbSize = 0x20;
    const int MaxApcbSize = 0x20000;

    /// <summary>Offsets of every valid APCB block in the image.</summary>
    public static IReadOnlyList<int> FindAll(RomImage rom)
    {
        var data = rom.Data;
        var hits = new List<int>();
        for (int o = 0; o + 0x10 <= data.Length; o++)
        {
            if (data[o] != 0x41 || data[o + 1] != 0x50 || data[o + 2] != 0x43 || data[o + 3] != 0x42)   // "APCB"
                continue;
            int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(o + 4));
            uint apcbSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o + 8));
            if (headerSize < 0x10 || headerSize > 0x100 || apcbSize < MinApcbSize || apcbSize > MaxApcbSize)
                continue;
            if ((long)o + apcbSize > data.Length || o + headerSize + 0x10 > data.Length)
                continue;
            if (!LooksLikeGroupSignature(data, o + headerSize))     // the first group must be plausible
                continue;
            hits.Add(o);
        }
        return hits;
    }

    /// <summary>The SPD/I2C address-map bytes (memory group, type 0x31), or empty if absent.</summary>
    public static IReadOnlyList<byte> SpdAddressMap(ReadOnlySpan<byte> rom, int apcbOffset)
    {
        int headerSize = BinaryPrimitives.ReadUInt16LittleEndian(rom.Slice(apcbOffset + 4));
        uint apcbSize = BinaryPrimitives.ReadUInt32LittleEndian(rom.Slice(apcbOffset + 8));
        int end = apcbOffset + (int)apcbSize;

        for (int g = apcbOffset + headerSize; g + 0x10 <= end;)
        {
            int groupHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(rom.Slice(g + 6));
            uint groupSize = BinaryPrimitives.ReadUInt32LittleEndian(rom.Slice(g + 12));
            if (groupSize < 0x10 || g + (long)groupSize > end) break;
            int groupEnd = g + (int)groupSize;

            for (int t = g + groupHeaderSize; t + 0x10 <= groupEnd;)
            {
                int typeId = BinaryPrimitives.ReadUInt16LittleEndian(rom.Slice(t + 2));
                int typeSize = BinaryPrimitives.ReadUInt16LittleEndian(rom.Slice(t + 4));
                if (typeSize < 0x10 || t + typeSize > groupEnd) break;
                if (typeId == MemSpdTypeId)
                    return rom.Slice(t + 0x10, typeSize - 0x10).ToArray();
                t += typeSize;
            }
            g += (int)groupSize;
        }
        return Array.Empty<byte>();
    }

    /// <summary>True when the two SPD address maps are identical; <paramref name="report"/> names both either way.</summary>
    public static bool SpdMapsCompatible(ReadOnlySpan<byte> oem, int oemApcb, ReadOnlySpan<byte> donor, int donorApcb, out string report)
    {
        var o = SpdAddressMap(oem, oemApcb);
        var d = SpdAddressMap(donor, donorApcb);
        bool equal = o.SequenceEqual(d);
        report = $"OEM SPD map [{Hex(o)}]; donor SPD map [{Hex(d)}]; " + (equal ? "compatible" : "INCOMPATIBLE");
        return equal;
    }

    static bool LooksLikeGroupSignature(byte[] data, int o)
    {
        for (int i = 0; i < 4; i++)
        {
            byte b = data[o + i];
            if (!((b >= 'A' && b <= 'Z') || b == ' '))
                return false;
        }
        return true;
    }

    static string Hex(IReadOnlyList<byte> bytes) => Convert.ToHexString(bytes.ToArray()).ToLowerInvariant();
}
