// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Profile;

/// <summary>
/// The composer's mutable authoring model. Holds the board identity metadata (user-entered), the chosen
/// bundled donor adapter, the OEM PEI layout needed by the build, and the ordered operation list.
/// <see cref="ToProfile"/> assembles an immutable <see cref="BoardProfile"/> for writing/testing;
/// <see cref="FromProfile"/> seeds a draft from a loaded profile (Import). Round-trips faithfully.
/// </summary>
public sealed class ProfileDraft
{
    public string Vendor { get; set; } = "";
    public string Model { get; set; } = "";
    public string BoardId { get; set; } = "";
    public string? Ssid { get; set; }
    public List<string> BiosVersions { get; set; } = new();
    public int? RomSize { get; set; }

    public string AdapterId { get; set; } = "";
    public string? DonorSha { get; set; }

    public List<string> Authors { get; set; } = new();
    public List<TestedEntry> Tested { get; set; } = new();
    public List<string> Untested { get; set; } = new();

    public string? IdentityExtractor { get; set; }
    public List<ModuleEntry> OemModules { get; set; } = new();
    public int? OemPeiFvOffset { get; set; }

    public List<OperationSpec> Operations { get; set; } = new();

    public BoardProfile ToProfile() => new(
        ProfileLoader.ProfileSchema,
        new BoardInfo(Vendor, Model, BoardId,
            new BoardMatch(Ssid, BiosVersions.Count > 0 ? BiosVersions : null, RomSize)),
        new DonorRef(AdapterId, DonorSha),
        Authors,
        new ProfileStatus(Tested.Count > 0 ? Tested : null, Untested.Count > 0 ? Untested : null),
        Operations,
        IdentityExtractor is null ? null : new IdentitySpec(IdentityExtractor),
        OemModules.Count > 0 ? OemModules : null,
        OemPeiFvOffset);

    public static ProfileDraft FromProfile(BoardProfile p) => new()
    {
        Vendor = p.Board.Vendor,
        Model = p.Board.Model,
        BoardId = p.Board.BoardId,
        Ssid = p.Board.Match.Ssid,
        BiosVersions = p.Board.Match.BiosVersions?.ToList() ?? new(),
        RomSize = p.Board.Match.RomSize,
        AdapterId = p.Donor.Adapter,
        DonorSha = p.Donor.Sha256,
        Authors = p.Authors.ToList(),
        Tested = p.Status.Tested?.ToList() ?? new(),
        Untested = p.Status.Untested?.ToList() ?? new(),
        IdentityExtractor = p.Identity?.Extractor,
        OemModules = p.OemModules?.ToList() ?? new(),
        OemPeiFvOffset = p.OemPeiFvOffset,
        Operations = p.Operations.ToList(),
    };
}
