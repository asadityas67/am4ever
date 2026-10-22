// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;

namespace Am4ever.Tests.Profile;

public class AdapterDraftTests
{
    [Fact] public void FromAdapter_ToAdapter_PreservesOverridesAndVolumes()
    {
        var a = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var round = AdapterDraft.FromAdapter(a).ToAdapter();
        Assert.Equal(a.Overrides.Select(o => o.Offset), round.Overrides.Select(o => o.Offset));
        Assert.Equal(a.Volumes.DxeFileGuid, round.Volumes.DxeFileGuid);
        Assert.Equal(a.Volumes.BootBlobOffset, round.Volumes.BootBlobOffset);
    }

    [Fact] public void ToAdapter_InvalidOverrideHex_Throws()
    {
        var d = new AdapterDraft { Id = "x" };
        d.Overrides.Add(new OverrideDraft("ZP", 10, "zzzz"));
        Assert.ThrowsAny<System.Exception>(() => d.ToAdapter());
    }
}
