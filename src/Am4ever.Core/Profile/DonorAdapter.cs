// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Profile;

/// <summary>
/// A curated donor adapter: which retail ROM it describes, which OEM PEIM serves which CPU family,
/// the runtime override routines to neutralise (pattern + expected bytes), and where the boot and DXE
/// volumes live. Ships with the app; carries no firmware beyond the short override expected-byte strings.
/// </summary>
public sealed record DonorAdapter(
    string Id, DonorIdentity Identity, IReadOnlyList<ModuleEntry> Modules,
    IReadOnlyList<OverrideSig> Overrides, VolumeMap Volumes,
    IReadOnlyList<string>? Hazards, string? DownloadUrl);

public sealed record DonorIdentity(string Vendor, string Board, string Version, string? Sha256);

public sealed record ModuleEntry(string Name, string Family, int Index);

public sealed record OverrideSig(string Family, int Offset, byte[] Expected);

public sealed record VolumeMap(int BootBlobOffset, int BootBlobLength, int BootFvOffset, int DxeFvOffset, string? Compression);
