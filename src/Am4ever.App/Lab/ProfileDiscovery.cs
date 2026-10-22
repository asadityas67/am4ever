// SPDX-License-Identifier: GPL-2.0-or-later
using System.Globalization;
using Am4ever.Core.Profile;

namespace Am4ever.App.Lab;

/// <summary>
/// Suggests the profile operations for a new board from its loaded OEM ROM, the donor ROM, and the
/// chosen donor adapter — the same path the HP 285 G3 profile was written by hand. Values are filled
/// in where they are discoverable (override sites from the adapter, GPIO platform mask and audio codec
/// from the OEM dump); DXIO/DDI come as inferred templates for the expert to refine. The returned list
/// is a menu the composer adds from, not a finished profile.
/// </summary>
public static class ProfileDiscovery
{
    public static IReadOnlyList<OperationSpec> Suggest(BoardAnalysis oem, BoardAnalysis donor, DonorAdapter adapter)
    {
        var ops = new List<OperationSpec>
        {
            new("apcb_check") { Notes = "OEM and donor DRAM SPD maps must match" },
        };

        uint? oemCodec = oem.AudioCodec();
        uint? donorCodec = donor.AudioCodec();

        foreach (var m in adapter.Modules)
        {
            var fam = m.Family;

            var ov = adapter.Overrides.FirstOrDefault(o => o.Family == fam);
            if (ov is not null)
                ops.Add(new OperationSpec("neutralise_override") { Family = fam, Offset = ov.Offset, Expected = ov.Expected });

            if (GpioMask(oem, m.Name, m.Index) is uint mask)
                ops.Add(new OperationSpec("port_gpio") { Family = fam, PlatformMask = mask, HpPlatformMask = mask });

            ops.Add(new OperationSpec("port_gevent") { Family = fam });
            ops.Add(new OperationSpec("empty_tables") { Family = fam, Tables = StandardEmpty() });
            ops.Add(new OperationSpec("dxio_layout")
            {
                Family = fam, Confidence = "inferred",
                Evidence = "template — edit the DXIO descriptors for this board's lane routing",
                Dxio = Array.Empty<DxioEdit>(),
            });

            if (oemCodec is uint nc)
                ops.Add(new OperationSpec("verb_table")
                {
                    Family = fam,
                    OldCodecs = donorCodec is uint dc ? new[] { dc } : Array.Empty<uint>(),
                    NewCodec = nc,
                });
        }

        ops.Add(new OperationSpec("smbios_identity") { Notes = "needs a Core identity extractor for this board" });
        ops.Add(new OperationSpec("add_ec_init"));
        return ops;
    }

    static uint? GpioMask(BoardAnalysis a, string module, int index)
    {
        foreach (var t in a.CpmTables(module, index))
            if (t.StartsWith("$A10/0x", StringComparison.Ordinal) &&
                uint.TryParse(t.AsSpan(7), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var m))
                return m;
        return null;
    }

    static List<EmptyTableSpec> StandardEmpty() => new()
    {
        new("$A12", new byte[] { 0xFF, 0x00 }),
        new("$A13", new byte[] { 0xFF }),
        new("$A14", new byte[] { 0xFF }),
        new("$A15", new byte[] { 0xFF }),
        new("$A17", new byte[] { 0xFF }),
        new("$A19", new byte[] { 0xFF, 0x00, 0x00, 0x00 }),
    };
}
