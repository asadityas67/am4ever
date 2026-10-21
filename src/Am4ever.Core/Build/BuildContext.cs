// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Build;

/// <summary>
/// The working state an operation edits: per-family donor CPM OEM-PEIM image sections (mutable), the
/// matching OEM source sections (read-only), GPIO base snapshots taken before any edit (for family-delta
/// merges), the extracted OEM verb list, and the DXE volume / ROM buffers some operations need.
/// </summary>
public sealed class BuildContext
{
    readonly Dictionary<string, byte[]> _donorPeim;
    readonly Dictionary<string, byte[]> _hpPeim;

    public BuildContext(Dictionary<string, byte[]> donorPeim, Dictionary<string, byte[]> hpPeim)
    {
        _donorPeim = donorPeim;
        _hpPeim = hpPeim;
    }

    public List<string> Report { get; } = new();

    /// <summary>Donor base GPIO entries captured before edits, keyed by family (for family-delta merges).</summary>
    public Dictionary<string, Dictionary<ushort, ushort>> BaseGpio { get; } = new();

    /// <summary>The OEM codec verb list, extracted once from the OEM ROM.</summary>
    public IReadOnlyList<uint>? HpVerbs { get; init; }

    // DXE target (set when a DXE-phase operation runs).
    public byte[]? DxeVolume { get; init; }
    public int DxeInnerFvOffset { get; init; }
    public int SmbiosStart { get; init; }
    public int SmbiosEnd { get; init; }
    public DmiIdentity? Dmi { get; init; }

    // APCB check (set when an apcb_check operation runs).
    public byte[]? OemRom { get; init; }
    public byte[]? DonorRom { get; init; }
    public int OemApcb { get; init; }
    public int DonorApcb { get; init; }

    /// <summary>The mutable donor image section for a family (or the only one when <paramref name="family"/> is null).</summary>
    public byte[] DonorPeim(string? family) => Resolve(_donorPeim, family, "donor");

    /// <summary>The read-only OEM source image section for a family.</summary>
    public byte[] HpPeim(string? family) => Resolve(_hpPeim, family, "OEM");

    static byte[] Resolve(Dictionary<string, byte[]> map, string? family, string which)
    {
        if (family is not null)
            return map.TryGetValue(family, out var v) ? v
                : throw new RomFormatException($"{which} module for family '{family}' is not available");
        if (map.Count == 1) return map.Values.First();
        throw new RomFormatException($"operation needs a family: {which} has {map.Count} modules to choose from");
    }
}
