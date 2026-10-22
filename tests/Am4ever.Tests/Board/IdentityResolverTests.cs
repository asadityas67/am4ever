// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;

namespace Am4ever.Tests.Board;

public class IdentityResolverTests
{
    [Fact] public void BaseFor_HpProfile_IsTheHpIdentity()
    {
        var id = IdentityResolver.BaseFor(BundledData.LoadProfile("hp-285-g3-8434"));
        Assert.Equal("HP", id.SystemManufacturer);
        Assert.Equal(DmiIdentity.HpDefault.SystemProduct, id.SystemProduct);   // exact HP strings preserved
    }

    [Fact] public void BaseFor_UnknownBoard_DerivesFromProfileMetadata()
    {
        var draft = new ProfileDraft { Vendor = "Lenovo", Model = "M715p", BoardId = "3134", IdentityExtractor = null };
        var id = IdentityResolver.BaseFor(draft.ToProfile());
        Assert.Equal("Lenovo", id.SystemManufacturer);
        Assert.Equal("M715p", id.SystemProduct);
        Assert.Equal("3134", id.BoardProduct);
    }
}
