// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Am4ever.Core.Profile;

/// <summary>A board profile (schema <c>am4ever.profile/1</c>): what to build, from which donor, and how.</summary>
public sealed record BoardProfile(
    string Schema, BoardInfo Board, DonorRef Donor, IReadOnlyList<string> Authors,
    ProfileStatus Status, IReadOnlyList<OperationSpec> Operations, IdentitySpec? Identity);

public sealed record BoardInfo(string Vendor, string Model, string BoardId, BoardMatch Match);

public sealed record BoardMatch(string? Ssid, IReadOnlyList<string>? BiosVersions, int? RomSize);

public sealed record DonorRef(string Adapter, string? Sha256);

public sealed record ProfileStatus(IReadOnlyList<TestedEntry>? Tested, IReadOnlyList<string>? Untested);

public sealed record TestedEntry(string Cpu, string Result, string? Notes);

public sealed record IdentitySpec(string Extractor);

/// <summary>
/// One operation in a profile. Carries a superset of every operation's arguments; a loader maps
/// <see cref="Op"/> to an implementation. Unknown JSON keys are collected in <see cref="Extra"/>, not
/// an error. <see cref="Expected"/>/<see cref="New"/> are the only byte strings a profile may carry,
/// each capped at 64 bytes (no firmware in profiles).
/// </summary>
public sealed record OperationSpec(string Op)
{
    public string? Family { get; init; }
    public string? From { get; init; }
    public string? Merge { get; init; }
    public uint? PlatformMask { get; init; }
    public string? Module { get; init; }
    public int? Offset { get; init; }
    public byte[]? Expected { get; init; }
    public byte[]? New { get; init; }
    public string? Confidence { get; init; }
    public string? Evidence { get; init; }
    public string? Notes { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}
