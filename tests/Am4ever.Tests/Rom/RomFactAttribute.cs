// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Tests.Rom;

/// <summary>A Fact that runs only when the developer's reference ROM files are present.</summary>
public sealed class RomFactAttribute : FactAttribute
{
    public RomFactAttribute() => Skip = TestRoms.SkipReason;
}
