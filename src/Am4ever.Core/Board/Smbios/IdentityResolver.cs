// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Profile;

namespace Am4ever.Core.Board.Smbios;

/// <summary>
/// Resolves the base SMBIOS identity for a build. A profile naming a known built-in extractor gets that
/// extractor's exact identity (so a shipped board's strings are byte-for-byte preserved); any other board
/// derives its identity generically from its own profile metadata. The build then merges a user --dmi on top.
/// </summary>
public static class IdentityResolver
{
    public static DmiIdentity BaseFor(BoardProfile p) => p.Identity?.Extractor switch
    {
        "hp-8434" => DmiIdentity.HpDefault,
        _ => DmiIdentity.FromBoard(p.Board.Vendor, p.Board.Model, p.Board.BoardId),
    };
}
