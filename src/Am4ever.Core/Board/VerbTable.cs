// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Core.Board;

/// <summary>
/// HD-Audio codec verb tables. <see cref="ExtractHp"/> recovers the OEM codec's verb list from the
/// OEM PEI volume (the codec-list entries in the AmdSocAm4* PEIMs); <see cref="Replace"/> swaps the
/// donor's codec list for a single OEM-codec block in place. Ports <c>hp_verbs</c>/<c>patch_verbs</c>.
/// </summary>
public static class VerbTable
{
    const uint End = 0xFFFFFFFF;

    /// <summary>
    /// The OEM codec's verb list. Each AmdSocAm4* PEIM holds codec-list entries
    /// {UInt32 codecId, UInt64 verbTablePtr}; the pointer (mapped at <paramref name="romBase"/>) points
    /// at a run of UInt32 verbs that ends where the next list entry begins. The verb list must be the
    /// same in every PEIM, and its subsystem-ID verbs are validated.
    /// </summary>
    public static IReadOnlyList<uint> ExtractHp(byte[] hpRom, int peiFvOffset, uint codecId, uint romBase)
    {
        var distinct = new HashSet<string>();
        List<uint>? verbs = null;
        var fv = new FirmwareVolume(hpRom, peiFvOffset);
        foreach (var f in fv.Files())
        {
            if (!f.Name().StartsWith("AmdSocAm4", StringComparison.Ordinal)) continue;
            int p = f.Offset;
            while ((p = FindU32(hpRom, codecId, p, f.End)) >= 0)
            {
                ulong ptr = Bin.U64(hpRom, p + 4);
                p += 4;
                if (ptr < romBase || ptr >= 0x1_0000_0000) continue;
                int start = (int)(ptr - romBase);
                var list = new List<uint>();
                int q = start;
                while ((Bin.U32(hpRom, q) >> 28) == 0)         // CAd (bits 31:28) == 0: still a verb
                {
                    list.Add(Bin.U32(hpRom, q));
                    q += 4;
                }
                if (q != p - 4)
                    throw new RomFormatException($"verb table at 0x{start:X} does not end at its codec-list entry");
                distinct.Add(string.Join(",", list));
                verbs = list;
            }
        }
        if (distinct.Count != 1 || verbs is null)
            throw new RomFormatException($"expected one 0x{codecId:X} verb table, found {distinct.Count}");

        // subsystem ID: the verbs that write pin 0x1 config default (NID in bits 27:20 == 0x01)
        var ssid = new Dictionary<uint, uint>();
        foreach (var w in verbs)
            if (((w >> 20) & 0xFF) == 0x01) ssid[(w >> 8) & 0xFFF] = w & 0xFF;
        var expected = new uint[] { 0x34, 0x84, 0x3C, 0x10 };
        for (int i = 0; i < 4; i++)
            if (!ssid.TryGetValue((uint)(0x720 + i), out var v) || v != expected[i])
                throw new RomFormatException($"0x{codecId:X} verb table subsystem ID unexpected");
        return verbs;
    }

    /// <summary>
    /// Replace the donor codec list (blocks {codecId, verbs..., 0xFFFFFFFF} ended by a codecId of
    /// 0xFFFFFFFF) with one block for <paramref name="newCodecId"/>, in place, padding the rest with 0xFF.
    /// </summary>
    public static void Replace(byte[] img, uint[] oldCodecs, uint newCodecId, IReadOnlyList<uint> verbs)
    {
        int start = FindU32(img, oldCodecs[0], 0, img.Length);
        if (start < 0 || FindU32(img, oldCodecs[0], start + 4, img.Length) >= 0)
            throw new RomFormatException("donor verb table not found exactly once");

        int p = start;
        var codecs = new List<uint>();
        uint cid;
        while ((cid = Bin.U32(img, p)) != End)
        {
            codecs.Add(cid);
            p += 4;
            while (Bin.U32(img, p) != End) p += 4;
            p += 4;
        }
        int end = p + 4;                      // past the list terminator
        if (!codecs.SequenceEqual(oldCodecs))
            throw new RomFormatException($"unexpected donor codec list [{string.Join(",", codecs.Select(c => $"0x{c:X}"))}]");

        var newList = new List<uint> { newCodecId };
        newList.AddRange(verbs);
        newList.Add(End);                     // end of this codec block
        newList.Add(End);                     // list terminator
        int newBytes = newList.Count * 4;
        if (newBytes > end - start)
            throw new RomEditException($"new verb table ({newBytes} bytes) does not fit the donor list ({end - start} bytes)");

        for (int i = 0; i < newList.Count; i++) Bin.W32(img, start + i * 4, newList[i]);
        for (int i = start + newBytes; i < end; i++) img[i] = 0xFF;
    }

    static int FindU32(byte[] buf, uint value, int start, int end)
    {
        for (int i = start; i + 4 <= end; i++)
            if (Bin.U32(buf, i) == value) return i;
        return -1;
    }
}
