// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;

namespace Am4ever.Tests.Profile;

public class ProfileDraftTests
{
    [Fact] public void FromProfile_ToProfile_PreservesOperationSequence()
    {
        var hp = BundledData.LoadProfile("hp-285-g3-8434");
        var draft = ProfileDraft.FromProfile(hp);
        Assert.Equal(hp.Operations.Select(o => o.Op), draft.ToProfile().Operations.Select(o => o.Op));
        Assert.Equal(hp.Board.BoardId, draft.ToProfile().Board.BoardId);
        Assert.Equal(hp.OemPeiFvOffset, draft.ToProfile().OemPeiFvOffset);   // needed by the build
    }

    [Fact] public void AddOperation_AppearsInProfile()
    {
        var d = new ProfileDraft { Vendor = "Acme", Model = "X", BoardId = "1", AdapterId = "asrock-ab350m-pro4-p10.43", RomSize = 16 << 20 };
        d.Operations.Add(new OperationSpec("apcb_check"));
        Assert.Equal("apcb_check", Assert.Single(d.ToProfile().Operations).Op);
        Assert.Equal(ProfileLoader.ProfileSchema, d.ToProfile().Schema);
    }
}
