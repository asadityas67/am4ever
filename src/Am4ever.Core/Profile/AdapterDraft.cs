// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;

namespace Am4ever.Core.Profile;

/// <summary>One override-site row in an adapter draft: the family, the routine offset, and the expected
/// bytes as a hex string the expert types (parsed to bytes in <see cref="AdapterDraft.ToAdapter"/>).</summary>
public sealed record OverrideDraft(string Family, int Offset, string ExpectedHex);

/// <summary>
/// The adapter-authoring mutable model. The detector fills what it can; the expert finishes the
/// identity, module family tags and the override rows. <see cref="ToAdapter"/> builds the immutable
/// <see cref="DonorAdapter"/> for writing; <see cref="FromAdapter"/> seeds a draft from an existing one.
/// </summary>
public sealed class AdapterDraft
{
    public string Id { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Board { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Sha256 { get; set; }

    public List<ModuleEntry> Modules { get; set; } = new();
    public List<OverrideDraft> Overrides { get; set; } = new();

    public string? Compression { get; set; }
    public int? BootBlobOffset { get; set; }
    public int? BootBlobLength { get; set; }
    public int? BootFvOffset { get; set; }
    public int? DxeFvOffset { get; set; }
    public string? DxeFileGuid { get; set; }

    public List<string> Hazards { get; set; } = new();
    public string? DownloadUrl { get; set; }

    public DonorAdapter ToAdapter() => new(
        Id,
        new DonorIdentity(Vendor, Board, Version, Sha256),
        Modules,
        Overrides.Select(o => new OverrideSig(o.Family, o.Offset, ParseHex(o.ExpectedHex))).ToList(),
        new VolumeMap(BootBlobOffset ?? 0, BootBlobLength ?? 0, BootFvOffset ?? 0, DxeFvOffset ?? 0, Compression, DxeFileGuid),
        Hazards.Count > 0 ? Hazards : null,
        DownloadUrl);

    static byte[] ParseHex(string hex)
    {
        try { return Convert.FromHexString(hex); }
        catch (FormatException) { throw new RomFormatException($"override expected bytes '{hex}' are not valid hex"); }
    }

    public static AdapterDraft FromAdapter(DonorAdapter a) => new()
    {
        Id = a.Id,
        Vendor = a.Identity.Vendor,
        Board = a.Identity.Board,
        Version = a.Identity.Version,
        Sha256 = a.Identity.Sha256,
        Modules = a.Modules.ToList(),
        Overrides = a.Overrides.Select(o => new OverrideDraft(o.Family, o.Offset, Convert.ToHexString(o.Expected))).ToList(),
        Compression = a.Volumes.Compression,
        BootBlobOffset = a.Volumes.BootBlobOffset,
        BootBlobLength = a.Volumes.BootBlobLength,
        BootFvOffset = a.Volumes.BootFvOffset,
        DxeFvOffset = a.Volumes.DxeFvOffset,
        DxeFileGuid = a.Volumes.DxeFileGuid,
        Hazards = a.Hazards?.ToList() ?? new(),
        DownloadUrl = a.DownloadUrl,
    };
}
