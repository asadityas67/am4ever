// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Core.Board.Smbios;

/// <summary>
/// The AMI SmbiosStaticData table (the static structures AMI ships in the DXE volume). Ports
/// <c>smbios_structs</c>/<c>set_strings</c>/<c>patch_smbios</c>: rewrites the identity string fields of
/// the System (1), Baseboard (2) and Chassis (3) structures, the type-1 UUID, the chassis type and
/// the type-11 OEM strings, renumbering strings in field order, and never grows the region.
/// </summary>
public static class SmbiosTable
{
    static readonly Guid StaticDataFileGuid = Guid.Parse("daf4bf89-ce71-4917-b522-c89d32fbc59f");
    static readonly Guid StaticDataGuid = Guid.Parse("ab56dc60-0057-11da-a8db-000102eee626");
    const byte FreeformSubtypeGuidSection = 0x18;

    static readonly Dictionary<byte, int[]> StringFields = new()
    {
        [0] = new[] { 4, 5, 8 },
        [1] = new[] { 4, 5, 6, 7, 0x19, 0x1A },
        [2] = new[] { 4, 5, 6, 7, 8, 0xA },
        [3] = new[] { 4, 6, 7, 8, 0x15 },
    };

    /// <summary>The (start, end) of the SMBIOS static-data bytes inside the DXE firmware volume.</summary>
    public static (int Start, int End) LocateStaticData(FirmwareVolume dxeFv)
    {
        var file = dxeFv.FindByGuid(StaticDataFileGuid);
        var section = file.Sections().FirstOrDefault(s => s.Type == FreeformSubtypeGuidSection)
            ?? throw new RomFormatException("SMBIOS static-data file has no freeform-subtype-GUID section");
        if (Bin.Guid(dxeFv.Buffer, section.Offset + 4) != StaticDataGuid)
            throw new RomFormatException("unexpected SMBIOS static data section GUID");
        return (section.Offset + 0x14, section.End);
    }

    /// <summary>Rewrite the identity structures in <c>img[start..end]</c> in place. Throws if the result would not fit.</summary>
    public static void ApplyIdentity(byte[] img, int start, int end, DmiIdentity dmi, out IReadOnlyList<string> report)
    {
        var body = img[start..end];
        var structs = ParseStructs(body);

        var plan = new Dictionary<byte, Dictionary<int, string?>>
        {
            [1] = new() { { 4, dmi.SystemManufacturer }, { 5, dmi.SystemProduct }, { 6, dmi.SystemVersion },
                          { 7, dmi.SystemSerial }, { 0x19, dmi.SystemSku }, { 0x1A, dmi.SystemFamily } },
            [2] = new() { { 4, dmi.BoardManufacturer }, { 5, dmi.BoardProduct }, { 6, dmi.BoardVersion },
                          { 7, dmi.BoardSerial }, { 8, dmi.BoardAssetTag }, { 0xA, dmi.BoardLocation } },
            [3] = new() { { 4, dmi.ChassisManufacturer }, { 6, dmi.ChassisVersion }, { 7, dmi.ChassisSerial },
                          { 8, dmi.ChassisAssetTag }, { 0x15, dmi.ChassisSku } },
        };

        var lines = new List<string>();
        var outBytes = new List<byte>();
        foreach (var (type, fmt, strs) in structs)
        {
            var currentStrs = strs;
            if (plan.TryGetValue(type, out var fields))
            {
                currentStrs = SetStrings(type, fmt, strs, fields);
                lines.Add($"type {type}: identity fields rewritten");
                if (type == 1 && dmi.SystemUuid is not null)
                {
                    Guid.Parse(dmi.SystemUuid).ToByteArray().CopyTo(fmt, 8);   // 16 bytes, SMBIOS 2.6+ order
                    lines.Add($"type 1 UUID -> {dmi.SystemUuid}");
                }
                if (type == 3 && dmi.ChassisType is not null)
                    fmt[5] = (byte)((dmi.ChassisType.Value & 0x7F) | (fmt[5] & 0x80));
            }
            if (type == 11 && dmi.OemStrings is not null)
            {
                currentStrs = dmi.OemStrings.Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => Encoding.Latin1.GetBytes(s)).ToList();
                fmt[4] = (byte)currentStrs.Count;
                lines.Add($"type 11: {currentStrs.Count} OEM strings");
            }

            outBytes.AddRange(fmt);
            if (currentStrs.Count > 0)
            {
                foreach (var s in currentStrs) { outBytes.AddRange(s); outBytes.Add(0); }
                outBytes.Add(0);                 // double-NUL terminator
            }
            else { outBytes.Add(0); outBytes.Add(0); }
        }

        if (outBytes.Count > body.Length)
            throw new RomEditException(
                $"new SMBIOS table is {outBytes.Count} bytes but the static-data section holds only {body.Length}");

        for (int i = 0; i < outBytes.Count; i++) img[start + i] = outBytes[i];
        for (int i = start + outBytes.Count; i < end; i++) img[i] = 0xFF;   // rest 0xFF, as AMI ships it
        report = lines;
    }

    static List<(byte Type, byte[] Fmt, List<byte[]> Strs)> ParseStructs(byte[] body)
    {
        var list = new List<(byte, byte[], List<byte[]>)>();
        int o = 0;
        while (o + 4 <= body.Length)
        {
            byte type = body[o];
            int len = body[o + 1];
            int e = IndexOfDoubleNul(body, o + len);
            var strs = e > o + len ? SplitNul(body, o + len, e) : new List<byte[]>();
            list.Add((type, body[o..(o + len)], strs));
            o = e + 2;
            if (type == 127) break;
        }
        return list;
    }

    static int IndexOfDoubleNul(byte[] b, int from)
    {
        for (int i = from; i + 1 < b.Length; i++)
            if (b[i] == 0 && b[i + 1] == 0) return i;
        throw new RomFormatException("SMBIOS structure is not NUL-terminated");
    }

    static List<byte[]> SplitNul(byte[] b, int start, int end)
    {
        var parts = new List<byte[]>();
        int s = start;
        for (int i = start; i < end; i++)
            if (b[i] == 0) { parts.Add(b[s..i]); s = i + 1; }
        parts.Add(b[s..end]);
        return parts;
    }

    static List<byte[]> SetStrings(byte type, byte[] fmt, List<byte[]> strs, Dictionary<int, string?> fields)
    {
        var newStrs = new List<byte[]>();
        foreach (var off in StringFields[type].Where(o => o < fmt.Length))
        {
            byte[]? val;
            if (fields.TryGetValue(off, out var requested))
                val = requested is null ? null : Encoding.Latin1.GetBytes(requested);
            else
            {
                int idx = fmt[off];
                val = idx > 0 && idx <= strs.Count ? strs[idx - 1] : null;
            }
            if (val is { Length: > 0 })
            {
                newStrs.Add(val);
                fmt[off] = (byte)newStrs.Count;
            }
            else fmt[off] = 0;
        }
        return newStrs;
    }
}
